using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class ProviderAvailabilityTemplate : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid FacilityId { get; private set; }
    public Guid? ServiceOfferingId { get; private set; }
    public int DayOfWeek { get; private set; }
    public TimeSpan StartTimeLocal { get; private set; }
    public TimeSpan EndTimeLocal { get; private set; }
    public int SlotDurationMinutes { get; private set; }
    public int Capacity { get; private set; }
    public DateTime? EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; }

    public Provider? Provider { get; private set; }
    public Facility? Facility { get; private set; }
    public ServiceOffering? ServiceOffering { get; private set; }

    private ProviderAvailabilityTemplate() { }

    public static ProviderAvailabilityTemplate Create(
        Guid tenantId,
        Guid providerId,
        Guid facilityId,
        Guid? serviceOfferingId,
        int dayOfWeek,
        TimeSpan startTimeLocal,
        TimeSpan endTimeLocal,
        int slotDurationMinutes,
        int capacity,
        DateTime? effectiveFrom,
        DateTime? effectiveTo,
        bool isActive,
        Guid? createdByUserId)
    {
        return new ProviderAvailabilityTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderId = providerId,
            FacilityId = facilityId,
            ServiceOfferingId = serviceOfferingId,
            DayOfWeek = dayOfWeek,
            StartTimeLocal = startTimeLocal,
            EndTimeLocal = endTimeLocal,
            SlotDurationMinutes = slotDurationMinutes,
            Capacity = capacity,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            IsActive = isActive,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void Update(
        Guid facilityId,
        Guid? serviceOfferingId,
        int dayOfWeek,
        TimeSpan startTimeLocal,
        TimeSpan endTimeLocal,
        int slotDurationMinutes,
        int capacity,
        DateTime? effectiveFrom,
        DateTime? effectiveTo,
        bool isActive,
        Guid? updatedByUserId)
    {
        FacilityId = facilityId;
        ServiceOfferingId = serviceOfferingId;
        DayOfWeek = dayOfWeek;
        StartTimeLocal = startTimeLocal;
        EndTimeLocal = endTimeLocal;
        SlotDurationMinutes = slotDurationMinutes;
        Capacity = capacity;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        IsActive = isActive;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
