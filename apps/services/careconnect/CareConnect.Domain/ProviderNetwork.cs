using BuildingBlocks.Domain;

namespace CareConnect.Domain;

// CC2-INT-B06: Tenant-scoped provider networks for the Network Manager role.
public class ProviderNetwork : AuditableEntity
{
    public Guid   Id          { get; private set; }
    public Guid   TenantId    { get; private set; }
    public string Name        { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool   IsDeleted   { get; private set; }

    public List<NetworkProvider> NetworkProviders { get; private set; } = new();

    private ProviderNetwork() { }

    public static ProviderNetwork Create(Guid tenantId, string name, string description)
    {
        return new ProviderNetwork
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            Name        = name.Trim(),
            Description = description.Trim(),
            IsDeleted   = false,
        };
    }

    public void Update(string name, string description)
    {
        Name        = name.Trim();
        Description = description.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Delete()
    {
        IsDeleted    = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
