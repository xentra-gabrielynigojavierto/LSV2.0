namespace Monitoring.Application.Queries;

// ── Per-component summary (used in rollup response) ───────────────────────────

/// <summary>Per-component uptime summary within a window.</summary>
public sealed record UptimeComponentSummary(
    Guid    EntityId,
    string  EntityName,
    int     UpCount,
    int     DegradedCount,
    int     DownCount,
    int     UnknownCount,
    int     TotalCountable,
    double? UptimePercent,
    double? WeightedAvailabilityPercent,
    double? AvgLatencyMs,
    long    MaxLatencyMs,
    bool    InsufficientData);

// ── Rollup response (across all components for a window) ─────────────────────

/// <summary>
/// Aggregate uptime result for all monitored entities over a given window.
/// </summary>
public sealed record UptimeRollupsResult(
    string    Window,
    DateTime  WindowStartUtc,
    DateTime  WindowEndUtc,
    double?   OverallUptimePercent,
    int       ComponentCount,
    bool      InsufficientData,
    IReadOnlyList<UptimeComponentSummary> Components);

// ── Hourly bucket (used in history response) ──────────────────────────────────

/// <summary>One hourly bucket entry in the history response.</summary>
public sealed record UptimeHistoryBucket(
    DateTime BucketStartUtc,
    int      UpCount,
    int      DegradedCount,
    int      DownCount,
    int      UnknownCount,
    double?  UptimePercent,
    string   DominantStatus,
    double?  AvgLatencyMs,
    long     MaxLatencyMs,
    bool     InsufficientData);

// ── History response (for a single entity) ────────────────────────────────────

/// <summary>
/// Per-entity uptime history over a given window, bucketed by hour.
/// Null when the entity is not found.
/// </summary>
public sealed record UptimeHistoryResult(
    Guid      EntityId,
    string    EntityName,
    string    Window,
    DateTime  WindowStartUtc,
    DateTime  WindowEndUtc,
    IReadOnlyList<UptimeHistoryBucket> Buckets);
