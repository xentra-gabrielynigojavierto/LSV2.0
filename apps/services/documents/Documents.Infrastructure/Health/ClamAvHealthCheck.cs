using System.Net.Sockets;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Scanner;
using Documents.Infrastructure.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;

namespace Documents.Infrastructure.Health;

/// <summary>
/// ASP.NET Core health check that verifies ClamAV daemon reachability.
/// Uses the PING/PONG command to confirm clamd is alive.
/// Reports Degraded (not Unhealthy) so the service continues running
/// while scanner is temporarily unavailable.
///
/// When the circuit breaker is OPEN the health status is immediately
/// Degraded without attempting a TCP connection.
/// </summary>
public sealed class ClamAvHealthCheck : IHealthCheck
{
    private readonly ClamAvOptions                _opts;
    private readonly IFileScannerProvider         _scanner;
    private readonly ILogger<ClamAvHealthCheck>   _log;

    public ClamAvHealthCheck(
        IOptions<ClamAvOptions>         opts,
        IFileScannerProvider            scanner,
        ILogger<ClamAvHealthCheck>      log)
    {
        _opts    = opts.Value;
        _scanner = scanner;
        _log     = log;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext   context,
        CancellationToken    ct = default)
    {
        // If the circuit breaker is active, surface its state before doing any TCP work.
        if (_scanner is CircuitBreakerScannerProvider cb)
        {
            var state = cb.CircuitState;
            if (state == CircuitState.Open)
            {
                ScanMetrics.ClamAvHealthy.Set(0);
                return HealthCheckResult.Degraded(
                    $"ClamAV circuit breaker is OPEN — scanner paused at {_opts.Host}:{_opts.Port}",
                    data: new Dictionary<string, object> { ["circuit_state"] = "open" });
            }

            if (state == CircuitState.HalfOpen)
            {
                // Still probe ClamAV below, but enrich the description with state context.
                var probeResult = await PingClamAvAsync(ct);
                var enriched    = $"[circuit=half-open] {probeResult.Description}";
                return probeResult.Status == HealthStatus.Healthy
                    ? HealthCheckResult.Healthy(enriched, probeResult.Data)
                    : HealthCheckResult.Degraded(enriched, data: probeResult.Data);
            }
        }

        return await PingClamAvAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<HealthCheckResult> PingClamAvAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(_opts.TimeoutMs, 5_000)));

        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(_opts.Host, _opts.Port, cts.Token);

            await using var stream = tcp.GetStream();
            var ping = System.Text.Encoding.ASCII.GetBytes("zPING\0");
            await stream.WriteAsync(ping, cts.Token);
            await stream.FlushAsync(cts.Token);

            using var reader = new StreamReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
            var response = (await reader.ReadLineAsync(cts.Token))?.Trim() ?? string.Empty;

            if (response.Equals("PONG", StringComparison.OrdinalIgnoreCase))
            {
                ScanMetrics.ClamAvHealthy.Set(1);
                return HealthCheckResult.Healthy(
                    $"ClamAV reachable at {_opts.Host}:{_opts.Port}",
                    data: new Dictionary<string, object> { ["circuit_state"] = "closed" });
            }

            ScanMetrics.ClamAvHealthy.Set(0);
            return HealthCheckResult.Degraded(
                $"ClamAV unexpected response: '{response}' at {_opts.Host}:{_opts.Port}");
        }
        catch (OperationCanceledException)
        {
            ScanMetrics.ClamAvHealthy.Set(0);
            _log.LogWarning("ClamAV health check timed out ({Host}:{Port})", _opts.Host, _opts.Port);
            return HealthCheckResult.Degraded(
                $"ClamAV health check timed out ({_opts.Host}:{_opts.Port})");
        }
        catch (Exception ex)
        {
            ScanMetrics.ClamAvHealthy.Set(0);
            _log.LogWarning(ex, "ClamAV health check failed ({Host}:{Port})", _opts.Host, _opts.Port);
            return HealthCheckResult.Degraded(
                $"ClamAV unreachable at {_opts.Host}:{_opts.Port} — {ex.Message}");
        }
    }
}
