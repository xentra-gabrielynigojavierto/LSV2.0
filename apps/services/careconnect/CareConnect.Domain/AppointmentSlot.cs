using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class AppointmentSlot : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid FacilityId { get; private set; }
    public Guid? ServiceOfferingId { get; private set; }
    public Guid? ProviderAvailabilityTemplateId { get; private set; }
    public DateTime StartAtUtc { get; private set; }
    public DateTime EndAtUtc { get; private set; }
    public int Capacity { get; private set; }
    public int ReservedCount { get; private set; }
    public string Status { get; private set; } = string.Empty;

    public Provider? Provider { get; private set; }
    public Facility? Facility { get; private set; }
    public ServiceOffering? ServiceOffering { get; private set; }
    public ProviderAvailabilityTemplate? ProviderAvailabilityTemplate { get; private set; }

    private AppointmentSlot() { }

    public static AppointmentSlot Create(
        Guid tenantId,
        Guid providerId,
        Guid facilityId,
        Guid? serviceOfferingId,
        Guid? providerAvailabilityTemplateId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        int capacity,
        Guid? createdByUserId)
    {
        return new AppointmentSlot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderId = providerId,
            FacilityId = facilityId,
            ServiceOfferingId = serviceOfferingId,
            ProviderAvailabilityTemplateId = providerAvailabilityTemplateId,
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            Capacity = capacity,
            ReservedCount = 0,
            Status = SlotStatus.Open,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void Reserve(Guid? updatedByUserId)
    {
        if (Status != SlotStatus.Open)
            throw new InvalidOperationException("Only Open slots can be reserved.");

        if (ReservedCount >= Capacity)
            throw new InvalidOperationException("Slot has no remaining capacity.");

        ReservedCount++;

        if (ReservedCount >= Capacity)
            Status = SlotStatus.Closed;

        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Release(Guid? updatedByUserId)
    {
        if (ReservedCount <= 0)
            return;

        ReservedCount--;

        if (Status == SlotStatus.Closed && ReservedCount < Capacity)
            Status = SlotStatus.Open;

        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Block(Guid? updatedByUserId)
    {
        if (ReservedCount > 0)
            return;

        Status = SlotStatus.Blocked;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
