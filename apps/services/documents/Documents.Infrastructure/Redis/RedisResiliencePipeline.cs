using Documents.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using StackExchange.Redis;
using System.Net.Sockets;

namespace Documents.Infrastructure.Redis;

/// <summary>
/// Circuit-breaker options for the shared Redis resilience policy.
/// Bound from "Redis:CircuitBreaker" in appsettings.json.
/// </summary>
public sealed class RedisCircuitBreakerOptions
{
    /// <summary>Number of failures within SamplingDurationSeconds that trigger the open state.</summary>
    public int FailureThreshold       { get; set; } = 5;

    /// <summary>How long the circuit stays open before moving to half-open (seconds).</summary>
    public int BreakDurationSeconds   { get; set; } = 30;

    /// <summary>Window (seconds) in which failures are counted.</summary>
    public int SamplingDurationSeconds { get; set; } = 60;

    /// <summary>Minimum calls required in the sampling window before the circuit can open.</summary>
    public int MinimumThroughput      { get; set; } = 5;
}

/// <summary>
/// Shared Polly-based circuit breaker for all Redis operations in the Documents service.
///
/// Single instance shared by RedisScanJobQueue, RedisScanCompletionPublisher,
/// and RedisStreamScanCompletionPublisher so the circuit state reflects the
/// Redis server health across all usage patterns.
///
/// State machine (same pattern as ClamAV circuit breaker):
///   CLOSED    → normal operation — commands execute against Redis
///   OPEN      → fast-fail — <see cref="BrokenCircuitException"/> thrown immediately
///   HALF-OPEN → one probe attempt allowed; success → CLOSED, failure → OPEN
///
/// Only Redis-specific exceptions (RedisException, SocketException) open the circuit.
/// Application logic errors (argument, JSON, etc.) are excluded.
/// </summary>
public sealed class RedisResiliencePipeline
{
    private readonly ILogger<RedisResiliencePipeline> _log;
    private readonly AsyncCircuitBreakerPolicy        _policy;

    /// <summary>Current circuit state: Closed, Open, or HalfOpen.</summary>
    public CircuitState CircuitState => _policy.CircuitState;

    public RedisResiliencePipeline(
        RedisCircuitBreakerOptions        opts,
        ILogger<RedisResiliencePipeline>  log)
    {
        _log = log;

        double failureRatio = opts.MinimumThroughput > 0
            ? Math.Clamp((double)opts.FailureThreshold / opts.MinimumThroughput, 0.01, 1.0)
            : 1.0;

        _policy = Policy
            .Handle<RedisException>()
            .Or<SocketException>()
            // Exclude BrokenCircuitException — Polly's own signal, not an infra failure.
            .AdvancedCircuitBreakerAsync(
                failureThreshold:   failureRatio,
                samplingDuration:   TimeSpan.FromSeconds(opts.SamplingDurationSeconds),
                minimumThroughput:  opts.MinimumThroughput,
                durationOfBreak:    TimeSpan.FromSeconds(opts.BreakDurationSeconds),
                onBreak: (ex, breakDuration) =>
                {
                    RedisMetrics.RedisCircuitState.Set(1);     // OPEN = 1
                    RedisMetrics.RedisCircuitOpenTotal.Inc();
                    log.LogWarning(ex,
                        "Redis circuit opened after repeated failures — fast-failing commands for {DurationSeconds}s",
                        breakDuration.TotalSeconds);
                },
                onReset: () =>
                {
                    RedisMetrics.RedisCircuitState.Set(0);     // CLOSED = 0
                    log.LogInformation("Redis circuit closed — normal operation resumed");
                },
                onHalfOpen: () =>
                {
                    RedisMetrics.RedisCircuitState.Set(2);     // HALF-OPEN = 2
                    log.LogInformation("Redis circuit half-open — probing Redis availability");
                });
    }

    // ── Execute wrappers ─────────────────────────────────────────────────────

    /// <summary>
    /// Execute a Redis operation inside the circuit breaker.
    /// Throws <see cref="BrokenCircuitException"/> when the circuit is OPEN.
    /// All other Redis exceptions propagate normally (and are counted by the breaker).
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await _policy.ExecuteAsync(action);
        }
        catch (BrokenCircuitException ex)
        {
            RedisMetrics.RedisCircuitShortCircuitTotal.Inc();
            _log.LogWarning(ex, "Redis command fast-failed — circuit is OPEN");
            throw;
        }
    }

    /// <summary>
    /// Execute a Redis operation (no return value) inside the circuit breaker.
    /// </summary>
    public async Task ExecuteAsync(Func<Task> action)
    {
        try
        {
            await _policy.ExecuteAsync(action);
        }
        catch (BrokenCircuitException ex)
        {
            RedisMetrics.RedisCircuitShortCircuitTotal.Inc();
            _log.LogWarning(ex, "Redis command fast-failed — circuit is OPEN");
            throw;
        }
    }
}
