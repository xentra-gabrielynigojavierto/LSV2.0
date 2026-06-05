namespace PlatformAuditEventService.DTOs.Analytics;

/// <summary>
/// Query parameters for GET /audit/analytics/anomalies.
///
/// Windows are always fixed relative to the request time (recent=24h, baseline=prior 7d).
/// This ensures anomaly results always reflect the current state without stale-baseline
/// confusion from caller-supplied date ranges.
///
/// <see cref="TenantId"/> follows the same platform-admin-only semantics as the analytics
/// summary endpoint: non-platform-admin callers always see only their own tenant's data.
/// </summary>
public sealed class AuditAnomalyRequest
{
    /// <summary>
    /// Restrict anomaly detection to a specific tenant.
    /// Platform admin only — omit for cross-tenant anomaly detection.
    /// Tenant-scoped callers: this field is ignored; their tenant is applied server-side.
    /// </summary>
    public string? TenantId { get; set; }
}
