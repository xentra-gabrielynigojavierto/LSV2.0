using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienWorkflowConfigRepository
{
    Task<LienWorkflowConfig?> GetByTenantProductAsync(Guid tenantId, string productCode, CancellationToken ct = default);
    Task<LienWorkflowConfig?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<LienWorkflowStage?> GetStageByIdAsync(Guid configId, Guid stageId, CancellationToken ct = default);

    /// <summary>Look up a stage without knowing its workflow config (used for runtime task validation).</summary>
    Task<LienWorkflowStage?> GetStageGlobalAsync(Guid stageId, CancellationToken ct = default);

    /// <summary>Full scan of all workflow configs across all tenants (used for startup sync).</summary>
    Task<List<LienWorkflowConfig>> GetAllConfigsAsync(CancellationToken ct = default);
    Task AddAsync(LienWorkflowConfig entity, CancellationToken ct = default);
    Task UpdateAsync(LienWorkflowConfig entity, CancellationToken ct = default);
    Task AddStageAsync(LienWorkflowStage stage, CancellationToken ct = default);
    Task UpdateStageAsync(LienWorkflowStage stage, CancellationToken ct = default);
    Task RemoveStageAsync(LienWorkflowStage stage, CancellationToken ct = default);

    // ── Transition methods (LS-LIENS-FLOW-005) ────────────────────────────────
    Task<List<LienWorkflowTransition>> GetActiveTransitionsAsync(Guid workflowConfigId, CancellationToken ct = default);
    Task<List<LienWorkflowTransition>> GetAllTransitionsAsync(Guid workflowConfigId, CancellationToken ct = default);
    Task<LienWorkflowTransition?> GetTransitionByIdAsync(Guid workflowConfigId, Guid transitionId, CancellationToken ct = default);
    Task<bool> TransitionExistsAsync(Guid workflowConfigId, Guid fromStageId, Guid toStageId, CancellationToken ct = default);
    Task AddTransitionAsync(LienWorkflowTransition transition, CancellationToken ct = default);
    Task UpdateTransitionAsync(LienWorkflowTransition transition, CancellationToken ct = default);
    Task AddTransitionsAsync(IEnumerable<LienWorkflowTransition> transitions, CancellationToken ct = default);
    Task DeactivateAllTransitionsAsync(Guid workflowConfigId, CancellationToken ct = default);
}
