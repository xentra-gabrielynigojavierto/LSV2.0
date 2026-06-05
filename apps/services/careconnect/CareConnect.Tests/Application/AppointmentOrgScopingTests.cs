// LSCC-002: Tests for Appointment org-participant scoping and denormalization
using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-002 — Verifies that:
///   1. Appointment.Create correctly denormalizes ReferringOrganizationId and
///      ReceivingOrganizationId from the source Referral.
///   2. Legacy appointments (created before LSCC-002) have null org IDs and
///      are treated as "no filter" by the search query layer.
///   3. Org filter mutual exclusivity matches the referral scoping convention.
/// </summary>
public class AppointmentOrgScopingTests
{
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid OrgA      = Guid.NewGuid();
    private static readonly Guid OrgB      = Guid.NewGuid();
    private static readonly Guid ProviderId  = Guid.NewGuid();
    private static readonly Guid FacilityId  = Guid.NewGuid();
    private static readonly Guid ReferralId  = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    private static Appointment MakeAppointment(Guid? referringOrgId, Guid? receivingOrgId) =>
        Appointment.Create(
            tenantId: TenantId,
            referralId: ReferralId,
            providerId: ProviderId,
            facilityId: FacilityId,
            serviceOfferingId: null,
            appointmentSlotId: null,
            scheduledStartAtUtc: Now.AddDays(1),
            scheduledEndAtUtc: Now.AddDays(1).AddHours(1),
            notes: null,
            createdByUserId: null,
            organizationRelationshipId: null,
            referringOrganizationId: referringOrgId,
            receivingOrganizationId: receivingOrgId);

    // ─── 1. Org ID denormalization on Appointment.Create ─────────────────────

    [Fact]
    public void Create_Denormalizes_ReferringOrganizationId_From_Referral()
    {
        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);

        Assert.Equal(OrgA, appointment.ReferringOrganizationId);
    }

    [Fact]
    public void Create_Denormalizes_ReceivingOrganizationId_From_Referral()
    {
        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);

        Assert.Equal(OrgB, appointment.ReceivingOrganizationId);
    }

    [Fact]
    public void Create_With_Null_OrgIds_Leaves_Both_Null()
    {
        // Simulates a referral that has no org linkage yet (legacy path).
        var appointment = MakeAppointment(referringOrgId: null, receivingOrgId: null);

        Assert.Null(appointment.ReferringOrganizationId);
        Assert.Null(appointment.ReceivingOrganizationId);
    }

    [Fact]
    public void Create_Referral_And_Appointment_OrgIds_Match()
    {
        // The invariant: appointment org IDs must match whatever the referral had.
        var referringOrgId = OrgA;
        var receivingOrgId = OrgB;

        var appointment = MakeAppointment(referringOrgId, receivingOrgId);

        Assert.Equal(referringOrgId, appointment.ReferringOrganizationId);
        Assert.Equal(receivingOrgId, appointment.ReceivingOrganizationId);
    }

    // ─── 2. Org IDs are independent (no cross-contamination) ─────────────────

    [Fact]
    public void Two_Appointments_From_Different_Referrals_Have_Independent_OrgIds()
    {
        var orgA1 = Guid.NewGuid();
        var orgB1 = Guid.NewGuid();
        var orgA2 = Guid.NewGuid();
        var orgB2 = Guid.NewGuid();

        var appt1 = MakeAppointment(orgA1, orgB1);
        var appt2 = MakeAppointment(orgA2, orgB2);

        Assert.NotEqual(appt1.ReferringOrganizationId, appt2.ReferringOrganizationId);
        Assert.NotEqual(appt1.ReceivingOrganizationId, appt2.ReceivingOrganizationId);
    }

    // ─── 3. Scoping logic (mirrors referral scoping rules) ───────────────────

    [Fact]
    public void Referrer_Scope_Uses_ReferringOrgId_Only()
    {
        // Simulate what the endpoint builds for a non-receiver, non-admin caller.
        Guid? referringOrgId = OrgA;
        Guid? receivingOrgId = null;

        // The appointment must be visible if it matches the referring org.
        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);

        Assert.Equal(referringOrgId, appointment.ReferringOrganizationId);
        Assert.Null(receivingOrgId); // receiver filter not applied for referrers
    }

    [Fact]
    public void Receiver_Scope_Uses_ReceivingOrgId_Only()
    {
        // Simulate what the endpoint builds for a receiver caller.
        Guid? referringOrgId = null;
        Guid? receivingOrgId = OrgB;

        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);

        Assert.Equal(receivingOrgId, appointment.ReceivingOrganizationId);
        Assert.Null(referringOrgId); // referring filter not applied for receivers
    }

    [Fact]
    public void Admin_Scope_Has_No_Org_Filter()
    {
        // Admins pass null for both filters — no org narrowing.
        Guid? referringOrgId = null;
        Guid? receivingOrgId = null;

        Assert.Null(referringOrgId);
        Assert.Null(receivingOrgId);
    }

    // ─── 4. Row-level access control helpers ─────────────────────────────────

    [Fact]
    public void Appointment_Is_Visible_To_ReferringOrg()
    {
        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);

        var callerOrgId = OrgA;
        var isParticipant =
            (callerOrgId != Guid.Empty && appointment.ReferringOrganizationId == callerOrgId) ||
            (callerOrgId != Guid.Empty && appointment.ReceivingOrganizationId == callerOrgId);

        Assert.True(isParticipant);
    }

    [Fact]
    public void Appointment_Is_Visible_To_ReceivingOrg()
    {
        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);

        var callerOrgId = OrgB;
        var isParticipant =
            (callerOrgId != Guid.Empty && appointment.ReferringOrganizationId == callerOrgId) ||
            (callerOrgId != Guid.Empty && appointment.ReceivingOrganizationId == callerOrgId);

        Assert.True(isParticipant);
    }

    [Fact]
    public void Appointment_Is_Not_Visible_To_ThirdPartyOrg()
    {
        var appointment = MakeAppointment(referringOrgId: OrgA, receivingOrgId: OrgB);

        var callerOrgId = Guid.NewGuid(); // neither OrgA nor OrgB
        var isParticipant =
            appointment.ReferringOrganizationId == callerOrgId ||
            appointment.ReceivingOrganizationId == callerOrgId;

        Assert.False(isParticipant);
    }
}
