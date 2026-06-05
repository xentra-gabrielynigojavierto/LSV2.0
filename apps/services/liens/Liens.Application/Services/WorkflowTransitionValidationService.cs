using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// LS-LIENS-FLOW-005 — My Tasks stage-transition validation service.
///
/// Architectural boundary:
///   - This service governs task-stage movement within the My Tasks module only.
///   - It does NOT govern case or lien workflow instance transitions.
///   - Case/lien workflow execution is owned by the Flow service (IFlowClient).
///     Flow manages WorkflowInstances via StartWorkflow / AdvanceWorkflow / CompleteWorkflow.
///     That logic lives in WorkflowEndpoints.cs and is entirely separate from this service.
///
/// TASK-MIG-04 — Dual-read: Task service first, Liens DB fallback.
///
/// Transitional note (LS-LIENS-FLOW-007):
///   In LS-LIENS-FLOW-007, this service will be extended to optionally accept a
///   Flow instance context (flowInstanceId) so task-stage validation can be correlated
///   with the active Flow workflow state for the task's linked case.
/// </summary>
public sealed class WorkflowTransitionValidationService : IWorkflowTransitionValidationService
{
    private const string ProductCode = "SYNQ_LIENS";

    private readonly ILienWorkflowConfigRepository                  _repo;
    private readonly ILiensTaskServiceClient                        _taskClient;
    private readonly ILogger<WorkflowTransitionValidationService>   _logger;

    public WorkflowTransitionValidationService(
        ILienWorkflowConfigRepository               repo,
        ILiensTaskServiceClient                     taskClient,
        ILogger<WorkflowTransitionValidationService> logger)
    {
        _repo       = repo;
        _taskClient = taskClient;
        _logger     = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsTransitionAllowedAsync(
        Guid tenantId,
        Guid workflowConfigId,
        Guid fromStageId,
        Guid toStageId,
        CancellationToken ct = default)
    {
        if (fromStageId == toStageId) return false;

        var transitions = await GetTransitionsDualReadAsync(tenantId, workflowConfigId, ct);

        // Open-move mode: no transitions configured → allow any task-stage movement.
        if (transitions.Count == 0) return true;

        // Strict mode: only explicitly allowed from→to pairs pass.
        // LS-LIENS-FLOW-007: future seam — optionally validate against Flow instance state here.
        return transitions.Any(t => t.FromStageId == fromStageId && t.ToStageId == toStageId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetAllowedNextStagesAsync(
        Guid tenantId,
        Guid workflowConfigId,
        Guid fromStageId,
        CancellationToken ct = default)
    {
        var transitions = await GetTransitionsDualReadAsync(tenantId, workflowConfigId, ct);
        return transitions
            .Where(t => t.FromStageId == fromStageId)
            .Select(t => t.ToStageId)
            .ToList();
    }

    // ── Private dual-read helper ──────────────────────────────────────────────────

    private async Task<List<TransitionTuple>> GetTransitionsDualReadAsync(
        Guid tenantId,
        Guid workflowConfigId,
        CancellationToken ct)
    {
        try
        {
            var dtos = await _taskClient.GetTransitionsAsync(tenantId, ProductCode, ct);
            if (dtos.Count > 0)
            {
                _logger.LogDebug(
                    "transition_source=task_service TenantId={TenantId} Count={Count}",
                    tenantId, dtos.Count);
                return dtos.Select(d => new TransitionTuple(d.FromStageId, d.ToStageId)).ToList();
            }

            _logger.LogDebug(
                "transition_source=liens_db_fallback_empty TenantId={TenantId}",
                tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "transition_source=liens_db_fallback_error TenantId={TenantId}; using Liens DB.",
                tenantId);
        }

        // Liens DB fallback
        var liens = await _repo.GetActiveTransitionsAsync(workflowConfigId, ct);
        return liens.Select(t => new TransitionTuple(t.FromStageId, t.ToStageId)).ToList();
    }

    private readonly record struct TransitionTuple(Guid FromStageId, Guid ToStageId);
}
