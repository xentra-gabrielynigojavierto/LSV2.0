using Flow.Domain.Entities;

namespace Flow.Application.Interfaces;

/// <summary>
/// LS-FLOW-E11.2 — produces a <see cref="WorkflowTask"/> for a
/// <see cref="WorkflowInstance"/>'s current step when the instance is
/// in an actionable state and no live (Open / InProgress) task already
/// exists for that <c>(WorkflowInstanceId, StepKey)</c> pair.
///
/// <para>
/// Foundational contract for the work-item layer:
///   <list type="bullet">
///     <item>The factory <b>does not</b> call <c>SaveChangesAsync</c>.
///       The task entity is staged on the shared <c>IFlowDbContext</c>
///       so it commits in the same EF unit-of-work as the workflow
///       state mutation that triggered it. If the workflow transition
///       fails to save, the task is never persisted.</item>
///     <item>The factory <b>does not</b> drive workflow execution.
///       <see cref="WorkflowInstance"/> remains the sole execution
///       authority. The factory is a pure derivation from observed
///       workflow state.</item>
///     <item>The factory <b>does not</b> raise outbox events, audit
///       events, or notifications.</item>
///   </list>
/// </para>
///
/// <para>
/// Eligibility / dedup rules and exact integration points are
/// documented in <c>analysis/E11.2-report.md</c>.
/// </para>
/// </summary>
public interface IWorkflowTaskFromWorkflowFactory
{
    /// <summary>
    /// Stages a new <see cref="WorkflowTask"/> on the shared
    /// <c>IFlowDbContext</c> for <paramref name="instance"/>'s current
    /// step iff:
    ///   <list type="number">
    ///     <item><paramref name="instance"/>.<see cref="WorkflowInstance.Status"/>
    ///       is <c>Active</c> (terminal states never produce a task).</item>
    ///     <item><see cref="WorkflowInstance.CurrentStepKey"/> is set.</item>
    ///     <item>No Open / InProgress <see cref="WorkflowTask"/> already
    ///       exists for <c>(instance.Id, instance.CurrentStepKey)</c>,
    ///       neither in the change tracker nor committed in the DB.</item>
    ///   </list>
    /// Returns the staged task, or <c>null</c> when any rule above
    /// short-circuits creation.
    /// </summary>
    Task<WorkflowTask?> EnsureForCurrentStepAsync(
        WorkflowInstance instance,
        CancellationToken cancellationToken = default);
}
