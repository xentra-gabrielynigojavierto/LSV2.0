// LSCC-002: Tests for CareConnectParticipantHelper — participant and admin bypass logic
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.Authorization;
using CareConnect.Domain;
using Moq;
using Xunit;

namespace CareConnect.Tests.Authorization;

/// <summary>
/// LSCC-002 — Tests for <see cref="CareConnectParticipantHelper"/>.
///
/// Covers:
///   - IsAdmin: PlatformAdmin bypass, TenantAdmin bypass, non-admin
///   - IsReferralParticipant: referring org, receiving org, third-party org, null org
///   - IsAppointmentParticipant: referring org, receiving org, third-party org, null org
///   - GetReferralOrgScope: admin = no filter, receiver = receivingOrgId, referrer = referringOrgId
///   - GetAppointmentOrgScope: mirrors referral scoping rules
/// </summary>
public class CareConnectParticipantHelperTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OrgA     = Guid.NewGuid();
    private static readonly Guid OrgB     = Guid.NewGuid();
    private static readonly Guid OrgC     = Guid.NewGuid();

    // ── Context helpers ───────────────────────────────────────────────────────

    private static ICurrentRequestContext MakeCtx(
        bool isPlatformAdmin = false,
        string? tenantAdminRole = null,
        Guid? orgId = null)
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

    private static Referral MakeReferral(Guid? referringOrgId, Guid? receivingOrgId) =>
        Referral.Create(
            tenantId: TenantId,
            referringOrganizationId: referringOrgId,
            receivingOrganizationId: receivingOrgId,
            providerId: Guid.NewGuid(),
            subjectPartyId: null,
            subjectNameSnapshot: null,
            subjectDobSnapshot: null,
            clientFirstName: "Test",
            clientLastName: "Patient",
            clientDob: null,
            clientPhone: "555-0000",
            clientEmail: "test@example.com",
            caseNumber: null,
            requestedService: "Therapy",
            urgency: "Routine",
            notes: null,
            createdByUserId: null);

    private static Appointment MakeAppointment(Guid? referringOrgId, Guid? receivingOrgId) =>
        Appointment.Create(
            tenantId: TenantId,
            referralId: Guid.NewGuid(),
            providerId: Guid.NewGuid(),
            facilityId: Guid.NewGuid(),
            serviceOfferingId: null,
            appointmentSlotId: null,
            scheduledStartAtUtc: DateTime.UtcNow.AddDays(1),
            scheduledEndAtUtc: DateTime.UtcNow.AddDays(1).AddHours(1),
            notes: null,
            createdByUserId: null,
            organizationRelationshipId: null,
            referringOrganizationId: referringOrgId,
            receivingOrganizationId: receivingOrgId);

    // ── IsAdmin ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsAdmin_Returns_True_For_PlatformAdmin()
    {
        var ctx = MakeCtx(isPlatformAdmin: true);
        Assert.True(CareConnectParticipantHelper.IsAdmin(ctx));
    }

    [Fact]
    public void IsAdmin_Returns_True_For_TenantAdmin()
    {
        var ctx = MakeCtx(tenantAdminRole: Roles.TenantAdmin);
        Assert.True(CareConnectParticipantHelper.IsAdmin(ctx));
    }

    [Fact]
    public void IsAdmin_Returns_False_For_Regular_User()
    {
        var ctx = MakeCtx(orgId: OrgA);
        Assert.False(CareConnectParticipantHelper.IsAdmin(ctx));
    }

    // ── IsReferralParticipant ─────────────────────────────────────────────────

    [Fact]
    public void IsReferralParticipant_Returns_True_For_ReferringOrg()
    {
        var referral = MakeReferral(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.True(CareConnectParticipantHelper.IsReferralParticipant(referral, OrgA));
    }

    [Fact]
    public void IsReferralParticipant_Returns_True_For_ReceivingOrg()
    {
        var referral = MakeReferral(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.True(CareConnectParticipantHelper.IsReferralParticipant(referral, OrgB));
    }

    [Fact]
    public void IsReferralParticipant_Returns_False_For_ThirdPartyOrg()
    {
        var referral = MakeReferral(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.False(CareConnectParticipantHelper.IsReferralParticipant(referral, OrgC));
    }

    [Fact]
    public void IsReferralParticipant_Returns_False_When_CallerOrgId_Is_Null()
    {
        var referral = MakeReferral(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.False(CareConnectParticipantHelper.IsReferralParticipant(referral, null));
    }

    [Fact]
    public void IsReferralParticipant_Returns_False_When_Referral_Has_No_Org_Ids()
    {
        // Referrals created before org linkage may have null org IDs.
        var referral = MakeReferral(referringOrgId: null, receivingOrgId: null);
        Assert.False(CareConnectParticipantHelper.IsReferralParticipant(referral, OrgA));
    }

    // ── IsAppointmentParticipant ──────────────────────────────────────────────

    [Fact]
    public void IsAppointmentParticipant_Returns_True_For_ReferringOrg()
    {
        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.True(CareConnectParticipantHelper.IsAppointmentParticipant(appointment, OrgA));
    }

    [Fact]
    public void IsAppointmentParticipant_Returns_True_For_ReceivingOrg()
    {
        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.True(CareConnectParticipantHelper.IsAppointmentParticipant(appointment, OrgB));
    }

    [Fact]
    public void IsAppointmentParticipant_Returns_False_For_ThirdPartyOrg()
    {
        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.False(CareConnectParticipantHelper.IsAppointmentParticipant(appointment, OrgC));
    }

    [Fact]
    public void IsAppointmentParticipant_Returns_False_When_CallerOrgId_Is_Null()
    {
        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);
        Assert.False(CareConnectParticipantHelper.IsAppointmentParticipant(appointment, null));
    }

    [Fact]
    public void IsAppointmentParticipant_Returns_False_When_Appointment_Has_No_Org_Ids()
    {
        // Appointments created before LSCC-002 denormalization may have null org IDs.
        var appointment = MakeAppointment(referringOrgId: null, receivingOrgId: null);
        Assert.False(CareConnectParticipantHelper.IsAppointmentParticipant(appointment, OrgA));
    }

    // ── GetReferralOrgScope ───────────────────────────────────────────────────

    [Fact]
    public void GetReferralOrgScope_Admin_Returns_No_Filter()
    {
        var ctx = MakeCtx(isPlatformAdmin: true, orgId: OrgA);
        var (referring, receiving) = CareConnectParticipantHelper.GetReferralOrgScope(ctx, callerIsReceiver: false);
        Assert.Null(referring);
        Assert.Null(receiving);
    }

    [Fact]
    public void GetReferralOrgScope_TenantAdmin_Returns_No_Filter()
    {
        var ctx = MakeCtx(tenantAdminRole: Roles.TenantAdmin, orgId: OrgA);
        var (referring, receiving) = CareConnectParticipantHelper.GetReferralOrgScope(ctx, callerIsReceiver: false);
        Assert.Null(referring);
        Assert.Null(receiving);
    }

    [Fact]
    public void GetReferralOrgScope_Referrer_Sets_ReferringOrgId_Only()
    {
        var ctx = MakeCtx(orgId: OrgA);
        var (referring, receiving) = CareConnectParticipantHelper.GetReferralOrgScope(ctx, callerIsReceiver: false);
        Assert.Equal(OrgA, referring);
        Assert.Null(receiving);
    }

    [Fact]
    public void GetReferralOrgScope_Receiver_Sets_ReceivingOrgId_Only()
    {
        var ctx = MakeCtx(orgId: OrgB);
        var (referring, receiving) = CareConnectParticipantHelper.GetReferralOrgScope(ctx, callerIsReceiver: true);
        Assert.Null(referring);
        Assert.Equal(OrgB, receiving);
    }

    // ── GetAppointmentOrgScope ────────────────────────────────────────────────

    [Fact]
    public void GetAppointmentOrgScope_Admin_Returns_No_Filter()
    {
        var ctx = MakeCtx(isPlatformAdmin: true, orgId: OrgA);
        var (referring, receiving) = CareConnectParticipantHelper.GetAppointmentOrgScope(ctx, callerIsReceiver: false);
        Assert.Null(referring);
        Assert.Null(receiving);
    }

    [Fact]
    public void GetAppointmentOrgScope_Referrer_Sets_ReferringOrgId_Only()
    {
        var ctx = MakeCtx(orgId: OrgA);
        var (referring, receiving) = CareConnectParticipantHelper.GetAppointmentOrgScope(ctx, callerIsReceiver: false);
        Assert.Equal(OrgA, referring);
        Assert.Null(receiving);
    }

    [Fact]
    public void GetAppointmentOrgScope_Receiver_Sets_ReceivingOrgId_Only()
    {
        var ctx = MakeCtx(orgId: OrgB);
        var (referring, receiving) = CareConnectParticipantHelper.GetAppointmentOrgScope(ctx, callerIsReceiver: true);
        Assert.Null(referring);
        Assert.Equal(OrgB, receiving);
    }
}
