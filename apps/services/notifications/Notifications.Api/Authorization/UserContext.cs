using System.Security.Claims;

namespace Notifications.Api.Authorization;

/// <summary>
/// Strongly-typed projection of the authenticated caller's JWT claims,
/// resolved per request from <see cref="ClaimsPrincipal"/>.
///
/// <para>
/// LS-NOTIF-CORE-021: <see cref="ServiceName"/> is populated from the
/// <c>svc</c> claim present in service tokens minted by
/// <c>ServiceTokenIssuer</c>.  It is <c>null</c> for ordinary user JWTs.
/// </para>
/// </summary>
public sealed record UserContext(
    string UserId,
    Guid TenantId,
    List<string> Roles,
    bool IsPlatformAdmin,
    string? ServiceName = null)
{
    public static readonly UserContext Empty = new(
        UserId: string.Empty,
        TenantId: Guid.Empty,
        Roles: [],
        IsPlatformAdmin: false,
        ServiceName: null);

    /// <summary>
    /// Returns <c>true</c> when the caller authenticated via a service
    /// token (i.e. a machine-to-machine JWT with a <c>svc</c> claim).
    /// </summary>
    public bool IsServiceCaller => !string.IsNullOrEmpty(ServiceName);
}

/// <summary>
/// Extension methods for extracting <see cref="UserContext"/> and tenant
/// information from <see cref="HttpContext"/> after JWT authentication.
/// </summary>
public static class HttpContextAuthExtensions
{
    private const string UserContextKey = "NotifUserContext";

    /// <summary>
    /// Resolves the <see cref="UserContext"/> from the authenticated
    /// <see cref="ClaimsPrincipal"/>. Returns <see cref="UserContext.Empty"/>
    /// if the user is not authenticated or required claims are missing.
    /// </summary>
    public static UserContext GetUserContext(this HttpContext context)
    {
        if (context.Items.TryGetValue(UserContextKey, out var cached) && cached is UserContext uc)
            return uc;

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
            return UserContext.Empty;

        var userId = user.FindFirst("sub")?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? string.Empty;

        var tenantIdStr = user.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdStr, out var tenantId))
            tenantId = Guid.Empty;

        var roles = user.FindAll(ClaimTypes.Role)
                        .Select(c => c.Value)
                        .ToList();

        var isPlatformAdmin = roles.Contains("PlatformAdmin", StringComparer.Ordinal);

        // LS-NOTIF-CORE-021: extract service identity from the svc claim
        // (present in tokens minted by ServiceTokenIssuer).
        var serviceName = user.FindFirst("svc")?.Value;

        var resolved = new UserContext(userId, tenantId, roles, isPlatformAdmin, serviceName);
        context.Items[UserContextKey] = resolved;
        return resolved;
    }

    /// <summary>
    /// Extracts <c>tenantId</c> from the JWT <c>tenant_id</c> claim.
    /// Throws <see cref="UnauthorizedAccessException"/> if the claim is
    /// missing or invalid — caller must ensure authentication has run first.
    /// </summary>
    public static Guid GetTenantIdFromClaims(this HttpContext context)
    {
        var uc = context.GetUserContext();
        if (uc.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("tenant_id claim is missing or invalid.");
        return uc.TenantId;
    }

    /// <summary>
    /// Parses an optional <c>tenantId</c> query string parameter for admin
    /// endpoints that support cross-tenant filtering.  Returns <c>null</c>
    /// when the parameter is absent, which signals "all tenants".
    /// </summary>
    public static Guid? ParseOptionalTenantIdQuery(this HttpContext context, string paramName = "tenantId")
    {
        var raw = context.Request.Query[paramName].FirstOrDefault();
        if (string.IsNullOrEmpty(raw)) return null;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
