namespace CareConnect.Domain;

public class ProviderFacility
{
    public Guid ProviderId { get; private set; }
    public Guid FacilityId { get; private set; }
    public bool IsPrimary { get; private set; }

    public Provider? Provider { get; private set; }
    public Facility? Facility { get; private set; }

    private ProviderFacility() { }

    public static ProviderFacility Create(Guid providerId, Guid facilityId, bool isPrimary)
    {
        return new ProviderFacility
        {
            ProviderId = providerId,
            FacilityId = facilityId,
            IsPrimary = isPrimary
        };
    }
}
