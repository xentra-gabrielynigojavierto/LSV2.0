namespace Notifications.Domain;

public class TenantChannelProviderSetting
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string ProviderMode { get; set; } = "platform_managed";
    public Guid? PrimaryTenantProviderConfigId { get; set; }
    public Guid? FallbackTenantProviderConfigId { get; set; }
    public bool AllowPlatformFallback { get; set; } = true;
    public bool AllowAutomaticFailover { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
