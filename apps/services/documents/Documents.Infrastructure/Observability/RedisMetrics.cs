using Prometheus;

namespace Documents.Infrastructure.Observability;

/// <summary>
/// Prometheus metrics for the Redis dependency and scan completion notification subsystem.
/// Exposed at GET /metrics alongside ScanMetrics.
/// </summary>
public static class RedisMetrics
{
    // ── Redis health ──────────────────────────────────────────────────────────

    /// <summary>1 if Redis is reachable and responding to PING, 0 otherwise.</summary>
    public static readonly Gauge RedisHealthy = Metrics.CreateGauge(
        "docs_redis_healthy",
        "1 if the Redis server is reachable and responding to PING, 0 otherwise.");

    /// <summary>Total connection/command failures against Redis.</summary>
    public static readonly Counter RedisConnectionFailures = Metrics.CreateCounter(
        "docs_redis_connection_failures_total",
        "Total Redis connection or command failures recorded by the Documents service.");

    // ── Redis Streams (queue) ─────────────────────────────────────────────────

    /// <summary>
    /// Total stale scan jobs reclaimed from the Redis Stream PEL via XAUTOCLAIM.
    /// High values indicate workers are crashing before ACK.
    /// </summary>
    public static readonly Counter RedisStreamReclaims = Metrics.CreateCounter(
        "docs_redis_stream_reclaims_total",
        "Total scan jobs reclaimed from crashed consumers via XAUTOCLAIM.");

    // ── Redis circuit breaker ─────────────────────────────────────────────────

    /// <summary>Current Redis circuit state: 0=closed (normal), 1=open (failing), 2=half-open (probing).</summary>
    public static readonly Gauge RedisCircuitState = Metrics.CreateGauge(
        "docs_redis_circuit_state",
        "Current Redis circuit breaker state: 0=closed, 1=open, 2=half-open.");

    /// <summary>Total times the Redis circuit has transitioned to OPEN due to repeated failures.</summary>
    public static readonly Counter RedisCircuitOpenTotal = Metrics.CreateCounter(
        "docs_redis_circuit_open_total",
        "Total times the Redis circuit breaker has opened due to repeated failures.");

    /// <summary>Total Redis operations fast-failed because the circuit was OPEN.</summary>
    public static readonly Counter RedisCircuitShortCircuitTotal = Metrics.CreateCounter(
        "docs_redis_circuit_short_circuit_total",
        "Total Redis operations fast-failed due to an open circuit breaker.");

    // ── Scan completion notifications ─────────────────────────────────────────

    /// <summary>Total DocumentScanCompleted events emitted (regardless of delivery outcome).</summary>
    public static readonly Counter ScanCompletionEventsEmitted = Metrics.CreateCounter(
        "docs_scan_completion_events_emitted_total",
        "Total DocumentScanCompleted events emitted after final scan resolution.",
        new CounterConfiguration { LabelNames = new[] { "status" } });

    /// <summary>Total notification deliveries that succeeded.</summary>
    public static readonly Counter ScanCompletionDeliverySuccess = Metrics.CreateCounter(
        "docs_scan_completion_delivery_success_total",
        "Total DocumentScanCompleted events delivered successfully.");

    /// <summary>Total notification deliveries that failed (delivery error, not scan failure).</summary>
    public static readonly Counter ScanCompletionDeliveryFailures = Metrics.CreateCounter(
        "docs_scan_completion_delivery_failures_total",
        "Total DocumentScanCompleted event delivery failures (pipeline unaffected).");

    // ── Redis Streams — durable event delivery ────────────────────────────────

    /// <summary>Total DocumentScanCompleted events successfully written to Redis Stream via XADD.</summary>
    public static readonly Counter ScanCompletionStreamPublishTotal = Metrics.CreateCounter(
        "docs_scan_completion_stream_publish_total",
        "Total scan completion events successfully published to Redis Stream (XADD).");

    /// <summary>Total XADD failures for scan completion events (circuit open, Redis down, etc.).</summary>
    public static readonly Counter ScanCompletionStreamPublishFailures = Metrics.CreateCounter(
        "docs_scan_completion_stream_publish_failures_total",
        "Total failures publishing scan completion events to Redis Stream.");
}
