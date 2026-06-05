using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-013: Read-only cost aggregation repository for SMS attempts.
///
/// All methods:
///   - Filter Channel = "sms" implicitly.
///   - Never return credentials, CredentialsJson, SettingsJson, or phone numbers.
///   - Never trigger sends, retries, reconciliation, or provider calls.
/// </summary>
public interface ISmsCostAnalyticsRepository
{
    Task<SmsCostSummaryDto> GetSummaryAsync(SmsCostQuery query, CancellationToken ct = default);

    Task<SmsCostTrendResult> GetTrendsAsync(
        SmsCostQuery query,
        DateTime windowFrom,
        DateTime windowTo,
        CancellationToken ct = default);

    Task<SmsCostProviderResult> GetProviderBreakdownAsync(SmsCostQuery query, CancellationToken ct = default);

    Task<SmsCostTenantResult> GetTenantBreakdownAsync(SmsCostQuery query, CancellationToken ct = default);

    Task<SmsCostFailureResult> GetFailureCostBreakdownAsync(SmsCostQuery query, CancellationToken ct = default);

    Task<SmsCostExportResult> ExportAsync(SmsCostQuery query, CancellationToken ct = default);
}
