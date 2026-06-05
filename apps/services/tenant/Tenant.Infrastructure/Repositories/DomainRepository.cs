using Microsoft.EntityFrameworkCore;
using Tenant.Application.Interfaces;
using Tenant.Domain;
using Tenant.Infrastructure.Data;

namespace Tenant.Infrastructure.Repositories;

public class DomainRepository : IDomainRepository
{
    private readonly TenantDbContext _db;
    private readonly IDbContextFactory<TenantDbContext> _dbFactory;

    public DomainRepository(TenantDbContext db, IDbContextFactory<TenantDbContext> dbFactory)
    {
        _db        = db;
        _dbFactory = dbFactory;
    }

    public Task<TenantDomain?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Domains.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<List<TenantDomain>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Domains
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.Host)
            .ToListAsync(ct);
    }

    public Task<TenantDomain?> GetActiveByHostAsync(string normalizedHost, CancellationToken ct = default) =>
        _db.Domains
            .FirstOrDefaultAsync(
                d => d.Host == normalizedHost && d.Status == TenantDomainStatus.Active,
                ct);

    public Task<TenantDomain?> GetActivePrimarySubdomainByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Domains
            .FirstOrDefaultAsync(
                d => d.TenantId    == tenantId
                  && d.Status      == TenantDomainStatus.Active
                  && d.DomainType  == TenantDomainType.Subdomain
                  && d.IsPrimary,
                ct);

    public Task<bool> ActiveHostExistsAsync(string normalizedHost, Guid? excludeId, CancellationToken ct = default) =>
        _db.Domains
            .AnyAsync(
                d => d.Host == normalizedHost
                  && d.Status == TenantDomainStatus.Active
                  && (excludeId == null || d.Id != excludeId),
                ct);

    public Task<List<TenantDomain>> GetActiveSubdomainsForTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Domains
            .Where(d => d.TenantId   == tenantId
                     && d.Status     == TenantDomainStatus.Active
                     && d.DomainType == TenantDomainType.Subdomain)
            .ToListAsync(ct);

    public async Task<TenantDomain?> GetActiveSubdomainByLabelAsync(string label, CancellationToken ct = default)
    {
        var prefix = label + ".";
        return await _db.Domains
            .Where(d => d.Status     == TenantDomainStatus.Active
                     && d.DomainType == TenantDomainType.Subdomain
                     && (d.Host == label || d.Host.StartsWith(prefix)))
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(TenantDomain domain, CancellationToken ct = default)
    {
        await _db.Domains.AddAsync(domain, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TenantDomain domain, CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateRangeAsync(IEnumerable<TenantDomain> domains, CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }
}
