namespace Flow.Application.Outbox;

/// <summary>
/// LS-FLOW-E10.2 — canonical event-type strings written to the outbox by
/// workflow and admin mutation paths. Each value is consumed by exactly
/// one handler in <c>OutboxDispatcher</c>; new event types must be
/// registered there before they can be enqueued.
///
/// <para>
/// Engine events ("workflow.*") cover the natural lifecycle:
/// start / advance / complete / cancel / fail. Admin events
/// ("workflow.admin.*") cover Control-Center overrides and produce both
/// a durable audit row and (for retry) a re-drive nudge so the worker
/// can verify the engine actually picked the instance back up.
/// </para>
/// </summary>
public static class OutboxEventTypes
{
    public const string WorkflowStart    = "workflow.start";
    public const string WorkflowAdvance  = "workflow.advance";
    public const string WorkflowComplete = "workflow.complete";
    public const string WorkflowCancel   = "workflow.cancel";
    public const string WorkflowFail     = "workflow.fail";

    public const string AdminRetry         = "workflow.admin.retry";
    public const string AdminForceComplete = "workflow.admin.force_complete";
    public const string AdminCancel        = "workflow.admin.cancel";

    // ----- LS-FLOW-E10.3 — SLA / timer transitions ---------------------
    // Emitted by WorkflowSlaEvaluator only when the persisted SlaStatus
    // (or EscalationLevel) actually changes for an instance. See
    // OutboxDispatcher for the audit/notification fan-out.
    public const string WorkflowSlaDueSoon   = "workflow.sla.dueSoon";
    public const string WorkflowSlaOverdue   = "workflow.sla.overdue";
    public const string WorkflowSlaEscalated = "workflow.sla.escalated";
}
