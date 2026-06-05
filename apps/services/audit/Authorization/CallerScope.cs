namespace PlatformAuditEventService.Authorization;

/// <summary>
/// Conceptual security scope for a query API caller.
///
/// Scopes are ordered from least-privileged (0) to most-privileged (6).
/// The numeric ordering is used when selecting the most permissive of two
/// candidate scopes during resolution.
///
/// Enforcement mapping:
/// <list type="table">
///   <listheader><term>Scope</term><term>Cross-tenant</term><term>Own-tenant</term><term>Visibility floor</term></listheader>
///   <item><term><see cref="Unknown"/></term><term>Denied</term><term>Denied</term><term>—</term></item>
///   <item><term><see cref="UserSelf"/></term><term>Denied</term><term>Own records only</term><term>User</term></item>
///   <item><term><see cref="TenantUser"/></term><term>Denied</term><term>User-scope records</term><term>User</term></item>
///   <item><term><see cref="Restricted"/></term><term>Denied</term><term>Tenant + below</term><term>Tenant</term></item>
///   <item><term><see cref="OrganizationAdmin"/></term><term>Denied</term><term>Own org + user scope</term><term>Organization</term></item>
///   <item><term><see cref="TenantAdmin"/></term><term>Denied</term><term>All within tenant</term><term>Tenant</term></item>
///   <item><term><see cref="PlatformAdmin"/></term><term>Allowed</term><term>All</term><term>Platform</term></item>
/// </list>
/// </summary>
public enum CallerScope
{
    /// <summary>
    /// Scope could not be determined. All query access is denied.
    /// Returned when the caller presents a token that cannot be mapped to any known role.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Self-service scope. The caller may only see their own records
    /// (records where <c>ActorId == caller.UserId</c>) with User-level visibility.
    /// Enforced by overriding <c>ActorId</c> on the query.
    /// </summary>
    UserSelf = 1,

    /// <summary>
    /// Standard tenant user scope. Restricted to the caller's own tenant.
    /// Only records with <see cref="Enums.VisibilityScope.User"/> visibility are accessible.
    /// Does not allow cross-tenant or higher-visibility access.
    /// </summary>
    TenantUser = 2,

    /// <summary>
    /// Compliance / read-only restricted scope. Restricted to the caller's own tenant.
    /// Records with <see cref="Enums.VisibilityScope.Tenant"/> and below are accessible.
    /// Role is read-only and cannot trigger exports or mutations.
    /// </summary>
    Restricted = 3,

    /// <summary>
    /// Organization administrator scope. Restricted to records within the caller's
    /// own organization (same TenantId + OrganizationId).
    /// Records with <see cref="Enums.VisibilityScope.Organization"/> and below are accessible.
    /// </summary>
    OrganizationAdmin = 4,

    /// <summary>
    /// Tenant administrator scope. Unrestricted access within the caller's own tenant.
    /// All non-Internal, non-Platform records within the tenant are accessible.
    /// Cannot access cross-tenant or Platform-scoped records.
    /// </summary>
    TenantAdmin = 5,

    /// <summary>
    /// Platform super-administrator scope. Unrestricted cross-tenant read access.
    /// Can access all records including Platform-scope events.
    /// Internal-scope records remain excluded by the query layer regardless.
    /// </summary>
    PlatformAdmin = 6,
}
