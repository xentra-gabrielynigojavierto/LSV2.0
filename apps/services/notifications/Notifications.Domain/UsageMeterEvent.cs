namespace Notifications.Domain;

public class UsageMeterEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? NotificationId { get; set; }
    public Guid? NotificationAttemptId { get; set; }
    public string? Channel { get; set; }
    public string? Provider { get; set; }
    public string? ProviderOwnershipMode { get; set; }
    public Guid? ProviderConfigId { get; set; }
    public string UsageUnit { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public bool IsBillable { get; set; }
    public decimal? ProviderUnitCost { get; set; }
    public decimal? ProviderTotalCost { get; set; }
    public string? Currency { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
