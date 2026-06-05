// LSCC-011: Activation funnel analytics service interface.
using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

/// <summary>
/// Computes activation funnel metrics from existing CareConnect data.
/// All metrics are derived — no analytics tables are created.
/// </summary>
public interface IActivationFunnelAnalyticsService
{
    /// <summary>
    /// Returns funnel counts and conversion rates for the given date range.
    /// The range is inclusive: [startDate, endDate] (UTC, date-only boundaries).
    /// If startDate > endDate the range is swapped before querying.
    /// BLK-SEC-02-01: tenantId scopes all queries to a single tenant.
    ///   null → platform-wide (PlatformAdmin only).
    /// </summary>
    Task<ActivationFunnelMetrics> GetMetricsAsync(
        DateTime          startDate,
        DateTime          endDate,
        Guid?             tenantId  = null,
        CancellationToken ct        = default);
}
