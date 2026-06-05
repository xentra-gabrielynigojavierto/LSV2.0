using BuildingBlocks.Context;
using Flow.Domain.Interfaces;

namespace Flow.Api.Services;

/// <summary>
/// LegalSynq-aligned tenant provider. Resolves the current tenant strictly from
/// the authenticated principal's <c>tenant_id</c> claim, surfaced via
/// <see cref="ICurrentRequestContext"/>.
///
/// There is intentionally NO silent default fallback: if no authenticated tenant
/// is present, callers will throw, ensuring tenant isolation cannot be bypassed
/// by anonymous or malformed traffic. Public/anonymous endpoints (health, info)
/// must not reach the data layer.
/// </summary>
public sealed class ClaimsTenantProvider : ITenantProvider
{
    public const string TenantHeaderName = "X-Tenant-Id";

    private readonly ICurrentRequestContext _requestContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ClaimsTenantProvider(
        ICurrentRequestContext requestContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _requestContext = requestContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetTenantId()
    {
        var tenantId = _requestContext.TenantId;
        if (tenantId is not null && tenantId.Value != Guid.Empty)
        {
            return tenantId.Value.ToString("D").ToLowerInvariant();
        }

        // Soft fallback for *unauthenticated* internal call sites only (e.g. EF
        // pre-startup ops). Authenticated requests routed through the platform
        // gateway will always have a tenant claim. We deliberately do not honor
        // the legacy X-Tenant-Id header to avoid header-based tenant spoofing.
        var path = _httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty;
        if (IsExcludedPath(path))
        {
            return string.Empty;
        }

        throw new InvalidOperationException(
            "Tenant context is required but no authenticated tenant_id claim was present on the request.");
    }

    private static bool IsExcludedPath(string path) =>
        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/ready", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/v1/status", StringComparison.OrdinalIgnoreCase);
}
