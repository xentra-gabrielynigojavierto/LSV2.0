namespace Identity.Domain;

public class AccessGroup
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public GroupStatus Status { get; private set; }
    public GroupScopeType ScopeType { get; private set; }
    public string? ProductCode { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private AccessGroup() { }

    public static AccessGroup Create(
        Guid tenantId,
        string name,
        string? description = null,
        GroupScopeType scopeType = GroupScopeType.Tenant,
        string? productCode = null,
        Guid? organizationId = null,
        Guid? createdByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (scopeType == GroupScopeType.Product && string.IsNullOrWhiteSpace(productCode))
            throw new InvalidOperationException("ProductCode is required for product-scoped groups.");
        if (scopeType == GroupScopeType.Organization && !organizationId.HasValue)
            throw new InvalidOperationException("OrganizationId is required for organization-scoped groups.");
        if (scopeType == GroupScopeType.Tenant)
        {
            productCode = null;
            organizationId = null;
        }

        var now = DateTime.UtcNow;
        return new AccessGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Status = GroupStatus.Active,
            ScopeType = scopeType,
            ProductCode = productCode?.ToUpperInvariant().Trim(),
            OrganizationId = organizationId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId
        };
    }

    public void Update(string name, string? description, Guid? updatedByUserId = null)
    {
        if (Status == GroupStatus.Archived)
            throw new InvalidOperationException("Cannot update an archived group.");

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }

    public void Archive(Guid? updatedByUserId = null)
    {
        Status = GroupStatus.Archived;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }
}
