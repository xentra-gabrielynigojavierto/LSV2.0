namespace CareConnect.Application.DTOs;

public class ProviderAvailabilityResponse
{
    public Guid   ProviderId   { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public DateTime From { get; init; }
    public DateTime To   { get; init; }
    public Guid? FacilityId        { get; init; }
    public string? FacilityName    { get; init; }
    public Guid? ServiceOfferingId { get; init; }
    public string? ServiceOfferingName { get; init; }
    public List<AvailableSlotSummary> Slots { get; init; } = new();
}

public class AvailableSlotSummary
{
    public Guid     Id             { get; init; }
    public DateTime StartAtUtc     { get; init; }
    public DateTime EndAtUtc       { get; init; }
    public int      AvailableCount { get; init; }
    public Guid     FacilityId     { get; init; }
    public string   FacilityName   { get; init; } = string.Empty;
    public Guid?    ServiceOfferingId   { get; init; }
    public string?  ServiceOfferingName { get; init; }
}
