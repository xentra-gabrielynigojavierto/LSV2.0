// LSCC-002-01: HTTP access validation tests — participant-check conditions.
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.Authorization;
using CareConnect.Application.Interfaces;
using CareConnect.Domain;
using Moq;
using Xunit;

namespace CareConnect.Tests.Authorization;

/// <summary>
/// LSCC-002-01 — Access control validation tests.
///
/// These tests validate the participant-check conditions that govern row-level
/// access to CareConnect resources (referrals and appointments). They exercise
/// the exact logic used by the endpoint handlers without requiring a running
/// HTTP server.
///
/// Scenarios covered:
///   Non-participant org is not a referral participant
///   Referring org is a referral participant
///   Receiving org is a referral participant
///   PlatformAdmin bypasses participant check (IsAdmin = true)
///   TenantAdmin bypasses participant check  (IsAdmin = true)
///   Non-admin regular user is not admin
///   Null caller org is never a referral participant
///   Null caller org is never an appointment participant
///   Appointment — referring org is participant
///   Appointment — receiving org is participant
///   Appointment — third-party org is blocked
///   Org scope — admin gets no filter (null, null)
///   Org scope — referrer gets own org as referring filter
///   Org scope — receiver gets own org as receiving filter
///   BackfillOrgIds sets both org ID fields on appointment
///   BackfillOrgIds is idempotent when called twice
///   BulkLinkReport exposes all counters correctly
/// </summary>
// LSCC-002-01: Access validation — participant, admin bypass, null-org, backfill domain scenarios
public class AccessControlValidationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OrgA     = Guid.NewGuid();
    private static readonly Guid OrgB     = Guid.NewGuid();
    private static readonly Guid OrgC     = Guid.NewGuid(); // third party

    // ── Context helper (mirrors CareConnectParticipantHelperTests pattern) ────

    private static ICurrentRequestContext MakeCtx(
        bool    isPlatformAdmin = false,
        string? tenantAdminRole = null,
        Guid?   orgId           = null)
    {
        var mock = new Mock<ICurrentRequestContext>();
        mock.Setup(c => c.IsPlatformAdmin).Returns(isPlatformAdmin);
        mock.Setup(c => c.OrgId).Returns(orgId);

        var roles = tenantAdminRole is not null
            ? new[] { tenantAdminRole }
            : Array.Empty<string>();
        mock.Setup(c => c.Roles).Returns(roles);
        return mock.Object;
    }

    // ── Entity helpers ────────────────────────────────────────────────────────

    private static Referral MakeReferral(Guid? referringOrgId = null, Guid? receivingOrgId = null) =>
        Referral.Create(
            tenantId:                TenantId,
            referringOrganizationId: referringOrgId,
            receivingOrganizationId: receivingOrgId,
            providerId:              Guid.NewGuid(),
            subjectPartyId:          null,
            subjectNameSnapshot:     null,
            subjectDobSnapshot:      null,
            clientFirstName:         "Test",
            clientLastName:          "Patient",
            clientDob:               null,
            clientPhone:             "555-0000",
            clientEmail:             "test@example.com",
            caseNumber:              null,
            requestedService:        "Therapy",
            urgency:                 "Routine",
            notes:                   null,
            createdByUserId:         null);

    private static Appointment MakeAppointment(Guid? referringOrgId, Guid? receivingOrgId) =>
        Appointment.Create(
            tenantId:                TenantId,
            referralId:              Guid.NewGuid(),
            providerId:              Guid.NewGuid(),
            facilityId:              Guid.NewGuid(),
            serviceOfferingId:       null,
            appointmentSlotId:       null,
            scheduledStartAtUtc:     DateTime.UtcNow.AddDays(1),
            scheduledEndAtUtc:       DateTime.UtcNow.AddDays(1).AddHours(1),
            notes:                   null,
            createdByUserId:         null,
            organizationRelationshipId: null,
            referringOrganizationId: referringOrgId,
            receivingOrganizationId: receivingOrgId);

    // ═══════════════════════════════════════════════════════════════
    // IsAdmin
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsAdmin_PlatformAdmin_ReturnsTrue()
    {
        var ctx = MakeCtx(isPlatformAdmin: true);
        Assert.True(CareConnectParticipantHelper.IsAdmin(ctx),
            "PlatformAdmin must always bypass participant checks.");
    }

    [Fact]
    public void IsAdmin_TenantAdmin_ReturnsTrue()
    {
        var ctx = MakeCtx(tenantAdminRole: Roles.TenantAdmin);
        Assert.True(CareConnectParticipantHelper.IsAdmin(ctx),
            "TenantAdmin must always bypass participant checks.");
    }

    [Fact]
    public void IsAdmin_RegularUser_ReturnsFalse()
    {
        var ctx = MakeCtx(orgId: OrgA);
        Assert.False(CareConnectParticipantHelper.IsAdmin(ctx));
    }

    // ═══════════════════════════════════════════════════════════════
    // IsReferralParticipant
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsReferralParticipant_NonParticipantOrg_ReturnsFalse()
    {
        var referral = MakeReferral(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.False(CareConnectParticipantHelper.IsReferralParticipant(referral, OrgC),
            "A third-party org must not be treated as a referral participant.");
    }

    [Fact]
    public void IsReferralParticipant_ReferringOrg_ReturnsTrue()
    {
        var referral = MakeReferral(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.True(CareConnectParticipantHelper.IsReferralParticipant(referral, OrgA));
    }

    [Fact]
    public void IsReferralParticipant_ReceivingOrg_ReturnsTrue()
    {
        var referral = MakeReferral(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.True(CareConnectParticipantHelper.IsReferralParticipant(referral, OrgB));
    }

    [Fact]
    public void IsReferralParticipant_NullCallerOrg_ReturnsFalse()
    {
        var referral = MakeReferral(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.False(CareConnectParticipantHelper.IsReferralParticipant(referral, null),
            "A user with no OrganizationId must never be a referral participant.");
    }

    // ═══════════════════════════════════════════════════════════════
    // IsAppointmentParticipant
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsAppointmentParticipant_ReferringOrg_ReturnsTrue()
    {
        var appt = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.True(CareConnectParticipantHelper.IsAppointmentParticipant(appt, OrgA));
    }

    [Fact]
    public void IsAppointmentParticipant_ReceivingOrg_ReturnsTrue()
    {
        var appt = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.True(CareConnectParticipantHelper.IsAppointmentParticipant(appt, OrgB));
    }

    [Fact]
    public void IsAppointmentParticipant_ThirdPartyOrg_ReturnsFalse()
    {
        var appt = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.False(CareConnectParticipantHelper.IsAppointmentParticipant(appt, OrgC),
            "A third-party org must not be an appointment participant.");
    }

    [Fact]
    public void IsAppointmentParticipant_NullCallerOrg_ReturnsFalse()
    {
        var appt = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.False(CareConnectParticipantHelper.IsAppointmentParticipant(appt, null),
            "A user with no OrganizationId must never be an appointment participant.");
    }

    // ═══════════════════════════════════════════════════════════════
    // Org scope (referral)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void GetReferralOrgScope_Admin_ReturnsNullNullTuple()
    {
        var ctx = MakeCtx(isPlatformAdmin: true, orgId: OrgA);
        var (referring, receiving) = CareConnectParticipantHelper.GetReferralOrgScope(ctx, callerIsReceiver: false);
        Assert.Null(referring);
        Assert.Null(receiving);
    }

    [Fact]
    public void GetReferralOrgScope_Referrer_SetsReferringOrgOnly()
    {
        var ctx = MakeCtx(orgId: OrgA);
        var (referring, receiving) = CareConnectParticipantHelper.GetReferralOrgScope(ctx, callerIsReceiver: false);
        Assert.Equal(OrgA, referring);
        Assert.Null(receiving);
    }

    [Fact]
    public void GetReferralOrgScope_Receiver_SetsReceivingOrgOnly()
    {
        var ctx = MakeCtx(orgId: OrgB);
        var (referring, receiving) = CareConnectParticipantHelper.GetReferralOrgScope(ctx, callerIsReceiver: true);
        Assert.Null(referring);
        Assert.Equal(OrgB, receiving);
    }

    // ═══════════════════════════════════════════════════════════════
    // Appointment domain — BackfillOrgIds
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BackfillOrgIds_SetsBothOrganizationIdFields()
    {
        // Simulate a legacy appointment with null org IDs (created before LSCC-002).
        var appt = MakeAppointment(referringOrgId: null, receivingOrgId: null);

        appt.BackfillOrgIds(OrgA, OrgB);

        Assert.Equal(OrgA, appt.ReferringOrganizationId);
        Assert.Equal(OrgB, appt.ReceivingOrganizationId);
    }

    [Fact]
    public void BackfillOrgIds_IsIdempotent_WhenCalledWithSameValues()
    {
        var appt = MakeAppointment(referringOrgId: null, receivingOrgId: null);

        appt.BackfillOrgIds(OrgA, OrgB);
        appt.BackfillOrgIds(OrgA, OrgB); // second call — same values

        Assert.Equal(OrgA, appt.ReferringOrganizationId);
        Assert.Equal(OrgB, appt.ReceivingOrganizationId);
    }

    // ═══════════════════════════════════════════════════════════════
    // BulkLinkReport DTO
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BulkLinkReport_ExposesAllCounters()
    {
        var report = new BulkLinkReport(Total: 10, Updated: 6, Skipped: 3, Unresolved: 1);

        Assert.Equal(10, report.Total);
        Assert.Equal(6,  report.Updated);
        Assert.Equal(3,  report.Skipped);
        Assert.Equal(1,  report.Unresolved);
    }
}
