namespace Monitoring.Api.Contracts;

/// <summary>Per-component uptime summary within a window.</summary>
public sealed record UptimeComponentResponse(
    Guid    EntityId,
    string  EntityName,
    double? UptimePercent,
    double? WeightedAvailabilityPercent,
    int     UpCount,
    int     DegradedCount,
    int     DownCount,
    int     UnknownCount,
    int     TotalCountable,
    double? AvgLatencyMs,
    long    MaxLatencyMs,
    bool    InsufficientData);

/// <summary>
/// Response body for GET /monitoring/uptime/rollups?window=...
/// </summary>
public sealed record UptimeRollupsResponse(
    string   Window,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    double?  OverallUptimePercent,
    int      ComponentCount,
    bool     InsufficientData,
    IReadOnlyList<UptimeComponentResponse> Components);

/// <summary>One hourly bucket in a history response.</summary>
public sealed record UptimeHistoryBucketResponse(
    DateTime BucketStartUtc,
    double?  UptimePercent,
    string   DominantStatus,
    int      UpCount,
    int      DegradedCount,
    int      DownCount,
    int      UnknownCount,
    double?  AvgLatencyMs,
    long     MaxLatencyMs,
    bool     InsufficientData);

/// <summary>
/// Response body for GET /monitoring/uptime/history?entityId=...&amp;window=...
/// </summary>
public sealed record UptimeHistoryResponse(
    Guid     EntityId,
    string   EntityName,
    string   Window,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    IReadOnlyList<UptimeHistoryBucketResponse> Buckets);
