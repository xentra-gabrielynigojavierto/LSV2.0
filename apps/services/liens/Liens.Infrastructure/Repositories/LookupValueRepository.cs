using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class LookupValueRepository : ILookupValueRepository
{
    private readonly LiensDbContext _db;

    public LookupValueRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<LookupValue?> GetByIdAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.LookupValues
            .Where(lv => lv.Id == id && (lv.TenantId == null || lv.TenantId == tenantId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<LookupValue>> GetByCategoryAsync(Guid? tenantId, string category, CancellationToken ct = default)
    {
        return await _db.LookupValues
            .Where(lv => (lv.TenantId == null || lv.TenantId == tenantId) && lv.Category == category && lv.IsActive)
            .OrderBy(lv => lv.SortOrder)
            .ThenBy(lv => lv.Name)
            .ToListAsync(ct);
    }

    public async Task<LookupValue?> GetByCodeAsync(Guid? tenantId, string category, string code, CancellationToken ct = default)
    {
        return await _db.LookupValues
            .Where(lv => (lv.TenantId == null || lv.TenantId == tenantId) && lv.Category == category && lv.Code == code && lv.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(LookupValue entity, CancellationToken ct = default)
    {
        await _db.LookupValues.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LookupValue entity, CancellationToken ct = default)
    {
        _db.LookupValues.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
