namespace Monitoring.Application.Queries;

/// <summary>
/// Read-only query service for uptime rollup data derived from check history.
/// Implemented by <c>EfCoreUptimeReadService</c> in the Infrastructure layer.
/// </summary>
public interface IUptimeReadService
{
    /// <summary>
    /// Returns per-component uptime summaries aggregated over the specified window.
    /// </summary>
    /// <param name="window">One of: 24h, 7d, 30d, 90d.</param>
    Task<UptimeRollupsResult> GetRollupsAsync(string window, CancellationToken ct);

    /// <summary>
    /// Returns hour-by-hour uptime buckets for a single monitored entity.
    /// </summary>
    /// <param name="entityId">The monitored entity's primary key.</param>
    /// <param name="window">One of: 24h, 7d, 30d, 90d.</param>
    Task<UptimeHistoryResult?> GetHistoryAsync(Guid entityId, string window, CancellationToken ct);
}
