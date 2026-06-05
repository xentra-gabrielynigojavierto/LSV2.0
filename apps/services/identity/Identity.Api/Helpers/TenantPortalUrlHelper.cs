using Identity.Domain;
using Identity.Infrastructure.Services;

namespace Identity.Api.Helpers;

/// <summary>
/// LS-ID-TNT-016-01: Builds tenant-subdomain-aware portal URLs for user-management emails.
///
/// Priority:
///   1. <c>NotificationsService:PortalBaseDomain</c> is set →
///      <c>https://{tenantSlug}.{PortalBaseDomain}/{path}?token=...</c>
///   2. <c>NotificationsService:PortalBaseUrl</c> is set (fallback) →
///      <c>{PortalBaseUrl}/{path}?token=...</c>
///   3. Both missing → returns <c>null</c>; callers must treat this as a
///      configuration error and NOT emit a malformed link.
///
/// Tenant slug resolution:
///   <c>tenant.Subdomain</c> (live DNS subdomain) if set, otherwise <c>tenant.Code</c>
///   (normalized slug assigned at tenant creation). A null <c>tenant</c> argument
///   falls back to the legacy <c>PortalBaseUrl</c> pattern.
/// </summary>
public static class TenantPortalUrlHelper
{
    /// <summary>
    /// Constructs the full URL for a user-management link.
    /// </summary>
    /// <param name="tenant">
    ///   The resolved <see cref="Tenant"/> entity. When <c>null</c> (e.g. tenant lookup
    ///   failed unexpectedly), the method falls back to <see cref="NotificationsServiceOptions.PortalBaseUrl"/>.
    /// </param>
    /// <param name="path">
    ///   The path segment after the host, without a leading slash.
    ///   Example: <c>"accept-invite"</c>, <c>"reset-password"</c>.
    /// </param>
    /// <param name="rawToken">The raw (un-hashed) token to append as <c>?token=</c>.</param>
    /// <param name="opts">Bound <see cref="NotificationsServiceOptions"/> instance.</param>
    /// <returns>
    ///   A fully-qualified URL string, or <c>null</c> if neither <c>PortalBaseDomain</c>
    ///   nor <c>PortalBaseUrl</c> is configured.
    /// </returns>
    public static string? Build(
        Tenant?                     tenant,
        string                      path,
        string                      rawToken,
        NotificationsServiceOptions opts)
    {
        var baseUrl = ResolveBaseUrl(tenant, opts);
        if (baseUrl is null) return null;

        var cleanPath = path.TrimStart('/');
        return $"{baseUrl}/{cleanPath}?token={Uri.EscapeDataString(rawToken)}";
    }

    /// <summary>
    /// Returns just the tenant portal base URL (no path, no token) for use as a
    /// login-redirect target after invite acceptance.
    /// </summary>
    /// <returns>
    ///   A fully-qualified base URL string (e.g. <c>https://acme.example.com</c>),
    ///   or <c>null</c> if neither <c>PortalBaseDomain</c> nor <c>PortalBaseUrl</c>
    ///   is configured.
    /// </returns>
    public static string? BuildBaseUrl(
        Tenant?                     tenant,
        NotificationsServiceOptions opts)
        => ResolveBaseUrl(tenant, opts);

    private static string? ResolveBaseUrl(
        Tenant?                     tenant,
        NotificationsServiceOptions opts)
    {
        var baseDomain = opts.PortalBaseDomain?.Trim().TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(baseDomain) && tenant is not null)
        {
            var slug = (tenant.Subdomain ?? tenant.Code).ToLowerInvariant().Trim();
            return $"https://{slug}.{baseDomain}";
        }

        if (!string.IsNullOrWhiteSpace(opts.PortalBaseUrl))
            return opts.PortalBaseUrl.TrimEnd('/');

        return null;
    }
}
