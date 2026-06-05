namespace Flow.Application.Outbox;

/// <summary>
/// LS-FLOW-E10.2 — payload schemas serialised into
/// <c>OutboxMessage.PayloadJson</c>. Kept narrow on purpose: the worker
/// re-loads the workflow instance from the database when it processes
/// the row, so the payload only needs the minimum durable context the
/// handler needs to produce the audit/notification message and to make
/// idempotency decisions.
/// </summary>
public sealed record WorkflowLifecyclePayload(
    Guid WorkflowInstanceId,
    string ProductKey,
    string? FromStepKey,
    string? ToStepKey,
    string? FromStatus,
    string ToStatus,
    string? Reason,
    string? PerformedBy,
    DateTime OccurredAtUtc);

/// <summary>
/// LS-FLOW-E10.2 — payload for admin-action events (retry / force-complete
/// / cancel) emitted from <c>AdminWorkflowInstancesController</c>. Carries
/// the actor + reason so the audit handler can render a meaningful audit
/// description and the re-drive handler can short-circuit safely if the
/// state has moved on since the action committed.
/// </summary>
public sealed record AdminActionPayload(
    Guid WorkflowInstanceId,
    string ProductKey,
    string Action,
    string PreviousStatus,
    string NewStatus,
    string Reason,
    string PerformedBy,
    bool IsPlatformAdmin,
    DateTime OccurredAtUtc);

/// <summary>
/// LS-FLOW-E10.3 — payload for SLA / timer transition events
/// (<c>workflow.sla.dueSoon</c>, <c>workflow.sla.overdue</c>,
/// <c>workflow.sla.escalated</c>) emitted by
/// <c>WorkflowSlaEvaluator</c>.
///
/// <para>
/// Carries the durable context the audit + notification handlers need
/// without forcing them to re-load the instance. <c>PreviousSlaStatus</c>
/// is included so dashboards / handlers can render a human-readable
/// transition label ("OnTrack → DueSoon") without keeping their own
/// state.
/// </para>
///
/// <para>
/// <c>OverdueDurationSeconds</c> is populated for the overdue and
/// escalated events (and may be 0 if the evaluator catches the
/// transition exactly at the boundary). For dueSoon it is null.
/// <c>AssignedToUserId</c> is captured at evaluation time so the
/// notification handler does not race against an assignment change.
/// </para>
/// </summary>
public sealed record WorkflowSlaTransitionPayload(
    Guid WorkflowInstanceId,
    string ProductKey,
    string? CurrentStepKey,
    DateTime DueAt,
    string PreviousSlaStatus,
    string NewSlaStatus,
    int EscalationLevel,
    long? OverdueDurationSeconds,
    string? AssignedToUserId,
    DateTime OccurredAtUtc);
