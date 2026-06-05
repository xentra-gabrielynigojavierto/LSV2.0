using Microsoft.EntityFrameworkCore;
using Tenant.Application.Interfaces;
using Tenant.Infrastructure.Data;

namespace Tenant.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly TenantDbContext _db;

    public TenantRepository(TenantDbContext db) => _db = db;

    public Task<Domain.Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Domain.Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Code == code, ct);

    public Task<Domain.Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Subdomain == subdomain, ct);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) =>
        _db.Tenants.AnyAsync(t => t.Code == code, ct);

    public Task<bool> ExistsBySubdomainAsync(string subdomain, Guid? excludeId, CancellationToken ct = default) =>
        _db.Tenants.AnyAsync(t => t.Subdomain == subdomain && (excludeId == null || t.Id != excludeId), ct);

    public async Task<(List<Domain.Tenant> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Tenants.OrderBy(t => t.DisplayName);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(Domain.Tenant tenant, CancellationToken ct = default)
    {
        await _db.Tenants.AddAsync(tenant, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Domain.Tenant tenant, CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }
}
