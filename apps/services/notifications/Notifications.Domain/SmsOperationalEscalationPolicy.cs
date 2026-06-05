namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-011: Escalation policy that routes active SMS operational alerts to
/// external operational channels (email, Teams webhook, Slack webhook, etc.).
///
/// Matching logic:
///   Null fields are wildcards — a policy with AlertType=null matches ANY alert type.
///   All non-null fields must match the alert for the policy to apply.
///
/// Security:
///   Target contains the raw webhook URL or email address. It is NEVER returned in
///   API responses in full. Only TargetDisplay (or a masked derivation) is exposed.
///   No credentials, phone numbers, or provider payloads are stored here.
/// </summary>
public class SmsOperationalEscalationPolicy
{
    public Guid Id { get; set; }

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Human-readable policy name (e.g. "Critical → Teams #ops-alerts").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether this policy is active. Disabled policies are never matched.</summary>
    public bool Enabled { get; set; } = true;

    // ── Matching criteria (null = wildcard) ───────────────────────────────────

    /// <summary>Alert type code to match, or null to match all alert types.</summary>
    public string? AlertType { get; set; }

    /// <summary>"warning" | "critical" to match, or null to match all severities.</summary>
    public string? Severity { get; set; }

    /// <summary>Tenant to match, or null to match all tenants (including platform-wide alerts).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Provider name to match, or null to match all providers.</summary>
    public string? Provider { get; set; }

    /// <summary>Provider config ID to match, or null to match all configs.</summary>
    public Guid? ProviderConfigId { get; set; }

    // ── Channel ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Delivery channel type. Valid values:
    ///   internal_notification | email | teams_webhook | slack_webhook | pagerduty | opsgenie
    /// </summary>
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Raw channel target: webhook URL or email address.
    /// SENSITIVE — never returned in full via admin APIs.
    /// Stored as plain text; encryption at rest is recommended for production.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Safe display label for the target (e.g. "#ops-alerts", "ops@example.com").
    /// Shown in admin UI. Must not contain the raw webhook URL.
    /// </summary>
    public string? TargetDisplay { get; set; }

    // ── Dedup + retry ─────────────────────────────────────────────────────────

    /// <summary>
    /// After a successful (or suppressed) escalation, do not re-escalate the same
    /// alert+policy combination for this many minutes.
    /// Default 60 minutes. Range: 1–10080 (7 days).
    /// </summary>
    public int CooldownMinutes { get; set; } = 60;

    /// <summary>Whether to retry transient delivery failures for this policy.</summary>
    public bool RetryEnabled { get; set; }

    /// <summary>Maximum number of retry attempts (in addition to the initial attempt).</summary>
    public int MaxRetryCount { get; set; } = 3;

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Identity (sub/email) of the operator who created this policy.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Identity (sub/email) of the operator who last updated this policy.</summary>
    public string? UpdatedBy { get; set; }
}
