namespace Identity.Domain;

/// <summary>
/// Catalog of named relationship types between organizations.
/// Seeded with system types; extensible at runtime for tenants.
/// </summary>
public class RelationshipType
{
    public Guid   Id          { get; private set; }
    public string Code        { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool   IsDirectional { get; private set; }
    public bool   IsSystem    { get; private set; }
    public bool   IsActive    { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public ICollection<OrganizationRelationship> Relationships { get; private set; } = [];
    public ICollection<ProductRelationshipTypeRule> ProductRules { get; private set; } = [];

    private RelationshipType() { }

    public static RelationshipType Create(
        string code,
        string displayName,
        string? description = null,
        bool isDirectional = true,
        bool isSystem = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new RelationshipType
        {
            Id           = Guid.NewGuid(),
            Code         = code.ToUpperInvariant().Trim(),
            DisplayName  = displayName.Trim(),
            Description  = description?.Trim(),
            IsDirectional = isDirectional,
            IsSystem     = isSystem,
            IsActive     = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
