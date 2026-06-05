namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Authorization options for audit event query/retrieval endpoints.
/// Bound from <c>"QueryAuth"</c> section in appsettings.
/// Environment variable override prefix: <c>QueryAuth__</c>
///
/// Role lists determine how a caller's token roles map to a <see cref="Authorization.CallerScope"/>.
/// Claim type names allow this service to work with any OIDC / JWT provider without
/// changes to application code — only config changes are needed.
/// </summary>
public sealed class QueryAuthOptions
{
    public const string SectionName = "QueryAuth";

    // ── Auth mode ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Auth mode for query/read endpoints.
    /// Allowed values: <c>"None"</c> | <c>"Bearer"</c>
    /// <c>"None"</c> is for development only — all callers receive PlatformAdmin scope.
    /// Environment variable: <c>QueryAuth__Mode</c>
    /// </summary>
    public string Mode { get; set; } = "None";

    // ── Role → scope mappings ─────────────────────────────────────────────────

    /// <summary>
    /// Roles that grant cross-tenant, unrestricted read access (<see cref="Authorization.CallerScope.PlatformAdmin"/>).
    /// Example: "platform-audit-admin"
    /// </summary>
    public List<string> PlatformAdminRoles { get; set; } = ["platform-audit-admin"];

    /// <summary>
    /// Roles that grant unrestricted read access within the caller's own tenant (<see cref="Authorization.CallerScope.TenantAdmin"/>).
    /// Example: "tenant-admin", "compliance-officer"
    /// </summary>
    public List<string> TenantAdminRoles { get; set; } = ["tenant-admin", "compliance-officer"];

    /// <summary>
    /// Roles that grant read access to records within the caller's own organization
    /// (<see cref="Authorization.CallerScope.OrganizationAdmin"/>).
    /// Example: "org-admin", "department-admin"
    /// </summary>
    public List<string> OrganizationAdminRoles { get; set; } = ["org-admin", "department-admin"];

    /// <summary>
    /// Roles that grant compliance-restricted read-only access to tenant-scoped records
    /// (<see cref="Authorization.CallerScope.Restricted"/>).
    /// Example: "compliance-reader", "auditor"
    /// </summary>
    public List<string> RestrictedRoles { get; set; } = ["compliance-reader", "auditor"];

    /// <summary>
    /// Roles that grant standard tenant-user read access to User-scope records only
    /// (<see cref="Authorization.CallerScope.TenantUser"/>).
    /// Example: "tenant-user", "user"
    /// </summary>
    public List<string> TenantUserRoles { get; set; } = ["tenant-user", "user"];

    /// <summary>
    /// Roles that restrict the caller to seeing only their own records
    /// (<see cref="Authorization.CallerScope.UserSelf"/>).
    /// Example: "self-reader"
    /// </summary>
    public List<string> UserSelfRoles { get; set; } = ["self-reader"];

    // ── Claim type names ──────────────────────────────────────────────────────

    /// <summary>
    /// JWT claim name for the tenant identifier.
    /// Common values: <c>"tenant_id"</c>, <c>"tid"</c>, <c>"tenantId"</c>.
    /// Environment variable: <c>QueryAuth__TenantIdClaimType</c>
    /// </summary>
    public string TenantIdClaimType { get; set; } = "tenant_id";

    /// <summary>
    /// JWT claim name for the organization identifier.
    /// Common values: <c>"org_id"</c>, <c>"organizationId"</c>.
    /// Environment variable: <c>QueryAuth__OrganizationIdClaimType</c>
    /// </summary>
    public string OrganizationIdClaimType { get; set; } = "org_id";

    /// <summary>
    /// JWT claim name for the subject / user identifier.
    /// Standard OIDC: <c>"sub"</c>. Some providers use <c>"oid"</c> (Entra ID) or <c>"uid"</c>.
    /// Environment variable: <c>QueryAuth__UserIdClaimType</c>
    /// </summary>
    public string UserIdClaimType { get; set; } = "sub";

    /// <summary>
    /// JWT claim name for roles.
    /// Auth0: <c>"role"</c> or a custom namespace claim.
    /// Entra ID: <c>"http://schemas.microsoft.com/ws/2008/06/identity/claims/role"</c> or <c>"roles"</c>.
    /// Keycloak: <c>"realm_access"</c> (requires custom resolver).
    /// Environment variable: <c>QueryAuth__RoleClaimType</c>
    /// </summary>
    public string RoleClaimType { get; set; } = "role";

    // ── Enforcement flags ─────────────────────────────────────────────────────

    /// <summary>
    /// When true, non-PlatformAdmin callers must have a non-null TenantId claim.
    /// A token without a TenantId claim is denied with 403.
    /// Set to false only if your deployment has platform-global callers that are not PlatformAdmins.
    /// </summary>
    public bool EnforceTenantScope { get; set; } = true;

    // ── Response shaping ──────────────────────────────────────────────────────

    /// <summary>
    /// Maximum number of records returnable in a single query, regardless of PageSize.
    /// Prevents unbounded reads in high-volume stores.
    /// </summary>
    public int MaxPageSize { get; set; } = 500;

    /// <summary>
    /// When true, the <c>Hash</c> field is included in query responses.
    /// Only PlatformAdmin callers receive it by default; lower scopes see null.
    /// </summary>
    public bool ExposeIntegrityHash { get; set; } = false;
}
