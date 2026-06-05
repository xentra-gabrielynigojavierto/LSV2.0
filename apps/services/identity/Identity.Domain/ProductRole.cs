namespace Identity.Domain;

public class ProductRole
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    // EligibleOrgType removed in migration 20260330200003_PhaseFRetirement.
    // Eligibility is now exclusively determined by ProductOrganizationTypeRules.
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Product Product { get; private set; } = null!;
    public ICollection<RolePermissionMapping> RolePermissionMappings { get; private set; } = [];
    public ICollection<ProductOrganizationTypeRule> OrgTypeRules { get; private set; } = [];

    private ProductRole() { }

    public static ProductRole Create(
        Guid productId,
        string code,
        string name,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new ProductRole
        {
            Id            = Guid.NewGuid(),
            ProductId     = productId,
            Code          = code.ToUpperInvariant().Trim(),
            Name          = name.Trim(),
            Description   = description?.Trim(),
            IsActive      = true,
            CreatedAtUtc  = DateTime.UtcNow
        };
    }
}
