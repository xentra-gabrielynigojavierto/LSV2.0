using Identity.Application.Interfaces;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

/// <summary>
/// BLK-ID-02 — Implementation of the formal membership API.
///
/// Both methods are idempotent and safe to call from the provisioning endpoint
/// or any other orchestrator without risk of duplicate DB records.
/// </summary>
public sealed class UserMembershipService : IUserMembershipService
{
    private readonly IdentityDbContext                 _db;
    private readonly ILogger<UserMembershipService>    _logger;

    public UserMembershipService(
        IdentityDbContext              db,
        ILogger<UserMembershipService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── AssignTenantAsync ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AssignTenantResult> AssignTenantAsync(
        AssignTenantCommand cmd,
        CancellationToken   ct = default)
    {
        if (cmd.UserId   == Guid.Empty) throw new ArgumentException("UserId is required.",   nameof(cmd));
        if (cmd.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(cmd));

        // Validate user exists.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, ct);
        if (user is null)
            throw new InvalidOperationException($"[UserMembership] User {cmd.UserId} not found in Identity.");

        // Check if already in the target tenant.
        var alreadyInTenant = user.TenantId == cmd.TenantId;

        if (alreadyInTenant)
        {
            _logger.LogInformation(
                "[UserMembership] AssignTenant: user {UserId} is already in tenant {TenantId}. " +
                "Skipping TenantId update — proceeding with role assignment.",
                cmd.UserId, cmd.TenantId);
        }
        else
        {
            // Update User.TenantId via ExecuteUpdateAsync (SQL-only; bypasses EF change tracking).
            var updated = await _db.Users
                .Where(u => u.Id == cmd.UserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.TenantId, cmd.TenantId), ct);

            if (updated == 0)
                throw new InvalidOperationException(
                    $"[UserMembership] AssignTenant: failed to update TenantId for user {cmd.UserId}.");

            _logger.LogInformation(
                "[UserMembership] AssignTenant: user {UserId} moved to tenant {TenantId}.",
                cmd.UserId, cmd.TenantId);
        }

        // Assign roles (idempotent — duplicates are logged and skipped).
        var rolesResult = new AssignRolesResult(cmd.UserId, cmd.TenantId, [], []);
        if (cmd.Roles is { Count: > 0 })
        {
            rolesResult = await AssignRolesAsync(
                new AssignRolesCommand(cmd.UserId, cmd.TenantId, cmd.Roles), ct);
        }

        return new AssignTenantResult(
            UserId:                    cmd.UserId,
            TenantId:                  cmd.TenantId,
            AlreadyInTenant:           alreadyInTenant,
            AssignedRoleAssignmentIds: []);  // role assignment IDs available via AssignRolesAsync if needed
    }

    // ── AssignRolesAsync ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<AssignRolesResult> AssignRolesAsync(
        AssignRolesCommand cmd,
        CancellationToken  ct = default)
    {
        if (cmd.UserId   == Guid.Empty) throw new ArgumentException("UserId is required.",   nameof(cmd));
        if (cmd.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(cmd));

        var assigned   = new List<string>();
        var duplicates = new List<string>();

        if (cmd.Roles is not { Count: > 0 })
            return new AssignRolesResult(cmd.UserId, cmd.TenantId, assigned, duplicates);

        // Validate user exists.
        var userExists = await _db.Users.AnyAsync(u => u.Id == cmd.UserId, ct);
        if (!userExists)
            throw new InvalidOperationException($"[UserMembership] User {cmd.UserId} not found in Identity.");

        // Load all requested roles in one query.
        var roleNames = cmd.Roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roles = await _db.Roles
            .Where(r => roleNames.Contains(r.Name))
            .ToListAsync(ct);

        var missingRoles = roleNames
            .Where(n => !roles.Any(r => string.Equals(r.Name, n, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingRoles.Count > 0)
        {
            _logger.LogWarning(
                "[UserMembership] AssignRoles: role(s) not found in Identity.Roles — skipping: [{Missing}]. " +
                "Ensure roles are seeded before calling AssignRoles.",
                string.Join(", ", missingRoles));
        }

        // Load existing active ScopedRoleAssignments for this user/tenant.
        var existingAssignments = await _db.ScopedRoleAssignments
            .Where(s => s.UserId   == cmd.UserId
                     && s.TenantId == cmd.TenantId
                     && s.IsActive)
            .Select(s => s.RoleId)
            .ToListAsync(ct);

        var existingRoleIdSet = existingAssignments.ToHashSet();

        var newAssignments = new List<ScopedRoleAssignment>();

        foreach (var role in roles)
        {
            if (existingRoleIdSet.Contains(role.Id))
            {
                _logger.LogInformation(
                    "[UserMembership] AssignRoles: user {UserId} already has role '{Role}' in tenant {TenantId}. " +
                    "Skipping duplicate assignment.",
                    cmd.UserId, role.Name, cmd.TenantId);
                duplicates.Add(role.Name);
                continue;
            }

            var sra = ScopedRoleAssignment.Create(
                userId:    cmd.UserId,
                roleId:    role.Id,
                scopeType: ScopedRoleAssignment.ScopeTypes.Global,
                tenantId:  cmd.TenantId);

            newAssignments.Add(sra);
            assigned.Add(role.Name);

            _logger.LogInformation(
                "[UserMembership] AssignRoles: assigning role '{Role}' (Id={RoleId}) to user {UserId} in tenant {TenantId}.",
                role.Name, role.Id, cmd.UserId, cmd.TenantId);
        }

        if (newAssignments.Count > 0)
        {
            _db.ScopedRoleAssignments.AddRange(newAssignments);
            await _db.SaveChangesAsync(ct);
        }

        return new AssignRolesResult(
            UserId:             cmd.UserId,
            TenantId:           cmd.TenantId,
            AssignedRoles:      assigned,
            SkippedDuplicates:  duplicates);
    }
}
