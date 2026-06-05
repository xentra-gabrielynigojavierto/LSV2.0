using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class FacilityRepository : IFacilityRepository
{
    private readonly LiensDbContext _db;

    public FacilityRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<Facility?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Facilities
            .Where(f => f.TenantId == tenantId && f.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<Facility> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? search, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Facilities.Where(f => f.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(f =>
                f.Name.Contains(term) ||
                (f.Code != null && f.Code.Contains(term)) ||
                (f.City != null && f.City.Contains(term)));
        }

        if (isActive.HasValue)
            q = q.Where(f => f.IsActive == isActive.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderBy(f => f.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Facility entity, CancellationToken ct = default)
    {
        await _db.Facilities.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Facility entity, CancellationToken ct = default)
    {
        _db.Facilities.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
