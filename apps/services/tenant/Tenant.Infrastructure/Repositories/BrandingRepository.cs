using Microsoft.EntityFrameworkCore;
using Tenant.Application.Interfaces;
using Tenant.Domain;
using Tenant.Infrastructure.Data;

namespace Tenant.Infrastructure.Repositories;

public class BrandingRepository : IBrandingRepository
{
    private readonly TenantDbContext _db;

    public BrandingRepository(TenantDbContext db) => _db = db;

    public Task<TenantBranding?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Brandings.FirstOrDefaultAsync(b => b.TenantId == tenantId, ct);

    public async Task AddAsync(TenantBranding branding, CancellationToken ct = default)
    {
        await _db.Brandings.AddAsync(branding, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TenantBranding branding, CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }
}
