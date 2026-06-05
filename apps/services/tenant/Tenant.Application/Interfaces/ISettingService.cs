using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface ISettingService
{
    Task<List<SettingResponse>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<SettingResponse>       GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Upsert a setting by (TenantId, SettingKey, ProductKey).
    /// Creates if not found; updates value and type if already exists.
    /// </summary>
    Task<SettingResponse>       UpsertAsync(Guid tenantId, UpsertSettingRequest request, CancellationToken ct = default);
    Task                        DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
