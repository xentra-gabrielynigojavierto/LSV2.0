namespace Notifications.Domain;

public class Notification
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = "accepted";
    public string RecipientJson { get; set; } = string.Empty;
    public string MessageJson { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ProviderUsed { get; set; }
    public string? FailureCategory { get; set; }
    public string? LastErrorMessage { get; set; }
    public Guid? TemplateId { get; set; }
    public Guid? TemplateVersionId { get; set; }
    public string? TemplateKey { get; set; }
    public string? RenderedSubject { get; set; }
    public string? RenderedBody { get; set; }
    public string? RenderedText { get; set; }
    public string? ProviderOwnershipMode { get; set; }
    public Guid? ProviderConfigId { get; set; }
    public bool PlatformFallbackUsed { get; set; }
    public bool BlockedByPolicy { get; set; }
    public string? BlockedReasonCode { get; set; }
    public bool OverrideUsed { get; set; }
    public string? Severity { get; set; }
    public string? Category { get; set; }
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public DateTime? NextRetryAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
