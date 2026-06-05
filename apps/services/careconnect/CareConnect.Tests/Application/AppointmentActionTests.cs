// LSCC-003-01: Tests for Confirm, NoShow, and Reschedule appointment actions.
// Covers: status transition model, reschedule slot update contract, DTO validation.
using CareConnect.Application.DTOs;
using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-003-01 — Verifies the backend contract for UX workflow completion actions:
///   - Confirm  : PUT /api/appointments/{id}  with status = "Confirmed"
///   - NoShow   : PUT /api/appointments/{id}  with status = "NoShow"
///   - Reschedule: POST /api/appointments/{id}/reschedule with a new slot
///
/// Tests are pure domain/DTO logic — no DB or HTTP stack.
/// </summary>
public class AppointmentActionTests
{
    private static readonly Guid TenantId   = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();
    private static readonly Guid FacilityId = Guid.NewGuid();
    private static readonly Guid ReferralId = Guid.NewGuid();
    private static readonly Guid UserId     = Guid.NewGuid();

    private static Appointment MakeScheduledAppointment() =>
        Appointment.Create(
            tenantId:               TenantId,
            referralId:             ReferralId,
            providerId:             ProviderId,
            facilityId:             FacilityId,
            serviceOfferingId:      null,
            appointmentSlotId:      null,
            scheduledStartAtUtc:    DateTime.UtcNow.AddDays(1),
            scheduledEndAtUtc:      DateTime.UtcNow.AddDays(1).AddHours(1),
            notes:                  null,
            createdByUserId:        UserId,
            organizationRelationshipId: null,
            referringOrganizationId: null,
            receivingOrganizationId: null);

    // ── 1. Confirm ────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateAppointmentRequest_Confirm_IsWellFormed()
    {
        var req = new UpdateAppointmentRequest
        {
            Status = AppointmentStatus.Confirmed,
        };

        Assert.Equal(AppointmentStatus.Confirmed, req.Status);
        Assert.Null(req.Notes);
    }

    [Fact]
    public void Appointment_UpdateStatus_ToConfirmed_ChangesStatus()
    {
        var appt = MakeScheduledAppointment();
        Assert.Equal(AppointmentStatus.Scheduled, appt.Status);

        appt.UpdateStatus(AppointmentStatus.Confirmed, UserId);

        Assert.Equal(AppointmentStatus.Confirmed, appt.Status);
    }

    // ── 2. NoShow ────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateAppointmentRequest_NoShow_IsWellFormed()
    {
        var req = new UpdateAppointmentRequest
        {
            Status = AppointmentStatus.NoShow,
            Notes  = "Patient did not attend",
        };

        Assert.Equal(AppointmentStatus.NoShow, req.Status);
        Assert.Equal("Patient did not attend", req.Notes);
    }

    [Fact]
    public void Appointment_UpdateStatus_ToNoShow_ChangesStatus()
    {
        var appt = MakeScheduledAppointment();
        appt.UpdateStatus(AppointmentStatus.Confirmed, UserId);

        appt.UpdateStatus(AppointmentStatus.NoShow, UserId);

        Assert.Equal(AppointmentStatus.NoShow, appt.Status);
    }

    [Fact]
    public void Appointment_UpdateStatus_IsTerminal_AfterNoShow()
    {
        var appt = MakeScheduledAppointment();
        appt.UpdateStatus(AppointmentStatus.NoShow, UserId);

        Assert.True(
            appt.Status is AppointmentStatus.NoShow
                        or AppointmentStatus.Cancelled
                        or AppointmentStatus.Completed,
            $"Unexpected terminal status: {appt.Status}");
    }

    // ── 3. Reschedule ─────────────────────────────────────────────────────────

    [Fact]
    public void RescheduleAppointmentRequest_RequiredField_NewSlotId_IsSet()
    {
        var slotId = Guid.NewGuid();
        var req = new RescheduleAppointmentRequest
        {
            NewAppointmentSlotId = slotId,
        };

        Assert.Equal(slotId, req.NewAppointmentSlotId);
        Assert.Null(req.Notes);
    }

    [Fact]
    public void RescheduleAppointmentRequest_WithNotes_IsWellFormed()
    {
        var req = new RescheduleAppointmentRequest
        {
            NewAppointmentSlotId = Guid.NewGuid(),
            Notes = "Client requested afternoon slot",
        };

        Assert.False(string.IsNullOrWhiteSpace(req.Notes));
    }

    [Fact]
    public void Appointment_Reschedule_UpdatesSlotAndProvider()
    {
        var appt       = MakeScheduledAppointment();
        var newSlotId  = Guid.NewGuid();
        var newFacId   = Guid.NewGuid();
        var newProvId  = Guid.NewGuid();
        var newStart   = DateTime.UtcNow.AddDays(3);
        var newEnd     = newStart.AddHours(1);

        var newSlot = AppointmentSlot.Create(
            tenantId:                     TenantId,
            providerId:                   newProvId,
            facilityId:                   newFacId,
            serviceOfferingId:            null,
            providerAvailabilityTemplateId: null,
            startAtUtc:                   newStart,
            endAtUtc:                     newEnd,
            capacity:                     1,
            createdByUserId:              UserId);

        appt.Reschedule(newSlot, notes: "Moved to Thursday", updatedByUserId: UserId);

        Assert.Equal(newProvId, appt.ProviderId);
        Assert.Equal(newFacId,  appt.FacilityId);
        Assert.Equal(newStart,  appt.ScheduledStartAtUtc);
        Assert.Equal(newEnd,    appt.ScheduledEndAtUtc);
        Assert.Equal("Moved to Thursday", appt.Notes);
    }

    [Fact]
    public void Appointment_Reschedule_WithNullNotes_DoesNotClearExistingNotes()
    {
        var appt = Appointment.Create(
            tenantId:               TenantId,
            referralId:             ReferralId,
            providerId:             ProviderId,
            facilityId:             FacilityId,
            serviceOfferingId:      null,
            appointmentSlotId:      null,
            scheduledStartAtUtc:    DateTime.UtcNow.AddDays(1),
            scheduledEndAtUtc:      DateTime.UtcNow.AddDays(1).AddHours(1),
            notes:                  "Original note",
            createdByUserId:        UserId,
            organizationRelationshipId: null,
            referringOrganizationId: null,
            receivingOrganizationId: null);

        var newSlot = AppointmentSlot.Create(
            tenantId:                     TenantId,
            providerId:                   ProviderId,
            facilityId:                   FacilityId,
            serviceOfferingId:            null,
            providerAvailabilityTemplateId: null,
            startAtUtc:                   DateTime.UtcNow.AddDays(5),
            endAtUtc:                     DateTime.UtcNow.AddDays(5).AddHours(1),
            capacity:                     1,
            createdByUserId:              UserId);

        appt.Reschedule(newSlot, notes: null, updatedByUserId: UserId);

        // null notes should not overwrite existing notes
        Assert.Equal("Original note", appt.Notes);
    }

    // ── 4. Status constants consistency ───────────────────────────────────────

    [Theory]
    [InlineData(AppointmentStatus.Scheduled)]
    [InlineData(AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.NoShow)]
    public void AppointmentStatus_Constants_AreNonEmpty(string status)
    {
        Assert.False(string.IsNullOrWhiteSpace(status));
    }

    [Fact]
    public void AppointmentStatus_TerminalSet_IsCorrect()
    {
        var terminals = new[] { AppointmentStatus.Completed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow };

        foreach (var t in terminals)
        {
            Assert.False(string.IsNullOrWhiteSpace(t));
        }

        Assert.DoesNotContain(AppointmentStatus.Scheduled, terminals);
        Assert.DoesNotContain(AppointmentStatus.Confirmed,  terminals);
    }
}
