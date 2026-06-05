namespace Notifications.Domain;

public class TenantRateLimitPolicy
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? Channel { get; set; }
    public string Status { get; set; } = "active";
    public int? MaxRequestsPerMinute { get; set; }
    public int? MaxAttemptsPerMinute { get; set; }
    public int? MaxDailyUsage { get; set; }
    public int? MaxMonthlyUsage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
