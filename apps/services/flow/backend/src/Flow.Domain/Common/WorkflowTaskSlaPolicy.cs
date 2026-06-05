namespace Flow.Domain.Common;

/// <summary>
/// LS-FLOW-E10.3 (task slice) — pure decision functions for task-level
/// SLA classification. No DB, no DI, no clock — every input is passed
/// in by the caller so this type can be unit-tested deterministically.
///
/// <para>
/// Two decisions live here:
///   • <see cref="ComputeStatus"/>  — given (now, dueAt, dueSoonHorizon),
///     return the appropriate <see cref="WorkflowSlaStatus"/>.
///   • <see cref="ComputeBreachedAt"/> — given the previously persisted
///     breach timestamp and the newly computed status, decide whether
///     to stamp a fresh first-observation breach.
/// </para>
///
/// <para>
/// The evaluator (<c>WorkflowTaskSlaEvaluator</c>) is the only caller in
/// production paths. Reusing the existing <see cref="WorkflowSlaStatus"/>
/// constants — rather than introducing a parallel task-only enum —
/// keeps the workflow- and task-level surfaces aligned vocabulary-wise
/// (operator UI labels DueSoon as "At Risk").
/// </para>
/// </summary>
public static class WorkflowTaskSlaPolicy
{
    /// <summary>
    /// Pure classifier. Returns <see cref="WorkflowSlaStatus.Overdue"/>
    /// when <paramref name="now"/> is past <paramref name="dueAt"/>,
    /// <see cref="WorkflowSlaStatus.DueSoon"/> when <paramref name="now"/>
    /// has entered the dueSoon window, and
    /// <see cref="WorkflowSlaStatus.OnTrack"/> otherwise. Never returns
    /// <see cref="WorkflowSlaStatus.Escalated"/> — task-level escalation
    /// is deferred to a later phase.
    /// </summary>
    public static string ComputeStatus(DateTime now, DateTime dueAt, int dueSoonThresholdMinutes)
    {
        if (now > dueAt) return WorkflowSlaStatus.Overdue;

        var dueSoonAt = dueAt.AddMinutes(-System.Math.Max(0, dueSoonThresholdMinutes));
        if (now >= dueSoonAt) return WorkflowSlaStatus.DueSoon;

        return WorkflowSlaStatus.OnTrack;
    }

    /// <summary>
    /// First-observation breach stamping. If the row is being newly
    /// classified as Overdue and we have not previously recorded a
    /// breach moment, return <paramref name="now"/>. In every other
    /// case (Overdue but already stamped, OnTrack, DueSoon) return the
    /// previously persisted value unchanged — we never rewrite the
    /// breach marker so it remains a stable audit anchor.
    /// </summary>
    public static DateTime? ComputeBreachedAt(string newStatus, DateTime? currentBreachedAt, DateTime now)
    {
        if (string.Equals(newStatus, WorkflowSlaStatus.Overdue, System.StringComparison.Ordinal)
            && currentBreachedAt is null)
        {
            return now;
        }
        return currentBreachedAt;
    }
}
