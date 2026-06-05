using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class LienRepository : ILienRepository
{
    private readonly LiensDbContext _db;

    public LienRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<Lien?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Liens
            .Where(l => l.TenantId == tenantId && l.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Lien?> GetByLienNumberAsync(Guid tenantId, string lienNumber, CancellationToken ct = default)
    {
        return await _db.Liens
            .Where(l => l.TenantId == tenantId && l.LienNumber == lienNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<Lien> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? search, string? status, string? lienType,
        Guid? caseId, Guid? facilityId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Liens.Where(l => l.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(l =>
                l.LienNumber.Contains(term) ||
                (l.SubjectFirstName != null && l.SubjectFirstName.Contains(term)) ||
                (l.SubjectLastName != null && l.SubjectLastName.Contains(term)) ||
                (l.Description != null && l.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(l => l.Status == status);

        if (!string.IsNullOrWhiteSpace(lienType))
            q = q.Where(l => l.LienType == lienType);

        if (caseId.HasValue)
            q = q.Where(l => l.CaseId == caseId.Value);

        if (facilityId.HasValue)
            q = q.Where(l => l.FacilityId == facilityId.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<List<Lien>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        return await _db.Liens
            .Where(l => l.TenantId == tenantId && l.CaseId == caseId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<Lien>> GetByFacilityIdAsync(Guid tenantId, Guid facilityId, CancellationToken ct = default)
    {
        return await _db.Liens
            .Where(l => l.TenantId == tenantId && l.FacilityId == facilityId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Lien entity, CancellationToken ct = default)
    {
        await _db.Liens.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Lien entity, CancellationToken ct = default)
    {
        _db.Liens.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
