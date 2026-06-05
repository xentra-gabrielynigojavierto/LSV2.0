namespace Flow.Domain.Common;

/// <summary>
/// LS-FLOW-E11.1 — string-constant status values for the
/// <see cref="Domain.Entities.WorkflowTask"/> work item.
///
/// <para>
/// Mirrors the existing Flow convention (e.g.
/// <see cref="WorkflowSlaStatus"/>, <c>WorkflowEngine.Status*</c>):
/// short, stable string keys persisted as <c>varchar</c>. Keeping these
/// out of an <c>enum</c> avoids implicit ordinal coupling and lets
/// future phases extend the lifecycle (e.g. <c>Blocked</c>, <c>OnHold</c>)
/// without a schema migration.
/// </para>
///
/// <para>
/// Lifecycle (this phase only models the four anchor states; transition
/// rules are intentionally NOT enforced — see report §"Status Model Notes"):
///   <list type="bullet">
///     <item><c>Open</c> — created, not yet picked up.</item>
///     <item><c>InProgress</c> — operator is actively working it.</item>
///     <item><c>Completed</c> — terminal success. <c>CompletedAt</c> must be set.</item>
///     <item><c>Cancelled</c> — terminal abort. <c>CancelledAt</c> must be set.</item>
///   </list>
/// </para>
/// </summary>
public static class WorkflowTaskStatus
{
    public const string Open       = "Open";
    public const string InProgress = "InProgress";
    public const string Completed  = "Completed";
    public const string Cancelled  = "Cancelled";

    /// <summary>True when <paramref name="status"/> is one of the four known values.</summary>
    public static bool IsKnown(string? status) =>
        status is Open or InProgress or Completed or Cancelled;

    /// <summary>True when <paramref name="status"/> is a terminal state.</summary>
    public static bool IsTerminal(string? status) =>
        status is Completed or Cancelled;
}
