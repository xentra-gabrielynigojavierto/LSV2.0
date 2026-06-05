using Tenant.Domain;

namespace Tenant.Application.Interfaces;

public interface IEntitlementRepository
{
    Task<TenantProductEntitlement?>        GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TenantProductEntitlement>>   ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantProductEntitlement?>        GetByTenantAndProductKeyAsync(Guid tenantId, string productKey, CancellationToken ct = default);
    Task<TenantProductEntitlement?>        GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Returns all entitlements for the tenant where IsDefault = true (should be at most one).</summary>
    Task<List<TenantProductEntitlement>>   GetDefaultsForTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task AddAsync(TenantProductEntitlement entitlement, CancellationToken ct = default);
    Task UpdateAsync(TenantProductEntitlement entitlement, CancellationToken ct = default);
    Task UpdateRangeAsync(IEnumerable<TenantProductEntitlement> entitlements, CancellationToken ct = default);
    Task DeleteAsync(TenantProductEntitlement entitlement, CancellationToken ct = default);
}
