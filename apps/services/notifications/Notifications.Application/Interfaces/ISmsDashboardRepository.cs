using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-008: Read-only repository for SMS dashboard aggregation queries.
///
/// All queries:
///  - Always filter Channel = "sms".
///  - Never return CredentialsJson, SettingsJson, RecipientJson, or phone numbers.
///  - Never trigger SMS sends, retries, reconciliation, or provider calls.
///  - Support optional tenantId, provider, providerConfigId, ownershipMode,
///    status, failureCategory, and date range filters via SmsDashboardQuery.
/// </summary>
public interface ISmsDashboardRepository
{
    /// <summary>
    /// Returns high-level SMS delivery and reconciliation KPI aggregate.
    /// </summary>
    Task<SmsDashboardSummaryDto> GetSummaryAsync(SmsDashboardQuery query, CancellationToken ct = default);

    /// <summary>
    /// Returns time-series trend data bucketed by hour/day/week.
    /// windowFrom and windowTo are the resolved (clamped/defaulted) date bounds used.
    /// </summary>
    Task<SmsDashboardTrendResult> GetTrendsAsync(SmsDashboardQuery query, DateTime windowFrom, DateTime windowTo, CancellationToken ct = default);

    /// <summary>
    /// Returns failure category/error breakdown grouped by (FailureCategory, LastReconciliationErrorCode).
    /// Only includes rows with Status=failed/dead_letter or non-null FailureCategory.
    /// </summary>
    Task<SmsDashboardFailureResult> GetFailureBreakdownAsync(SmsDashboardQuery query, CancellationToken ct = default);

    /// <summary>
    /// Returns per-tenant activity breakdown, ordered by total attempts descending.
    /// </summary>
    Task<SmsDashboardTenantResult> GetTenantBreakdownAsync(SmsDashboardQuery query, CancellationToken ct = default);

    /// <summary>
    /// Returns per-provider/config activity breakdown, ordered by total attempts descending.
    /// </summary>
    Task<SmsDashboardProviderResult> GetProviderBreakdownAsync(SmsDashboardQuery query, CancellationToken ct = default);
}
