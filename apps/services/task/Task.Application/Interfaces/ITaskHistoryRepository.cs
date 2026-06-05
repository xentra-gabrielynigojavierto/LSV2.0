using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskHistoryRepository
{
    System.Threading.Tasks.Task<IReadOnlyList<TaskHistory>> GetByTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(TaskHistory entry, CancellationToken ct = default);
}
