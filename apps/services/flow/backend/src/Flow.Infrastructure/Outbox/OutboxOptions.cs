namespace Flow.Infrastructure.Outbox;

/// <summary>
/// LS-FLOW-E10.2 — runtime configuration for the outbox processor.
/// Bound to the <c>Outbox</c> section of <c>appsettings.json</c> /
/// environment variables. All defaults are sane for local dev.
/// </summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Master switch. Disable to keep enqueueing rows but pause processing (useful in staging).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Polling cadence between worker ticks.</summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>Max rows claimed per tick.</summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>Cap on attempts before a row is moved to <c>DeadLettered</c>.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Base of the exponential backoff series, in seconds.</summary>
    public int BaseBackoffSeconds { get; set; } = 30;

    /// <summary>Multiplier applied between attempts (e.g. 2 → 30s, 60s, 120s, 240s, 480s).</summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Lease window for a claimed row. If a row sits in <c>Processing</c>
    /// longer than this without progressing, it is considered orphaned
    /// (worker crashed, container terminated, etc.) and is reaped back to
    /// <c>Pending</c> at the start of the next tick — or moved straight to
    /// <c>DeadLettered</c> if attempts are already exhausted. Default is
    /// 5 minutes, comfortably larger than any sane downstream HTTP timeout.
    /// </summary>
    public int ProcessingLeaseSeconds { get; set; } = 300;
}
