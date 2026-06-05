using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public sealed class LienTaskGenerationRuleRepository : ILienTaskGenerationRuleRepository
{
    private readonly LiensDbContext _db;

    public LienTaskGenerationRuleRepository(LiensDbContext db) => _db = db;

    public async Task<List<LienTaskGenerationRule>> GetByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        return await _db.LienTaskGenerationRules
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<LienTaskGenerationRule>> GetActiveByTenantAndEventAsync(
        Guid tenantId, string eventType, CancellationToken ct = default)
    {
        return await _db.LienTaskGenerationRules
            .Where(r => r.TenantId == tenantId && r.IsActive && r.EventType == eventType)
            .ToListAsync(ct);
    }

    public async Task<LienTaskGenerationRule?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.LienTaskGenerationRules
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);
    }

    public async Task AddAsync(LienTaskGenerationRule entity, CancellationToken ct = default)
    {
        await _db.LienTaskGenerationRules.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienTaskGenerationRule entity, CancellationToken ct = default)
    {
        _db.LienTaskGenerationRules.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
