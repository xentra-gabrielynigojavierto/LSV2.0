namespace Notifications.Domain;

public class ProviderWebhookLog
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? RequestHeadersJson { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public bool SignatureVerified { get; set; }
    public string ProcessingStatus { get; set; } = "received";
    public string? ErrorMessage { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
