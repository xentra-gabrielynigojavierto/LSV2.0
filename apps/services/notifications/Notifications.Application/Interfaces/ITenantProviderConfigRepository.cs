using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ITenantProviderConfigRepository
{
    Task<TenantProviderConfig?> GetByIdAsync(Guid id);
    Task<TenantProviderConfig?> FindByIdAndTenantAsync(Guid id, Guid tenantId);
    Task<List<TenantProviderConfig>> GetByTenantAsync(Guid tenantId);
    Task<List<TenantProviderConfig>> GetByTenantAndChannelAsync(Guid tenantId, string channel);
    Task<List<TenantProviderConfig>> GetActiveByTenantAndChannelAsync(Guid tenantId, string channel);
    Task<TenantProviderConfig> CreateAsync(TenantProviderConfig config);
    Task UpdateAsync(TenantProviderConfig config);
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Returns all active provider configs for the given providerType across all tenants.
    /// Used by InboundSmsResolverService to match inbound Twilio `To` numbers.
    /// </summary>
    Task<List<TenantProviderConfig>> GetActiveSmsProviderConfigsAsync(string providerType);
}
