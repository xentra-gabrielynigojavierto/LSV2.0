using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class Appointment : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ReferralId { get; private set; }

    // ── Multi-org workflow participants (denormalized from Referral at create time) ──
    public Guid? ReferringOrganizationId { get; private set; }
    public Guid? ReceivingOrganizationId { get; private set; }
    public Guid? SubjectPartyId { get; private set; }

    // Phase 5: explicit relationship context (denormalized from Referral at create time)
    public Guid? OrganizationRelationshipId { get; private set; }

    public Guid ProviderId { get; private set; }
    public Guid FacilityId { get; private set; }
    public Guid? ServiceOfferingId { get; private set; }
    public Guid? AppointmentSlotId { get; private set; }
    public DateTime ScheduledStartAtUtc { get; private set; }
    public DateTime ScheduledEndAtUtc { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    public Referral? Referral { get; private set; }
    public Provider? Provider { get; private set; }
    public Facility? Facility { get; private set; }
    public ServiceOffering? ServiceOffering { get; private set; }
    public AppointmentSlot? AppointmentSlot { get; private set; }

    private Appointment() { }

    public void UpdateStatus(string newStatus, Guid? updatedByUserId)
    {
        Status = newStatus;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateNotes(string? notes, Guid? updatedByUserId)
    {
        Notes = notes?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reschedule(AppointmentSlot newSlot, string? notes, Guid? updatedByUserId)
    {
        AppointmentSlotId = newSlot.Id;
        ProviderId = newSlot.ProviderId;
        FacilityId = newSlot.FacilityId;
        ServiceOfferingId = newSlot.ServiceOfferingId;
        ScheduledStartAtUtc = newSlot.StartAtUtc;
        ScheduledEndAtUtc = newSlot.EndAtUtc;
        if (notes is not null)
            Notes = notes.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Phase C: link this appointment to a formal OrganizationRelationship.
    /// Typically denormalized from the linked Referral at create time.
    /// </summary>
    public void SetOrganizationRelationshipId(Guid organizationRelationshipId)
    {
        OrganizationRelationshipId = organizationRelationshipId;
        UpdatedByUserId = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// LSCC-002-01: Backfill org participant IDs for legacy appointments created
    /// before LSCC-002 denormalization enforcement. Values are derived only from
    /// the parent Referral — never inferred.
    /// Idempotent: calling again with the same values is a no-op from domain perspective.
    /// </summary>
    // LSCC-002-01: Legacy appointment org-ID backfill — safe, explicit, admin-only
    public void BackfillOrgIds(Guid referringOrganizationId, Guid receivingOrganizationId)
    {
        ReferringOrganizationId = referringOrganizationId;
        ReceivingOrganizationId = receivingOrganizationId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Create an appointment, denormalizing org participant IDs from the source referral
    /// so that appointment queries can be org-scoped without joining back to Referral.
    /// </summary>
    // LSCC-002: Denormalize ReferringOrganizationId/ReceivingOrganizationId from Referral at creation
    public static Appointment Create(
        Guid tenantId,
        Guid referralId,
        Guid providerId,
        Guid facilityId,
        Guid? serviceOfferingId,
        Guid? appointmentSlotId,
        DateTime scheduledStartAtUtc,
        DateTime scheduledEndAtUtc,
        string? notes,
        Guid? createdByUserId,
        Guid? organizationRelationshipId = null,
        Guid? referringOrganizationId = null,
        Guid? receivingOrganizationId = null)
    {
        return new Appointment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReferralId = referralId,
            ProviderId = providerId,
            FacilityId = facilityId,
            ServiceOfferingId = serviceOfferingId,
            AppointmentSlotId = appointmentSlotId,
            ScheduledStartAtUtc = scheduledStartAtUtc,
            ScheduledEndAtUtc = scheduledEndAtUtc,
            OrganizationRelationshipId = organizationRelationshipId,
            ReferringOrganizationId = referringOrganizationId,
            ReceivingOrganizationId = receivingOrganizationId,
            Status = AppointmentStatus.Scheduled,
            Notes = notes?.Trim(),
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
