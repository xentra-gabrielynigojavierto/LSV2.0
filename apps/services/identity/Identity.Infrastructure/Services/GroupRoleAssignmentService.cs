using System.Text.Json;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class GroupRoleAssignmentService : IGroupRoleAssignmentService
{
    private readonly IdentityDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly INotificationsCacheClient _notificationsCache;
    private readonly ILogger<GroupRoleAssignmentService> _logger;

    public GroupRoleAssignmentService(
        IdentityDbContext db,
        IAuditPublisher audit,
        INotificationsCacheClient notificationsCache,
        ILogger<GroupRoleAssignmentService> logger)
    {
        _db = db;
        _audit = audit;
        _notificationsCache = notificationsCache;
        _logger = logger;
    }

    public async Task<GroupRoleAssignment> AssignAsync(
        Guid tenantId, Guid groupId, string roleCode,
        string? productCode = null, Guid? organizationId = null,
        Guid? actorUserId = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (groupId == Guid.Empty) throw new ArgumentException("GroupId is required.", nameof(groupId));
        if (string.IsNullOrWhiteSpace(roleCode)) throw new ArgumentException("RoleCode is required.", nameof(roleCode));

        var group = await _db.AccessGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Group {groupId} not found in tenant {tenantId}.");

        if (group.Status == GroupStatus.Archived)
            throw new InvalidOperationException("Cannot assign roles to an archived group.");

        var code = productCode?.ToUpperInvariant().Trim();
        if (code != null)
        {
            var entitled = await _db.TenantProductEntitlements
                .AnyAsync(e => e.TenantId == tenantId && e.ProductCode == code && e.Status == EntitlementStatus.Active, ct);
            if (!entitled)
                throw new InvalidOperationException($"Product '{code}' is not entitled to tenant {tenantId}.");
        }

        if (organizationId.HasValue)
        {
            var orgValid = await _db.Organizations
                .AnyAsync(o => o.Id == organizationId.Value && o.TenantId == tenantId && o.IsActive, ct);
            if (!orgValid)
                throw new InvalidOperationException($"Organization {organizationId} not found or does not belong to tenant {tenantId}.");
        }

        var duplicate = await _db.GroupRoleAssignments
            .AnyAsync(a => a.TenantId == tenantId && a.GroupId == groupId
                          && a.RoleCode == roleCode.Trim()
                          && a.ProductCode == code
                          && a.OrganizationId == organizationId
                          && a.AssignmentStatus == AssignmentStatus.Active, ct);
        if (duplicate)
            throw new InvalidOperationException($"Active role assignment '{roleCode}' already exists on group {groupId}.");

        var assignment = GroupRoleAssignment.Create(tenantId, groupId, roleCode, code, organizationId, actorUserId);
        _db.GroupRoleAssignments.Add(assignment);

        await IncrementMemberVersionsAsync(tenantId, groupId, ct);
        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.group.role.assigned", "Assigned",
            $"Role '{roleCode}' assigned to group {groupId} in tenant {tenantId}.",
            tenantId, actorUserId,
            "GroupRoleAssignment", assignment.Id.ToString(),
            after: JsonSerializer.Serialize(new { assignment.GroupId, assignment.RoleCode, assignment.ProductCode, assignment.AssignmentStatus }));

        // Group role binding changes the effective membership of every user
        // in the group — refresh notifications' cache for this tenant.
        _notificationsCache.InvalidateTenant(
            tenantId,
            eventType: "identity.group.role.assigned",
            reason:    $"role {roleCode} assigned to group {groupId}");

        return assignment;
    }

    public async Task<bool> RemoveAsync(
        Guid tenantId, Guid groupId, Guid assignmentId,
        Guid? actorUserId = null, CancellationToken ct = default)
    {
        var existing = await _db.GroupRoleAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.TenantId == tenantId && a.GroupId == groupId, ct);
        if (existing == null) return false;

        var before = JsonSerializer.Serialize(new { existing.AssignmentStatus, existing.AssignedAtUtc });
        existing.Remove(actorUserId);

        await IncrementMemberVersionsAsync(tenantId, groupId, ct);
        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.group.role.removed", "Removed",
            $"Role '{existing.RoleCode}' removed from group {groupId} in tenant {tenantId}.",
            tenantId, actorUserId,
            "GroupRoleAssignment", existing.Id.ToString(),
            before: before,
            after: JsonSerializer.Serialize(new { existing.AssignmentStatus, existing.RemovedAtUtc }));

        // Group role binding removed — refresh notifications' cache so all
        // group members drop out of role-addressed fan-out immediately.
        _notificationsCache.InvalidateTenant(
            tenantId,
            eventType: "identity.group.role.removed",
            reason:    $"role {existing.RoleCode} removed from group {groupId}");

        return true;
    }

    public async Task<List<GroupRoleAssignment>> ListAsync(Guid tenantId, Guid groupId, CancellationToken ct = default)
    {
        return await _db.GroupRoleAssignments
            .Where(a => a.TenantId == tenantId && a.GroupId == groupId)
            .OrderBy(a => a.RoleCode)
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
