namespace Notifications.Application.DTOs;

public class BillingPlanDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string BillingMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? MonthlyFlatRate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BillingRateDto
{
    public Guid Id { get; set; }
    public Guid BillingPlanId { get; set; }
    public string UsageUnit { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public string? ProviderOwnershipMode { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsBillable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
