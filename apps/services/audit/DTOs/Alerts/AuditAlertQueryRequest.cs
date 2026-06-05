namespace PlatformAuditEventService.DTOs.Alerts;

/// <summary>
/// Query parameters for <c>GET /audit/analytics/alerts</c>.
///
/// All filters are optional. Omitting <see cref="Status"/> returns all statuses.
/// Platform admin callers may supply <see cref="TenantId"/> to scope results;
/// tenant-scoped callers always see only their own tenant's alerts.
/// </summary>
public sealed class AuditAlertQueryRequest
{
    /// <summary>
    /// Filter by lifecycle status. Accepts "Open", "Acknowledged", or "Resolved".
    /// When omitted, all statuses are returned.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Platform admin only. When supplied, restricts results to alerts for this tenant.
    /// When omitted by a platform admin, all tenant + platform-wide alerts are returned.
    /// Ignored for tenant-scoped callers.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>Maximum number of alerts to return. Default: 50. Max: 200.</summary>
    public int? Limit { get; set; }
}
