// LSCC-003: API integration layer tests for the CareConnect workflow UI
// Covers: referral creation, provider accept/decline status transitions,
// appointment booking via referral+slot, and authorization enforcement.
using CareConnect.Application.DTOs;
using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-003 — Verifies the backend API contract expectations that the UI depends on.
/// Tests are pure logic: no DB, no HTTP stack.
///
/// Scenario mapping (per LSCC-003 spec section "TESTING"):
///   1. Referral creation flow — DTO is well-formed for POST /api/referrals
///   2. Provider accept — PUT /api/referrals/{id} with Accepted is a valid workflow step
///   3. Provider decline — PUT /api/referrals/{id} with Declined is a valid workflow step
///   4. Booking flow — POST /api/appointments DTO links referral + slot
///   5. Unauthorized access — terminal statuses block further transitions (backend guard)
///   6. Cancellation — CancelAppointmentRequest is accepted; appointment cancel is valid
/// </summary>
public class WorkflowIntegrationTests
{
    private static readonly Guid ProviderId       = Guid.NewGuid();
    private static readonly Guid ReferralId       = Guid.NewGuid();
    private static readonly Guid AppointmentSlotId = Guid.NewGuid();

    // ── 1. Referral creation — POST /api/referrals ────────────────────────────

    [Fact]
    public void CreateReferralRequest_RequiredFields_ArePopulated()
    {
        var req = new CreateReferralRequest
        {
            ProviderId       = ProviderId,
            ClientFirstName  = "Jane",
            ClientLastName   = "Doe",
            ClientPhone      = "702-555-0100",
            ClientEmail      = "jane.doe@example.com",
            RequestedService = "Physical Therapy",
            Urgency          = "Normal",
        };

        Assert.NotEqual(Guid.Empty, req.ProviderId);
        Assert.False(string.IsNullOrWhiteSpace(req.ClientFirstName));
        Assert.False(string.IsNullOrWhiteSpace(req.ClientLastName));
        Assert.False(string.IsNullOrWhiteSpace(req.ClientPhone));
        Assert.False(string.IsNullOrWhiteSpace(req.ClientEmail));
        Assert.False(string.IsNullOrWhiteSpace(req.RequestedService));
        Assert.False(string.IsNullOrWhiteSpace(req.Urgency));
    }

    [Fact]
    public void CreateReferralRequest_OptionalFields_DefaultToNull()
    {
        var req = new CreateReferralRequest
        {
            ProviderId       = ProviderId,
            ClientFirstName  = "Jane",
            ClientLastName   = "Doe",
            ClientPhone      = "702-555-0100",
            ClientEmail      = "jane.doe@example.com",
            RequestedService = "Chiropractic Care",
            Urgency          = "Urgent",
        };

        Assert.Null(req.ClientDob);
        Assert.Null(req.CaseNumber);
        Assert.Null(req.Notes);
        Assert.Null(req.ReferringOrganizationId);
        Assert.Null(req.ReceivingOrganizationId);
    }

    // ── 2. Provider accept — workflow transition ───────────────────────────────

    [Fact]
    public void ReferralWorkflowRules_NewToAccepted_IsValidTransition()
    {
        var isValid = ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.New,
            Referral.ValidStatuses.Accepted);

        Assert.True(isValid, "New → Accepted must be a valid referral transition (provider accepts).");
    }

    [Fact]
    public void ReferralWorkflowRules_Accept_RequiresCorrectPermissionCode()
    {
        var perm = ReferralWorkflowRules.RequiredPermissionFor(Referral.ValidStatuses.Accepted);
        Assert.Equal(BuildingBlocks.Authorization.PermissionCodes.ReferralAccept, perm);
    }

    [Fact]
    public void UpdateReferralRequest_AcceptStatus_CanBeConstructed()
    {
        var req = new UpdateReferralRequest
        {
            Status           = Referral.ValidStatuses.Accepted,
            RequestedService = "Physical Therapy",
            Urgency          = "Normal",
            Notes            = "Accepting this referral. Please confirm appointment.",
        };

        Assert.Equal(Referral.ValidStatuses.Accepted, req.Status);
        Assert.False(string.IsNullOrWhiteSpace(req.RequestedService));
    }

    // ── 3. Provider decline — PUT /api/referrals/{id} ─────────────────────────

    [Fact]
    public void ReferralWorkflowRules_NewToDeclined_IsValidTransition()
    {
        Assert.True(ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.New,
            Referral.ValidStatuses.Declined));
    }

    [Fact]
    public void ReferralWorkflowRules_AcceptedToDeclined_IsValidTransition()
    {
        Assert.True(ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.Accepted,
            Referral.ValidStatuses.Declined));
    }

    [Fact]
    public void ReferralWorkflowRules_Decline_RequiresCorrectPermissionCode()
    {
        var perm = ReferralWorkflowRules.RequiredPermissionFor(Referral.ValidStatuses.Declined);
        Assert.Equal(BuildingBlocks.Authorization.PermissionCodes.ReferralDecline, perm);
    }

    // ── 4. Booking flow — POST /api/appointments ──────────────────────────────

    [Fact]
    public void CreateAppointmentRequest_WithReferralAndSlot_HasRequiredFields()
    {
        var req = new CreateAppointmentRequest
        {
            ReferralId       = ReferralId,
            AppointmentSlotId = AppointmentSlotId,
            Notes            = "Booking via referral from law firm.",
        };

        Assert.Equal(ReferralId,        req.ReferralId);
        Assert.Equal(AppointmentSlotId, req.AppointmentSlotId);
    }

    [Fact]
    public void CreateAppointmentRequest_Notes_IsOptional()
    {
        var req = new CreateAppointmentRequest
        {
            ReferralId        = ReferralId,
            AppointmentSlotId = AppointmentSlotId,
            Notes             = null,
        };

        Assert.Null(req.Notes);
    }

    [Fact]
    public void CancelAppointmentRequest_CanBeConstructedWithNotes()
    {
        var req = new CancelAppointmentRequest
        {
            Notes = "Client requested cancellation.",
        };

        Assert.Equal("Client requested cancellation.", req.Notes);
    }

    [Fact]
    public void CancelAppointmentRequest_Notes_IsOptional()
    {
        var req = new CancelAppointmentRequest { Notes = null };
        Assert.Null(req.Notes);
    }

    // ── 5. Unauthorized access — terminal statuses block transitions ──────────

    [Fact]
    public void ReferralWorkflowRules_CompletedToAccepted_IsInvalidTransition()
    {
        Assert.False(ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.Completed,
            Referral.ValidStatuses.Accepted),
            "Completed referrals must not be re-opened.");
    }

    [Fact]
    public void ReferralWorkflowRules_CancelledToAny_IsInvalidTransition()
    {
        Assert.False(ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.Cancelled,
            Referral.ValidStatuses.New));
        Assert.False(ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.Cancelled,
            Referral.ValidStatuses.Accepted));
    }

    [Fact]
    public void ReferralWorkflowRules_DeclinedToAny_IsInvalidTransition()
    {
        Assert.False(ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.Declined,
            Referral.ValidStatuses.Accepted));
    }

    [Fact]
    public void ReferralWorkflowRules_IsTerminal_MatchesExpectedStatuses()
    {
        Assert.True(ReferralWorkflowRules.IsTerminal(Referral.ValidStatuses.Completed));
        Assert.True(ReferralWorkflowRules.IsTerminal(Referral.ValidStatuses.Cancelled));
        Assert.True(ReferralWorkflowRules.IsTerminal(Referral.ValidStatuses.Declined));
        Assert.False(ReferralWorkflowRules.IsTerminal(Referral.ValidStatuses.New));
        Assert.False(ReferralWorkflowRules.IsTerminal(Referral.ValidStatuses.Accepted));
    }

    // ── 6. Cancellation paths ─────────────────────────────────────────────────

    [Fact]
    public void ReferralWorkflowRules_NewToCancelled_IsValidTransition()
    {
        Assert.True(ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.New,
            Referral.ValidStatuses.Cancelled));
    }

    [Fact]
    public void ReferralWorkflowRules_AcceptedToCancelled_IsValidTransition()
    {
        Assert.True(ReferralWorkflowRules.IsValidTransition(
            Referral.ValidStatuses.Accepted,
            Referral.ValidStatuses.Cancelled));
    }

    [Fact]
    public void ReferralWorkflowRules_Cancel_RequiresCorrectPermissionCode()
    {
        var perm = ReferralWorkflowRules.RequiredPermissionFor(Referral.ValidStatuses.Cancelled);
        Assert.Equal(BuildingBlocks.Authorization.PermissionCodes.ReferralCancel, perm);
    }
}
