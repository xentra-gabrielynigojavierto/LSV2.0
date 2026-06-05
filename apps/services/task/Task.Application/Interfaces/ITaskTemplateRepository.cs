using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskTemplateRepository
{
    System.Threading.Tasks.Task<TaskTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<IReadOnlyList<TaskTemplate>> GetByTenantAsync(Guid tenantId, string? sourceProductCode = null, bool activeOnly = true, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(TaskTemplate template, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(TaskTemplate template, CancellationToken ct = default);
}
