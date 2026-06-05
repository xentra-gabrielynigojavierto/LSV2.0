using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;

namespace PlatformAuditEventService.Authorization;

/// <summary>
/// Caller resolver for <c>QueryAuth:Mode = "Bearer"</c>.
///
/// Reads <see cref="System.Security.Claims.ClaimsPrincipal"/> from
/// <c>HttpContext.User</c> — populated by whatever JWT/OIDC middleware is registered
/// upstream in the pipeline (Auth0, Entra ID, Keycloak, custom, etc.).
///
/// This resolver is deliberately identity-provider-neutral:
/// - It reads claim names from <see cref="QueryAuthOptions"/> rather than hardcoding them.
/// - Scope is determined by comparing the caller's roles against the configured role lists
///   in <see cref="QueryAuthOptions"/>, not by reading any provider-specific claim.
/// - No JWT validation is performed here — that responsibility belongs to the upstream
///   JWT middleware (e.g. <c>AddJwtBearer()</c>).
///
/// Scope resolution priority (first match wins, highest scope first):
/// <list type="number">
///   <item>Any role in <c>QueryAuth:PlatformAdminRoles</c> → <see cref="CallerScope.PlatformAdmin"/></item>
///   <item>Any role in <c>QueryAuth:TenantAdminRoles</c> → <see cref="CallerScope.TenantAdmin"/></item>
///   <item>Any role in <c>QueryAuth:OrganizationAdminRoles</c> → <see cref="CallerScope.OrganizationAdmin"/></item>
///   <item>Any role in <c>QueryAuth:RestrictedRoles</c> → <see cref="CallerScope.Restricted"/></item>
///   <item>Any role in <c>QueryAuth:TenantUserRoles</c> → <see cref="CallerScope.TenantUser"/></item>
///   <item>Authenticated with any recognized claim but no matching role → <see cref="CallerScope.TenantUser"/> (safe fallback)</item>
///   <item><c>QueryAuth:UserSelfRoles</c> match → <see cref="CallerScope.UserSelf"/></item>
///   <item>Not authenticated or resolution failed → <see cref="CallerScope.Unknown"/></item>
/// </list>
///
/// Extension path:
///   To add a new identity provider, register a different <see cref="IQueryCallerResolver"/>
///   implementation for that provider's mode. This class remains unchanged.
/// </summary>
public sealed class ClaimsCallerResolver : IQueryCallerResolver
{
    private readonly QueryAuthOptions                   _opts;
    private readonly ILogger<ClaimsCallerResolver>      _logger;

    // Pre-computed HashSets for O(1) role lookup.
    private readonly HashSet<string> _platformAdminRoles;
    private readonly HashSet<string> _tenantAdminRoles;
    private readonly HashSet<string> _orgAdminRoles;
    private readonly HashSet<string> _restrictedRoles;
    private readonly HashSet<string> _tenantUserRoles;
    private readonly HashSet<string> _userSelfRoles;

    public string Mode => "Bearer";

    public ClaimsCallerResolver(
        IOptions<QueryAuthOptions>       opts,
        ILogger<ClaimsCallerResolver>    logger)
    {
        _opts   = opts.Value;
        _logger = logger;

        _platformAdminRoles = ToSet(_opts.PlatformAdminRoles);
        _tenantAdminRoles   = ToSet(_opts.TenantAdminRoles);
        _orgAdminRoles      = ToSet(_opts.OrganizationAdminRoles);
        _restrictedRoles    = ToSet(_opts.RestrictedRoles);
        _tenantUserRoles    = ToSet(_opts.TenantUserRoles);
        _userSelfRoles      = ToSet(_opts.UserSelfRoles);
    }

    public Task<IQueryCallerContext> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        var user = context.User;

        // Not authenticated — the upstream JWT middleware did not set a principal.
        // Return Unknown so the middleware can issue a 401.
        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("ClaimsCallerResolver: user is not authenticated.");
            return Task.FromResult<IQueryCallerContext>(QueryCallerContext.Failed(Mode));
        }

        // ── Extract standard identity claims ──────────────────────────────────
        var tenantId       = FirstClaim(user, _opts.TenantIdClaimType);
        var organizationId = FirstClaim(user, _opts.OrganizationIdClaimType);
        var userId         = FirstClaim(user, _opts.UserIdClaimType);

        // ── Collect all role claims ───────────────────────────────────────────
        var roles = user.Claims
            .Where(c => c.Type.Equals(_opts.RoleClaimType, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToList();

        // ── Resolve scope (highest match wins) ────────────────────────────────
        var scope = ResolveScope(roles);

        _logger.LogDebug(
            "ClaimsCallerResolver: UserId={UserId} TenantId={TenantId} " +
            "Scope={Scope} Roles=[{Roles}]",
            userId, tenantId, scope, string.Join(", ", roles));

        var ctx = QueryCallerContext.Authenticated(
            scope:          scope,
            tenantId:       tenantId,
            organizationId: organizationId,
            userId:         userId,
            roles:          roles,
            authMode:       Mode);

        return Task.FromResult<IQueryCallerContext>(ctx);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private CallerScope ResolveScope(IReadOnlyList<string> roles)
    {
        if (roles.Count == 0)
            return CallerScope.TenantUser; // Authenticated, no roles → minimum safe scope.

        if (HasAny(roles, _platformAdminRoles)) return CallerScope.PlatformAdmin;
        if (HasAny(roles, _tenantAdminRoles))   return CallerScope.TenantAdmin;
        if (HasAny(roles, _orgAdminRoles))       return CallerScope.OrganizationAdmin;
        if (HasAny(roles, _restrictedRoles))     return CallerScope.Restricted;
        if (HasAny(roles, _userSelfRoles))       return CallerScope.UserSelf;
        if (HasAny(roles, _tenantUserRoles))     return CallerScope.TenantUser;

        // Authenticated with unrecognized roles — safe fallback.
        return CallerScope.TenantUser;
    }

    private static bool HasAny(IReadOnlyList<string> roles, HashSet<string> set) =>
        roles.Any(r => set.Contains(r));

    private static string? FirstClaim(System.Security.Claims.ClaimsPrincipal user, string claimType) =>
        user.Claims.FirstOrDefault(c =>
            c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase))?.Value;

    private static HashSet<string> ToSet(IEnumerable<string>? values) =>
        new(values ?? [], StringComparer.OrdinalIgnoreCase);
}
