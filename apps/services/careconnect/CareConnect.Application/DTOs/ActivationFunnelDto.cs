// LSCC-011: Activation Funnel Analytics DTOs.
// All metrics are derived from existing DB tables (Referrals + ActivationRequests).
// Stages not available from DB are explicitly documented in the report.
namespace CareConnect.Application.DTOs;

/// <summary>
/// Raw stage counts for the activation funnel, computed over a date range.
/// Counts marked [snapshot] are current-state totals, not date-filtered.
/// </summary>
public sealed class FunnelCounts
{
    /// <summary>Referrals created within the date range.</summary>
    public int ReferralsSent { get; set; }

    /// <summary>
    /// Referrals whose status has advanced to Accepted / Scheduled / Completed
    /// within the date range. Proxy for provider acceptance.
    /// </summary>
    public int ReferralsAccepted { get; set; }

    /// <summary>
    /// ActivationRequests created within the date range.
    /// 1:1 with the ActivationStarted funnel event (deduplicated by (ReferralId, ProviderId)).
    /// </summary>
    public int ActivationStarted { get; set; }

    /// <summary>
    /// ActivationRequests that were auto-approved (ApprovedByUserId IS NULL, Status=Approved)
    /// and created within the date range.
    /// Proxy for AutoProvisionSucceeded.
    /// </summary>
    public int AutoProvisionSucceeded { get; set; }

    /// <summary>
    /// ActivationRequests that were approved by a named admin (ApprovedByUserId IS NOT NULL)
    /// and approved within the date range.
    /// </summary>
    public int AdminApproved { get; set; }

    /// <summary>
    /// ActivationRequests created within the date range that are still Pending.
    /// Proxy for FallbackToQueue (auto-provision did not complete).
    /// </summary>
    public int FallbackPending { get; set; }

    /// <summary>[snapshot] All ActivationRequests currently in Pending state.</summary>
    public int TotalPendingSnapshot { get; set; }

    /// <summary>[snapshot] All ActivationRequests currently in Approved state.</summary>
    public int TotalApprovedSnapshot { get; set; }

    /// <summary>
    /// ReferralViewed event count.
    /// NOT AVAILABLE from DB — audit log only. Always null.
    /// </summary>
    public int? ReferralViewed => null;
}

/// <summary>
/// Derived conversion rates for the activation funnel.
/// All rates are in [0.0, 1.0]. Zero denominators → 0.0 (never NaN or Infinity).
/// </summary>
public sealed class FunnelRates
{
    /// <summary>ActivationStarted / ReferralsSent</summary>
    public double ActivationRate { get; set; }

    /// <summary>AutoProvisionSucceeded / ActivationStarted</summary>
    public double AutoProvisionSuccessRate { get; set; }

    /// <summary>FallbackPending / ActivationStarted</summary>
    public double FallbackRate { get; set; }

    /// <summary>(AutoProvisionSucceeded + AdminApproved) / ActivationStarted</summary>
    public double OverallApprovalRate { get; set; }

    /// <summary>ReferralsAccepted / ReferralsSent</summary>
    public double ReferralAcceptanceRate { get; set; }

    /// <summary>
    /// ViewRate (Viewed / Sent) is NOT AVAILABLE — ReferralViewed is audit-log only.
    /// Always null.
    /// </summary>
    public double? ViewRate => null;
}

/// <summary>
/// Full funnel metrics response for the admin analytics page.
/// </summary>
public sealed class ActivationFunnelMetrics
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate   { get; set; }

    public FunnelCounts Counts { get; set; } = new();
    public FunnelRates  Rates  { get; set; } = new();

    /// <summary>True when no ActivationStarted events exist in the date range.</summary>
    public bool IsEmpty => Counts.ActivationStarted == 0 && Counts.ReferralsSent == 0;
}
