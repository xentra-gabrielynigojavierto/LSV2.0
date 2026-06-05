namespace Identity.Domain;

public class TenantDomain
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Domain { get; private set; } = string.Empty;
    public string DomainType { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private TenantDomain() { }

    public static TenantDomain Create(Guid tenantId, string domain, string domainType, bool isPrimary = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainType);

        return new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Domain = domain.Trim().ToLowerInvariant(),
            DomainType = domainType.ToUpperInvariant(),
            IsPrimary = isPrimary,
            IsVerified = false,
            VerifiedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkVerified()
    {
        IsVerified = true;
        VerifiedAtUtc = DateTime.UtcNow;
    }
}
