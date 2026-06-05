namespace Identity.Domain;

public class TenantProduct
{
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime? EnabledAtUtc { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    private TenantProduct() { }

    public static TenantProduct Create(Guid tenantId, Guid productId)
    {
        return new TenantProduct
        {
            TenantId = tenantId,
            ProductId = productId,
            IsEnabled = true,
            EnabledAtUtc = DateTime.UtcNow
        };
    }
}
