using Tenant.Domain;

namespace Tenant.Application.Interfaces;

public interface ISettingRepository
{
    Task<TenantSetting?>       GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<TenantSetting>>  ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSetting?>       GetByKeyAsync(Guid tenantId, string settingKey, string? productKey, CancellationToken ct = default);
    Task AddAsync(TenantSetting setting, CancellationToken ct = default);
    Task UpdateAsync(TenantSetting setting, CancellationToken ct = default);
    Task DeleteAsync(TenantSetting setting, CancellationToken ct = default);
}
