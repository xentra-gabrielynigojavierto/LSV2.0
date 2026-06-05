using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ITenantChannelProviderSettingRepository
{
    Task<TenantChannelProviderSetting?> FindByTenantAndChannelAsync(Guid tenantId, string channel);
    Task<List<TenantChannelProviderSetting>> GetByTenantAsync(Guid tenantId);
    Task<TenantChannelProviderSetting> UpsertAsync(TenantChannelProviderSetting setting);
}
