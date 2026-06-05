using Monitoring.Domain.Monitoring;

namespace Monitoring.Application.Queries;

/// <summary>
/// Application-layer result record for a single entity's current monitoring status.
/// Produced by <see cref="IMonitoringReadService"/>; mapped to a wire response by the API layer.
/// </summary>
/// <param name="EntityId">Primary key of the monitored entity.</param>
/// <param name="Name">Display name (snapshotted from MonitoredEntity).</param>
/// <param name="Scope">Product/platform grouping.</param>
/// <param name="Status">Evaluated status; <see cref="EntityStatus.Unknown"/> when the scheduler has not yet produced a result row.</param>
/// <param name="LastCheckedAtUtc">Timestamp of the last completed check; null when no result row exists yet.</param>
/// <param name="LastElapsedMs">Round-trip latency of the last check in milliseconds; null when no result row exists yet.</param>
public sealed record MonitoringStatusResult(
    Guid EntityId,
    string Name,
    string Scope,
    EntityStatus Status,
    DateTime? LastCheckedAtUtc,
    long? LastElapsedMs);

/// <summary>
/// Application-layer result record for a single monitoring alert.
/// Only active alerts (IsActive = true) are returned by the read service.
/// </summary>
public sealed record MonitoringAlertResult(
    Guid AlertId,
    Guid EntityId,
    string Name,
    string Scope,
    ImpactLevel ImpactLevel,
    AlertType AlertType,
    string Message,
    DateTime TriggeredAtUtc,
    DateTime? ResolvedAtUtc);

/// <summary>
/// Application-layer aggregate result for the full monitoring summary.
/// </summary>
/// <param name="Statuses">All monitored entities with their current status.</param>
/// <param name="ActiveAlerts">All currently active alerts (IsActive = true).</param>
public sealed record MonitoringSummaryResult(
    IReadOnlyList<MonitoringStatusResult> Statuses,
    IReadOnlyList<MonitoringAlertResult> ActiveAlerts);
