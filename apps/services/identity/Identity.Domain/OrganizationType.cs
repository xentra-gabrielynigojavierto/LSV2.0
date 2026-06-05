namespace Identity.Domain;

/// <summary>
/// Catalog entity for organization types.  Replaces the hard-coded OrgType string enum.
/// The OrgType static class is kept for backward-compat; OrganizationTypeId is the new FK.
/// </summary>
public class OrganizationType
{
    public Guid   Id          { get; private set; }
    public string Code        { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool   IsSystem    { get; private set; }
    public bool   IsActive    { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public ICollection<Organization> Organizations { get; private set; } = [];

    private OrganizationType() { }

    public static OrganizationType Create(
        string code,
        string displayName,
        string? description = null,
        bool isSystem = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new OrganizationType
        {
            Id          = Guid.NewGuid(),
            Code        = code.ToUpperInvariant().Trim(),
            DisplayName = displayName.Trim(),
            Description = description?.Trim(),
            IsSystem    = isSystem,
            IsActive    = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
