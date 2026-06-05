namespace Identity.Domain;

public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public ICollection<TenantProduct> TenantProducts { get; private set; } = [];
    public ICollection<OrganizationProduct> OrganizationProducts { get; private set; } = [];
    public ICollection<ProductRole> ProductRoles { get; private set; } = [];
    public ICollection<Permission> Permissions { get; private set; } = [];

    private Product() { }

    public static Product Create(string name, string code, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = code.ToUpperInvariant().Trim(),
            Description = description?.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
