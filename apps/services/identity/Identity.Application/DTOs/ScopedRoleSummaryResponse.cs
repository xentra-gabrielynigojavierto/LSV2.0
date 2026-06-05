namespace Identity.Application.DTOs;

/// <summary>
/// A single active scoped role assignment as returned by the admin API.
/// </summary>
public record ScopedRoleEntry(
    Guid   AssignmentId,
    string RoleName,
    string ScopeType,
    Guid?  OrganizationId,
    Guid?  ProductId,
    Guid?  OrganizationRelationshipId,
    Guid?  TenantId);

/// <summary>
/// Full scoped-role summary for a single user.
/// Returned by GET /api/admin/users/{id}/scoped-roles.
/// </summary>
public record ScopedRoleSummaryResponse(
    Guid                           UserId,
    int                            TotalActive,
    IReadOnlyList<ScopedRoleEntry> Assignments);
