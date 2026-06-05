namespace Identity.Application.Interfaces;

/// <summary>
/// LS-ID-TNT-011 — Backend permission-check contract for future product API enforcement.
///
/// This service provides the standard query surface for checking and inspecting effective
/// permissions for a given user within a tenant context.  It delegates resolution to
/// <see cref="IEffectiveAccessService"/> which aggregates product permissions (via
/// ProductRole → RolePermissionMapping) and tenant permissions (via system Role →
/// RolePermissionAssignment).
///
/// Usage pattern for downstream services:
/// <code>
/// // Check a specific product permission:
/// if (!await _perms.HasProductPermissionAsync(userId, tenantId, ProductCodes.SynqFund, PermissionCodes.ApplicationApprove, ct))
///     throw new ForbiddenException(PermissionCodes.ApplicationApprove);
///
/// // Check a tenant-level permission:
/// if (!await _perms.HasTenantPermissionAsync(userId, tenantId, PermissionCodes.TenantUsersManage, ct))
///     throw new ForbiddenException(PermissionCodes.TenantUsersManage);
/// </code>
///
/// PlatformAdmin bypass is the responsibility of the caller (via ICurrentRequestContext.IsPlatformAdmin).
/// TenantAdmin users automatically receive all product and tenant permissions through the
/// effective-access resolution layer — no special bypass is required here.
///
/// UI ownership:
/// - Tenant Portal (future LS-ID-TNT-012): surfaces tenant role → tenant permission visibility.
/// - Control Center (future LS-ID-TNT-013): manages product permission catalog + role governance.
/// </summary>
public interface IEffectivePermissionService
{
    /// <summary>
    /// Returns true if the user holds the given product-scoped permission code within the tenant.
    /// Checks the union of all effective product permissions resolved from product roles.
    /// </summary>
    Task<bool> HasProductPermissionAsync(
        Guid userId,
        Guid tenantId,
        string productCode,
        string permissionCode,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true if the user holds the given tenant-level permission code within the tenant.
    /// Checks the union of all tenant permissions resolved from system roles (TenantAdmin, StandardUser, etc.).
    /// </summary>
    Task<bool> HasTenantPermissionAsync(
        Guid userId,
        Guid tenantId,
        string permissionCode,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full set of effective permissions for the user, separated into tenant
    /// permissions and per-product permission sets, plus the combined flat list for JWT use.
    /// </summary>
    Task<EffectivePermissionsDto> GetEffectivePermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken ct = default);
}

/// <summary>
/// LS-ID-TNT-011 — Output of <see cref="IEffectivePermissionService.GetEffectivePermissionsAsync"/>.
/// </summary>
/// <param name="TenantPermissions">
/// Permissions resolved from system roles via RolePermissionAssignment (TENANT.* codes).
/// </param>
/// <param name="ProductPermissions">
/// Permissions resolved from product roles via RolePermissionMapping, keyed by product code.
/// </param>
/// <param name="AllPermissions">
/// Union of tenant and product permissions — mirrors the JWT <c>permissions</c> claim.
/// </param>
public record EffectivePermissionsDto(
    List<string> TenantPermissions,
    Dictionary<string, List<string>> ProductPermissions,
    List<string> AllPermissions);
