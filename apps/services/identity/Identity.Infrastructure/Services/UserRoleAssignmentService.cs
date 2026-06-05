using System.Text.Json;
using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class UserRoleAssignmentService : IUserRoleAssignmentService
{
    private readonly IdentityDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly INotificationsCacheClient _notificationsCache;
    private readonly ILogger<UserRoleAssignmentService> _logger;

    public UserRoleAssignmentService(
        IdentityDbContext db,
        IAuditPublisher audit,
        INotificationsCacheClient notificationsCache,
        ILogger<UserRoleAssignmentService> logger)
    {
        _db = db;
        _audit = audit;
        _notificationsCache = notificationsCache;
        _logger = logger;
    }

    public async Task<List<UserRoleAssignment>> GetByTenantUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        return await _db.UserRoleAssignments
            .Where(a => a.TenantId == tenantId && a.UserId == userId)
            .OrderBy(a => a.RoleCode)
            .ToListAsync(ct);
    }

    public async Task<UserRoleAssignment> AssignAsync(
        Guid tenantId, Guid userId, string roleCode,
        string? productCode = null, Guid? organizationId = null,
        Guid? actorUserId = null, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
        if (user == null)
            throw new InvalidOperationException($"User {userId} not found in tenant {tenantId}.");

        var code = productCode?.ToUpperInvariant().Trim();
        if (code != null)
        {
            var productEntitled = await _db.TenantProductEntitlements
                .AnyAsync(e => e.TenantId == tenantId && e.ProductCode == code && e.Status == EntitlementStatus.Active, ct);
            if (!productEntitled)
                throw new InvalidOperationException($"Product '{code}' is not entitled to tenant {tenantId}.");
        }

        if (organizationId.HasValue)
        {
            var orgBelongsToTenant = await _db.Organizations
                .AnyAsync(o => o.Id == organizationId.Value && o.TenantId == tenantId && o.IsActive, ct);
            if (!orgBelongsToTenant)
                throw new InvalidOperationException($"Organization {organizationId} not found or does not belong to tenant {tenantId}.");
        }

        var duplicate = await _db.UserRoleAssignments
            .AnyAsync(a => a.TenantId == tenantId && a.UserId == userId
                && a.RoleCode == roleCode.Trim()
                && a.ProductCode == code
                && a.OrganizationId == organizationId
                && a.AssignmentStatus == AssignmentStatus.Active, ct);
        if (duplicate)
            throw new InvalidOperationException($"Active assignment already exists for role '{roleCode}' on user {userId}.");

        var assignment = UserRoleAssignment.Create(tenantId, userId, roleCode, code, organizationId, actorUserId);
        _db.UserRoleAssignments.Add(assignment);

        user.IncrementAccessVersion();

        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.user.role.assigned",
            "Assigned",
            $"Role '{roleCode}' assigned to user {userId} in tenant {tenantId}.",
            tenantId, actorUserId,
            "UserRoleAssignment", assignment.Id.ToString(),
            after: JsonSerializer.Serialize(new { assignment.TenantId, assignment.UserId, assignment.RoleCode, assignment.ProductCode, assignment.AssignmentStatus }));

        // Membership for this tenant changed — refresh notifications' cache.
        _notificationsCache.InvalidateTenant(
            tenantId,
            eventType: "identity.user.role.assigned",
            reason:    $"role {roleCode} assigned to user {userId}");

        return assignment;
    }

    public async Task<bool> RemoveAsync(Guid tenantId, Guid userId, Guid assignmentId, Guid? actorUserId = null, CancellationToken ct = default)
    {
        var existing = await _db.UserRoleAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.TenantId == tenantId && a.UserId == userId, ct);

        if (existing == null)
            return false;

        var beforeJson = JsonSerializer.Serialize(new { existing.AssignmentStatus, existing.AssignedAtUtc });
        existing.Remove(actorUserId);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);
        user?.IncrementAccessVersion();

        await _db.SaveChangesAsync(ct);

        _audit.Publish(
            "identity.user.role.removed",
            "Removed",
            $"Role '{existing.RoleCode}' removed from user {existing.UserId} in tenant {tenantId}.",
            tenantId, actorUserId,
            "UserRoleAssignment", existing.Id.ToString(),
            before: beforeJson,
            after: JsonSerializer.Serialize(new { existing.AssignmentStatus, existing.RemovedAtUtc }));

        // Membership for this tenant changed — refresh notifications' cache.
        _notificationsCache.InvalidateTenant(
            tenantId,
            eventType: "identity.user.role.removed",
            reason:    $"role {existing.RoleCode} removed from user {existing.UserId}");

        return true;
    }
}
