using Flow.Application.DTOs;

namespace Flow.Application.Interfaces;

/// <summary>
/// LS-FLOW-E11.7 — orchestrates the binding between a single
/// <see cref="Domain.Entities.WorkflowTask"/> completion and the owning
/// <see cref="Domain.Entities.WorkflowInstance"/> progression.
///
/// <para>
/// The two operations are executed under one EF transaction (acquired
/// through the database's retrying execution strategy) so the persisted
/// outcome is one of:
/// </para>
/// <list type="bullet">
///   <item>task <c>InProgress → Completed</c> AND workflow advanced (terminal step ⇒ workflow Completed); or</item>
///   <item>nothing was committed and the caller sees the originating exception.</item>
/// </list>
///
/// <para>
/// <b>Authority preserved:</b> this service is a thin orchestrator. It
/// delegates the task transition to <see cref="IWorkflowTaskLifecycleService"/>
/// (E11.4) and the workflow transition to
/// <see cref="Engines.WorkflowEngine.IWorkflowEngine"/> (E10/MERGE-P5).
/// Nothing here mutates <c>WorkflowInstance</c> directly; the engine
/// remains the sole execution authority.
/// </para>
///
/// <para>
/// <b>Stale / step-mismatch behaviour:</b> if the task's <c>StepKey</c>
/// no longer matches the workflow's <c>CurrentStepKey</c> (because some
/// other path already advanced the workflow, or the workflow is no
/// longer Active), the engine raises <c>InvalidWorkflowTransitionException</c>
/// which the middleware maps to <c>409 Conflict</c>. The whole transaction
/// rolls back — the task stays <c>InProgress</c> — so the operator can
/// re-read and decide.
/// </para>
///
/// <para>
/// <b>Duplicate-progression protection:</b> the underlying
/// <c>InProgress → Completed</c> CAS guarantees only one concurrent
/// caller crosses the lifecycle boundary; the loser receives
/// <c>WorkflowTaskConcurrencyException</c> ⇒ <c>409</c>. Because the
/// workflow advance is enclosed in the same transaction as the CAS,
/// the workflow can never be advanced twice for the same task.
/// </para>
/// </summary>
public interface IWorkflowTaskCompletionService
{
    /// <summary>
    /// Completes <paramref name="taskId"/> and, if the task corresponds to
    /// the owning workflow's current active step, advances the workflow
    /// through the engine.
    /// </summary>
    /// <returns>
    /// The workflow advancement outcome:
    /// <see cref="WorkflowTaskCompletionResult.WorkflowAdvanced"/> is
    /// <c>true</c> on every success path of this service (because the
    /// service does not commit a "completed but not advanced" outcome).
    /// </returns>
    Task<WorkflowTaskCompletionResult> CompleteAndProgressAsync(Guid taskId, CancellationToken ct = default);
}

/// <summary>
/// LS-FLOW-E11.7 — outcome of a successful
/// <see cref="IWorkflowTaskCompletionService.CompleteAndProgressAsync"/>.
/// Mirrors the lifecycle service's
/// <see cref="WorkflowTaskTransitionResult"/> shape and adds the
/// resulting workflow snapshot for callers that want to refresh their
/// UI in one round-trip.
/// </summary>
/// <param name="TaskId">Task that was transitioned to <c>Completed</c>.</param>
/// <param name="PreviousStatus">
/// Task status BEFORE the call. Always <c>InProgress</c> on a successful
/// return (kept for parity with the pre-E11.7
/// <see cref="WorkflowTaskTransitionResult"/> response shape so existing
/// clients reading this field do not break).
/// </param>
/// <param name="NewStatus">
/// Task status AFTER the call. Always <c>Completed</c> on a successful
/// return. Same backward-compat rationale as <see cref="PreviousStatus"/>.
/// </param>
/// <param name="WorkflowInstanceId">Owning workflow instance.</param>
/// <param name="FromStepKey">Step the workflow was on before the advance.</param>
/// <param name="ToStepKey">Step the workflow is on after the advance.</param>
/// <param name="WorkflowStatus">Resulting workflow status (<c>Active</c> or <c>Completed</c>).</param>
/// <param name="WorkflowAdvanced">
/// Always <c>true</c> on a successful return (kept as an explicit field so
/// future "complete-without-advance" branches stay backward-compatible
/// without breaking the contract).
/// </param>
/// <param name="TransitionedAtUtc">UTC instant the task transition was applied.</param>
public sealed record WorkflowTaskCompletionResult(
    Guid TaskId,
    string PreviousStatus,
    string NewStatus,
    Guid WorkflowInstanceId,
    string FromStepKey,
    string ToStepKey,
    string WorkflowStatus,
    bool WorkflowAdvanced,
    DateTime TransitionedAtUtc);
