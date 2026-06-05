using Reports.Domain.Entities;

namespace Reports.Contracts.Persistence;

public interface ITenantReportViewRepository
{
    /// <summary>
    /// Fetch a saved view scoped to the given tenant. Returns null if not found OR if the view belongs to a different tenant.
    /// </summary>
    Task<TenantReportView?> GetByIdAsync(Guid viewId, string tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<TenantReportView>> ListByTenantAndTemplateAsync(string tenantId, Guid templateId, CancellationToken ct = default);
    Task<TenantReportView?> GetDefaultViewAsync(string tenantId, Guid templateId, CancellationToken ct = default);
    Task<bool> HasDefaultViewAsync(string tenantId, Guid templateId, Guid? excludeViewId = null, CancellationToken ct = default);
    Task<TenantReportView> CreateAsync(TenantReportView entity, CancellationToken ct = default);
    Task<TenantReportView> UpdateAsync(TenantReportView entity, CancellationToken ct = default);

    /// <summary>
    /// Delete a saved view only if it belongs to the given tenant.
    /// </summary>
    Task DeleteAsync(Guid viewId, string tenantId, CancellationToken ct = default);
}
