// LSCC-011: Activation funnel analytics service.
//
// Data sources:
//   - CareConnect.Referrals   → ReferralsSent, ReferralsAccepted
//   - CareConnect.ActivationRequests → ActivationStarted, AutoProvisionSucceeded,
//                                       AdminApproved, FallbackPending, snapshots
//
// NOT available from DB (audit-log only, documented as null):
//   - ReferralViewed
//   - AutoProvisionFailed (direct)
//
// Auto-provision proxy:
//   Succeeded  = ActivationRequest approved WITHOUT an approver (ApprovedByUserId IS NULL)
//   FallbackPending = ActivationRequest created in range, still Pending
//
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CareConnect.Infrastructure.Services;

public sealed class ActivationFunnelAnalyticsService : IActivationFunnelAnalyticsService
{
    private readonly CareConnectDbContext                         _db;
    private readonly ILogger<ActivationFunnelAnalyticsService>   _logger;

    public ActivationFunnelAnalyticsService(
        CareConnectDbContext                       db,
        ILogger<ActivationFunnelAnalyticsService>  logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<ActivationFunnelMetrics> GetMetricsAsync(
        DateTime          startDate,
        DateTime          endDate,
        Guid?             tenantId  = null,
        CancellationToken ct        = default)
    {
        // Normalise: ensure startDate ≤ endDate, then set UTC time boundaries
        if (startDate > endDate) (startDate, endDate) = (endDate, startDate);

        var from = startDate.Date.ToUniversalTime();
        var to   = endDate.Date.AddDays(1).ToUniversalTime(); // exclusive upper bound

        _logger.LogDebug(
            "LSCC-011 Computing funnel metrics from {From:O} to {To:O} tenant={TenantId}.",
            from, to, tenantId?.ToString() ?? "platform");

        try
        {
            var counts = await ComputeCountsAsync(from, to, tenantId, ct);
            var rates  = ComputeRates(counts);

            return new ActivationFunnelMetrics
            {
                StartDate = from,
                EndDate   = to.AddDays(-1), // restore inclusive display boundary
                Counts    = counts,
                Rates     = rates,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LSCC-011 Failed to compute activation funnel metrics.");
            throw;
        }
    }

    // ── Private: DB queries ───────────────────────────────────────────────────

    // BLK-SEC-02-01: tenantId scopes all DB queries for TenantAdmin callers.
    // null = platform-wide (PlatformAdmin).
    private async Task<FunnelCounts> ComputeCountsAsync(
        DateTime from, DateTime to, Guid? tenantId, CancellationToken ct)
    {
        // ── Base query sets — apply tenant scope once ─────────────────────────
        var referralsBase        = _db.Referrals.AsQueryable();
        var activationRequestsBase = _db.ActivationRequests.AsQueryable();

        if (tenantId.HasValue)
        {
            referralsBase          = referralsBase.Where(r => r.TenantId == tenantId.Value);
            activationRequestsBase = activationRequestsBase.Where(a => a.TenantId == tenantId.Value);
        }

        // ── Referrals Sent ────────────────────────────────────────────────────
        var referralsSent = await referralsBase
            .CountAsync(r => r.CreatedAtUtc >= from && r.CreatedAtUtc < to, ct);

        // ── Referrals Accepted (advanced status within date range) ────────────
        // Uses UpdatedAtUtc as a proxy for "when the referral was accepted".
        // Limitation: UpdatedAtUtc updates on any change, not just Accept.
        // LSCC-01-001-01: Scheduled replaced by InProgress as canonical active state.
        var acceptedStatuses = new[]
        {
            Referral.ValidStatuses.Accepted,
            Referral.ValidStatuses.InProgress,
            Referral.ValidStatuses.Completed,
        };
        var referralsAccepted = await referralsBase
            .CountAsync(r =>
                acceptedStatuses.Contains(r.Status) &&
                r.CreatedAtUtc >= from &&
                r.CreatedAtUtc < to,
                ct);

        // ── Activation Started ────────────────────────────────────────────────
        // ActivationRequest is created exactly when ActivationStarted fires.
        // Deduplication by (ReferralId, ProviderId) is enforced by the domain.
        var activationStarted = await activationRequestsBase
            .CountAsync(a => a.CreatedAtUtc >= from && a.CreatedAtUtc < to, ct);

        // ── Auto Provision Succeeded ──────────────────────────────────────────
        // ApprovedByUserId IS NULL → auto-approved by the system (LSCC-010).
        // Note: "ApprovedAtUtc IS NOT NULL" ensures it was actually approved.
        var autoProvisionSucceeded = await activationRequestsBase
            .CountAsync(a =>
                a.CreatedAtUtc >= from &&
                a.CreatedAtUtc < to &&
                a.Status == ActivationRequestStatus.Approved &&
                a.ApprovedByUserId == null,
                ct);

        // ── Admin Approved ────────────────────────────────────────────────────
        // ApprovedByUserId IS NOT NULL → manually approved by an admin.
        var adminApproved = await activationRequestsBase
            .CountAsync(a =>
                a.CreatedAtUtc >= from &&
                a.CreatedAtUtc < to &&
                a.Status == ActivationRequestStatus.Approved &&
                a.ApprovedByUserId != null,
                ct);

        // ── Fallback Pending ──────────────────────────────────────────────────
        // Activation requests that were submitted in range but remain Pending.
        // Proxy for "auto-provision fell back to queue and hasn't been processed yet".
        var fallbackPending = await activationRequestsBase
            .CountAsync(a =>
                a.CreatedAtUtc >= from &&
                a.CreatedAtUtc < to &&
                a.Status == ActivationRequestStatus.Pending,
                ct);

        // ── Snapshots (current state, no date filter) ─────────────────────────
        var totalPending  = await activationRequestsBase
            .CountAsync(a => a.Status == ActivationRequestStatus.Pending, ct);

        var totalApproved = await activationRequestsBase
            .CountAsync(a => a.Status == ActivationRequestStatus.Approved, ct);

        return new FunnelCounts
        {
            ReferralsSent         = referralsSent,
            ReferralsAccepted     = referralsAccepted,
            ActivationStarted     = activationStarted,
            AutoProvisionSucceeded = autoProvisionSucceeded,
            AdminApproved         = adminApproved,
            FallbackPending       = fallbackPending,
            TotalPendingSnapshot  = totalPending,
            TotalApprovedSnapshot = totalApproved,
        };
    }

    // ── Private: rate math (static, testable independently) ──────────────────

    internal static FunnelRates ComputeRates(FunnelCounts c) => new()
    {
        ActivationRate          = SafeRate(c.ActivationStarted,     c.ReferralsSent),
        AutoProvisionSuccessRate = SafeRate(c.AutoProvisionSucceeded, c.ActivationStarted),
        FallbackRate            = SafeRate(c.FallbackPending,        c.ActivationStarted),
        OverallApprovalRate     = SafeRate(c.AutoProvisionSucceeded + c.AdminApproved, c.ActivationStarted),
        ReferralAcceptanceRate  = SafeRate(c.ReferralsAccepted,     c.ReferralsSent),
    };

    internal static double SafeRate(int numerator, int denominator)
        => denominator == 0 ? 0.0 : Math.Round((double)numerator / denominator, 6);
}
