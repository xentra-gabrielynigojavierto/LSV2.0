using Flow.Domain.Common;

namespace Flow.Domain.Entities;

/// <summary>
/// LS-FLOW-MERGE-P4 — dedicated workflow-instance grain. Replaces the
/// Phase-3 surrogate that overloaded <see cref="TaskItem"/> as the
/// instance pointer in <see cref="ProductWorkflowMapping"/>.
///
/// LS-FLOW-MERGE-P5 — promoted to the execution authority. Owns
/// <see cref="CurrentStageId"/> + <see cref="CurrentStepKey"/> (driven by
/// <see cref="WorkflowStage"/> + <see cref="WorkflowTransition"/>) and
/// lifecycle timestamps. The <see cref="WorkflowEngine"/> mutates these
/// under optimistic concurrency (caller must pass the expected current
/// step key on advance).
/// </summary>
public class WorkflowInstance : AuditableEntity
{
    /// <summary>Definition this instance was started from.</summary>
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>Product this instance belongs to (mirrors definition's ProductKey).</summary>
    public string ProductKey { get; set; } = ProductKeys.FlowGeneric;

    /// <summary>Free-form correlation key (external case number, etc.).</summary>
    public string? CorrelationKey { get; set; }

    /// <summary>
    /// Optional initial driving task. Today every instance is bootstrapped
    /// from one TaskItem; richer multi-task instances are a later phase.
    /// </summary>
    public Guid? InitialTaskId { get; set; }

    /// <summary>Lifecycle status — one of "Active", "Completed", "Cancelled", "Failed".</summary>
    public string Status { get; set; } = "Active";

    // ---------------- LS-FLOW-MERGE-P5 — execution state ----------------

    /// <summary>FK to the current <see cref="WorkflowStage"/> (definition's stage table).</summary>
    public Guid? CurrentStageId { get; set; }

    /// <summary>
    /// Stable string key of the current step (mirrors <c>WorkflowStage.Key</c>).
    /// Held alongside <see cref="CurrentStageId"/> so callers can pass an
    /// expected step name without resolving the stage row first.
    /// </summary>
    public string? CurrentStepKey { get; set; }

    /// <summary>UTC time the instance entered its first stage.</summary>
    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Optional user the instance is currently assigned to.</summary>
    public string? AssignedToUserId { get; set; }

    /// <summary>
    /// Last error message produced by an attempted advance/complete/cancel
    /// (truncated). Cleared on the next successful transition.
    /// </summary>
    public string? LastErrorMessage { get; set; }

    // ---------------- LS-FLOW-E10.3 — SLA / timer state -----------------
    //
    // Persisted SLA columns. Kept on the workflow grain (single UPDATE
    // covers both execution AND SLA mutations) rather than a side table.
    //
    // The evaluator (WorkflowSlaEvaluator) is the only writer; the
    // engine sets DueAt + initial SlaStatus at start time. All evaluator
    // writes are gated on (computed != persisted) so repeated polling
    // never re-emits the same transition — see report §"Timer Evaluator
    // Notes" / §"Outbox / Event Notes" for the full idempotency story.

    /// <summary>
    /// UTC deadline for the workflow. Null means "no SLA configured" —
    /// the evaluator skips these instances entirely. Assigned by
    /// <see cref="Application.Engines.WorkflowEngine.WorkflowEngine.StartAsync"/>
    /// using the precedence documented in the E10.3 report.
    /// </summary>
    public DateTime? DueAt { get; set; }

    /// <summary>
    /// One of <see cref="Domain.Common.WorkflowSlaStatus"/>: OnTrack,
    /// DueSoon, Overdue, Escalated. Default OnTrack so a freshly
    /// started instance with a future <see cref="DueAt"/> reads correctly
    /// without an evaluator pass.
    /// </summary>
    public string SlaStatus { get; set; } = Domain.Common.WorkflowSlaStatus.OnTrack;

    /// <summary>
    /// UTC time the instance first crossed into Overdue. Cleared back
    /// to null only on terminal transitions (operator/admin overrides
    /// do not roll an instance back from Overdue → OnTrack — that is a
    /// product decision, not a timer one).
    /// </summary>
    public DateTime? OverdueSince { get; set; }

    /// <summary>
    /// Discrete escalation level. 0 = not escalated. This phase only
    /// implements a single escalation step (0 → 1). Higher levels are
    /// reserved for a future phase; documented in the report.
    /// </summary>
    public int EscalationLevel { get; set; }

    /// <summary>
    /// UTC time the evaluator last looked at this instance, regardless
    /// of whether the look produced a transition. Useful for operator
    /// triage ("did the evaluator even tick recently?"). Indexed so a
    /// future evaluator could prioritise stalest-first.
    /// </summary>
    public DateTime? LastSlaEvaluatedAt { get; set; }

    public FlowDefinition? WorkflowDefinition { get; set; }
    public TaskItem? InitialTask { get; set; }
    public WorkflowStage? CurrentStage { get; set; }
}
