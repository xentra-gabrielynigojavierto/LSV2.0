namespace CareConnect.Domain;

public class ProviderServiceOffering
{
    public Guid Id { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid ServiceOfferingId { get; private set; }
    public Guid? FacilityId { get; private set; }
    public bool IsActive { get; private set; }

    public Provider? Provider { get; private set; }
    public ServiceOffering? ServiceOffering { get; private set; }
    public Facility? Facility { get; private set; }

    private ProviderServiceOffering() { }

    public static ProviderServiceOffering Create(
        Guid providerId,
        Guid serviceOfferingId,
        Guid? facilityId,
        bool isActive)
    {
        return new ProviderServiceOffering
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            ServiceOfferingId = serviceOfferingId,
            FacilityId = facilityId,
            IsActive = isActive
        };
    }
}
