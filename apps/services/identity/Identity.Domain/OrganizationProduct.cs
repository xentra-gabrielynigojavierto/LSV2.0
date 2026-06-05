namespace Identity.Domain;

public class OrganizationProduct
{
    public Guid OrganizationId { get; private set; }
    public Guid ProductId { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime? EnabledAtUtc { get; private set; }
    public Guid? GrantedByUserId { get; private set; }

    public Organization Organization { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    private OrganizationProduct() { }

    public static OrganizationProduct Create(
        Guid organizationId,
        Guid productId,
        Guid? grantedByUserId = null)
    {
        var now = DateTime.UtcNow;
        return new OrganizationProduct
        {
            OrganizationId = organizationId,
            ProductId = productId,
            IsEnabled = true,
            EnabledAtUtc = now,
            GrantedByUserId = grantedByUserId
        };
    }

    public void Disable() => IsEnabled = false;

    public void Enable()
    {
        IsEnabled = true;
        EnabledAtUtc = DateTime.UtcNow;
    }
}
