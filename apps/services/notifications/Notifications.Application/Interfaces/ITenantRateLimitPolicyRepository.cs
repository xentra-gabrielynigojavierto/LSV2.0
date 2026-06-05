using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface ITenantRateLimitPolicyRepository
{
    Task<List<TenantRateLimitPolicy>> FindActivePoliciesAsync(Guid tenantId, string? channel = null);
    Task<List<TenantRateLimitPolicy>> GetByTenantAsync(Guid tenantId);
    Task<TenantRateLimitPolicy> CreateAsync(TenantRateLimitPolicy policy);
    Task UpdateAsync(TenantRateLimitPolicy policy);
}
