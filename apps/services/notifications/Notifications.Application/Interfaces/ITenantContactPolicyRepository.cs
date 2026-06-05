using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ITenantContactPolicyRepository
{
    Task<TenantContactPolicy?> FindEffectivePolicyAsync(Guid tenantId, string? channel = null);
    Task<List<TenantContactPolicy>> GetByTenantAsync(Guid tenantId);
    Task<TenantContactPolicy> UpsertAsync(TenantContactPolicy policy);
}
