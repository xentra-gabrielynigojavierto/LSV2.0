// LSCC-01-005: Pure, static referral performance calculator.
//
// All computation logic lives here, separated from DB concerns.
// This enables deterministic unit testing without an EF/DB dependency.
//
// Metric rules (authoritative):
//   - Cohort anchor    : referral.CreatedAtUtc >= windowFrom
//   - AcceptedAt       : earliest ReferralStatusHistory.ChangedAtUtc where NewStatus=="Accepted"
//   - TTA              : (AcceptedAtUtc - CreatedAtUtc).TotalHours  [>= 0 only; negatives excluded]
//   - AcceptanceRate   : AcceptedCount / TotalCount  [0 when Total == 0]
//   - AvgTTA           : average of valid TTAs       [null when no valid TTA entries]
//   - Aging            : ALL currently-New referrals (NowUtc - CreatedAtUtc)
//   - Aging buckets    : < 1h | [1h, 24h) | [24h, 72h) | >= 72h
using CareConnect.Application.DTOs;

namespace CareConnect.Infrastructure.Services;

/// <summary>
/// LSCC-01-005: Stateless, testable referral performance calculator.
/// Input is plain <see cref="RawReferralRecord"/> collections; output is <see cref="ReferralPerformanceResult"/>.
/// </summary>
public static class ReferralPerformanceCalculator
{
    /// <summary>
    /// Computes the full performance result from pre-loaded records.
    /// </summary>
    /// <param name="windowRecords">
    /// Referrals whose <see cref="RawReferralRecord.CreatedAtUtc"/> falls within the selected window.
    /// Must include AcceptedAtUtc where derivable from status history.
    /// </param>
    /// <param name="currentNewReferrals">
    /// All referrals currently in Status=New (for aging distribution).
    /// Not filtered by window — this is the live operational state.
    /// </param>
    /// <param name="nowUtc">The reference UTC time (used for aging age computation).</param>
    /// <param name="windowFrom">The UTC start of the performance window.</param>
    public static ReferralPerformanceResult Compute(
        IReadOnlyList<RawReferralRecord> windowRecords,
        IReadOnlyList<(Guid Id, DateTime CreatedAtUtc)> currentNewReferrals,
        DateTime nowUtc,
        DateTime windowFrom)
    {
        var summary   = ComputeSummary(windowRecords, currentNewReferrals.Count);
        var aging     = ComputeAging(currentNewReferrals, nowUtc);
        var providers = ComputeProviders(windowRecords);

        return new ReferralPerformanceResult
        {
            WindowFrom = windowFrom,
            WindowTo   = nowUtc,
            Summary    = summary,
            Aging      = aging,
            Providers  = providers,
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Summary
    // ──────────────────────────────────────────────────────────────────────────

    internal static PerformanceSummary ComputeSummary(
        IReadOnlyList<RawReferralRecord> records,
        int currentNewCount)
    {
        var total    = records.Count;
        var accepted = records.Count(r => r.AcceptedAtUtc.HasValue);

        var validTtas = records
            .Where(r => r.AcceptedAtUtc.HasValue)
            .Select(r => (r.AcceptedAtUtc!.Value - r.CreatedAtUtc).TotalHours)
            .Where(h => h >= 0)  // exclude corrupt timestamps
            .ToList();

        return new PerformanceSummary
        {
            TotalReferrals       = total,
            AcceptedReferrals    = accepted,
            AcceptanceRate       = total > 0 ? (double)accepted / total : 0.0,
            AvgTimeToAcceptHours = validTtas.Count > 0 ? validTtas.Average() : null,
            CurrentNewReferrals  = currentNewCount,
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Aging distribution
    // ──────────────────────────────────────────────────────────────────────────

    internal static AgingDistribution ComputeAging(
        IReadOnlyList<(Guid Id, DateTime CreatedAtUtc)> newReferrals,
        DateTime nowUtc)
    {
        int lt1h = 0, h1to24 = 0, d1to3 = 0, gt3d = 0;

        foreach (var (_, createdAt) in newReferrals)
        {
            var ageHours = (nowUtc - createdAt).TotalHours;

            if      (ageHours < 1)   lt1h++;
            else if (ageHours < 24)  h1to24++;
            else if (ageHours < 72)  d1to3++;
            else                     gt3d++;
        }

        return new AgingDistribution
        {
            Lt1h   = lt1h,
            H1To24 = h1to24,
            D1To3  = d1to3,
            Gt3d   = gt3d,
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Provider performance
    // ──────────────────────────────────────────────────────────────────────────

    internal static IReadOnlyList<ProviderPerformanceRow> ComputeProviders(
        IReadOnlyList<RawReferralRecord> records)
    {
        // Group by ProviderId. Keep all providers even if no accepted referrals.
        var byProvider = records
            .GroupBy(r => r.ProviderId)
            .Select(g =>
            {
                var providerName    = g.First().ProviderName;
                var total           = g.Count();
                var acceptedRecords = g.Where(r => r.AcceptedAtUtc.HasValue).ToList();
                var accepted        = acceptedRecords.Count;

                var validTtas = acceptedRecords
                    .Select(r => (r.AcceptedAtUtc!.Value - r.CreatedAtUtc).TotalHours)
                    .Where(h => h >= 0)
                    .ToList();

                return new ProviderPerformanceRow
                {
                    ProviderId            = g.Key,
                    ProviderName          = providerName,
                    TotalReferrals        = total,
                    AcceptedReferrals     = accepted,
                    AcceptanceRate        = total > 0 ? (double)accepted / total : 0.0,
                    AvgTimeToAcceptHours  = validTtas.Count > 0 ? validTtas.Average() : null,
                };
            })
            .OrderByDescending(p => p.TotalReferrals)
            .ThenBy(p => p.ProviderName)
            .ToList();

        return byProvider;
    }
}
