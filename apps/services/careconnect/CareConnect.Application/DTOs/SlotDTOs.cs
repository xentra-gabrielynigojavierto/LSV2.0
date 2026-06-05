namespace CareConnect.Application.DTOs;

public class GenerateSlotsRequest
{
    public DateTime FromDateUtc { get; set; }
    public DateTime ToDateUtc { get; set; }
}

public class GenerateSlotsResponse
{
    public Guid ProviderId { get; init; }
    public DateTime FromDateUtc { get; init; }
    public DateTime ToDateUtc { get; init; }
    public int SlotsCreated { get; init; }
}

public class SlotResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ProviderId { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public Guid FacilityId { get; init; }
    public string FacilityName { get; init; } = string.Empty;
    public Guid? ServiceOfferingId { get; init; }
    public string? ServiceOfferingName { get; init; }
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public int Capacity { get; init; }
    public int ReservedCount { get; init; }
    public int AvailableCount { get; init; }
    public string Status { get; init; } = string.Empty;
}

public class SlotSearchParams
{
    public Guid? ProviderId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? ServiceOfferingId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Status { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
