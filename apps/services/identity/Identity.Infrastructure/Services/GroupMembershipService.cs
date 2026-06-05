using System.Text.Json;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class GroupMembershipService : IGroupMembershipService
{
    private readonly IdentityDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly INotificationsCacheClient _notificationsCache;
    private readonly ILogger<GroupMembershipService> _logger;

    public GroupMembershipService(
        IdentityDbContext db,
        IAuditPublisher audit,
        INotificationsCacheClient notificationsCache,
        ILogger<GroupMembershipService> logger)
    {
        _db = db;
        _audit = audit;
        _notificationsCache = notificationsCache;
        _logger = logger;
    }

    public async Task<AccessGroupMembership> AddMemberAsync(
        Guid tenantId, Guid groupId, Guid userId,
        Guid? actorUserId = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (groupId == Guid.Empty) throw new ArgumentException("GroupId is required.", nameof(groupId));
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));

        var group = await _db.AccessGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Group {groupId} not found in tenant {tenantId}.");

        if (group.Status == GroupStatus.Archived)
            throw new InvalidOperationException("Cannot add members to an archived group.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}.");

        var existing = await _db.AccessGroupMemberships
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.GroupId == groupId && m.UserId == userId, ct);

        if (existing != null && existing.MembershipStatus == MembershipStatus.Active)
            throw new InvalidOperationException($"User {userId} is already an active member of group {groupId}.");

        if (existing != null)
        {
            _db.AccessGroupMemberships.Remove(existing);
        }

        // Capture the before-state when we are re-activating a previously removed membership.
        var beforeJson = existing != null
            ? JsonSerializer.Serialize(new { existing.MembershipStatus, existing.AddedAtUtc, existing.RemovedAtUtc })
            : null;

        var membership = AccessGroupMembership.Create(tenantId, groupId, userId, actorUserId);
        _db.AccessGroupMemberships.Add(membership);

        user.IncrementAccessVersion();

        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.group.member.added", "Added",
            $"User {userId} added to group '{group.Name}' in tenant {tenantId}.",
            tenantId, actorUserId,
            "AccessGroupMembership", membership.Id.ToString(),
            before: beforeJson,
            after: JsonSerializer.Serialize(new { membership.GroupId, membership.UserId, membership.MembershipStatus }));

        // Adding a user to a group changes their effective role membership
        // (groups grant roles via GroupRoleAssignment) — refresh notifications.
        _notificationsCache.InvalidateTenant(
            tenantId,
            eventType: "identity.group.member.added",
            reason:    $"user {userId} added to group {groupId}");

        return membership;
    }

    public async Task<bool> RemoveMemberAsync(
        Guid tenantId, Guid groupId, Guid userId,
        Guid? actorUserId = null, CancellationToken ct = default)
    {
        var membership = await _db.AccessGroupMemberships
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.GroupId == groupId && m.UserId == userId
                                     && m.MembershipStatus == MembershipStatus.Active, ct);
        if (membership == null) return false;

        // Capture before-state prior to mutation so the audit trail shows the full diff.
        var beforeJson = JsonSerializer.Serialize(new { membership.MembershipStatus, membership.AddedAtUtc });
        membership.Remove(actorUserId);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
        user?.IncrementAccessVersion();

        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.group.member.removed", "Removed",
            $"User {userId} removed from group {groupId} in tenant {tenantId}.",
            tenantId, actorUserId,
            "AccessGroupMembership", membership.Id.ToString(),
            before: beforeJson,
            after: JsonSerializer.Serialize(new { membership.GroupId, membership.UserId, membership.MembershipStatus, membership.RemovedAtUtc }));

        // Removing a user from a group strips group-granted roles — refresh
        // notifications so role-addressed alerts drop the user immediately.
        _notificationsCache.InvalidateTenant(
            tenantId,
            eventType: "identity.group.member.removed",
            reason:    $"user {userId} removed from group {groupId}");

        return true;
    }

    public async Task<List<AccessGroupMembership>> ListMembersAsync(Guid tenantId, Guid groupId, CancellationToken ct = default)
    {
        return await _db.AccessGroupMemberships
            .Where(m => m.TenantId == tenantId && m.GroupId == groupId)
            .OrderBy(m => m.AddedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<AccessGroupMembership>> ListGroupsForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        return await _db.AccessGroupMemberships
            .Where(m => m.TenantId == tenantId && m.UserId == userId)
            .OrderBy(m => m.AddedAtUtc)
            .ToListAsync(ct);
    }
}
