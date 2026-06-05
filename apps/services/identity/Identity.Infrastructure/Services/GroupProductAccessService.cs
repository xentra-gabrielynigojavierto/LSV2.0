using System.Text.Json;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class GroupProductAccessService : IGroupProductAccessService
{
    private readonly IdentityDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<GroupProductAccessService> _logger;

    public GroupProductAccessService(IdentityDbContext db, IAuditPublisher audit, ILogger<GroupProductAccessService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<GroupProductAccess> GrantAsync(
        Guid tenantId, Guid groupId, string productCode,
        Guid? actorUserId = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (groupId == Guid.Empty) throw new ArgumentException("GroupId is required.", nameof(groupId));
        if (string.IsNullOrWhiteSpace(productCode)) throw new ArgumentException("ProductCode is required.", nameof(productCode));

        var code = productCode.ToUpperInvariant().Trim();

        var group = await _db.AccessGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Group {groupId} not found in tenant {tenantId}.");

        if (group.Status == GroupStatus.Archived)
            throw new InvalidOperationException("Cannot grant product access to an archived group.");

        var entitled = await _db.TenantProductEntitlements
            .AnyAsync(e => e.TenantId == tenantId && e.ProductCode == code && e.Status == EntitlementStatus.Active, ct);
        if (!entitled)
            throw new InvalidOperationException($"Product '{code}' is not entitled to tenant {tenantId}.");

        var existing = await _db.GroupProductAccessRecords
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.GroupId == groupId && a.ProductCode == code, ct);

        if (existing != null)
        {
            var before = JsonSerializer.Serialize(new { existing.AccessStatus, existing.GrantedAtUtc, existing.RevokedAtUtc });
            existing.Grant(actorUserId);

            await IncrementMemberVersionsAsync(tenantId, groupId, ct);
            await _db.SaveChangesAsync(ct);

            _audit.Publish(
                "identity.group.product.granted", "Granted",
                $"Product {code} re-granted to group {groupId} in tenant {tenantId}.",
                tenantId, actorUserId,
                "GroupProductAccess", existing.Id.ToString(),
                before: before,
                after: JsonSerializer.Serialize(new { existing.AccessStatus, existing.GrantedAtUtc }));

            return existing;
        }

        var access = GroupProductAccess.Create(tenantId, groupId, code, actorUserId);
        _db.GroupProductAccessRecords.Add(access);

        await IncrementMemberVersionsAsync(tenantId, groupId, ct);
        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.group.product.granted", "Created",
            $"Product {code} granted to group {groupId} in tenant {tenantId}.",
            tenantId, actorUserId,
            "GroupProductAccess", access.Id.ToString(),
            after: JsonSerializer.Serialize(new { access.GroupId, access.ProductCode, access.AccessStatus }));

        return access;
    }

    public async Task<bool> RevokeAsync(
        Guid tenantId, Guid groupId, string productCode,
        Guid? actorUserId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(productCode)) throw new ArgumentException("ProductCode is required.", nameof(productCode));

        var code = productCode.ToUpperInvariant().Trim();
        var existing = await _db.GroupProductAccessRecords
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.GroupId == groupId && a.ProductCode == code, ct);
        if (existing == null) return false;

        var before = JsonSerializer.Serialize(new { existing.AccessStatus, existing.GrantedAtUtc });
        existing.Revoke(actorUserId);

        await IncrementMemberVersionsAsync(tenantId, groupId, ct);
        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.group.product.revoked", "Revoked",
            $"Product {code} revoked from group {groupId} in tenant {tenantId}.",
            tenantId, actorUserId,
            "GroupProductAccess", existing.Id.ToString(),
            before: before,
            after: JsonSerializer.Serialize(new { existing.AccessStatus, existing.RevokedAtUtc }));

        return true;
    }

    public async Task<List<GroupProductAccess>> ListAsync(Guid tenantId, Guid groupId, CancellationToken ct = default)
    {
        return await _db.GroupProductAccessRecords
            .Where(a => a.TenantId == tenantId && a.GroupId == groupId)
            .OrderBy(a => a.ProductCode)
            .ToListAsync(ct);
    }

    private async Task IncrementMemberVersionsAsync(Guid tenantId, Guid groupId, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var memberUserIds = await _db.AccessGroupMemberships
            .Where(m => m.GroupId == groupId && m.TenantId == tenantId && m.MembershipStatus == MembershipStatus.Active)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);

        if (memberUserIds.Count == 0) return;

        var updated = await _db.Users
            .Where(u => memberUserIds.Contains(u.Id) && u.TenantId == tenantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.AccessVersion, u => u.AccessVersion + 1)
                .SetProperty(u => u.UpdatedAtUtc, DateTime.UtcNow), ct);

        sw.Stop();
        _logger.LogInformation(
            "Batch AccessVersion increment for group {GroupId}: {UpdatedCount}/{MemberCount} users in {ElapsedMs}ms.",
            groupId, updated, memberUserIds.Count, sw.ElapsedMilliseconds);
    }
}
