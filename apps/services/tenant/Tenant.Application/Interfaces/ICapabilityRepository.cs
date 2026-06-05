using Tenant.Domain;

namespace Tenant.Application.Interfaces;

public interface ICapabilityRepository
{
    Task<TenantCapability?>       GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TenantCapability>>  ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantCapability?>       GetByKeyAsync(Guid tenantId, string capabilityKey, Guid? productEntitlementId, CancellationToken ct = default);
    Task AddAsync(TenantCapability capability, CancellationToken ct = default);
    Task UpdateAsync(TenantCapability capability, CancellationToken ct = default);
    Task DeleteAsync(TenantCapability capability, CancellationToken ct = default);
}
