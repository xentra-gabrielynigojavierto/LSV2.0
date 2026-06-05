using Support.Api.Auth;

namespace Support.Api.Tenancy;

public interface ITenantContext
{
    string? TenantId { get; }
    string? UserId { get; }
    bool IsResolved { get; }
    void Set(string tenantId, string? userId);
}

public class TenantContext : ITenantContext
{
    public string? TenantId { get; private set; }
    public string? UserId { get; private set; }
    public bool IsResolved => !string.IsNullOrWhiteSpace(TenantId);

    public void Set(string tenantId, string? userId)
    {
        TenantId = tenantId;
        UserId = userId;
    }
}

/// <summary>
/// Tenant resolution rules:
///
/// Production / non-development & non-testing:
///   * Tenant comes from authenticated JWT claim only.
///     Accepted claim names (priority): tenant_id, tenantId, tid.
///   * X-Tenant-Id header is IGNORED.
///   * Body tenant_id is IGNORED (never trusted in middleware; service layer
///     also refuses body fallback in production).
///   * If user is authenticated but no tenant claim is present, the request is
///     short-circuited with 403 (only for paths under /support/api/* that are
///     not health/metrics/swagger).
///   * Unauthenticated requests pass through; the authorization policy on the
///     endpoint will reject them with 401.
///
/// Development / Testing:
///   * Same priority order: claim → header.
///   * Body tenant_id fallback for the create endpoint remains enabled in the
///     service layer.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    private static readonly string[] TenantClaimNames = { "tenant_id", "tenantId", "tid" };

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        var devOrTest = _env.IsDevelopment() || _env.IsEnvironment("Testing");

        string? claim = null;
        foreach (var name in TenantClaimNames)
        {
            claim = context.User?.FindFirst(name)?.Value;
            if (!string.IsNullOrWhiteSpace(claim)) break;
        }

        var userId = context.User?.FindFirst("sub")?.Value
            ?? context.Request.Headers["X-User-Id"].FirstOrDefault();

        string? resolved = claim;

        if (string.IsNullOrWhiteSpace(resolved) && devOrTest)
        {
            resolved = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(resolved))
        {
            tenant.Set(resolved!, userId);
        }
        else
        {
            _logger.LogDebug("Tenant not resolved for path {Path}", context.Request.Path);
        }

        // In production, an authenticated user accessing a protected support
        // endpoint without a tenant claim is a forbidden request —
        // UNLESS the user is a PlatformAdmin, who is platform-wide and does
        // not need to be scoped to a specific tenant.
        var isPlatformAdmin = context.User?.FindAll("role")
            .Any(c => c.Value == SupportRoles.PlatformAdmin) == true;

        if (!devOrTest
            && context.User?.Identity?.IsAuthenticated == true
            && string.IsNullOrWhiteSpace(claim)
            && !isPlatformAdmin
            && IsProtectedSupportPath(context.Request.Path))
        {
            _logger.LogWarning(
                "SUPPORT-TENANT: 403 — authenticated user has no tenant_id claim. " +
                "path={Path} sub={Sub} roles=[{Roles}]",
                context.Request.Path,
                context.User?.FindFirst("sub")?.Value ?? "(none)",
                string.Join(",", context.User?.FindAll("role").Select(c => c.Value) ?? []));
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Forbidden",
                detail = "Authenticated request is missing required tenant claim.",
                status = 403
            });
            return;
        }

        if (!devOrTest && context.User?.Identity?.IsAuthenticated == true)
        {
            _logger.LogDebug(
                "SUPPORT-TENANT: resolved tenant={Tenant} isPlatformAdmin={IsAdmin} path={Path}",
                resolved ?? "(none)", isPlatformAdmin, context.Request.Path);
        }

        await _next(context);
    }

    private static bool IsProtectedSupportPath(PathString path)
    {
        if (!path.HasValue) return false;
        var p = path.Value!;
        if (!p.StartsWith("/support/api/", StringComparison.OrdinalIgnoreCase)) return false;
        if (p.StartsWith("/support/api/health", StringComparison.OrdinalIgnoreCase)) return false;
        if (p.StartsWith("/support/api/metrics", StringComparison.OrdinalIgnoreCase)) return false;
        if (p.StartsWith("/support/api/swagger", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
