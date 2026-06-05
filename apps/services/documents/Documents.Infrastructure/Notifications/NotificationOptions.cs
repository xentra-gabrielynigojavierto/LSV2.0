namespace Documents.Infrastructure.Notifications;

/// <summary>
/// Top-level notifications configuration.
/// Bound from the "Notifications" section in appsettings.json.
/// </summary>
public sealed class NotificationOptions
{
    public ScanCompletionNotificationOptions ScanCompletion { get; set; } = new();
}

/// <summary>
/// Configuration for the DocumentScanCompleted event publisher.
/// </summary>
public sealed class ScanCompletionNotificationOptions
{
    /// <summary>
    /// Delivery provider. Choose based on required delivery guarantee:
    ///
    ///   "log"          — structured log only (default, dev/test, zero dependencies)
    ///   "redis"        — Redis Pub/Sub channel (best-effort at-most-once; live subscribers only)
    ///   "redis-stream" — Redis Stream XADD (RECOMMENDED for production; durable + replayable)
    ///   "none"         — no-op publisher (disable notifications entirely)
    ///
    /// Production recommendation: "redis-stream" with Redis AOF/RDB persistence enabled.
    /// </summary>
    public string Provider { get; set; } = "log";

    /// <summary>Redis options (used when Provider=redis or Provider=redis-stream).</summary>
    public RedisNotificationOptions Redis { get; set; } = new();
}

/// <summary>
/// Redis options for scan completion notifications.
/// Applies to both Pub/Sub (Provider=redis) and Stream (Provider=redis-stream) publishers.
/// </summary>
public sealed class RedisNotificationOptions
{
    /// <summary>Redis Pub/Sub channel name (used when Provider=redis).</summary>
    public string Channel { get; set; } = "documents.scan.completed";

    /// <summary>
    /// Redis Stream key for durable event delivery (used when Provider=redis-stream).
    /// Consumers read via XREADGROUP for independent tracking and replay.
    /// </summary>
    public string StreamKey { get; set; } = "documents:scan:completed";

    /// <summary>
    /// MAXLEN for the completion event stream (XADD MAXLEN ~).
    /// Older entries are trimmed when the stream exceeds this length.
    /// 0 = no trim (unbounded — not recommended for production).
    /// Default: 100 000 entries (~30 days at 10 events/min).
    /// </summary>
    public int StreamMaxLength { get; set; } = 100_000;
}
