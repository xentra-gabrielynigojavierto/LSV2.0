using Flow.Application.DTOs;
using Flow.Application.Engines.WorkflowEngine;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E11.7 — default implementation of
/// <see cref="IWorkflowTaskCompletionService"/>.
///
/// <para>
/// <b>Orchestration shape</b>:
/// </para>
/// <list type="number">
///   <item>Pre-load the task from the Task service: existence check,
///         <c>StepKey</c>, <c>WorkflowInstanceId</c>, current
///         <c>Status</c>. A non-existent task surfaces as
///         <see cref="NotFoundException"/>; a task already in a terminal
///         state — or still <c>Open</c> — surfaces as
///         <see cref="InvalidStateTransitionException"/>.</item>
///   <item>Open a transaction through the database's
///         <see cref="Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy"/>
///         for the engine advance (the engine writes to Flow DB).</item>
///   <item>Run the task lifecycle via
///         <see cref="IWorkflowTaskLifecycleService.CompleteTaskAsync"/>
///         which delegates to the Task service (TASK-FLOW-03: no shadow
///         CAS — Task service is the sole write authority).</item>
///   <item>Run the workflow advance via
///         <see cref="IWorkflowEngine.AdvanceAsync"/>.</item>
///   <item>Commit.</item>
/// </list>
///
/// <para>
/// <b>TASK-FLOW-03 (post-migration):</b> the shadow table
/// (<c>flow_workflow_tasks</c>) has been dropped. Phase-1 pre-load uses
/// <see cref="IFlowTaskServiceClient.GetTaskByIdAsync"/>. The lifecycle
/// service call (<c>CompleteTaskAsync</c>) no longer performs a shadow
/// CAS; the DB execution strategy wrapper exists solely for the engine's
/// Flow DB writes. The TaskService HTTP call inside the lifecycle service
/// is therefore not bounded by the Flow DB transaction (an expected
/// trade-off documented in TASK-FLOW-01 §"Inconsistency window").
/// </para>
/// </summary>
public sealed class WorkflowTaskCompletionService : IWorkflowTaskCompletionService
{
    private readonly IFlowDbContext _db;
    private readonly IFlowTaskServiceClient _taskClient;
    private readonly IWorkflowTaskLifecycleService _lifecycle;
    private readonly IWorkflowEngine _engine;
    private readonly ILogger<WorkflowTaskCompletionService> _log;

    public WorkflowTaskCompletionService(
        IFlowDbContext db,
        IFlowTaskServiceClient taskClient,
        IWorkflowTaskLifecycleService lifecycle,
        IWorkflowEngine engine,
        ILogger<WorkflowTaskCompletionService> log)
    {
        _db         = db;
        _taskClient = taskClient;
        _lifecycle  = lifecycle;
        _engine     = engine;
        _log        = log;
    }

    public async Task<WorkflowTaskCompletionResult> CompleteAndProgressAsync(
        Guid taskId, CancellationToken ct = default)
    {
        // ---- Phase 1: cheap pre-validation outside any transaction ----
        // Read the task from the Task service (write authority, post-TASK-FLOW-03).
        // Status is normalised from Task service UPPERCASE to Flow PascalCase
        // inside the lifecycle service; we do a local normalise here too so
        // the pre-check and the lifecycle check use the same vocabulary.
        var taskDto = await _taskClient.GetTaskByIdAsync(taskId, ct);

        if (taskDto is null)
        {
            // Includes both "doesn't exist" and access-denied — surfaced
            // identically to prevent cross-tenant id probing.
            throw new NotFoundException("WorkflowTask", taskId);
        }

        var normalizedStatus = NormalizeStatus(taskDto.Status);

        if (!string.Equals(normalizedStatus, WorkflowTaskStatus.InProgress, StringComparison.Ordinal))
        {
            throw new InvalidStateTransitionException(normalizedStatus, WorkflowTaskStatus.Completed);
        }

        var workflowInstanceId = taskDto.WorkflowInstanceId ?? Guid.Empty;
        var stepKey            = taskDto.WorkflowStepKey ?? string.Empty;

        if (string.IsNullOrWhiteSpace(stepKey))
        {
            throw new ValidationException(
                $"WorkflowTask {taskId} has no StepKey; cannot bind to workflow progression.");
        }

        // ---- Phase 2: atomic engine advance (inside EF execution strategy) ----
        //
        // The lifecycle.CompleteTaskAsync call below calls the Task service
        // (HTTP). That HTTP call is not bounded by the Flow DB transaction —
        // an expected trade-off: if the DB transaction rolls back after the
        // Task service write, the task is Completed in Task service but the
        // workflow has not advanced. The caller may safely retry because the
        // Task service transition is idempotent for terminal states, and the
        // engine's step-match check prevents double-advance.

        var strategy = _db.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.BeginTransactionAsync(ct);

            // Task service complete (primary write authority, TASK-FLOW-03).
            // If another caller already completed/cancelled the task between
            // phase-1 read and here, the Task service returns 409, which the
            // lifecycle service wraps as WorkflowTaskConcurrencyException ⇒ 409.
            var taskResult = await _lifecycle.CompleteTaskAsync(taskId, ct);

            // Drive the workflow engine. The engine performs its own guards:
            //   - workflow must be Active               ⇒ 409
            //   - workflow.CurrentStepKey must match    ⇒ 409 (stale)
            //   - exactly one outbound transition       ⇒ 409 if ambiguous
            // Any failure rolls back the transaction (but not the Task
            // service write which already committed — see comment above).
            WorkflowInstanceResponse engineResult;
            try
            {
                engineResult = await _engine.AdvanceAsync(
                    workflowInstanceId:     workflowInstanceId,
                    expectedCurrentStepKey: stepKey,
                    toStepKey:              null,
                    ct:                     ct);
            }
            catch (NotFoundException)
            {
                throw new InvalidWorkflowTransitionException(
                    $"Owning workflow instance {workflowInstanceId} for task {taskId} could not be loaded.",
                    "owning_workflow_missing");
            }

            await tx.CommitAsync(ct);

            _log.LogInformation(
                "WorkflowTaskCompletion bound task {TaskId} → workflow {InstanceId} advance ({From} → {To}, status={WfStatus})",
                taskId, workflowInstanceId,
                stepKey, engineResult.CurrentStepKey, engineResult.Status);

            return new WorkflowTaskCompletionResult(
                TaskId:             taskId,
                PreviousStatus:     taskResult.PreviousStatus,
                NewStatus:          taskResult.NewStatus,
                WorkflowInstanceId: workflowInstanceId,
                FromStepKey:        stepKey,
                ToStepKey:          engineResult.CurrentStepKey ?? stepKey,
                WorkflowStatus:     engineResult.Status,
                WorkflowAdvanced:   true,
                TransitionedAtUtc:  taskResult.TransitionedAtUtc);
        });
    }

    /// <summary>
    /// Maps Task service UPPERCASE status strings to Flow's PascalCase
    /// <see cref="WorkflowTaskStatus"/> constants.
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
