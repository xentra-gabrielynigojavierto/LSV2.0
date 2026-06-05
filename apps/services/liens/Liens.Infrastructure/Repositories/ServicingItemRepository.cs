using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class ServicingItemRepository : IServicingItemRepository
{
    private readonly LiensDbContext _db;

    public ServicingItemRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<ServicingItem?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.ServicingItems
            .Where(s => s.TenantId == tenantId && s.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ServicingItem?> GetByTaskNumberAsync(Guid tenantId, string taskNumber, CancellationToken ct = default)
    {
        return await _db.ServicingItems
            .Where(s => s.TenantId == tenantId && s.TaskNumber == taskNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(List<ServicingItem> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? search, string? status, string? priority, string? assignedTo,
        Guid? caseId, Guid? lienId, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.ServicingItems.Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(s =>
                s.TaskNumber.Contains(term) ||
                s.Description.Contains(term) ||
                s.AssignedTo.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(s => s.Status == status);

        if (!string.IsNullOrWhiteSpace(priority))
            q = q.Where(s => s.Priority == priority);

        if (!string.IsNullOrWhiteSpace(assignedTo))
            q = q.Where(s => s.AssignedTo == assignedTo);

        if (caseId.HasValue)
            q = q.Where(s => s.CaseId == caseId.Value);

        if (lienId.HasValue)
            q = q.Where(s => s.LienId == lienId.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(ServicingItem entity, CancellationToken ct = default)
    {
        await _db.ServicingItems.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ServicingItem entity, CancellationToken ct = default)
    {
        _db.ServicingItems.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
