namespace Flow.Domain.Common;

/// <summary>
/// LS-FLOW-E10.3 — string-valued SLA status constants for
/// <see cref="Flow.Domain.Entities.WorkflowInstance.SlaStatus"/>. Stored
/// as <c>varchar(16)</c> for human readability in operator queries and
/// kept as constants (not an enum) so existing string-conversion
/// patterns elsewhere on the entity (e.g. <c>Status</c>) continue to
/// apply uniformly.
///
/// <para>
/// Transition rules (deterministic, evaluated once per evaluator tick):
///   • OnTrack    — DueAt has not yet entered the dueSoon threshold.
///   • DueSoon    — now &gt;= DueAt - WorkflowSla:DueSoonThresholdMinutes.
///   • Overdue    — now &gt;  DueAt.
///   • Escalated  — Overdue duration &gt;= WorkflowSla:EscalationThresholdMinutes
///                  AND EscalationLevel == 0 (single escalation level
///                  this phase; see report Known Issues / Gaps).
/// </para>
/// </summary>
public static class WorkflowSlaStatus
{
    public const string OnTrack   = "OnTrack";
    public const string DueSoon   = "DueSoon";
    public const string Overdue   = "Overdue";
    public const string Escalated = "Escalated";
}
