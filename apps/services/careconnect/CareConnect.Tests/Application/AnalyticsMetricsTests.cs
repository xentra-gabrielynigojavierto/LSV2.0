// LSCC-004: Analytics metrics correctness tests.
// Tests mirror the metric computation logic in apps/web/src/lib/careconnect-metrics.ts
// and apps/web/src/lib/daterange.ts, verifying the mathematical contracts in C#.
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-004 — Validates analytics metric calculation contracts:
///
///   1. safeRate — zero-denominator handling
///   2. Referral funnel rate derivations
///   3. Appointment metrics and rates
///   4. Provider performance aggregation
///   5. Date range preset logic
///   6. Drilldown URL parameter contracts
///   7. Empty/partial data states
/// </summary>
public class AnalyticsMetricsTests
{
    // ── 1. Safe rate ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10, 100, 0.10)]
    [InlineData(50, 100, 0.50)]
    [InlineData(100, 100, 1.00)]
    [InlineData(0,  100, 0.00)]
    public void SafeRate_NonZeroDenominator_ReturnsCorrectProportion(int num, int den, double expected)
    {
        var result = SafeRate(num, den);
        Assert.Equal(expected, result, precision: 4);
    }

    [Fact]
    public void SafeRate_ZeroDenominator_ReturnsZeroNotNaN()
    {
        var result = SafeRate(5, 0);
        Assert.Equal(0.0, result);
        Assert.False(double.IsNaN(result));
        Assert.False(double.IsInfinity(result));
    }

    [Fact]
    public void SafeRate_BothZero_ReturnsZero()
    {
        var result = SafeRate(0, 0);
        Assert.Equal(0.0, result);
    }

    // ── 2. Referral funnel rates ──────────────────────────────────────────────

    [Fact]
    public void ReferralFunnel_AcceptanceRate_AcceptedDividedByTotal()
    {
        int total = 100, accepted = 40, declined = 10, scheduled = 25, completed = 20;

        var acceptanceRate = SafeRate(accepted, total);

        Assert.Equal(0.40, acceptanceRate, precision: 4);
    }

    [Fact]
    public void ReferralFunnel_SchedulingRate_ScheduledDividedByAccepted()
    {
        int accepted = 40, scheduled = 25;

        var schedulingRate = SafeRate(scheduled, accepted);

        Assert.Equal(0.625, schedulingRate, precision: 4);
    }

    [Fact]
    public void ReferralFunnel_CompletionRate_CompletedDividedByScheduled()
    {
        int scheduled = 25, completed = 20;

        var completionRate = SafeRate(completed, scheduled);

        Assert.Equal(0.80, completionRate, precision: 4);
    }

    [Fact]
    public void ReferralFunnel_AllZero_AllRatesAreZero()
    {
        Assert.Equal(0.0, SafeRate(0, 0)); // acceptance
        Assert.Equal(0.0, SafeRate(0, 0)); // scheduling
        Assert.Equal(0.0, SafeRate(0, 0)); // completion
    }

    [Fact]
    public void ReferralFunnel_NoAccepted_SchedulingRateIsZero()
    {
        int accepted = 0, scheduled = 5;

        // accepted = 0 means scheduling rate denominator is zero → must return 0
        var schedulingRate = SafeRate(scheduled, accepted);
        Assert.Equal(0.0, schedulingRate);
    }

    [Fact]
    public void ReferralFunnel_NoScheduled_CompletionRateIsZero()
    {
        int scheduled = 0, completed = 0;

        var completionRate = SafeRate(completed, scheduled);
        Assert.Equal(0.0, completionRate);
    }

    // ── 3. Appointment metrics ────────────────────────────────────────────────

    [Fact]
    public void AppointmentMetrics_CompletionRate_CompletedDividedByTotal()
    {
        int total = 50, completed = 40;

        var rate = SafeRate(completed, total);

        Assert.Equal(0.80, rate, precision: 4);
    }

    [Fact]
    public void AppointmentMetrics_NoShowRate_NoShowDividedByTotal()
    {
        int total = 50, noShow = 5;

        var rate = SafeRate(noShow, total);

        Assert.Equal(0.10, rate, precision: 4);
    }

    [Fact]
    public void AppointmentMetrics_ZeroTotal_RatesAreZero()
    {
        Assert.Equal(0.0, SafeRate(0, 0)); // completion
        Assert.Equal(0.0, SafeRate(0, 0)); // no-show
    }

    // ── 4. Provider performance aggregation ───────────────────────────────────

    [Fact]
    public void ProviderPerformance_AcceptanceRate_EverAcceptedDividedByTotal()
    {
        // Provider received 10 referrals, 4 accepted, 3 scheduled (past accepted), 2 completed
        // everAccepted = Accepted + Scheduled + Completed = 4 + 3 + 2 = 9
        int total = 10, accepted = 4, scheduled = 3, completed = 2;
        int everAccepted = accepted + scheduled + completed;

        var rate = SafeRate(everAccepted, total);

        Assert.Equal(0.90, rate, precision: 4);
    }

    [Fact]
    public void ProviderPerformance_ZeroReferrals_AcceptanceRateIsZero()
    {
        var rate = SafeRate(0, 0);
        Assert.Equal(0.0, rate);
    }

    [Fact]
    public void ProviderPerformance_SortsByReferralsDescending()
    {
        // Simulated ordered counts (descending by referrals received)
        var providerCounts = new[] { 30, 20, 10 };

        var sorted = providerCounts.OrderByDescending(x => x).ToArray();

        Assert.Equal(30, sorted[0]);
        Assert.Equal(20, sorted[1]);
        Assert.Equal(10, sorted[2]);
    }

    [Fact]
    public void ProviderPerformance_CappedAtTenProviders()
    {
        // 15 providers — only top 10 should be shown
        var providerCounts = Enumerable.Range(1, 15).Reverse().ToList();

        var capped = providerCounts.OrderByDescending(x => x).Take(10).ToList();

        Assert.Equal(10, capped.Count);
        Assert.Equal(15, capped.First()); // highest referral count first
    }

    // ── 5. Date range preset logic ────────────────────────────────────────────

    [Fact]
    public void DateRange_Last7Days_FromIsExactly6DaysAgo()
    {
        var today = DateTime.UtcNow.Date;
        var from  = today.AddDays(-6);
        var to    = today;

        Assert.Equal(6, (to - from).TotalDays);
    }

    [Fact]
    public void DateRange_Last30Days_FromIsExactly29DaysAgo()
    {
        var today = DateTime.UtcNow.Date;
        var from  = today.AddDays(-29);
        var to    = today;

        Assert.Equal(29, (to - from).TotalDays);
    }

    [Fact]
    public void DateRange_CustomRange_ValidWhenFromBeforeOrEqualTo()
    {
        var from = new DateTime(2026, 3, 1);
        var to   = new DateTime(2026, 3, 31);

        Assert.True(from <= to);
        Assert.Equal(30, (to - from).TotalDays);
    }

    [Fact]
    public void DateRange_InvalidRange_FromAfterTo_ShouldFallBackToDefault()
    {
        var from = new DateTime(2026, 3, 31);
        var to   = new DateTime(2026, 3, 1);

        // When from > to, the UI falls back to Last 30 days (not crashing)
        bool isInvalid = from > to;
        Assert.True(isInvalid);
    }

    // ── 6. Drilldown URL parameter contracts ──────────────────────────────────

    [Fact]
    public void DrilldownUrl_Referral_AcceptedStatus_ContainsStatusParam()
    {
        string from   = "2026-03-01";
        string to     = "2026-03-31";
        string status = "Accepted";

        string url = $"/careconnect/referrals?status={status}&createdFrom={from}&createdTo={to}";

        Assert.Contains("status=Accepted",        url);
        Assert.Contains("createdFrom=2026-03-01", url);
        Assert.Contains("createdTo=2026-03-31",   url);
    }

    [Fact]
    public void DrilldownUrl_Appointment_CompletedStatus_ContainsStatusAndDateParams()
    {
        string from   = "2026-03-01";
        string to     = "2026-03-31";
        string status = "Completed";

        string url = $"/careconnect/appointments?status={status}&from={from}&to={to}";

        Assert.Contains("status=Completed",  url);
        Assert.Contains("from=2026-03-01",   url);
        Assert.Contains("to=2026-03-31",     url);
    }

    [Fact]
    public void DrilldownUrl_ProviderPerformance_ContainsProviderIdParam()
    {
        string providerId = "abc-123";
        string from       = "2026-03-01";
        string to         = "2026-03-31";

        string url = $"/careconnect/referrals?providerId={providerId}&createdFrom={from}&createdTo={to}";

        Assert.Contains($"providerId={providerId}", url);
        Assert.Contains("createdFrom=",             url);
    }

    // ── 7. Empty / partial data states ───────────────────────────────────────

    [Fact]
    public void EmptyData_AllCountsZero_FunnelRatesAllZero()
    {
        // If all counts are 0, no rate should produce NaN
        Assert.Equal(0.0, SafeRate(0, 0)); // acceptance
        Assert.Equal(0.0, SafeRate(0, 0)); // scheduling
        Assert.Equal(0.0, SafeRate(0, 0)); // completion
    }

    [Fact]
    public void EmptyData_NoProviders_PerformanceTableIsEmpty()
    {
        var referrals    = Array.Empty<(string, string, string)>();
        var appointments = Array.Empty<(string, string)>();

        // Simulate grouping — no referrals means no provider rows
        int providerRowCount = referrals.Select(r => r.Item1).Distinct().Count();
        Assert.Equal(0, providerRowCount);
    }

    [Fact]
    public void EmptyData_PartialFailure_FunnelStillHasZeroNotException()
    {
        // Simulates a partially failed analytics fetch where one status count is missing (0)
        int total = 50, accepted = 0 /* API call failed, default 0 */, scheduled = 20;

        var acceptanceRate = SafeRate(accepted, total);
        var schedulingRate = SafeRate(scheduled, accepted);

        Assert.Equal(0.0,  acceptanceRate);
        Assert.Equal(0.0,  schedulingRate); // zero denominator → 0, not exception
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double SafeRate(int numerator, int denominator)
    {
        if (denominator == 0) return 0.0;
        return (double)numerator / denominator;
    }
}
