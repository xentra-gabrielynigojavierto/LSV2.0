namespace Identity.Domain;

public class TenantProductEntitlement
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public EntitlementStatus Status { get; private set; }
    public DateTime? EnabledAtUtc { get; private set; }
    public DateTime? DisabledAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private TenantProductEntitlement() { }

    public static TenantProductEntitlement Create(
        Guid tenantId,
        string productCode,
        Guid? createdByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);

        var now = DateTime.UtcNow;
        return new TenantProductEntitlement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductCode = productCode.ToUpperInvariant().Trim(),
            Status = EntitlementStatus.Active,
            EnabledAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId
        };
    }

    public void Disable(Guid? updatedByUserId = null)
    {
        Status = EntitlementStatus.Disabled;
        DisabledAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }

    public void Enable(Guid? updatedByUserId = null)
    {
        Status = EntitlementStatus.Active;
        EnabledAtUtc = DateTime.UtcNow;
        DisabledAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }
}
