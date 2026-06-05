namespace Documents.Infrastructure.Scanner;

public sealed class ScanWorkerOptions
{
    /// <summary>Queue provider: "memory" (dev) or "redis" (production).</summary>
    public string QueueProvider { get; set; } = "memory";

    /// <summary>In-memory queue capacity (ignored when QueueProvider=redis).</summary>
    public int QueueCapacity { get; set; } = 1000;

    /// <summary>Number of concurrent scan worker tasks.</summary>
    public int WorkerCount { get; set; } = 2;

    // ── Retry ────────────────────────────────────────────────────────────────

    /// <summary>Maximum scan attempts before permanently marking as Failed.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Initial delay before first retry (seconds).</summary>
    public int InitialRetryDelaySeconds { get; set; } = 5;

    /// <summary>Maximum delay cap for exponential backoff (seconds).</summary>
    public int MaxRetryDelaySeconds { get; set; } = 120;

    // ── Redis Streams ────────────────────────────────────────────────────────

    /// <summary>Redis Stream key for scan jobs.</summary>
    public string StreamKey { get; set; } = "docs:scan:jobs";

    /// <summary>Redis consumer group name.</summary>
    public string ConsumerGroup { get; set; } = "scan-workers";

    /// <summary>Idle time (seconds) before a pending message is reclaimed for retry.</summary>
    public int ClaimStaleJobsAfterSeconds { get; set; } = 300;

    /// <summary>Max stream length (MAXLEN) — trim old processed entries. 0 = no trim.</summary>
    public int StreamMaxLength { get; set; } = 50_000;
}
