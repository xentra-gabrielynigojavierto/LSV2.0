using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-008: Service facade for SMS dashboard aggregation.
/// Orchestrates query validation, date-range defaulting, and repository calls.
/// All operations are read-only — no sends, retries, reconciliation, or provider calls.
/// </summary>
public interface ISmsDashboardService
{
    /// <summary>Returns high-level SMS delivery and reconciliation KPI aggregate.</summary>
    Task<SmsDashboardSummaryDto> GetSummaryAsync(SmsDashboardQuery query, CancellationToken ct = default);

    /// <summary>
    /// Returns time-series trend data. Defaults From/To to the last 30 days when not supplied.
    /// Normalizes bucket to hour | day | week; invalid values default to "day".
    /// </summary>
    Task<SmsDashboardTrendResult> GetTrendsAsync(SmsDashboardQuery query, CancellationToken ct = default);

    /// <summary>Returns failure category and error code breakdown.</summary>
    Task<SmsDashboardFailureResult> GetFailureBreakdownAsync(SmsDashboardQuery query, CancellationToken ct = default);

    /// <summary>Returns per-tenant activity breakdown.</summary>
    Task<SmsDashboardTenantResult> GetTenantBreakdownAsync(SmsDashboardQuery query, CancellationToken ct = default);

    /// <summary>Returns per-provider/config activity breakdown.</summary>
    Task<SmsDashboardProviderResult> GetProviderBreakdownAsync(SmsDashboardQuery query, CancellationToken ct = default);
}
