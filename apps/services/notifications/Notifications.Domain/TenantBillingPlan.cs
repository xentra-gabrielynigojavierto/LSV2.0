namespace Notifications.Domain;

public class TenantBillingPlan
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string BillingMode { get; set; } = "usage_based";
    public string Status { get; set; } = "active";
    public decimal? MonthlyFlatRate { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
