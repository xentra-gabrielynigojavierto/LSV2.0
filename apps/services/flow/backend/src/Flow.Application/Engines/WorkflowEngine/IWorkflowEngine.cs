using Flow.Application.DTOs;

namespace Flow.Application.Engines.WorkflowEngine;

/// <summary>
/// LS-FLOW-MERGE-P5 — execution authority for <see cref="Flow.Domain.Entities.WorkflowInstance"/>.
///
/// <para>
/// All transitions go through this engine; product code never mutates
/// instance state directly. Optimistic concurrency is enforced via the
/// <c>expectedCurrentStepKey</c> argument on <see cref="AdvanceAsync"/> —
/// a mismatch throws <see cref="Exceptions.InvalidWorkflowTransitionException"/>
/// which the API maps to HTTP 409.
/// </para>
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Position a freshly created instance on its definition's initial
    /// stage. Sets <c>CurrentStageId</c>, <c>CurrentStepKey</c>,
    /// <c>StartedAt</c>, and ensures <c>Status="Active"</c>.
    /// Idempotent — calling twice on an already-started instance is a
    /// no-op.
    /// </summary>
    Task<WorkflowInstanceResponse> StartAsync(Guid workflowInstanceId, CancellationToken ct = default);

    /// <summary>
    /// Move the instance from its current stage to the next reachable
    /// stage via an active <see cref="Flow.Domain.Entities.WorkflowTransition"/>.
    /// If <paramref name="toStepKey"/> is supplied, the transition must
    /// target that exact stage; otherwise the unique outbound transition
    /// is selected (ambiguity → 409).
    /// </summary>
    Task<WorkflowInstanceResponse> AdvanceAsync(
        Guid workflowInstanceId,
        string expectedCurrentStepKey,
        string? toStepKey,
        CancellationToken ct = default);

    /// <summary>Terminal-complete the instance.</summary>
    Task<WorkflowInstanceResponse> CompleteAsync(Guid workflowInstanceId, CancellationToken ct = default);

    /// <summary>Cancel the instance. Reason recorded on <c>LastErrorMessage</c> if provided.</summary>
    Task<WorkflowInstanceResponse> CancelAsync(Guid workflowInstanceId, string? reason, CancellationToken ct = default);

    /// <summary>Mark the instance as failed and record the error.</summary>
    Task<WorkflowInstanceResponse> FailAsync(Guid workflowInstanceId, string errorMessage, CancellationToken ct = default);
}
