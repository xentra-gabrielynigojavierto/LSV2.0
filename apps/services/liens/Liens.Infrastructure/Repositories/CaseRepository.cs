using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class CaseRepository : ICaseRepository
{
    private readonly LiensDbContext _db;

    public CaseRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<Case?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Cases
            .Where(c => c.TenantId == tenantId && c.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Case?> GetByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default)
    {
        return await _db.Cases
            .Where(c => c.TenantId == tenantId && c.CaseNumber == caseNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<Case> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? search, string? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Cases.Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(c =>
                c.CaseNumber.Contains(term) ||
                c.ClientFirstName.Contains(term) ||
                c.ClientLastName.Contains(term) ||
                (c.Title != null && c.Title.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(c => c.Status == status);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Case entity, CancellationToken ct = default)
    {
        await _db.Cases.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Case entity, CancellationToken ct = default)
    {
        _db.Cases.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
