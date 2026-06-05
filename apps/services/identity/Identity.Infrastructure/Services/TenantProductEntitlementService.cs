using System.Text.Json;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class TenantProductEntitlementService : ITenantProductEntitlementService
{
    private readonly IdentityDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<TenantProductEntitlementService> _logger;

    public TenantProductEntitlementService(
        IdentityDbContext db,
        IAuditPublisher audit,
        ILogger<TenantProductEntitlementService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<List<TenantProductEntitlement>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.TenantProductEntitlements
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.ProductCode)
            .ToListAsync(ct);
    }

    public async Task<TenantProductEntitlement?> GetByTenantAndCodeAsync(Guid tenantId, string productCode, CancellationToken ct = default)
    {
        var code = productCode.ToUpperInvariant().Trim();
        return await _db.TenantProductEntitlements
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ProductCode == code, ct);
    }

    public async Task<TenantProductEntitlement> UpsertAsync(Guid tenantId, string productCode, Guid? actorUserId = null, CancellationToken ct = default)
    {
        var code = productCode.ToUpperInvariant().Trim();

        var tenant = await _db.Tenants.AnyAsync(t => t.Id == tenantId, ct);
        if (!tenant)
            throw new InvalidOperationException($"Tenant {tenantId} not found.");

        var product = await _db.Products.AnyAsync(p => p.Code == code && p.IsActive, ct);
        if (!product)
            throw new InvalidOperationException($"Product '{code}' not found or inactive.");

        var existing = await _db.TenantProductEntitlements
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ProductCode == code, ct);

        if (existing != null)
        {
            var beforeJson = JsonSerializer.Serialize(new { existing.Status, existing.EnabledAtUtc, existing.DisabledAtUtc });
            existing.Enable(actorUserId);

            var reEnabledUserIds = await _db.UserProductAccessRecords
                .Where(a => a.TenantId == tenantId && a.ProductCode == code && a.AccessStatus == AccessStatus.Granted)
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync(ct);
            var reEnabledUsers = await _db.Users
                .Where(u => reEnabledUserIds.Contains(u.Id) && u.TenantId == tenantId)
                .ToListAsync(ct);
            foreach (var u in reEnabledUsers)
                u.IncrementAccessVersion();

            await _db.SaveChangesAsync(ct);

            _audit.Publish(
                "identity.tenant.product.enabled",
                "Enabled",
                $"Product {code} re-enabled for tenant {tenantId}.",
                tenantId, actorUserId,
                "TenantProductEntitlement", existing.Id.ToString(),
                before: beforeJson,
                after: JsonSerializer.Serialize(new { existing.Status, existing.EnabledAtUtc }));

            return existing;
        }

        var entitlement = TenantProductEntitlement.Create(tenantId, code, actorUserId);
        _db.TenantProductEntitlements.Add(entitlement);
        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.tenant.product.created",
            "Created",
            $"Product {code} entitled to tenant {tenantId}.",
            tenantId, actorUserId,
            "TenantProductEntitlement", entitlement.Id.ToString(),
            after: JsonSerializer.Serialize(new { entitlement.TenantId, entitlement.ProductCode, entitlement.Status }));

        return entitlement;
    }

    public async Task<bool> DisableAsync(Guid tenantId, string productCode, Guid? actorUserId = null, CancellationToken ct = default)
    {
        var code = productCode.ToUpperInvariant().Trim();
        var existing = await _db.TenantProductEntitlements
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ProductCode == code, ct);

        if (existing == null)
            return false;

        var beforeJson = JsonSerializer.Serialize(new { existing.Status, existing.EnabledAtUtc });
        existing.Disable(actorUserId);

        var affectedUserIds = await _db.UserProductAccessRecords
            .Where(a => a.TenantId == tenantId && a.ProductCode == code && a.AccessStatus == AccessStatus.Granted)
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(ct);
        var affectedUsers = await _db.Users
            .Where(u => affectedUserIds.Contains(u.Id) && u.TenantId == tenantId)
            .ToListAsync(ct);
        foreach (var u in affectedUsers)
            u.IncrementAccessVersion();

        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.tenant.product.disabled",
            "Disabled",
            $"Product {code} disabled for tenant {tenantId}.",
            tenantId, actorUserId,
            "TenantProductEntitlement", existing.Id.ToString(),
            before: beforeJson,
            after: JsonSerializer.Serialize(new { existing.Status, existing.DisabledAtUtc }));

        return true;
    }
}
