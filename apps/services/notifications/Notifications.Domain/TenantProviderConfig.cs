namespace Notifications.Domain;

public class TenantProviderConfig
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CredentialsJson { get; set; } = "{}";
    public string SettingsJson { get; set; } = "{}";
    public string Status { get; set; } = "active";
    public string ValidationStatus { get; set; } = "not_validated";
    public string? ValidationMessage { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public string HealthStatus { get; set; } = "unknown";
    public DateTime? LastHealthCheckAt { get; set; }
    public int? HealthCheckLatencyMs { get; set; }
    public int Priority { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
