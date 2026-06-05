namespace PlatformAuditEventService.DTOs.Alerts;

/// <summary>
/// Response from <c>POST /audit/analytics/alerts/evaluate</c>.
///
/// Summarises the outcome of running the full anomaly → alert pipeline.
/// </summary>
public sealed class AuditEvaluateAlertsResponse
{
    /// <summary>UTC timestamp when evaluation ran.</summary>
    public DateTimeOffset EvaluatedAt { get; set; }

    /// <summary>The effective tenant scope used for detection, or null for cross-tenant.</summary>
    public string? EffectiveTenantId { get; set; }

    /// <summary>Total anomalies detected by the rule engine in this evaluation run.</summary>
    public int AnomaliesDetected { get; set; }

    /// <summary>Number of new <c>Open</c> alert records created.</summary>
    public int AlertsCreated { get; set; }

    /// <summary>Number of existing active alerts refreshed (LastDetectedAtUtc + DetectionCount incremented).</summary>
    public int AlertsRefreshed { get; set; }

    /// <summary>
    /// Number of anomalies that matched a recently-resolved alert within the cooldown window
    /// and were therefore suppressed (no new record created).
    /// </summary>
    public int AlertsSuppressed { get; set; }

    /// <summary>The newly created or refreshed alerts (up to 50 most severe).</summary>
    public List<AuditAlertItem> ActiveAlerts { get; set; } = [];
}
