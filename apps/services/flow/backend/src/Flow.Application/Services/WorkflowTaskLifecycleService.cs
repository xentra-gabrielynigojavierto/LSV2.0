using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E11.4 — default implementation of
/// <see cref="IWorkflowTaskLifecycleService"/>.
///
/// <para>
/// <b>TASK-FLOW-03 (post-migration):</b> the shadow table
/// (<c>flow_workflow_tasks</c>) has been dropped. All status reads and
/// writes are now delegated exclusively to the Task service.
/// <c>ReadCurrentStatusAsync</c> calls <see cref="IFlowTaskServiceClient.GetTaskByIdAsync"/>
/// and normalises the Task service's UPPERCASE status strings to Flow's
/// PascalCase constants (<see cref="WorkflowTaskStatus"/>). The
/// compare-and-swap is gone; the Task service's own status-transition
/// validation (idempotent for terminal states, 409 for invalid
/// transitions) is the concurrency primitive. Any HTTP 409 from the
/// Task service is re-thrown as
/// <see cref="WorkflowTaskConcurrencyException"/> to preserve the
/// existing API contract.
/// </para>
/// </summary>
public sealed class WorkflowTaskLifecycleService : IWorkflowTaskLifecycleService
{
    private readonly IFlowTaskServiceClient _taskClient;
    private readonly ILogger<WorkflowTaskLifecycleService> _log;

    public WorkflowTaskLifecycleService(
        IFlowTaskServiceClient taskClient,
        ILogger<WorkflowTaskLifecycleService> log)
    {
        _taskClient = taskClient;
        _log        = log;
    }

    public Task<WorkflowTaskTransitionResult> StartTaskAsync(Guid taskId, CancellationToken ct = default) =>
        TransitionAsync(
            taskId,
            expectedStatus:       WorkflowTaskStatus.Open,
            newStatus:            WorkflowTaskStatus.InProgress,
            taskServiceDelegate:  id => _taskClient.StartTaskAsync(id, ct),
            ct);

    public Task<WorkflowTaskTransitionResult> CompleteTaskAsync(Guid taskId, CancellationToken ct = default) =>
        TransitionAsync(
            taskId,
            expectedStatus:       WorkflowTaskStatus.InProgress,
            newStatus:            WorkflowTaskStatus.Completed,
            taskServiceDelegate:  id => _taskClient.CompleteTaskAsync(id, ct),
            ct);

    public async Task<WorkflowTaskTransitionResult> CancelTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        // Cancel accepts two source states (Open and InProgress).
        // We read first to give a precise error (invalid-state vs.
        // not-found) and to know which source state to echo back in
        // WorkflowTaskTransitionResult.PreviousStatus.
        var current = await ReadCurrentStatusAsync(taskId, ct);
        if (current is not (WorkflowTaskStatus.Open or WorkflowTaskStatus.InProgress))
        {
            throw new InvalidStateTransitionException(current, WorkflowTaskStatus.Cancelled);
        }

        await DelegateToTaskServiceAsync(taskId, id => _taskClient.CancelTaskAsync(id, ct), "Cancel");

        var now = DateTime.UtcNow;
        _log.LogInformation(
            "WorkflowTask lifecycle transition: TaskId={TaskId} {From}→{To}",
            taskId, current, WorkflowTaskStatus.Cancelled);
        return new WorkflowTaskTransitionResult(taskId, current, WorkflowTaskStatus.Cancelled, now);
    }

    // ── internals ──────────────────────────────────────────────────────────

    private async Task<WorkflowTaskTransitionResult> TransitionAsync(
        Guid              taskId,
        string            expectedStatus,
        string            newStatus,
        Func<Guid, Task>  taskServiceDelegate,
        CancellationToken ct)
    {
        var current = await ReadCurrentStatusAsync(taskId, ct);
        if (!string.Equals(current, expectedStatus, StringComparison.Ordinal))
        {
            throw new InvalidStateTransitionException(current, newStatus);
        }

        await DelegateToTaskServiceAsync(taskId, taskServiceDelegate, newStatus);

        var now = DateTime.UtcNow;
        _log.LogInformation(
            "WorkflowTask lifecycle transition: TaskId={TaskId} {From}→{To}",
            taskId, expectedStatus, newStatus);
        return new WorkflowTaskTransitionResult(taskId, expectedStatus, newStatus, now);
    }

    /// <summary>
    /// Delegates the lifecycle call to the Task service. A non-success
    /// HTTP response propagates as-is; an HTTP 409 is additionally
    /// wrapped as <see cref="WorkflowTaskConcurrencyException"/> so
    /// callers receive the same exception type they relied on when the
    /// shadow CAS existed.
    /// </summary>
    private async Task DelegateToTaskServiceAsync(
        Guid              taskId,
        Func<Guid, Task>  delegate_,
        string            operationLabel)
    {
        try
        {
            await delegate_(taskId);
        }
        catch (HttpRequestException ex) when (
            ex.Message.Contains("409", StringComparison.Ordinal))
        {
            _log.LogWarning(
                "WorkflowTaskLifecycleService: Task service returned 409 for task {TaskId} op={Op}. " +
                "Re-throwing as WorkflowTaskConcurrencyException.",
                taskId, operationLabel);
            throw new WorkflowTaskConcurrencyException(taskId, operationLabel);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "WorkflowTaskLifecycleService: Task service call FAILED for task {TaskId} op={Op}. " +
                "Propagating error.",
                taskId, operationLabel);
            throw;
        }
    }

    /// <summary>
    /// Reads and normalises the current status from the Task service.
    /// Returns Flow PascalCase status on success; throws
    /// <see cref="NotFoundException"/> when the task is not found.
    /// </summary>
    private async Task<string> ReadCurrentStatusAsync(Guid taskId, CancellationToken ct)
    {
        var taskDto = await _taskClient.GetTaskByIdAsync(taskId, ct);
        if (taskDto is null)
        {
            throw new NotFoundException(nameof(WorkflowTask), taskId);
        }
        return NormalizeStatus(taskDto.Status);
    }

    /// <summary>
    /// Maps Task service UPPERCASE status strings to Flow's PascalCase
    /// <see cref="WorkflowTaskStatus"/> constants. Unknown values are
    /// passed through unchanged so downstream validation (invalid-state
    /// checks) can produce a meaningful error.
    /// </summary>
    private static string NormalizeStatus(string tsStatus) =>
        tsStatus switch
        {
            "OPEN"        => WorkflowTaskStatus.Open,
            "IN_PROGRESS" => WorkflowTaskStatus.InProgress,
            "COMPLETED"   => WorkflowTaskStatus.Completed,
            "CANCELLED"   => WorkflowTaskStatus.Cancelled,
            _             => tsStatus,
        };
}
