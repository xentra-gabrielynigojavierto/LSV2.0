namespace Liens.Application.Interfaces;

/// <summary>
/// LS-LIENS-FLOW-005 — My Tasks stage-transition validation.
///
/// This service governs task-stage movement only — it controls which workflow stages
/// a LienTask is allowed to move between inside the My Tasks module.
///
/// It does NOT govern case or lien workflow instance transitions.
/// Case/lien workflow execution is owned by the Flow service (IFlowClient) and operates
/// independently of this validation layer.
///
/// TASK-MIG-04: all reads are dual-read (Task service first, Liens DB fallback).
///
/// Transitional architecture note (LS-LIENS-FLOW-007):
/// A future version of this interface will accept an optional Flow instance context
/// so that task-stage transitions can be validated against (or enriched by) the active
/// Flow workflow instance state for the linked case.
/// </summary>
public interface IWorkflowTransitionValidationService
{
    /// <summary>
    /// Returns true if the task-stage move from <paramref name="fromStageId"/> to
    /// <paramref name="toStageId"/> is permitted by the workflow configuration.
    ///
    /// Open-move mode: when no active transitions are configured, any task-stage movement is allowed.
    /// Strict mode: when transitions are configured, only explicitly defined from→to pairs are permitted.
    ///
    /// TASK-MIG-04: uses dual-read (Task service first, Liens DB fallback).
    /// </summary>
    Task<bool> IsTransitionAllowedAsync(
        Guid tenantId,
        Guid workflowConfigId,
        Guid fromStageId,
        Guid toStageId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all task stage IDs that a task is allowed to move to from <paramref name="fromStageId"/>.
    /// Returns an empty list when in open-move mode (no transitions configured).
    ///
    /// TASK-MIG-04: uses dual-read (Task service first, Liens DB fallback).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAllowedNextStagesAsync(
        Guid tenantId,
        Guid workflowConfigId,
        Guid fromStageId,
        CancellationToken ct = default);
}
