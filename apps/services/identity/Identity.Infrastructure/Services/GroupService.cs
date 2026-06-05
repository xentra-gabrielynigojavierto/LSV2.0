using System.Text.Json;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class GroupService : IGroupService
{
    private readonly IdentityDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<GroupService> _logger;

    public GroupService(IdentityDbContext db, IAuditPublisher audit, ILogger<GroupService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<AccessGroup> CreateAsync(
        Guid tenantId, string name, string? description, GroupScopeType scopeType,
        string? productCode, Guid? organizationId, Guid? actorUserId = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var tenant = await _db.Tenants.AnyAsync(t => t.Id == tenantId, ct);
        if (!tenant)
            throw new InvalidOperationException($"Tenant {tenantId} not found.");

        var trimmedName = name.Trim();
        var nameExists = await _db.AccessGroups
            .AnyAsync(g => g.TenantId == tenantId && g.Name == trimmedName, ct);
        if (nameExists)
            throw new InvalidOperationException($"A group named '{trimmedName}' already exists in this tenant.");

        if (scopeType == GroupScopeType.Product)
        {
            var code = productCode?.ToUpperInvariant().Trim();
            var entitled = await _db.TenantProductEntitlements
                .AnyAsync(e => e.TenantId == tenantId && e.ProductCode == code! && e.Status == EntitlementStatus.Active, ct);
            if (!entitled)
                throw new InvalidOperationException($"Product '{code}' is not entitled to tenant {tenantId}.");
        }

        if (scopeType == GroupScopeType.Organization && organizationId.HasValue)
        {
            var orgValid = await _db.Organizations
                .AnyAsync(o => o.Id == organizationId.Value && o.TenantId == tenantId && o.IsActive, ct);
            if (!orgValid)
                throw new InvalidOperationException($"Organization {organizationId} not found or does not belong to tenant {tenantId}.");
        }

        var group = AccessGroup.Create(tenantId, name, description, scopeType, productCode, organizationId, actorUserId);
        _db.AccessGroups.Add(group);
        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.group.created", "Created",
            $"Group '{group.Name}' created in tenant {tenantId}.",
            tenantId, actorUserId,
            "AccessGroup", group.Id.ToString(),
            after: JsonSerializer.Serialize(new { group.Name, group.ScopeType, group.ProductCode, group.OrganizationId }));

        return group;
    }

    public async Task<AccessGroup> UpdateAsync(
        Guid tenantId, Guid groupId, string name, string? description,
        Guid? actorUserId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (groupId == Guid.Empty)
            throw new ArgumentException("GroupId is required.", nameof(groupId));

        var group = await _db.AccessGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Group {groupId} not found in tenant {tenantId}.");

        var trimmedName = name.Trim();
        var nameConflict = await _db.AccessGroups
            .AnyAsync(g => g.TenantId == tenantId && g.Name == trimmedName && g.Id != groupId, ct);
        if (nameConflict)
            throw new InvalidOperationException($"A group named '{trimmedName}' already exists in this tenant.");

        var before = JsonSerializer.Serialize(new { group.Name, group.Description });
        group.Update(name, description, actorUserId);
        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.group.updated", "Updated",
            $"Group '{group.Name}' updated in tenant {tenantId}.",
            tenantId, actorUserId,
            "AccessGroup", group.Id.ToString(),
            before: before,
            after: JsonSerializer.Serialize(new { group.Name, group.Description }));

        return group;
    }

    public async Task<bool> ArchiveAsync(Guid tenantId, Guid groupId, Guid? actorUserId = null, CancellationToken ct = default)
    {
        var group = await _db.AccessGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId, ct);
        if (group == null) return false;

        var before = JsonSerializer.Serialize(new { group.Status });
        group.Archive(actorUserId);

        var affectedUserIds = await _db.AccessGroupMemberships
            .Where(m => m.GroupId == groupId && m.TenantId == tenantId && m.MembershipStatus == MembershipStatus.Active)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);
        var affectedUsers = await _db.Users
            .Where(u => affectedUserIds.Contains(u.Id) && u.TenantId == tenantId)
            .ToListAsync(ct);
        foreach (var u in affectedUsers)
            u.IncrementAccessVersion();

        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.group.archived", "Archived",
            $"Group '{group.Name}' archived in tenant {tenantId}. {affectedUsers.Count} user(s) affected.",
            tenantId, actorUserId,
            "AccessGroup", group.Id.ToString(),
            before: before,
            after: JsonSerializer.Serialize(new { group.Status }));

        return true;
    }

    public async Task<AccessGroup?> GetByIdAsync(Guid tenantId, Guid groupId, CancellationToken ct = default)
    {
        return await _db.AccessGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId, ct);
    }

    public async Task<List<AccessGroup>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.AccessGroups
            .Where(g => g.TenantId == tenantId)
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
    }
}
