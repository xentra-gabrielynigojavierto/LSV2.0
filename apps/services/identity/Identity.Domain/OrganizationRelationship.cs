namespace Identity.Domain;

/// <summary>
/// First-class entity representing a named relationship between two organizations.
/// E.g., Law Firm A –[REFERS_TO]→ Provider B.
/// </summary>
public class OrganizationRelationship
{
    public Guid   Id                   { get; private set; }
    public Guid   TenantId             { get; private set; }
    public Guid   SourceOrganizationId { get; private set; }
    public Guid   TargetOrganizationId { get; private set; }
    public Guid   RelationshipTypeId   { get; private set; }
    public Guid?  ProductId            { get; private set; }
    public bool   IsActive             { get; private set; }
    public DateTime EstablishedAtUtc   { get; private set; }
    public DateTime CreatedAtUtc       { get; private set; }
    public DateTime UpdatedAtUtc       { get; private set; }
    public Guid?  CreatedByUserId      { get; private set; }

    public Organization  SourceOrganization { get; private set; } = null!;
    public Organization  TargetOrganization { get; private set; } = null!;
    public RelationshipType RelationshipType { get; private set; } = null!;
    public Product? Product { get; private set; }

    private OrganizationRelationship() { }

    public static OrganizationRelationship Create(
        Guid   tenantId,
        Guid   sourceOrganizationId,
        Guid   targetOrganizationId,
        Guid   relationshipTypeId,
        Guid?  productId = null,
        Guid?  createdByUserId = null)
    {
        var now = DateTime.UtcNow;
        return new OrganizationRelationship
        {
            Id                   = Guid.NewGuid(),
            TenantId             = tenantId,
            SourceOrganizationId = sourceOrganizationId,
            TargetOrganizationId = targetOrganizationId,
            RelationshipTypeId   = relationshipTypeId,
            ProductId            = productId,
            IsActive             = true,
            EstablishedAtUtc     = now,
            CreatedAtUtc         = now,
            UpdatedAtUtc         = now,
            CreatedByUserId      = createdByUserId
        };
    }

    public void Deactivate(Guid? updatedByUserId = null)
    {
        IsActive     = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
