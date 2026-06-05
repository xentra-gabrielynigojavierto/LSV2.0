namespace PlatformAuditEventService.DTOs.Alerts;

/// <summary>
/// Response envelope for <c>GET /audit/analytics/alerts</c>.
/// </summary>
public sealed class AuditAlertListResponse
{
    /// <summary>The applied status filter, or null if all statuses were requested.</summary>
    public string? StatusFilter { get; set; }

    /// <summary>The effective tenant scope, or null for cross-tenant platform views.</summary>
    public string? EffectiveTenantId { get; set; }

    /// <summary>Total number of alert records matching the filter (capped to Limit).</summary>
    public int TotalReturned { get; set; }

    public int OpenCount         { get; set; }
    public int AcknowledgedCount { get; set; }
    public int ResolvedCount     { get; set; }

    public List<AuditAlertItem> Alerts { get; set; } = [];
}
