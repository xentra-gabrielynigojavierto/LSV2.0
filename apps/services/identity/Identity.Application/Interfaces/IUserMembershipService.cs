namespace Identity.Application.Interfaces;

/// <summary>
/// BLK-ID-02 — Formal membership API for Identity service.
///
/// Provides explicit, idempotent operations for:
///   - Assigning a user to a tenant (updates User.TenantId + grants roles)
///   - Assigning roles to a user (ScopedRoleAssignment, GLOBAL scope)
///
/// Both operations are safe to call multiple times — duplicate assignments
/// are detected, logged, and skipped without error.
///
/// The provisioning endpoint (POST /api/internal/tenant-provisioning/provision)
/// delegates all tenant assignment and role assignment logic to this service.
/// </summary>
public interface IUserMembershipService
{
    /// <summary>
    /// Assigns an existing Identity user to the specified tenant.
    /// Updates User.TenantId and grants any provided roles via AssignRolesAsync.
    /// Idempotent — safe to call if the user is already in the target tenant.
    /// </summary>
    Task<AssignTenantResult> AssignTenantAsync(AssignTenantCommand cmd, CancellationToken ct = default);

    /// <summary>
    /// Assigns one or more named roles to a user within a tenant.
    /// Uses ScopedRoleAssignment (GLOBAL scope) — the Phase G authoritative role model.
    /// Idempotent — already-active assignments are logged and skipped.
    /// </summary>
    Task<AssignRolesResult> AssignRolesAsync(AssignRolesCommand cmd, CancellationToken ct = default);
}

/// <summary>
/// Command for assigning a user to a tenant + optional roles.
/// </summary>
/// <param name="UserId">Identity user to assign.</param>
/// <param name="TenantId">Target tenant.</param>
/// <param name="Roles">Role names to assign (e.g. "TenantAdmin"). May be empty.</param>
public record AssignTenantCommand(
    Guid         UserId,
    Guid         TenantId,
    List<string> Roles);

/// <summary>
/// Result of AssignTenantAsync.
/// </summary>
public record AssignTenantResult(
    Guid         UserId,
    Guid         TenantId,
    bool         AlreadyInTenant,
    List<Guid>   AssignedRoleAssignmentIds);

/// <summary>
/// Command for assigning roles to a user within a tenant.
/// </summary>
/// <param name="UserId">User to assign roles to.</param>
/// <param name="TenantId">Tenant scope for the ScopedRoleAssignment.</param>
/// <param name="Roles">Role names to assign (e.g. "TenantAdmin").</param>
public record AssignRolesCommand(
    Guid         UserId,
    Guid         TenantId,
    List<string> Roles);

/// <summary>
/// Result of AssignRolesAsync.
/// </summary>
public record AssignRolesResult(
    Guid         UserId,
    Guid         TenantId,
    List<string> AssignedRoles,
    List<string> SkippedDuplicates);
