using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienTaskGenerationRuleService
{
    Task<List<TaskGenerationRuleResponse>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TaskGenerationRuleResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<TaskGenerationRuleResponse> CreateAsync(Guid tenantId, Guid actingUserId, CreateTaskGenerationRuleRequest request, CancellationToken ct = default);
    Task<TaskGenerationRuleResponse> UpdateAsync(Guid tenantId, Guid id, Guid actingUserId, UpdateTaskGenerationRuleRequest request, CancellationToken ct = default);
    Task<TaskGenerationRuleResponse> ActivateAsync(Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateRuleRequest request, CancellationToken ct = default);
    Task<TaskGenerationRuleResponse> DeactivateAsync(Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateRuleRequest request, CancellationToken ct = default);
}
