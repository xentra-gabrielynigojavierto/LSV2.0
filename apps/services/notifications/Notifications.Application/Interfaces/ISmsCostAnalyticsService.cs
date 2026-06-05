using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-013: Orchestration service for SMS cost analytics.
/// Read-only — delegates all aggregation to <see cref="ISmsCostAnalyticsRepository"/>.
/// </summary>
public interface ISmsCostAnalyticsService
{
    Task<SmsCostSummaryDto> GetSummaryAsync(SmsCostQuery query, CancellationToken ct = default);
    Task<SmsCostTrendResult> GetTrendsAsync(SmsCostQuery query, CancellationToken ct = default);
    Task<SmsCostProviderResult> GetProviderBreakdownAsync(SmsCostQuery query, CancellationToken ct = default);
    Task<SmsCostTenantResult> GetTenantBreakdownAsync(SmsCostQuery query, CancellationToken ct = default);
    Task<SmsCostFailureResult> GetFailureCostBreakdownAsync(SmsCostQuery query, CancellationToken ct = default);
    Task<SmsCostExportResult> ExportAsync(SmsCostQuery query, CancellationToken ct = default);
}
