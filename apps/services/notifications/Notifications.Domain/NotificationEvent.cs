namespace Notifications.Domain;

public class NotificationEvent
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? NotificationId { get; set; }
    public Guid? NotificationAttemptId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string RawEventType { get; set; } = string.Empty;
    public string NormalizedEventType { get; set; } = string.Empty;
    public DateTime EventTimestamp { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? MetadataJson { get; set; }
    public string? DedupKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
