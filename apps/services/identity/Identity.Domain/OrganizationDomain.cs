namespace Identity.Domain;

public class OrganizationDomain
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Domain { get; private set; } = string.Empty;
    public string DomainType { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Organization Organization { get; private set; } = null!;

    private OrganizationDomain() { }

    public static OrganizationDomain Create(
        Guid organizationId,
        string domain,
        string domainType,
        bool isPrimary = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(domainType);

        if (!Identity.Domain.DomainType.IsValid(domainType))
            throw new ArgumentException($"Invalid DomainType: {domainType}", nameof(domainType));

        return new OrganizationDomain
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Domain = domain.Trim().ToLowerInvariant(),
            DomainType = domainType,
            IsPrimary = isPrimary,
            IsVerified = false,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Verify() => IsVerified = true;
}
