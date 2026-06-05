using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ITenantBrandingRepository
{
    Task<TenantBranding?> FindByTenantAndProductAsync(Guid tenantId, string productType);
    Task<List<TenantBranding>> GetByTenantAsync(Guid tenantId);
    Task<TenantBranding> UpsertAsync(TenantBranding branding);
}
