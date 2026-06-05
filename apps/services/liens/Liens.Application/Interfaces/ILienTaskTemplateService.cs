using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienTaskTemplateService
{
    Task<List<TaskTemplateResponse>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<TaskTemplateResponse>> GetContextualAsync(Guid tenantId, string? contextType, Guid? workflowStageId, CancellationToken ct = default);
    Task<TaskTemplateResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<TaskTemplateResponse> CreateAsync(Guid tenantId, Guid actingUserId, CreateTaskTemplateRequest request, CancellationToken ct = default);
    Task<TaskTemplateResponse> UpdateAsync(Guid tenantId, Guid id, Guid actingUserId, UpdateTaskTemplateRequest request, CancellationToken ct = default);
    Task<TaskTemplateResponse> ActivateAsync(Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateTemplateRequest request, CancellationToken ct = default);
    Task<TaskTemplateResponse> DeactivateAsync(Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateTemplateRequest request, CancellationToken ct = default);

    /// <summary>
    /// TASK-MIG-02 — Dual-read lookup for use in the generation engine.
    /// Tries Task service first; falls back to Liens DB if not found or unavailable.
    /// Does NOT auto-create. Returns null if not found in either system.
    /// </summary>
    Task<TaskTemplateResponse?> GetForGenerationAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
