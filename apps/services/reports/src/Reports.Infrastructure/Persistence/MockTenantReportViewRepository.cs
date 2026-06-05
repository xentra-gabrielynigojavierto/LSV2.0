using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class MockTenantReportViewRepository : ITenantReportViewRepository
{
    private readonly List<TenantReportView> _store = new();

    public Task<TenantReportView?> GetByIdAsync(Guid viewId, string tenantId, CancellationToken ct)
        => Task.FromResult(_store.FirstOrDefault(v => v.Id == viewId && v.TenantId == tenantId));

    public Task<IReadOnlyList<TenantReportView>> ListByTenantAndTemplateAsync(string tenantId, Guid templateId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<TenantReportView>>(
            _store.Where(v => v.TenantId == tenantId && v.ReportTemplateId == templateId && v.IsActive)
                .OrderByDescending(v => v.IsDefault).ThenBy(v => v.Name).ToList());

    public Task<TenantReportView?> GetDefaultViewAsync(string tenantId, Guid templateId, CancellationToken ct)
        => Task.FromResult(_store.FirstOrDefault(v => v.TenantId == tenantId && v.ReportTemplateId == templateId && v.IsDefault && v.IsActive));

    public Task<bool> HasDefaultViewAsync(string tenantId, Guid templateId, Guid? excludeViewId, CancellationToken ct)
    {
        var q = _store.Where(v => v.TenantId == tenantId && v.ReportTemplateId == templateId && v.IsDefault && v.IsActive);
        if (excludeViewId.HasValue) q = q.Where(v => v.Id != excludeViewId.Value);
        return Task.FromResult(q.Any());
    }

    public Task<TenantReportView> CreateAsync(TenantReportView entity, CancellationToken ct)
    {
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        _store.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<TenantReportView> UpdateAsync(TenantReportView entity, CancellationToken ct)
    {
        var idx = _store.FindIndex(v => v.Id == entity.Id);
        if (idx >= 0) _store[idx] = entity;
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(Guid viewId, string tenantId, CancellationToken ct)
    {
        _store.RemoveAll(v => v.Id == viewId && v.TenantId == tenantId);
        return Task.CompletedTask;
    }
}
