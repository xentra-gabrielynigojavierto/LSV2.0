using Microsoft.EntityFrameworkCore;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class EfTenantReportViewRepository : ITenantReportViewRepository
{
    private readonly ReportsDbContext _db;

    public EfTenantReportViewRepository(ReportsDbContext db) => _db = db;

    public async Task<TenantReportView?> GetByIdAsync(Guid viewId, string tenantId, CancellationToken ct)
    {
        return await _db.TenantReportViews
            .FirstOrDefaultAsync(v => v.Id == viewId && v.TenantId == tenantId, ct);
    }

    public async Task<IReadOnlyList<TenantReportView>> ListByTenantAndTemplateAsync(string tenantId, Guid templateId, CancellationToken ct)
    {
        return await _db.TenantReportViews
            .Where(v => v.TenantId == tenantId && v.ReportTemplateId == templateId && v.IsActive)
            .OrderByDescending(v => v.IsDefault)
            .ThenBy(v => v.Name)
            .ToListAsync(ct);
    }

    public async Task<TenantReportView?> GetDefaultViewAsync(string tenantId, Guid templateId, CancellationToken ct)
    {
        return await _db.TenantReportViews
            .Where(v => v.TenantId == tenantId && v.ReportTemplateId == templateId && v.IsDefault && v.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> HasDefaultViewAsync(string tenantId, Guid templateId, Guid? excludeViewId, CancellationToken ct)
    {
        var query = _db.TenantReportViews
            .Where(v => v.TenantId == tenantId
                        && v.ReportTemplateId == templateId
                        && v.IsDefault
                        && v.IsActive);

        if (excludeViewId.HasValue)
            query = query.Where(v => v.Id != excludeViewId.Value);

        return await query.AnyAsync(ct);
    }

    public async Task<TenantReportView> CreateAsync(TenantReportView entity, CancellationToken ct)
    {
        _db.TenantReportViews.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<TenantReportView> UpdateAsync(TenantReportView entity, CancellationToken ct)
    {
        _db.TenantReportViews.Update(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task DeleteAsync(Guid viewId, string tenantId, CancellationToken ct)
    {
        var entity = await _db.TenantReportViews
            .FirstOrDefaultAsync(v => v.Id == viewId && v.TenantId == tenantId, ct);
        if (entity is not null)
        {
            _db.TenantReportViews.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}
