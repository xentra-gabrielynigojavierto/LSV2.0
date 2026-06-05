namespace Notifications.Domain;

public class RecipientContactHealth
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string ContactValue { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = "valid";
    public int BounceCount { get; set; }
    public int ComplaintCount { get; set; }
    public int DeliveryCount { get; set; }
    public DateTime? LastBounceAt { get; set; }
    public DateTime? LastComplaintAt { get; set; }
    public DateTime? LastDeliveryAt { get; set; }
    public string? LastRawEventType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
