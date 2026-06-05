using Task.Domain.Entities;

namespace Task.Application.Repositories;

public interface ITaskStageTransitionRepository
{
    System.Threading.Tasks.Task<List<TaskStageTransition>> GetActiveByTenantProductAsync(
        Guid tenantId, string productCode, CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskStageTransition?> GetByTenantProductStagesAsync(
        Guid tenantId, string productCode, Guid fromStageId, Guid toStageId, CancellationToken ct = default);

    System.Threading.Tasks.Task AddAsync(TaskStageTransition transition, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(TaskStageTransition transition, CancellationToken ct = default);
    System.Threading.Tasks.Task AddRangeAsync(IEnumerable<TaskStageTransition> transitions, CancellationToken ct = default);
    System.Threading.Tasks.Task DeactivateAllAsync(Guid tenantId, string productCode, CancellationToken ct = default);
}
