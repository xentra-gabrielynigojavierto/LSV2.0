using Documents.Infrastructure.Observability;
using Documents.Infrastructure.Redis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using StackExchange.Redis;

namespace Documents.Infrastructure.Health;

/// <summary>
/// Health check for the Redis dependency.
///
/// Performs a direct PING (bypassing the circuit breaker) to verify actual Redis reachability.
/// Also surfaces circuit breaker state as additional context in the health description so
/// operators can distinguish "Redis unreachable" from "circuit open due to past failures".
///
/// Registered only when Redis is actively used.
/// Tagged "ready" — surfaces in /health/ready only (not in liveness /health).
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer    _redis;
    private readonly RedisResiliencePipeline   _resilience;
    private readonly ILogger<RedisHealthCheck> _log;

    public RedisHealthCheck(
        IConnectionMultiplexer    redis,
        RedisResiliencePipeline   resilience,
        ILogger<RedisHealthCheck> log)
    {
        _redis      = redis;
        _resilience = resilience;
        _log        = log;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        var circuitState  = _resilience.CircuitState;
        var circuitLabel  = circuitState.ToString().ToLowerInvariant();  // closed / open / halfopen

        try
        {
            // Always ping directly — not via the circuit breaker.
            // Reason: the health check itself is the probe that allows the circuit to close.
            var db      = _redis.GetDatabase();
            var latency = await db.PingAsync();

            RedisMetrics.RedisHealthy.Set(1);

            var description = $"Redis reachable — latency {latency.TotalMilliseconds:F1} ms | " +
                              $"circuit={circuitLabel}";

            _log.LogDebug(
                "Redis health check passed — latency {LatencyMs} ms circuit={Circuit}",
                latency.TotalMilliseconds, circuitLabel);

            // If the circuit is open but Redis actually responds, still return Healthy for
            // the connectivity probe — but include the state so operators notice the mismatch.
            return circuitState == CircuitState.Open
                ? HealthCheckResult.Degraded(description + " [circuit open — probing for recovery]")
                : HealthCheckResult.Healthy(description);
        }
        catch (Exception ex)
        {
            RedisMetrics.RedisHealthy.Set(0);
            RedisMetrics.RedisConnectionFailures.Inc();

            _log.LogWarning(ex,
                "Redis health check failed — Redis may be unreachable. circuit={Circuit}",
                circuitLabel);

            return HealthCheckResult.Unhealthy(
                description: $"Redis unreachable: {ex.Message} | circuit={circuitLabel}",
                exception:   ex);
        }
    }
}
