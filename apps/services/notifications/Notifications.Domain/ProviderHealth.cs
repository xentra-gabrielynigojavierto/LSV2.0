namespace Notifications.Domain;

public class ProviderHealth
{
    public Guid Id { get; set; }
    public string ProviderType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string OwnershipMode { get; set; } = "platform";
    public Guid? TenantProviderConfigId { get; set; }
    public string HealthStatus { get; set; } = "healthy";
    public int ConsecutiveFailures { get; set; }
    public int ConsecutiveSuccesses { get; set; }
    public int? LastLatencyMs { get; set; }
    public DateTime? LastCheckAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public DateTime? LastRecoveryAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
