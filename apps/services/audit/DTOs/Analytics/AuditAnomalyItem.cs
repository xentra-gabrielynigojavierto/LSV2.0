namespace PlatformAuditEventService.DTOs.Analytics;

/// <summary>
/// A single firing anomaly detected by one of the v1 rule evaluators.
///
/// Only anomalies that cross their threshold appear in the response list.
/// When no rules fire the list is empty (not an error condition).
/// </summary>
public sealed class AuditAnomalyItem
{
    // ── Rule identity ─────────────────────────────────────────────────────────

    /// <summary>
    /// Stable machine-readable key for this anomaly rule
    /// (e.g. "DENIAL_SPIKE", "ACTOR_CONCENTRATION").
    /// </summary>
    public required string RuleKey { get; init; }

    /// <summary>Short human-readable title for display in the UI.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// Plain-English explanation of why this anomaly fired, including
    /// the actual metric values that exceeded the threshold.
    /// </summary>
    public required string Description { get; init; }

    // ── Severity ──────────────────────────────────────────────────────────────

    /// <summary>Anomaly severity label: "High", "Medium", or "Low".</summary>
    public required string Severity { get; init; }

    // ── Metric values ─────────────────────────────────────────────────────────

    /// <summary>Observed count in the recent window (last 24 hours).</summary>
    public required long RecentValue { get; init; }

    /// <summary>
    /// Baseline reference value used for ratio rules.
    /// For spike rules: the 7-day daily average (baseline_total / 7).
    /// For concentration rules: the total event count in the recent window.
    /// Null for rules that use only a fixed threshold.
    /// </summary>
    public double? BaselineValue { get; init; }

    /// <summary>
    /// The threshold that was exceeded (ratio or percentage, depending on rule type).
    /// </summary>
    public required double Threshold { get; init; }

    /// <summary>
    /// The actual ratio or percentage that triggered the rule.
    /// </summary>
    public required double ActualValue { get; init; }

    // ── Context ───────────────────────────────────────────────────────────────

    /// <summary>Actor identifier affected, if the rule is actor-specific.</summary>
    public string? AffectedActorId { get; init; }

    /// <summary>Actor display name, if available.</summary>
    public string? AffectedActorName { get; init; }

    /// <summary>Tenant identifier affected, if the rule is tenant-specific.</summary>
    public string? AffectedTenantId { get; init; }

    /// <summary>Event type affected, if the rule is event-type-specific.</summary>
    public string? AffectedEventType { get; init; }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Suggested drill-down path within Control Center (relative URL).
    /// Includes pre-populated query params where applicable (actorId, eventType, category).
    /// </summary>
    public required string DrillDownPath { get; init; }
}
