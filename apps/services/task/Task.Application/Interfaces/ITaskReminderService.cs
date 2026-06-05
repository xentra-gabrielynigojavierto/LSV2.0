using Task.Application.DTOs;

namespace Task.Application.Interfaces;

public interface ITaskReminderService
{
    /// <summary>
    /// Creates or reschedules DUE_SOON and OVERDUE reminders for the given task.
    /// Called after create/update when DueAt changes.
    /// </summary>
    System.Threading.Tasks.Task SyncRemindersAsync(Guid tenantId, Guid taskId, DateTime? dueAt, CancellationToken ct = default);

    /// <summary>Cancels all PENDING reminders for a task (called when task is closed or due date removed).</summary>
    System.Threading.Tasks.Task CancelRemindersAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Processes due PENDING reminders up to <paramref name="batchSize"/>.
    /// Returns a summary of the processing run.
    /// </summary>
    System.Threading.Tasks.Task<ReminderProcessResult> ProcessDueRemindersAsync(int batchSize = 100, CancellationToken ct = default);
}
