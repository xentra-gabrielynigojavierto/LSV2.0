using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskNoteRepository
{
    System.Threading.Tasks.Task<IReadOnlyList<TaskNote>> GetByTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskNote?> GetByIdAsync(Guid tenantId, Guid noteId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(TaskNote note, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(TaskNote note, CancellationToken ct = default);
}
