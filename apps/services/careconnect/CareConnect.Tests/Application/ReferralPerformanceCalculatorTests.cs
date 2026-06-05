// LSCC-01-005: ReferralPerformanceCalculator unit tests.
//
// Tests the pure computation layer without any DB dependency.
// Scenarios covered:
//   1.  Acceptance rate — standard case
//   2.  Acceptance rate — zero denominator (total=0) → returns 0
//   3.  Acceptance rate — all accepted
//   4.  Avg TTA — standard case
//   5.  Avg TTA — no accepted referrals → returns null
//   6.  Avg TTA — negative TTA (corrupt timestamp) excluded from average
//   7.  Aging distribution — all four buckets
//   8.  Aging distribution — empty list → all zeros
//   9.  Provider aggregation — multiple providers grouped correctly
//   10. Provider aggregation — provider with zero accepted shows null avg TTA
//   11. Empty dataset — summary returns zeros, aging returns zeros
//   12. Summary — currentNewReferrals reflects the count passed in
//   13. Compute — windowFrom/windowTo are set correctly
using CareConnect.Application.DTOs;
using CareConnect.Infrastructure.Services;
using Xunit;

namespace CareConnect.Tests.Application;

public class ReferralPerformanceCalculatorTests
{
    private static readonly Guid ProviderId1 = Guid.NewGuid();
    private static readonly Guid ProviderId2 = Guid.NewGuid();
    private static readonly DateTime Base = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RawReferralRecord MakeRecord(
        Guid     providerId,
        string   status,
        double   createdHoursAgo,
        double?  acceptedHoursAgo,
        DateTime nowUtc,
        string   providerName = "Provider A")
    {
        var created  = nowUtc.AddHours(-createdHoursAgo);
        DateTime? accepted = acceptedHoursAgo.HasValue
            ? nowUtc.AddHours(-acceptedHoursAgo.Value)
            : null;

        return new RawReferralRecord(
            Id:           Guid.NewGuid(),
            CreatedAtUtc: created,
            Status:       status,
            ProviderId:   providerId,
            ProviderName: providerName,
            AcceptedAtUtc: accepted);
    }

    // ── 1. Acceptance rate — standard ─────────────────────────────────────────

    [Fact]
    public void ComputeSummary_StandardCase_AcceptanceRateIsCorrect()
    {
        var records = new List<RawReferralRecord>
        {
            MakeRecord(ProviderId1, "Accepted", 48, 24, Base),  // TTA = 24h
            MakeRecord(ProviderId1, "New",       10, null, Base),
            MakeRecord(ProviderId1, "Declined",  72, null, Base),
        };

        var summary = ReferralPerformanceCalculator.ComputeSummary(records, 0);

        Assert.Equal(3, summary.TotalReferrals);
        Assert.Equal(1, summary.AcceptedReferrals);
        Assert.Equal(1.0 / 3.0, summary.AcceptanceRate, precision: 10);
    }

    // ── 2. Acceptance rate — total=0 → 0 ─────────────────────────────────────

    [Fact]
    public void ComputeSummary_EmptyRecords_AcceptanceRateIsZero()
    {
        var summary = ReferralPerformanceCalculator.ComputeSummary(
            Array.Empty<RawReferralRecord>(), currentNewCount: 0);

        Assert.Equal(0, summary.TotalReferrals);
        Assert.Equal(0.0, summary.AcceptanceRate);
        Assert.Null(summary.AvgTimeToAcceptHours);
    }

    // ── 3. Acceptance rate — all accepted ─────────────────────────────────────

    [Fact]
    public void ComputeSummary_AllAccepted_AcceptanceRateIsOne()
    {
        var records = new List<RawReferralRecord>
        {
            MakeRecord(ProviderId1, "Accepted", 48, 24, Base),
            MakeRecord(ProviderId1, "Accepted", 20, 10, Base),
        };

        var summary = ReferralPerformanceCalculator.ComputeSummary(records, 0);

        Assert.Equal(1.0, summary.AcceptanceRate, precision: 10);
    }

    // ── 4. Avg TTA — standard ─────────────────────────────────────────────────

    [Fact]
    public void ComputeSummary_MultipleAccepted_AvgTtaIsCorrect()
    {
        // Record A: created 48h ago, accepted 24h ago → TTA = 24h
        // Record B: created 30h ago, accepted  6h ago → TTA = 24h
        var records = new List<RawReferralRecord>
        {
            MakeRecord(ProviderId1, "Accepted", 48, 24, Base),
            MakeRecord(ProviderId1, "Accepted", 30,  6, Base),
        };

        var summary = ReferralPerformanceCalculator.ComputeSummary(records, 0);

        Assert.NotNull(summary.AvgTimeToAcceptHours);
        Assert.Equal(24.0, summary.AvgTimeToAcceptHours!.Value, precision: 6);
    }

    // ── 5. Avg TTA — no accepted → null ──────────────────────────────────────

    [Fact]
    public void ComputeSummary_NoAcceptedReferrals_AvgTtaIsNull()
    {
        var records = new List<RawReferralRecord>
        {
            MakeRecord(ProviderId1, "New",      10, null, Base),
            MakeRecord(ProviderId1, "Declined", 20, null, Base),
        };

        var summary = ReferralPerformanceCalculator.ComputeSummary(records, 0);

        Assert.Null(summary.AvgTimeToAcceptHours);
    }

    // ── 6. Avg TTA — negative TTA excluded ───────────────────────────────────

    [Fact]
    public void ComputeSummary_NegativeTtaExcluded_AvgTtaIgnoresCorruptRecord()
    {
        // Normal: TTA = 24h. Corrupt: accepted before created (negative TTA).
        var normalRecord = MakeRecord(ProviderId1, "Accepted", 48, 24, Base);

        // Corrupt: AcceptedAt is before CreatedAt → TTA < 0
        var corruptRecord = new RawReferralRecord(
            Id:           Guid.NewGuid(),
            CreatedAtUtc: Base.AddHours(-10),
            Status:       "Accepted",
            ProviderId:   ProviderId1,
            ProviderName: "Provider A",
            AcceptedAtUtc: Base.AddHours(-20));  // accepted before created

        var records = new List<RawReferralRecord> { normalRecord, corruptRecord };
        var summary = ReferralPerformanceCalculator.ComputeSummary(records, 0);

        // Only the normal record contributes to avg TTA
        Assert.Equal(24.0, summary.AvgTimeToAcceptHours!.Value, precision: 6);
    }

    // ── 7. Aging distribution — all four buckets ──────────────────────────────

    [Fact]
    public void ComputeAging_AllBuckets_CountsAreCorrect()
    {
        var nowUtc = Base;

        var newReferrals = new List<(Guid, DateTime)>
        {
            (Guid.NewGuid(), nowUtc.AddMinutes(-30)),   // < 1h  → lt1h
            (Guid.NewGuid(), nowUtc.AddHours(-12)),     // 12h   → h1to24
            (Guid.NewGuid(), nowUtc.AddHours(-36)),     // 36h   → d1to3
            (Guid.NewGuid(), nowUtc.AddHours(-100)),    // 100h  → gt3d
        };

        var aging = ReferralPerformanceCalculator.ComputeAging(newReferrals, nowUtc);

        Assert.Equal(1, aging.Lt1h);
        Assert.Equal(1, aging.H1To24);
        Assert.Equal(1, aging.D1To3);
        Assert.Equal(1, aging.Gt3d);
        Assert.Equal(4, aging.Total);
    }

    // ── 8. Aging distribution — empty list ────────────────────────────────────

    [Fact]
    public void ComputeAging_EmptyList_AllBucketsZero()
    {
        var aging = ReferralPerformanceCalculator.ComputeAging(
            Array.Empty<(Guid, DateTime)>(), Base);

        Assert.Equal(0, aging.Lt1h);
        Assert.Equal(0, aging.H1To24);
        Assert.Equal(0, aging.D1To3);
        Assert.Equal(0, aging.Gt3d);
        Assert.Equal(0, aging.Total);
    }

    // ── 9. Provider aggregation — grouped correctly ────────────────────────────

    [Fact]
    public void ComputeProviders_MultipleProviders_GroupedCorrectly()
    {
        // Alpha: 2 referrals — 1 accepted 24h after creation (TTA=24h), 1 New
        // Beta:  2 referrals — both accepted
        //   Record B1: created 20h ago, accepted 10h ago → TTA = 10h
        //   Record B2: created 30h ago, accepted 10h ago → TTA = 20h
        //   Beta avg TTA = (10+20)/2 = 15h
        var records = new List<RawReferralRecord>
        {
            MakeRecord(ProviderId1, "Accepted", 48, 24, Base, "Alpha"),  // TTA = 24h
            MakeRecord(ProviderId1, "New",      10, null, Base, "Alpha"),
            MakeRecord(ProviderId2, "Accepted", 20, 10, Base, "Beta"),   // TTA = 10h
            MakeRecord(ProviderId2, "Accepted", 30, 10, Base, "Beta"),   // TTA = 20h
        };

        var rows = ReferralPerformanceCalculator.ComputeProviders(records);

        Assert.Equal(2, rows.Count);

        var alpha = rows.First(r => r.ProviderId == ProviderId1);
        Assert.Equal(2, alpha.TotalReferrals);
        Assert.Equal(1, alpha.AcceptedReferrals);
        Assert.Equal(0.5, alpha.AcceptanceRate, precision: 10);
        Assert.Equal(24.0, alpha.AvgTimeToAcceptHours!.Value, precision: 6);

        var beta = rows.First(r => r.ProviderId == ProviderId2);
        Assert.Equal(2, beta.TotalReferrals);
        Assert.Equal(2, beta.AcceptedReferrals);
        Assert.Equal(1.0, beta.AcceptanceRate, precision: 10);
        Assert.Equal(15.0, beta.AvgTimeToAcceptHours!.Value, precision: 6);  // (10+20)/2
    }

    // ── 10. Provider with zero accepted → null avg TTA ────────────────────────

    [Fact]
    public void ComputeProviders_ZeroAccepted_AvgTtaIsNull()
    {
        var records = new List<RawReferralRecord>
        {
            MakeRecord(ProviderId1, "New",      10, null, Base),
            MakeRecord(ProviderId1, "Declined", 20, null, Base),
        };

        var rows = ReferralPerformanceCalculator.ComputeProviders(records);

        Assert.Single(rows);
        Assert.Equal(0, rows[0].AcceptedReferrals);
        Assert.Null(rows[0].AvgTimeToAcceptHours);
    }

    // ── 11. Empty dataset — all zeros ─────────────────────────────────────────

    [Fact]
    public void Compute_EmptyDataset_ReturnsSafeZeroValues()
    {
        var result = ReferralPerformanceCalculator.Compute(
            windowRecords:       Array.Empty<RawReferralRecord>(),
            currentNewReferrals: Array.Empty<(Guid, DateTime)>(),
            nowUtc:              Base,
            windowFrom:          Base.AddDays(-7));

        Assert.Equal(0, result.Summary.TotalReferrals);
        Assert.Equal(0, result.Summary.AcceptedReferrals);
        Assert.Equal(0.0, result.Summary.AcceptanceRate);
        Assert.Null(result.Summary.AvgTimeToAcceptHours);
        Assert.Equal(0, result.Aging.Total);
        Assert.Empty(result.Providers);
    }

    // ── 12. currentNewReferrals count is propagated ───────────────────────────

    [Fact]
    public void ComputeSummary_CurrentNewCountIsPropagated()
    {
        var summary = ReferralPerformanceCalculator.ComputeSummary(
            Array.Empty<RawReferralRecord>(), currentNewCount: 42);

        Assert.Equal(42, summary.CurrentNewReferrals);
    }

    // ── 13. Compute — windowFrom/windowTo set correctly ───────────────────────

    [Fact]
    public void Compute_WindowBoundaries_AreSetCorrectly()
    {
        var since = Base.AddDays(-7);
        var now   = Base;

        var result = ReferralPerformanceCalculator.Compute(
            windowRecords:       Array.Empty<RawReferralRecord>(),
            currentNewReferrals: Array.Empty<(Guid, DateTime)>(),
            nowUtc:              now,
            windowFrom:          since);

        Assert.Equal(since, result.WindowFrom);
        Assert.Equal(now,   result.WindowTo);
    }
}
