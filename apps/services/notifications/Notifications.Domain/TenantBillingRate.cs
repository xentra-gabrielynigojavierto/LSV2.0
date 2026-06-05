namespace Notifications.Domain;

public class TenantBillingRate
{
    public Guid Id { get; set; }
    public Guid BillingPlanId { get; set; }
    public string UsageUnit { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public string? ProviderOwnershipMode { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsBillable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
