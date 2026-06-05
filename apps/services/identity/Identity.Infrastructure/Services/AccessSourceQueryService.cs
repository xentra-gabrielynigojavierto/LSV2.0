using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public class AccessSourceQueryService : IAccessSourceQueryService
{
    private readonly IdentityDbContext _db;

    public AccessSourceQueryService(IdentityDbContext db)
    {
        _db = db;
    }

    public async Task<AccessSourceSnapshot> GetSnapshotAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var tenantProducts = await _db.TenantProductEntitlements
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.ProductCode)
            .ToListAsync(ct);

        var userProducts = await _db.UserProductAccessRecords
            .Where(a => a.TenantId == tenantId && a.UserId == userId)
            .OrderBy(a => a.ProductCode)
            .ToListAsync(ct);

        var userRoles = await _db.UserRoleAssignments
            .Where(a => a.TenantId == tenantId && a.UserId == userId)
            .OrderBy(a => a.RoleCode)
            .ToListAsync(ct);

        return new AccessSourceSnapshot(tenantProducts, userProducts, userRoles);
    }
}
