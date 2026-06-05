namespace Monitoring.Application.Queries;

/// <summary>
/// Read-only service providing aggregated monitoring state for API consumers.
///
/// <para>All methods are read-only and non-mutating. Implementations must
/// use <c>AsNoTracking()</c> on every query to avoid change-tracker overhead.</para>
///
/// <para>When the DB is unavailable, implementations should surface exceptions
/// so the API layer can return an appropriate problem response, rather than
/// silently returning empty results that would be indistinguishable from
/// a healthy, empty registry.</para>
/// </summary>
public interface IMonitoringReadService
{
    /// <summary>
    /// Returns the current monitoring status for every registered entity.
    /// Entities that have never been checked return <c>EntityStatus.Unknown</c>
    /// with null latency and timestamp fields.
    /// </summary>
    Task<MonitoringStatusResult[]> GetStatusAsync(CancellationToken ct);

    /// <summary>
    /// Returns all currently active alerts (<c>IsActive = true</c>),
    /// ordered by <c>TriggeredAtUtc</c> descending (newest first).
    /// </summary>
    Task<MonitoringAlertResult[]> GetActiveAlertsAsync(CancellationToken ct);

    /// <summary>
    /// Returns the full monitoring summary: per-entity statuses and
    /// all active alerts. Callers that need both sets should prefer
    /// this method to avoid two round-trips.
    /// </summary>
    Task<MonitoringSummaryResult> GetSummaryAsync(CancellationToken ct);
}
