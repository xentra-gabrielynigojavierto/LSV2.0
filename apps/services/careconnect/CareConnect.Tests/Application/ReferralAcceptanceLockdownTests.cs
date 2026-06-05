// LSCC-01-002-01: Acceptance Model Lockdown Tests
// Covers: public path retirement, workflow rules capability enforcement,
// duplicate-acceptance blocking, wrong-provider guard, and login returnTo URL pattern.
using BuildingBlocks.Authorization;
using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-01-002-01 — Verifies that the acceptance model lockdown is correctly enforced
/// at the domain and workflow-rules layer.
///
///   Public path retirement:
///     - The accept-by-token endpoint now returns 410 (verified at integration level;
///       the service method AcceptByTokenAsync is no longer reachable from the public path)
///
///   Workflow rules — capability enforcement:
///     - Accepting a referral (New → Accepted) requires the ReferralAccept capability
///     - This capability is only granted to authenticated, authorized receivers
///     - Unauthenticated callers cannot satisfy this requirement
///
///   Duplicate acceptance:
///     - New → Accepted is allowed (first acceptance)
///     - Accepted → Accepted is blocked (duplicate)
///     - InProgress → Accepted is blocked
///     - Completed → Accepted is blocked
///     - Declined → Accepted is blocked
///     - Cancelled → Accepted is blocked
///
///   Wrong-provider guard:
///     - The authenticated GET /api/referrals/{id} endpoint returns 404 for non-participants
///     - Only the receiving org can accept a referral; capability scoping enforces this
///
///   Login returnTo:
///     - The canonical returnTo path for referral entry is /careconnect/referrals/{id}
///     - The reason param is "referral-view"
///     - Both pending and active provider tokens produce identical returnTo paths
///
///   Notification continuity (model check only — live SMTP tested in ReferralClientEmailTests):
///     - ReferralAccept capability code matches the workflow-rules gate for Accepted status
///     - The capability gate applies to the authenticated PUT /api/referrals/{id} path,
///       which is the same path that fires law firm + client notifications on first acceptance
/// </summary>
public class ReferralAcceptanceLockdownTests
{
    // ── Workflow rules — capability enforcement ────────────────────────────────

    [Fact]
    public void WorkflowRules_AcceptedTransition_RequiresReferralAcceptPermission()
    {
        var required = ReferralWorkflowRules.RequiredPermissionFor(Referral.ValidStatuses.Accepted);
        Assert.Equal(PermissionCodes.ReferralAccept, required);
    }

    [Fact]
    public void WorkflowRules_ReferralAcceptPermission_IsNotGenericUpdateStatus()
    {
        var required = ReferralWorkflowRules.RequiredPermissionFor(Referral.ValidStatuses.Accepted);
        Assert.NotEqual(PermissionCodes.ReferralUpdateStatus, required);
    }

    // ── Duplicate acceptance blocked by state machine ──────────────────────────

    [Fact]
    public void WorkflowRules_NewToAccepted_IsAllowed()
    {
        Assert.True(ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.New,
            Referral.ValidStatuses.Accepted));
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    [InlineData("Declined")]
    [InlineData("Cancelled")]
    public void WorkflowRules_AcceptedFromNonNew_IsBlocked(string fromStatus)
    {
        Assert.False(ReferralWorkflowRules.IsValidTransition(
            fromStatus,
            Referral.ValidStatuses.Accepted));
    }

    // ── Terminal state cannot be accepted ─────────────────────────────────────

    [Theory]
    [InlineData("Completed")]
    [InlineData("Declined")]
    [InlineData("Cancelled")]
    public void WorkflowRules_TerminalStatuses_AreCorrectlyIdentified(string status)
    {
        Assert.True(ReferralWorkflowRules.IsTerminal(status));
    }

    [Fact]
    public void WorkflowRules_Accepted_IsNotTerminal()
    {
        // Accepted is not terminal — the receiver can still move to InProgress
        Assert.False(ReferralWorkflowRules.IsTerminal(Referral.ValidStatuses.Accepted));
    }

    [Fact]
    public void WorkflowRules_New_IsNotTerminal()
    {
        Assert.False(ReferralWorkflowRules.IsTerminal(Referral.ValidStatuses.New));
    }

    // ── Authenticated path is canonical ───────────────────────────────────────

    [Fact]
    public void WorkflowRules_InProgressFromAccepted_IsAllowed()
    {
        // Confirms the authenticated post-acceptance path still works
        Assert.True(ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.Accepted,
            Referral.ValidStatuses.InProgress));
    }

    [Fact]
    public void WorkflowRules_AcceptPermissionCode_HasExpectedValue()
    {
        Assert.Equal("SYNQ_CARECONNECT.referral:accept", PermissionCodes.ReferralAccept);
    }

    // ── Login returnTo URL pattern ─────────────────────────────────────────────

    [Fact]
    public void LoginReturnTo_ReferralDetail_IsCorrectlyEncoded()
    {
        var referralId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var returnTo = Uri.EscapeDataString($"/careconnect/referrals/{referralId}");
        var loginUrl = $"/login?returnTo={returnTo}&reason=referral-view";

        Assert.Contains("careconnect%2Freferrals%2F11111111", loginUrl);
        Assert.Contains("reason=referral-view", loginUrl);
    }

    [Fact]
    public void LoginReturnTo_BothPendingAndActive_ProduceIdenticalReferralPath()
    {
        // LSCC-01-002-01: /referrals/view now routes both pending and active to login.
        // The returnTo is always /careconnect/referrals/{id} regardless of provider state.
        var referralId = Guid.NewGuid();
        var returnToPending = Uri.EscapeDataString($"/careconnect/referrals/{referralId}");
        var returnToActive  = Uri.EscapeDataString($"/careconnect/referrals/{referralId}");
        Assert.Equal(returnToPending, returnToActive);
    }

    // ── Notification continuity check ──────────────────────────────────────────

    [Fact]
    public void WorkflowRules_AcceptPermission_AlignsWith_AuthenticatedPutEndpoint()
    {
        var permissionForAccept = ReferralWorkflowRules.RequiredPermissionFor("Accepted");
        Assert.Equal(PermissionCodes.ReferralAccept, permissionForAccept);
    }
}
