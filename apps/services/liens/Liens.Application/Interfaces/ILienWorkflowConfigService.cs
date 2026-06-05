using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienWorkflowConfigService
{
    Task<WorkflowConfigResponse?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// TASK-MIG-03 dual-read: looks up a single stage by ID.
    /// Tries Task service first (GET /api/tasks/stages/{stageId}); falls back to Liens DB.
    /// Returns null if not found in either. Never throws — failures fall back silently.
    /// </summary>
    Task<WorkflowStageResponse?> GetStageForRuntimeAsync(Guid tenantId, Guid stageId, CancellationToken ct = default);

    Task<WorkflowConfigResponse> CreateAsync(Guid tenantId, Guid actingUserId, CreateWorkflowConfigRequest request, CancellationToken ct = default);

    Task<WorkflowConfigResponse> UpdateAsync(Guid tenantId, Guid id, Guid actingUserId, UpdateWorkflowConfigRequest request, CancellationToken ct = default);

    Task<WorkflowConfigResponse> AddStageAsync(Guid tenantId, Guid id, Guid actingUserId, AddWorkflowStageRequest request, CancellationToken ct = default);

    Task<WorkflowConfigResponse> UpdateStageAsync(Guid tenantId, Guid id, Guid stageId, Guid actingUserId, UpdateWorkflowStageRequest request, CancellationToken ct = default);

    Task<WorkflowConfigResponse> RemoveStageAsync(Guid tenantId, Guid id, Guid stageId, Guid actingUserId, CancellationToken ct = default);

    Task<WorkflowConfigResponse> ReorderStagesAsync(Guid tenantId, Guid id, Guid actingUserId, ReorderStagesRequest request, CancellationToken ct = default);

    // ── Transition methods (LS-LIENS-FLOW-005) ────────────────────────────────

    Task<IReadOnlyList<WorkflowTransitionResponse>> GetTransitionsAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<WorkflowConfigResponse> AddTransitionAsync(Guid tenantId, Guid id, Guid actingUserId, AddWorkflowTransitionRequest request, CancellationToken ct = default);

    Task<WorkflowConfigResponse> DeactivateTransitionAsync(Guid tenantId, Guid id, Guid transitionId, Guid actingUserId, CancellationToken ct = default);

    /// <summary>
    /// Batch-replace all active transitions. Deactivates unlisted ones; creates missing ones.
    /// Updates workflow version and governance metadata.
    /// </summary>
    Task<IReadOnlyList<WorkflowTransitionResponse>> SaveTransitionsAsync(Guid tenantId, Guid id, Guid actingUserId, SaveWorkflowTransitionsRequest request, CancellationToken ct = default);
}
