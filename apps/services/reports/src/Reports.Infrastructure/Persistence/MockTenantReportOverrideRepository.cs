using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class MockTenantReportOverrideRepository : ITenantReportOverrideRepository
{
    private readonly List<TenantReportOverride> _overrides = new();

    public Task<TenantReportOverride?> GetByIdAsync(Guid overrideId, CancellationToken ct)
    {
        var entity = _overrides.FirstOrDefault(o => o.Id == overrideId);
        return Task.FromResult(entity);
    }

    public Task<TenantReportOverride?> GetByTenantAndTemplateAsync(string tenantId, Guid templateId, CancellationToken ct)
    {
        var entity = _overrides.FirstOrDefault(o =>
            o.TenantId == tenantId && o.ReportTemplateId == templateId && o.IsActive);
        return Task.FromResult(entity);
    }

    public Task<TenantReportOverride?> GetAnyByTenantAndTemplateAsync(string tenantId, Guid templateId, CancellationToken ct)
    {
        var entity = _overrides.FirstOrDefault(o =>
            o.TenantId == tenantId && o.ReportTemplateId == templateId);
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<TenantReportOverride>> ListByTenantAsync(string tenantId, CancellationToken ct)
    {
        var list = _overrides
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<TenantReportOverride>>(list.AsReadOnly());
    }

    public Task<IReadOnlyList<TenantReportOverride>> ListByTemplateAsync(Guid templateId, string? tenantId, CancellationToken ct)
    {
        var query = _overrides.Where(o => o.ReportTemplateId == templateId);
        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(o => o.TenantId == tenantId);

        var list = query.OrderByDescending(o => o.CreatedAtUtc).ToList();
        return Task.FromResult<IReadOnlyList<TenantReportOverride>>(list.AsReadOnly());
    }

    public Task<bool> HasActiveOverrideAsync(string tenantId, Guid templateId, Guid? excludeOverrideId, CancellationToken ct)
    {
        var exists = _overrides.Any(o =>
            o.TenantId == tenantId
            && o.ReportTemplateId == templateId
            && o.IsActive
            && (!excludeOverrideId.HasValue || o.Id != excludeOverrideId.Value));
        return Task.FromResult(exists);
    }

    public Task<TenantReportOverride> CreateAsync(TenantReportOverride entity, CancellationToken ct)
    {
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        _overrides.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<TenantReportOverride> UpdateAsync(TenantReportOverride entity, CancellationToken ct)
    {
        var idx = _overrides.FindIndex(o => o.Id == entity.Id);
        if (idx >= 0) _overrides[idx] = entity;
        return Task.FromResult(entity);
    }
}
