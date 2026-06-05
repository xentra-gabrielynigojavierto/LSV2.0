namespace Notifications.Domain;

public class ContactSuppression
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string ContactValue { get; set; } = string.Empty;
    public string SuppressionType { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string? Reason { get; set; }
    public string? Source { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
