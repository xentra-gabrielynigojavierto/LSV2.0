namespace Tenant.Application.Interfaces;

/// <summary>
/// TENANT-B11 — Read-through adapter for Identity-owned compat data.
/// TENANT-STABILIZATION — Extended with write-through proxy methods.
///
/// Responsibilities:
///   - GetSessionTimeoutMinutesAsync: best-effort read of Identity-owned
///     session timeout for inclusion in the Tenant admin aggregate response.
///   - SetSessionTimeoutAsync: proxy PATCH /api/admin/tenants/{id}/session-settings
///     to Identity so Control Center can route this operation via the Tenant service.
///
/// All operations are non-blocking best-effort: a failure or timeout returns
/// false/null instead of throwing. Callers must treat null/false as "Identity
/// unavailable" and surface an appropriate message to the operator.
/// </summary>
public interface IIdentityCompatAdapter
{
    /// <summary>
    /// Returns the per-tenant idle session timeout (minutes) from Identity,
    /// or <c>null</c> if Identity is unreachable or has no override configured.
    /// </summary>
    Task<int?> GetSessionTimeoutMinutesAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Proxies PATCH /api/admin/tenants/{id}/session-settings to Identity.
    /// Returns <c>true</c> on HTTP 200, <c>false</c> on any failure/timeout.
    /// Pass <c>null</c> to reset to the platform default.
    /// </summary>
    Task<bool> SetSessionTimeoutAsync(
        Guid              tenantId,
        int?              sessionTimeoutMinutes,
        CancellationToken ct = default);
}
