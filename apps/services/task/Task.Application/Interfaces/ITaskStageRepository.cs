using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskStageRepository
{
    System.Threading.Tasks.Task<TaskStageConfig?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskStageConfig?> GetByIdAnyTenantAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<IReadOnlyList<TaskStageConfig>> GetByTenantAsync(Guid tenantId, string? sourceProductCode = null, bool activeOnly = true, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(TaskStageConfig stage, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(TaskStageConfig stage, CancellationToken ct = default);
}
