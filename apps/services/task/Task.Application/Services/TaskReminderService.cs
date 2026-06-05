using Task.Application.DTOs;
using Task.Application.Interfaces;
using Task.Domain.Entities;
using Task.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Task.Application.Services;

public class TaskReminderService : ITaskReminderService
{
    private const int DueSoonHoursBeforeDue = 48;

    private readonly ITaskReminderRepository       _reminders;
    private readonly ITaskRepository               _tasks;
    private readonly ITaskHistoryRepository        _history;
    private readonly ITaskNotificationClient       _notifications;
    private readonly IUnitOfWork                   _uow;
    private readonly ILogger<TaskReminderService>  _logger;

    public TaskReminderService(
        ITaskReminderRepository      reminders,
        ITaskRepository              tasks,
        ITaskHistoryRepository       history,
        ITaskNotificationClient      notifications,
        IUnitOfWork                  uow,
        ILogger<TaskReminderService> logger)
    {
        _reminders     = reminders;
        _tasks         = tasks;
        _history       = history;
        _notifications = notifications;
        _uow           = uow;
        _logger        = logger;
    }

    public async System.Threading.Tasks.Task SyncRemindersAsync(
        Guid tenantId, Guid taskId, DateTime? dueAt, CancellationToken ct = default)
    {
        if (dueAt is null)
        {
            await CancelRemindersAsync(tenantId, taskId, ct);
            return;
        }

        var dueSoonAt = dueAt.Value.AddHours(-DueSoonHoursBeforeDue);
        var overdueAt = dueAt.Value;

        await SyncReminderAsync(tenantId, taskId, ReminderType.DueSoon, dueSoonAt, ct);
        await SyncReminderAsync(tenantId, taskId, ReminderType.Overdue, overdueAt, ct);

        await _uow.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task CancelRemindersAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        foreach (var type in new[] { ReminderType.DueSoon, ReminderType.Overdue, ReminderType.Custom })
        {
            var reminder = await _reminders.GetByTaskAndTypeAsync(tenantId, taskId, type, ct);
            if (reminder is not null && reminder.Status == ReminderStatus.Pending)
                reminder.Cancel();
        }
        await _uow.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task<ReminderProcessResult> ProcessDueRemindersAsync(
        int batchSize = 100, CancellationToken ct = default)
    {
        var now       = DateTime.UtcNow;
        var reminders = await _reminders.GetPendingDueAsync(now, batchSize, ct);

        int sent = 0, failed = 0, skipped = 0;

        foreach (var reminder in reminders)
        {
            var task = await _tasks.GetByIdAsync(reminder.TenantId, reminder.TaskId, ct);
            if (task is null || IsTerminal(task.Status))
            {
                reminder.Cancel();
                skipped++;
                continue;
            }

            try
            {
                if (task.AssignedUserId.HasValue)
                {
                    await _notifications.NotifyReminderAsync(
                        reminder.TenantId, reminder.TaskId, task.Title,
                        task.AssignedUserId.Value, reminder.ReminderType,
                        task.DueAt, task.SourceProductCode, ct);
                }

                reminder.MarkSent();

                await _history.AddAsync(
                    TaskHistory.Record(task.Id, reminder.TenantId, TaskActions.ReminderSent,
                        TaskActions.SystemActorId, $"Reminder type: {reminder.ReminderType}"), ct);

                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to dispatch reminder {ReminderId} for task {TaskId}",
                    reminder.Id, reminder.TaskId);

                reminder.MarkFailed(ex.Message.Length > 500 ? ex.Message[..500] : ex.Message);
                failed++;
            }
        }

        if (reminders.Count > 0)
            await _uow.SaveChangesAsync(ct);

        var result = new ReminderProcessResult(reminders.Count, sent, failed, skipped, now);

        _logger.LogInformation(
            "Reminder processing complete: {Processed} processed, {Sent} sent, {Failed} failed, {Skipped} skipped",
            result.Processed, result.Sent, result.Failed, result.Skipped);

        return result;
    }

    private async System.Threading.Tasks.Task SyncReminderAsync(
        Guid tenantId, Guid taskId, string type, DateTime remindAt, CancellationToken ct)
    {
        var existing = await _reminders.GetByTaskAndTypeAsync(tenantId, taskId, type, ct);
        if (existing is null)
        {
            await _reminders.AddAsync(
                TaskReminder.Create(taskId, tenantId, type, remindAt), ct);
        }
        else if (existing.Status == ReminderStatus.Pending)
        {
            existing.Reschedule(remindAt);
        }
    }

    private static bool IsTerminal(string status) =>
        status == "COMPLETED" || status == "CANCELLED";
}
