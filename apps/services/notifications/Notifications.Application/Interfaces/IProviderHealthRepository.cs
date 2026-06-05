using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface IProviderHealthRepository
{
    Task<ProviderHealth?> FindByProviderAsync(string providerType, string channel, string ownershipMode, Guid? tenantProviderConfigId = null);
    Task<List<ProviderHealth>> GetAllAsync();
    Task<ProviderHealth> UpsertAsync(ProviderHealth health);
}
