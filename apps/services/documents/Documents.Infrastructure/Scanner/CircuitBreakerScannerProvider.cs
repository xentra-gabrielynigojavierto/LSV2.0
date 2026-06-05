using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Documents.Infrastructure.Scanner;

/// <summary>
/// Decorates <see cref="IFileScannerProvider"/> with a Polly advanced circuit breaker.
///
/// State machine:
///   CLOSED    → normal scanning via inner provider
///   OPEN      → immediately returns ScanStatus.Failed (no ClamAV call)
///   HALF-OPEN → allows one probe request; success → CLOSED, failure → OPEN
///
/// Fail-closed guarantee: documents are NEVER marked CLEAN without an actual scan.
/// When the circuit is OPEN, the worker's existing retry/backoff logic continues
/// to apply — no changes to worker behavior required.
/// </summary>
public sealed class CircuitBreakerScannerProvider : IFileScannerProvider
{
    private readonly IFileScannerProvider                  _inner;
    private readonly ILogger<CircuitBreakerScannerProvider> _log;
    private readonly AsyncCircuitBreakerPolicy             _policy;

    public string ProviderName => _inner.ProviderName;

    /// <summary>Exposes current circuit state for health checks and diagnostics.</summary>
    public CircuitState CircuitState => _policy.CircuitState;

    public CircuitBreakerScannerProvider(
        IFileScannerProvider                   inner,
        ClamAvCircuitBreakerOptions            opts,
        ILogger<CircuitBreakerScannerProvider> log)
    {
        _inner = inner;
        _log   = log;

        // Map integer FailureThreshold + MinimumThroughput to a Polly failure ratio.
        // Example defaults: 5 failures out of 5 calls (100%) within 60 s → open for 30 s.
        double failureRatio = opts.MinimumThroughput > 0
            ? Math.Clamp((double)opts.FailureThreshold / opts.MinimumThroughput, 0.01, 1.0)
            : 1.0;

        _policy = Policy
            // Count any exception except a short-circuit (BrokenCircuitException) as a failure.
            // INFECTED is a normal ScanResult, not an exception, so it is never counted.
            .Handle<Exception>(ex => ex is not BrokenCircuitException)
            .AdvancedCircuitBreakerAsync(
                failureThreshold:  failureRatio,
                samplingDuration:  TimeSpan.FromSeconds(opts.SamplingDurationSeconds),
                minimumThroughput: opts.MinimumThroughput,
                durationOfBreak:   TimeSpan.FromSeconds(opts.BreakDurationSeconds),
                onBreak: (ex, breakDuration) =>
                {
                    ScanMetrics.ClamAvCircuitState.Set(1);   // OPEN = 1
                    ScanMetrics.ClamAvCircuitOpenTotal.Inc();
                    _log.LogWarning(ex,
                        "ClamAV circuit opened after repeated failures — pausing for {DurationSeconds}s",
                        breakDuration.TotalSeconds);
                },
                onReset: () =>
                {
                    ScanMetrics.ClamAvCircuitState.Set(0);   // CLOSED = 0
                    _log.LogInformation("ClamAV circuit closed — normal operation resumed");
                },
                onHalfOpen: () =>
                {
                    ScanMetrics.ClamAvCircuitState.Set(2);   // HALF-OPEN = 2
                    _log.LogInformation(
                        "ClamAV circuit half-open — probing ClamAV availability");
                });
    }

    /// <inheritdoc/>
    public async Task<ScanResult> ScanAsync(
        Stream            content,
        string            fileName,
        CancellationToken ct = default)
    {
        try
        {
            return await _policy.ExecuteAsync(
                async () => await _inner.ScanAsync(content, fileName, ct));
        }
        catch (BrokenCircuitException ex)
        {
            // Circuit is OPEN — short-circuit the call.
            ScanMetrics.ClamAvCircuitShortCircuitTotal.Inc();
            _log.LogWarning(ex,
                "ClamAV scan skipped due to open circuit for file {FileName}", fileName);

            // Return Failed — worker will retry via its normal backoff schedule.
            // NEVER return Clean here; fail-closed is mandatory.
            return new ScanResult
            {
                Status        = ScanStatus.Failed,
                Threats       = new(),
                DurationMs    = 0,
                EngineVersion = $"{_inner.ProviderName}/circuit-open",
            };
        }
    }
}
