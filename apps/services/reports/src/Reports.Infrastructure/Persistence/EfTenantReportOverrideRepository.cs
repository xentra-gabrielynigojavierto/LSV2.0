using Microsoft.EntityFrameworkCore;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class EfTenantReportOverrideRepository : ITenantReportOverrideRepository
{
    private readonly ReportsDbContext _db;

    public EfTenantReportOverrideRepository(ReportsDbContext db) => _db = db;

    public async Task<TenantReportOverride?> GetByIdAsync(Guid overrideId, CancellationToken ct)
    {
        return await _db.TenantReportOverrides
            .FirstOrDefaultAsync(o => o.Id == overrideId, ct);
    }

    public async Task<TenantReportOverride?> GetByTenantAndTemplateAsync(string tenantId, Guid templateId, CancellationToken ct)
    {
        return await _db.TenantReportOverrides
            .Where(o => o.TenantId == tenantId && o.ReportTemplateId == templateId && o.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TenantReportOverride?> GetAnyByTenantAndTemplateAsync(string tenantId, Guid templateId, CancellationToken ct)
    {
        return await _db.TenantReportOverrides
            .Where(o => o.TenantId == tenantId && o.ReportTemplateId == templateId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TenantReportOverride>> ListByTenantAsync(string tenantId, CancellationToken ct)
    {
        return await _db.TenantReportOverrides
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TenantReportOverride>> ListByTemplateAsync(Guid templateId, string? tenantId, CancellationToken ct)
    {
        var query = _db.TenantReportOverrides
            .Where(o => o.ReportTemplateId == templateId);

        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(o => o.TenantId == tenantId);

        return await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<bool> HasActiveOverrideAsync(string tenantId, Guid templateId, Guid? excludeOverrideId, CancellationToken ct)
    {
        var query = _db.TenantReportOverrides
            .Where(o => o.TenantId == tenantId
                        && o.ReportTemplateId == templateId
                        && o.IsActive);

        if (excludeOverrideId.HasValue)
            query = query.Where(o => o.Id != excludeOverrideId.Value);

        return await query.AnyAsync(ct);
    }

    public async Task<TenantReportOverride> CreateAsync(TenantReportOverride entity, CancellationToken ct)
    {
        _db.TenantReportOverrides.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException("A conflicting tenant report override already exists.", ex);
        }
        return entity;
    }

    public async Task<TenantReportOverride> UpdateAsync(TenantReportOverride entity, CancellationToken ct)
    {
        _db.TenantReportOverrides.Update(entity);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException("A conflicting tenant report override already exists.", ex);
        }
        return entity;
    }
}
