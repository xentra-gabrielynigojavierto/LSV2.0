// LSCC-011: Activation Funnel Analytics Tests
//
// Tests the pure rate-calculation logic of ActivationFunnelAnalyticsService.ComputeRates
// and SafeRate, which are deterministic given only count inputs.
//
// Note: The DB-query layer (ComputeCountsAsync) is not tested here — it requires an
// EF Core InMemory provider. Rate calculations are the highest-value test surface
// because they govern every number shown in the admin UI.
using CareConnect.Application.DTOs;
using CareConnect.Infrastructure.Services;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-011 — Activation Funnel Analytics:
///
///   SafeRate:
///     - Non-zero denominator → correct proportion
///     - Zero denominator → 0.0 (not NaN, not Infinity)
///     - Both zero → 0.0
///
///   ComputeRates:
///     - Full data: all rates populated correctly
///     - Zero ActivationStarted: activation/approval rates all 0
///     - Zero ReferralsSent: acceptance rate 0
///     - AutoProvision 100% success rate
///     - Mixed auto + admin approval → OverallApprovalRate correct
///     - Fallback rate correct
///     - ReferralsAccepted → referralAcceptanceRate correct
///     - ViewRate always null (audit-log only)
///
///   ActivationFunnelMetrics.IsEmpty:
///     - Empty when both referralsSent and activationStarted are 0
///     - Not empty when any count > 0
/// </summary>
public class ActivationFunnelAnalyticsTests
{
    // ── SafeRate ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(10,  100, 0.1)]
    [InlineData(50,  100, 0.5)]
    [InlineData(100, 100, 1.0)]
    [InlineData(0,   100, 0.0)]
    [InlineData(1,   3,   0.333333)]
    public void SafeRate_NonZero_ReturnsCorrectProportion(int num, int den, double expected)
    {
        var result = ActivationFunnelAnalyticsService.SafeRate(num, den);
        Assert.Equal(expected, result, precision: 4);
    }

    [Fact]
    public void SafeRate_ZeroDenominator_ReturnsZero_NotNaN()
    {
        var result = ActivationFunnelAnalyticsService.SafeRate(5, 0);
        Assert.Equal(0.0, result);
        Assert.False(double.IsNaN(result));
        Assert.False(double.IsInfinity(result));
    }

    [Fact]
    public void SafeRate_BothZero_ReturnsZero()
    {
        var result = ActivationFunnelAnalyticsService.SafeRate(0, 0);
        Assert.Equal(0.0, result);
    }

    // ── ComputeRates — full data ──────────────────────────────────────────────

    [Fact]
    public void ComputeRates_FullData_AllRatesCorrect()
    {
        var counts = new FunnelCounts
        {
            ReferralsSent          = 100,
            ActivationStarted      = 40,
            AutoProvisionSucceeded = 30,
            AdminApproved          = 5,
            FallbackPending        = 5,
            ReferralsAccepted      = 20,
        };

        var rates = ActivationFunnelAnalyticsService.ComputeRates(counts);

        Assert.Equal(0.40,  rates.ActivationRate,           precision: 4); // 40/100
        Assert.Equal(0.75,  rates.AutoProvisionSuccessRate, precision: 4); // 30/40
        Assert.Equal(0.125, rates.FallbackRate,             precision: 4); // 5/40
        Assert.Equal(0.875, rates.OverallApprovalRate,      precision: 4); // 35/40
        Assert.Equal(0.20,  rates.ReferralAcceptanceRate,   precision: 4); // 20/100
    }

    // ── ComputeRates — zero activation started ────────────────────────────────

    [Fact]
    public void ComputeRates_ZeroActivationStarted_ActivationRelatedRatesAreZero()
    {
        var counts = new FunnelCounts
        {
            ReferralsSent          = 50,
            ActivationStarted      = 0,
            AutoProvisionSucceeded = 0,
            AdminApproved          = 0,
            FallbackPending        = 0,
            ReferralsAccepted      = 0,
        };

        var rates = ActivationFunnelAnalyticsService.ComputeRates(counts);

        Assert.Equal(0.0, rates.ActivationRate);
        Assert.Equal(0.0, rates.AutoProvisionSuccessRate);
        Assert.Equal(0.0, rates.FallbackRate);
        Assert.Equal(0.0, rates.OverallApprovalRate);
        Assert.False(double.IsNaN(rates.AutoProvisionSuccessRate));
        Assert.False(double.IsNaN(rates.FallbackRate));
    }

    // ── ComputeRates — zero referrals sent ────────────────────────────────────

    [Fact]
    public void ComputeRates_ZeroReferralsSent_AcceptanceRateIsZero()
    {
        var counts = new FunnelCounts
        {
            ReferralsSent          = 0,
            ActivationStarted      = 0,
            ReferralsAccepted      = 0,
        };

        var rates = ActivationFunnelAnalyticsService.ComputeRates(counts);

        Assert.Equal(0.0, rates.ActivationRate);
        Assert.Equal(0.0, rates.ReferralAcceptanceRate);
        Assert.False(double.IsNaN(rates.ReferralAcceptanceRate));
    }

    // ── ComputeRates — all empty ──────────────────────────────────────────────

    [Fact]
    public void ComputeRates_AllZero_AllRatesZero_NoNaN()
    {
        var counts = new FunnelCounts();

        var rates = ActivationFunnelAnalyticsService.ComputeRates(counts);

        Assert.Equal(0.0, rates.ActivationRate);
        Assert.Equal(0.0, rates.AutoProvisionSuccessRate);
        Assert.Equal(0.0, rates.FallbackRate);
        Assert.Equal(0.0, rates.OverallApprovalRate);
        Assert.Equal(0.0, rates.ReferralAcceptanceRate);
        Assert.False(double.IsNaN(rates.ActivationRate));
        Assert.False(double.IsNaN(rates.AutoProvisionSuccessRate));
    }

    // ── ComputeRates — 100% auto-provision success ────────────────────────────

    [Fact]
    public void ComputeRates_AllAutoProvisioned_SuccessRateIsOne()
    {
        var counts = new FunnelCounts
        {
            ReferralsSent          = 20,
            ActivationStarted      = 20,
            AutoProvisionSucceeded = 20,
            FallbackPending        = 0,
            AdminApproved          = 0,
        };

        var rates = ActivationFunnelAnalyticsService.ComputeRates(counts);

        Assert.Equal(1.0, rates.AutoProvisionSuccessRate, precision: 4);
        Assert.Equal(0.0, rates.FallbackRate);
        Assert.Equal(1.0, rates.OverallApprovalRate, precision: 4);
    }

    // ── ComputeRates — mixed auto + admin approval ────────────────────────────

    [Fact]
    public void ComputeRates_MixedAutoAndAdmin_OverallApprovalCombinesBoth()
    {
        var counts = new FunnelCounts
        {
            ActivationStarted      = 10,
            AutoProvisionSucceeded = 6,
            AdminApproved          = 3,
            FallbackPending        = 1,
        };

        var rates = ActivationFunnelAnalyticsService.ComputeRates(counts);

        // OverallApproval = (6 + 3) / 10 = 0.9
        Assert.Equal(0.9,  rates.OverallApprovalRate,      precision: 4);
        Assert.Equal(0.6,  rates.AutoProvisionSuccessRate, precision: 4);
        Assert.Equal(0.1,  rates.FallbackRate,             precision: 4);
    }

    // ── ComputeRates — ViewRate always null ───────────────────────────────────

    [Fact]
    public void ComputeRates_ViewRate_AlwaysNull()
    {
        var rates = ActivationFunnelAnalyticsService.ComputeRates(new FunnelCounts
        {
            ReferralsSent     = 50,
            ActivationStarted = 10,
        });

        Assert.Null(rates.ViewRate);
    }

    // ── FunnelCounts.ReferralViewed always null ───────────────────────────────

    [Fact]
    public void FunnelCounts_ReferralViewed_AlwaysNull()
    {
        var counts = new FunnelCounts { ReferralsSent = 100 };
        Assert.Null(counts.ReferralViewed);
    }

    // ── ActivationFunnelMetrics.IsEmpty ───────────────────────────────────────

    [Fact]
    public void IsEmpty_WhenBothReferralsSentAndActivationStartedAreZero_True()
    {
        var metrics = new ActivationFunnelMetrics
        {
            Counts = new FunnelCounts { ReferralsSent = 0, ActivationStarted = 0 },
        };
        Assert.True(metrics.IsEmpty);
    }

    [Fact]
    public void IsEmpty_WhenReferralsSentIsNonZero_False()
    {
        var metrics = new ActivationFunnelMetrics
        {
            Counts = new FunnelCounts { ReferralsSent = 5, ActivationStarted = 0 },
        };
        Assert.False(metrics.IsEmpty);
    }

    [Fact]
    public void IsEmpty_WhenActivationStartedIsNonZero_False()
    {
        var metrics = new ActivationFunnelMetrics
        {
            Counts = new FunnelCounts { ReferralsSent = 0, ActivationStarted = 3 },
        };
        Assert.False(metrics.IsEmpty);
    }

    // ── Date range swap ───────────────────────────────────────────────────────

    [Fact]
    public void ComputeRates_Deterministic_SameInputSameOutput()
    {
        var counts = new FunnelCounts
        {
            ReferralsSent          = 75,
            ActivationStarted      = 30,
            AutoProvisionSucceeded = 24,
            AdminApproved          = 2,
            FallbackPending        = 4,
            ReferralsAccepted      = 15,
        };

        var r1 = ActivationFunnelAnalyticsService.ComputeRates(counts);
        var r2 = ActivationFunnelAnalyticsService.ComputeRates(counts);

        Assert.Equal(r1.ActivationRate,           r2.ActivationRate);
        Assert.Equal(r1.AutoProvisionSuccessRate, r2.AutoProvisionSuccessRate);
        Assert.Equal(r1.FallbackRate,             r2.FallbackRate);
        Assert.Equal(r1.OverallApprovalRate,      r2.OverallApprovalRate);
        Assert.Equal(r1.ReferralAcceptanceRate,   r2.ReferralAcceptanceRate);
    }
}
