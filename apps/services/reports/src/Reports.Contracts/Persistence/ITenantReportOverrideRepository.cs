using Reports.Domain.Entities;

namespace Reports.Contracts.Persistence;

public interface ITenantReportOverrideRepository
{
    Task<TenantReportOverride?> GetByIdAsync(Guid overrideId, CancellationToken ct = default);
    Task<TenantReportOverride?> GetByTenantAndTemplateAsync(string tenantId, Guid templateId, CancellationToken ct = default);
    Task<TenantReportOverride?> GetAnyByTenantAndTemplateAsync(string tenantId, Guid templateId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantReportOverride>> ListByTenantAsync(string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantReportOverride>> ListByTemplateAsync(Guid templateId, string? tenantId = null, CancellationToken ct = default);
    Task<bool> HasActiveOverrideAsync(string tenantId, Guid templateId, Guid? excludeOverrideId = null, CancellationToken ct = default);
    Task<TenantReportOverride> CreateAsync(TenantReportOverride entity, CancellationToken ct = default);
    Task<TenantReportOverride> UpdateAsync(TenantReportOverride entity, CancellationToken ct = default);
}
