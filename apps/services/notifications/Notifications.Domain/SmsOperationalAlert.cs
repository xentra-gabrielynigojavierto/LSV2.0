namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-010: A persisted operational alert raised when an SMS threshold rule fires.
///
/// Alerts are deduped by (AlertType, TenantId, Provider, ProviderConfigId) while active.
/// When a new evaluation detects the same condition the existing active alert is updated
/// (OccurrenceCount++, LastObservedAt refreshed) instead of creating a new record.
/// A cooldown period prevents re-alerting immediately after resolution.
///
/// No credentials, phone numbers, RecipientJson, CredentialsJson, or SettingsJson
/// are stored in any alert field.
/// </summary>
public class SmsOperationalAlert
{
    public Guid Id { get; set; }

    // ── Classification ────────────────────────────────────────────────────────

    /// <summary>
    /// Alert type code — one of the LS-NOTIF-SMS-010 rule codes:
    ///   sms.failure_rate_high | sms.dead_letter_spike | sms.retry_spike |
    ///   sms.reconciliation_failure_rate_high | sms.provider_degraded |
    ///   sms.provider_config_failure_spike | sms.tenant_anomaly |
    ///   sms.reconciliation_stale
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Severity: "warning" | "critical".
    /// Determined by the rule that fires and the magnitude of the breach.
    /// </summary>
    public string Severity { get; set; } = "warning";

    // ── Scope ─────────────────────────────────────────────────────────────────

    /// <summary>Scoped tenant, or null for platform-wide alerts.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Scoped provider name (e.g. "twilio"), or null for cross-provider rules.</summary>
    public string? Provider { get; set; }

    /// <summary>Scoped provider config ID, or null for cross-config rules.</summary>
    public Guid? ProviderConfigId { get; set; }

    // ── Threshold context ────────────────────────────────────────────────────

    /// <summary>
    /// The observed metric value that triggered this alert
    /// (e.g. failure rate 0.42, dead-letter count 17).
    /// Stored as a decimal for consistent comparison.
    /// </summary>
    public decimal MetricValue { get; set; }

    /// <summary>
    /// The configured threshold that was breached.
    /// </summary>
    public decimal ThresholdValue { get; set; }

    /// <summary>
    /// Human-readable message summarising the breach.
    /// No credentials or raw provider payloads.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The UTC start of the evaluation window used to compute MetricValue.
    /// </summary>
    public DateTime EvaluationWindowStart { get; set; }

    /// <summary>
    /// The UTC end of the evaluation window used to compute MetricValue.
    /// </summary>
    public DateTime EvaluationWindowEnd { get; set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Alert lifecycle status: "active" | "resolved" | "suppressed".
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// How many evaluations have observed this condition while the alert is active.
    /// Starts at 1 on creation, incremented on each matching evaluation cycle.
    /// </summary>
    public int OccurrenceCount { get; set; } = 1;

    /// <summary>UTC timestamp of the first observation (= CreatedAt).</summary>
    public DateTime FirstObservedAt { get; set; }

    /// <summary>UTC timestamp of the most recent evaluation that matched this condition.</summary>
    public DateTime LastObservedAt { get; set; }

    /// <summary>UTC timestamp when Status was set to "resolved". Null if still active.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Identity (sub/email) of the operator who resolved the alert. Null if auto-resolved.</summary>
    public string? ResolvedBy { get; set; }

    /// <summary>Optional free-text resolution note left by the operator.</summary>
    public string? ResolutionNote { get; set; }

    /// <summary>
    /// When set, new occurrences of this (AlertType, TenantId, Provider, ProviderConfigId)
    /// will not re-create the alert until this timestamp has passed.
    /// Used to suppress alerts during a known maintenance window.
    /// </summary>
    public DateTime? SuppressedUntil { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
