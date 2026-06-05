namespace Notifications.Domain;

public class DeliveryIssue
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid NotificationId { get; set; }
    public Guid? NotificationAttemptId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string? RecommendedAction { get; set; }
    public string? DetailsJson { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
