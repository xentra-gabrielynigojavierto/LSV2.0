namespace Task.Application.Interfaces;

public interface ITaskNotificationClient
{
    System.Threading.Tasks.Task NotifyAssignedAsync(Guid tenantId, Guid taskId, string taskTitle, Guid assignedUserId, string? sourceProductCode, CancellationToken ct = default);
    System.Threading.Tasks.Task NotifyReassignedAsync(Guid tenantId, Guid taskId, string taskTitle, Guid assignedUserId, string? sourceProductCode, CancellationToken ct = default);
    System.Threading.Tasks.Task NotifyReminderAsync(Guid tenantId, Guid taskId, string taskTitle, Guid assignedUserId, string reminderType, DateTime? dueAt, string? sourceProductCode, CancellationToken ct = default);
}
