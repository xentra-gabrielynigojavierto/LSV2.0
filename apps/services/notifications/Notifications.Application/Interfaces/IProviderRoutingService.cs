namespace Notifications.Application.Interfaces;

public class ProviderRoute
{
    public string ProviderType { get; set; } = string.Empty;
    public string OwnershipMode { get; set; } = "platform";
    public Guid? TenantProviderConfigId { get; set; }
    public bool IsFailover { get; set; }
    public bool IsPlatformFallback { get; set; }
}

public interface IProviderRoutingService
{
    Task<List<ProviderRoute>> ResolveRoutesAsync(Guid tenantId, string channel);
}
