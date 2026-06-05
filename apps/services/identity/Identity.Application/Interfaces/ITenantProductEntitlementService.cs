using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface ITenantProductEntitlementService
{
    Task<List<TenantProductEntitlement>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantProductEntitlement?> GetByTenantAndCodeAsync(Guid tenantId, string productCode, CancellationToken ct = default);
    Task<TenantProductEntitlement> UpsertAsync(Guid tenantId, string productCode, Guid? actorUserId = null, CancellationToken ct = default);
    Task<bool> DisableAsync(Guid tenantId, string productCode, Guid? actorUserId = null, CancellationToken ct = default);
}
