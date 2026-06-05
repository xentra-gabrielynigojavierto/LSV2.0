using System.Text.Json;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class UserProductAccessService : IUserProductAccessService
{
    private readonly IdentityDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<UserProductAccessService> _logger;

    public UserProductAccessService(
        IdentityDbContext db,
        IAuditPublisher audit,
        ILogger<UserProductAccessService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<List<UserProductAccess>> GetByTenantUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        return await _db.UserProductAccessRecords
            .Where(a => a.TenantId == tenantId && a.UserId == userId)
            .OrderBy(a => a.ProductCode)
            .ToListAsync(ct);
    }

    public async Task<UserProductAccess?> GetByTenantUserAndCodeAsync(Guid tenantId, Guid userId, string productCode, CancellationToken ct = default)
    {
        var code = productCode.ToUpperInvariant().Trim();
        return await _db.UserProductAccessRecords
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.UserId == userId && a.ProductCode == code, ct);
    }

    public async Task<UserProductAccess> GrantAsync(Guid tenantId, Guid userId, string productCode, Guid? actorUserId = null, CancellationToken ct = default)
    {
        var code = productCode.ToUpperInvariant().Trim();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
        if (user == null)
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}.");

        // Auto-entitle the tenant to the product if not already active.
        // A TenantAdmin granting a product to a user implicitly means the tenant
        // should have access; requiring a separate entitlement step is an internal
        // concern that should not surface as an error to the portal user.
        var entitlement = await _db.TenantProductEntitlements
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ProductCode == code, ct);

        if (entitlement is null)
        {
            entitlement = TenantProductEntitlement.Create(tenantId, code, createdByUserId: actorUserId);
            _db.TenantProductEntitlements.Add(entitlement);
        }
        else if (entitlement.Status != EntitlementStatus.Active)
        {
            entitlement.Enable(actorUserId);
        }

        var existing = await _db.UserProductAccessRecords
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.UserId == userId && a.ProductCode == code, ct);

        if (existing != null)
        {
            var beforeJson = JsonSerializer.Serialize(new { existing.AccessStatus, existing.GrantedAtUtc, existing.RevokedAtUtc });
            existing.Grant(actorUserId);

            user.IncrementAccessVersion();

            await _db.SaveChangesAsync(ct);

            _audit.Publish(
                "identity.user.product.granted",
                "Granted",
                $"Product {code} re-granted to user {userId} in tenant {tenantId}.",
                tenantId, actorUserId,
                "UserProductAccess", existing.Id.ToString(),
                before: beforeJson,
                after: JsonSerializer.Serialize(new { existing.AccessStatus, existing.GrantedAtUtc }));

            return existing;
        }

        var access = UserProductAccess.Create(tenantId, userId, code, createdByUserId: actorUserId);
        _db.UserProductAccessRecords.Add(access);

        user.IncrementAccessVersion();

        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.user.product.granted",
            "Created",
            $"Product {code} granted to user {userId} in tenant {tenantId}.",
            tenantId, actorUserId,
            "UserProductAccess", access.Id.ToString(),
            after: JsonSerializer.Serialize(new { access.TenantId, access.UserId, access.ProductCode, access.AccessStatus }));

        return access;
    }

    public async Task<bool> RevokeAsync(Guid tenantId, Guid userId, string productCode, Guid? actorUserId = null, CancellationToken ct = default)
    {
        var code = productCode.ToUpperInvariant().Trim();
        var existing = await _db.UserProductAccessRecords
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.UserId == userId && a.ProductCode == code, ct);

        if (existing == null)
            return false;

        var beforeJson = JsonSerializer.Serialize(new { existing.AccessStatus, existing.GrantedAtUtc });
        existing.Revoke(actorUserId);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
        user?.IncrementAccessVersion();

        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.user.product.revoked",
            "Revoked",
            $"Product {code} revoked from user {userId} in tenant {tenantId}.",
            tenantId, actorUserId,
            "UserProductAccess", existing.Id.ToString(),
            before: beforeJson,
            after: JsonSerializer.Serialize(new { existing.AccessStatus, existing.RevokedAtUtc }));

        return true;
    }
}
