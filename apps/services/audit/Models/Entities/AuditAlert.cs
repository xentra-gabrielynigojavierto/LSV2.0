using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Entities;

/// <summary>
/// Durable alert record created from anomaly detection results.
///
/// Design principles:
/// - One record per unique condition (rule + scope + context), deduped via Fingerprint.
/// - Detection of the same condition while Open/Acknowledged increments DetectionCount
///   and refreshes LastDetectedAtUtc rather than creating a duplicate record.
/// - After resolution, re-detection outside the cooldown window creates a new record.
/// - Status transitions are manual (operators acknowledge/resolve through the UI).
/// - No secrets, PII, or large payloads — ContextJson stores only metrics and safe keys.
/// - Append-friendly: Title/Description/ContextJson/DrillDownPath may be refreshed on
///   re-detection; all other identity fields are set at creation time and immutable.
/// </summary>
public sealed class AuditAlert
{
    // ── Primary key ───────────────────────────────────────────────────────────

    /// <summary>Auto-increment surrogate PK. Internal use only.</summary>
    public long Id { get; init; }

    // ── Public identifier ─────────────────────────────────────────────────────

    /// <summary>
    /// Stable public identifier for this alert (exposed in API responses and UI URLs).
    /// Generated as a Guid at creation time.
    /// </summary>
    public required Guid AlertId { get; init; }

    // ── Detection identity ────────────────────────────────────────────────────

    /// <summary>
    /// The anomaly rule key that triggered this alert.
    /// Examples: DENIAL_SPIKE, ACTOR_CONCENTRATION, SEVERITY_ESCALATION.
    /// </summary>
    public required string RuleKey { get; set; }

    /// <summary>
    /// Deterministic deduplication key computed from RuleKey + scope + context identifiers.
    /// SHA-256 hex (64 chars). Two alerts with the same fingerprint represent the same condition.
    /// </summary>
    public required string Fingerprint { get; init; }

    // ── Scope ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Visibility scope: "Platform" (cross-tenant, platform-admin only) or "Tenant" (single tenant).
    /// </summary>
    public required string ScopeType { get; set; }

    /// <summary>
    /// The tenant this alert is scoped to. Null for truly platform-wide alerts.
    /// </summary>
    public string? TenantId { get; set; }

    // ── Classification ────────────────────────────────────────────────────────

    /// <summary>Alert severity: "High", "Medium", or "Low". Inherited from the triggering anomaly.</summary>
    public required string Severity { get; set; }

    /// <summary>Lifecycle status. See <see cref="AlertStatus"/>.</summary>
    public AlertStatus Status { get; set; } = AlertStatus.Open;

    // ── Human-readable content ────────────────────────────────────────────────

    /// <summary>Short, operator-readable alert title (refreshed on re-detection).</summary>
    public required string Title { get; set; }

    /// <summary>
    /// Plain-English explanation of the anomaly condition, including metric values
    /// and thresholds. Refreshed on each re-detection while the alert is active.
    /// </summary>
    public required string Description { get; set; }

    // ── Context ───────────────────────────────────────────────────────────────

    /// <summary>
    /// JSON object carrying safe context about the detection:
    /// actual/baseline/threshold values, affected actor/tenant/event-type keys, etc.
    /// Never contains secrets or PII. Refreshed on re-detection.
    /// </summary>
    public string? ContextJson { get; set; }

    /// <summary>
    /// Relative UI path for drilling into the investigation view filtered to this alert's context.
    /// Refreshed on re-detection. Example: "/synqaudit/investigation?category=Security".
    /// </summary>
    public string? DrillDownPath { get; set; }

    // ── Detection timeline ────────────────────────────────────────────────────

    /// <summary>UTC timestamp when the alert was first created. Immutable after creation.</summary>
    public required DateTimeOffset FirstDetectedAtUtc { get; init; }

    /// <summary>UTC timestamp of the most recent detection of the same condition. Refreshed on re-detection.</summary>
    public DateTimeOffset LastDetectedAtUtc { get; set; }

    /// <summary>Number of times this condition was detected while this alert record was active.</summary>
    public int DetectionCount { get; set; } = 1;

    // ── Lifecycle actions ─────────────────────────────────────────────────────

    /// <summary>When this alert was acknowledged. Null if not yet acknowledged.</summary>
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }

    /// <summary>Identity (userId or email) of the operator who acknowledged. Null if not acknowledged.</summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>When this alert was resolved. Null if not yet resolved.</summary>
    public DateTimeOffset? ResolvedAtUtc { get; set; }

    /// <summary>Identity of the operator who resolved. Null if not resolved.</summary>
    public string? ResolvedBy { get; set; }
}
