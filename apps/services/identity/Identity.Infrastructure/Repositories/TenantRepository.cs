using Identity.Application;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly IdentityDbContext _db;

    public TenantRepository(IdentityDbContext db) => _db = db;

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Code == code, ct);

    public Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Subdomain == subdomain, ct);

    /// <summary>
    /// Resolves a tenant from the full HTTP hostname.
    /// Strips any port (e.g. "firm-a.legalsynq.com:3000" → "firm-a.legalsynq.com")
    /// then looks for a matching TenantDomain record.
    /// </summary>
    public Task<Tenant?> GetByHostAsync(string host, CancellationToken ct = default)
    {
        // Strip port number if present (e.g. localhost:3000 → localhost)
        var hostWithoutPort = host.Contains(':')
            ? host[..host.LastIndexOf(':')]
            : host;

        return _db.TenantDomains
            .Include(td => td.Tenant)
            .Where(td => td.Domain == hostWithoutPort.ToLowerInvariant())
            .Select(td => td.Tenant)
            .FirstOrDefaultAsync(ct);
    }

    public Task<List<string>> GetEnabledProductCodesAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.TenantProducts
            .Include(tp => tp.Product)
            .Where(tp => tp.TenantId == tenantId && tp.IsEnabled)
            .Select(tp => tp.Product.Code)
            .ToListAsync(ct);
}
