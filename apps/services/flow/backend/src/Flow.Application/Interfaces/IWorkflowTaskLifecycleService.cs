using Flow.Domain.Entities;

namespace Flow.Application.Interfaces;

/// <summary>
/// LS-FLOW-E11.4 — applies lifecycle transitions
/// (<c>Open → InProgress</c>, <c>InProgress → Completed</c>,
/// <c>Open → Cancelled</c>, <c>InProgress → Cancelled</c>) to an
/// existing <see cref="WorkflowTask"/>.
///
/// <para>
/// The service is intentionally NARROW: it only changes status and the
/// matching lifecycle timestamp on a single task. It does NOT mutate the
/// owning <see cref="WorkflowInstance"/>, advance the workflow engine,
/// touch the outbox, or re-evaluate SLA. Workflow-progression-on-task-
/// completion is reserved for a later phase (E11.7).
/// </para>
///
/// <para>
/// All transitions are persisted via an atomic compare-and-swap (the
/// expected source <c>Status</c> is included in the SQL <c>WHERE</c>
/// clause), so under concurrent invocation exactly one transition
/// succeeds and the loser sees a
/// <see cref="Exceptions.WorkflowTaskConcurrencyException"/>.
/// </para>
///
/// <para>
/// Tenant scoping: the same <c>HasQueryFilter</c> that protects every
/// other Flow grain applies to the underlying <c>IQueryable</c>, so a
/// caller in tenant <c>A</c> cannot transition a task owned by tenant
/// <c>B</c> — such an attempt surfaces as
/// <see cref="Exceptions.NotFoundException"/>, identical to a missing
/// task.
/// </para>
///
/// <para>
/// Assignment fields (<see cref="WorkflowTask.AssignedUserId"/>,
/// <see cref="WorkflowTask.AssignedRole"/>,
/// <see cref="WorkflowTask.AssignedOrgId"/>) are NEVER modified by this
/// service. Reassignment is a separate concern and is out of scope for
/// E11.4.
/// </para>
/// </summary>
public interface IWorkflowTaskLifecycleService
{
    /// <summary>
    /// Move a task from <c>Open</c> to <c>InProgress</c>. Sets
    /// <see cref="WorkflowTask.StartedAt"/> on the first start (idempotent
    /// re-invocation while the task is already <c>InProgress</c> is NOT
    /// allowed and surfaces as
    /// <see cref="Exceptions.InvalidStateTransitionException"/>).
    /// </summary>
    Task<WorkflowTaskTransitionResult> StartTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Move a task from <c>InProgress</c> to <c>Completed</c>. Sets
    /// <see cref="WorkflowTask.CompletedAt"/>. Cannot be called from
    /// <c>Open</c> (must start first) or from a terminal state.
    /// </summary>
    Task<WorkflowTaskTransitionResult> CompleteTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Move a task from <c>Open</c> or <c>InProgress</c> to <c>Cancelled</c>.
    /// Sets <see cref="WorkflowTask.CancelledAt"/>. Cannot be called from
    /// a terminal state.
    /// </summary>
    Task<WorkflowTaskTransitionResult> CancelTaskAsync(Guid taskId, CancellationToken ct = default);
}

/// <summary>
/// LS-FLOW-E11.4 — outcome of a successful lifecycle transition. Returned
/// to callers that want to react to the new state without a second
/// round-trip. The record is immutable and carries only fields the
/// service has authority over (no workflow-instance state).
/// </summary>
/// <param name="TaskId">Id of the task that was transitioned.</param>
/// <param name="PreviousStatus">Status before the call (e.g. <c>Open</c>).</param>
/// <param name="NewStatus">Status after the call (e.g. <c>InProgress</c>).</param>
/// <param name="TransitionedAtUtc">UTC instant the transition was applied.</param>
public sealed record WorkflowTaskTransitionResult(
    Guid TaskId,
    string PreviousStatus,
    string NewStatus,
    DateTime TransitionedAtUtc);
