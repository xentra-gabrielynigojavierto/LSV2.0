using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskReminderRepository
{
    System.Threading.Tasks.Task<TaskReminder?> GetByTaskAndTypeAsync(Guid tenantId, Guid taskId, string reminderType, CancellationToken ct = default);
    System.Threading.Tasks.Task<IReadOnlyList<TaskReminder>> GetPendingDueAsync(DateTime asOf, int batchSize = 100, CancellationToken ct = default);
    System.Threading.Tasks.Task<IReadOnlyList<TaskReminder>> GetByTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(TaskReminder reminder, CancellationToken ct = default);
}
