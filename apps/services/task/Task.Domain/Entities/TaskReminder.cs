using Task.Domain.Enums;
using ReminderTypeEnum = Task.Domain.Enums.ReminderType;

namespace Task.Domain.Entities;

/// <summary>
/// Tracks a scheduled reminder for a task. One record per reminder type per task.
/// Created/updated when <see cref="PlatformTask.DueAt"/> is set or changed.
/// Cancelled when the task is closed or the due date is removed.
/// </summary>
public class TaskReminder
{
    public Guid      Id              { get; private set; }
    public Guid      TaskId          { get; private set; }
    public Guid      TenantId        { get; private set; }
    public string    ReminderType    { get; private set; } = string.Empty;
    public DateTime  RemindAt        { get; private set; }
    public string    Status          { get; private set; } = ReminderStatus.Pending;
    public DateTime? LastAttemptAt   { get; private set; }
    public DateTime? SentAt          { get; private set; }
    public string?   FailureReason   { get; private set; }
    public DateTime  CreatedAtUtc    { get; private set; }
    public DateTime  UpdatedAtUtc    { get; private set; }

    private TaskReminder() { }

    public static TaskReminder Create(
        Guid     taskId,
        Guid     tenantId,
        string   reminderType,
        DateTime remindAt)
    {
        if (taskId == Guid.Empty)   throw new ArgumentException("TaskId is required.", nameof(taskId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (!ReminderTypeEnum.All.Contains(reminderType))
            throw new ArgumentException($"Invalid reminder type: '{reminderType}'.", nameof(reminderType));

        var now = DateTime.UtcNow;
        return new TaskReminder
        {
            Id           = Guid.NewGuid(),
            TaskId       = taskId,
            TenantId     = tenantId,
            ReminderType = reminderType,
            RemindAt     = remindAt,
            Status       = ReminderStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    /// <summary>Update the scheduled time (when due date changes).</summary>
    public void Reschedule(DateTime newRemindAt)
    {
        if (Status != ReminderStatus.Pending)
            throw new InvalidOperationException($"Cannot reschedule reminder in status '{Status}'.");

        RemindAt     = newRemindAt;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Mark the reminder as successfully sent.</summary>
    public void MarkSent()
    {
        Status       = ReminderStatus.Sent;
        SentAt       = DateTime.UtcNow;
        LastAttemptAt = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Mark the reminder as failed.</summary>
    public void MarkFailed(string reason)
    {
        Status        = ReminderStatus.Failed;
        FailureReason = reason;
        LastAttemptAt = DateTime.UtcNow;
        UpdatedAtUtc  = DateTime.UtcNow;
    }

    /// <summary>Cancel the reminder — e.g. task closed or due date removed.</summary>
    public void Cancel()
    {
        if (Status == ReminderStatus.Sent) return; // already done, nothing to cancel
        Status       = ReminderStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Reset a FAILED reminder back to PENDING for a retry attempt.</summary>
    public void ResetForRetry()
    {
        if (Status != ReminderStatus.Failed)
            throw new InvalidOperationException("Only FAILED reminders can be reset.");

        Status        = ReminderStatus.Pending;
        FailureReason = null;
        UpdatedAtUtc  = DateTime.UtcNow;
    }
}
