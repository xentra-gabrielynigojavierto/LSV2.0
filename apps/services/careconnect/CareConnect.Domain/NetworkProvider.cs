using BuildingBlocks.Domain;

namespace CareConnect.Domain;

// CC2-INT-B06: Join entity linking a ProviderNetwork to a Provider.
public class NetworkProvider : AuditableEntity
{
    public Guid Id              { get; private set; }
    public Guid TenantId        { get; private set; }
    public Guid ProviderNetworkId { get; private set; }
    public Guid ProviderId      { get; private set; }

    public ProviderNetwork Network  { get; private set; } = null!;
    public Provider        Provider { get; private set; } = null!;

    private NetworkProvider() { }

    public static NetworkProvider Create(Guid tenantId, Guid networkId, Guid providerId)
    {
        return new NetworkProvider
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            ProviderNetworkId = networkId,
            ProviderId       = providerId,
        };
    }
}
