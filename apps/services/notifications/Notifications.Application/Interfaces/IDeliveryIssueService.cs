namespace Notifications.Application.Interfaces;

public class DeliveryIssueContext
{
    public Guid TenantId { get; set; }
    public Guid NotificationId { get; set; }
    public Guid? NotificationAttemptId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string NormalizedEventType { get; set; } = string.Empty;
    public string RawEventType { get; set; } = string.Empty;
    public string? RecipientContact { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IDeliveryIssueService
{
    Task ProcessEventAsync(DeliveryIssueContext context);
}
