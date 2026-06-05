using Microsoft.EntityFrameworkCore;
using Task.Application.Interfaces;
using Task.Domain.Entities;
using Task.Domain.Enums;

namespace Task.Infrastructure.Persistence.Repositories;

public class TaskReminderRepository : ITaskReminderRepository
{
    private readonly TasksDbContext _db;
    public TaskReminderRepository(TasksDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<TaskReminder?> GetByTaskAndTypeAsync(
        Guid tenantId, Guid taskId, string reminderType, CancellationToken ct = default)
        => await _db.Reminders
            .FirstOrDefaultAsync(r => r.TenantId == tenantId
                                   && r.TaskId == taskId
                                   && r.ReminderType == reminderType, ct);

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskReminder>> GetPendingDueAsync(
        DateTime asOf, int batchSize = 100, CancellationToken ct = default)
        => await _db.Reminders
            .Where(r => r.Status == ReminderStatus.Pending && r.RemindAt <= asOf)
            .OrderBy(r => r.RemindAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskReminder>> GetByTaskAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
        => await _db.Reminders
            .Where(r => r.TenantId == tenantId && r.TaskId == taskId)
            .OrderBy(r => r.RemindAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task AddAsync(
        TaskReminder reminder, CancellationToken ct = default)
        => await _db.Reminders.AddAsync(reminder, ct);
}
