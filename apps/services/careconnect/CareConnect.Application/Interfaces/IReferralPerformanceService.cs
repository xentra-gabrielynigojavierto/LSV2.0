// LSCC-01-005: Referral performance metrics service interface.
using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

/// <summary>
/// LSCC-01-005: Computes admin-facing referral performance metrics.
///
/// All metrics are read-only.  Implementations must handle empty/partial data
/// without throwing (return safe zero/null values instead).
/// </summary>
public interface IReferralPerformanceService
{
    /// <summary>
    /// Returns referral performance metrics for the given time window.
    /// </summary>
    /// <param name="since">
    /// UTC start of the window.  Referrals with CreatedAtUtc &gt;= since are included
    /// in cohort metrics (total, accepted, acceptance rate, TTA, provider stats).
    /// Aging distribution always covers ALL currently-New referrals.
    /// </param>
    /// <param name="tenantId">
    /// BLK-SEC-02-01: When non-null, restricts all queries to the specified tenant.
    /// Null means platform-wide (PlatformAdmin only).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<ReferralPerformanceResult> GetPerformanceAsync(
        DateTime          since,
        Guid?             tenantId  = null,
        CancellationToken ct        = default);
}
