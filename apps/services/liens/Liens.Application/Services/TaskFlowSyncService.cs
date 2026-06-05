using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// LS-LIENS-FLOW-009 — reusable task ↔ Flow step-key synchronization service.
///
/// Centralises the sync logic so that:
/// - FLOW-009 event-driven sync can call it
/// - FLOW-008 read-time / stage-mapping sync can call it once implemented
///
/// The service is idempotent: when <see cref="LienTask.WorkflowStepKey"/> already matches
/// the incoming <paramref name="newStepKey"/>, no write is issued and
/// <see cref="SyncOutcome.NoOp"/> is returned.
///
/// FLOW-008 hook: when stage mapping is implemented, this service can be extended to also
/// resolve <c>WorkflowStageId</c> from <paramref name="newStepKey"/> via the stage config;
/// no architectural change is required.
/// </summary>
public sealed class TaskFlowSyncService : ITaskFlowSyncService
{
    private readonly ILienTaskRepository         _taskRepo;
    private readonly ILogger<TaskFlowSyncService> _logger;

    public TaskFlowSyncService(ILienTaskRepository taskRepo, ILogger<TaskFlowSyncService> logger)
    {
        _taskRepo = taskRepo;
        _logger   = logger;
    }

    public async Task<SyncOutcome> SyncAsync(
        LienTask task,
        string   newStepKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newStepKey))
            return SyncOutcome.NoOp;

        var trimmed = newStepKey.Trim();

        // Idempotency check: already aligned → no-op
        if (string.Equals(task.WorkflowStepKey, trimmed, StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "TaskFlowSyncService: Task {TaskId} already at step '{StepKey}' — no-op.",
                task.Id, trimmed);
            return SyncOutcome.NoOp;
        }

        var previousStepKey = task.WorkflowStepKey;

        task.SyncWorkflowStep(trimmed);
        await _taskRepo.UpdateAsync(task, ct);

        _logger.LogInformation(
            "TaskFlowSyncService: Task {TaskId} step synced '{Prev}' → '{New}'.",
            task.Id, previousStepKey ?? "(null)", trimmed);

        return SyncOutcome.Synced;
    }
}
