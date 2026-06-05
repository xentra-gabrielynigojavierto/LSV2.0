namespace CareConnect.Application.DTOs;

public class CreateAvailabilityTemplateRequest
{
    public Guid FacilityId { get; set; }
    public Guid? ServiceOfferingId { get; set; }
    public int DayOfWeek { get; set; }
    public string StartTimeLocal { get; set; } = string.Empty;
    public string EndTimeLocal { get; set; } = string.Empty;
    public int SlotDurationMinutes { get; set; }
    public int Capacity { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateAvailabilityTemplateRequest
{
    public Guid FacilityId { get; set; }
    public Guid? ServiceOfferingId { get; set; }
    public int DayOfWeek { get; set; }
    public string StartTimeLocal { get; set; } = string.Empty;
    public string EndTimeLocal { get; set; } = string.Empty;
    public int SlotDurationMinutes { get; set; }
    public int Capacity { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public class AvailabilityTemplateResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ProviderId { get; init; }
    public Guid FacilityId { get; init; }
    public string FacilityName { get; init; } = string.Empty;
    public Guid? ServiceOfferingId { get; init; }
    public string? ServiceOfferingName { get; init; }
    public int DayOfWeek { get; init; }
    public string StartTimeLocal { get; init; } = string.Empty;
    public string EndTimeLocal { get; init; } = string.Empty;
    public int SlotDurationMinutes { get; init; }
    public int Capacity { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
}
