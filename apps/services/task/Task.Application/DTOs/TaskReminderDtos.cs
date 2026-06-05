using Task.Domain.Entities;

namespace Task.Application.DTOs;

public record TaskReminderDto(
    Guid      Id,
    Guid      TaskId,
    Guid      TenantId,
    string    ReminderType,
    DateTime  RemindAt,
    string    Status,
    DateTime? LastAttemptAt,
    DateTime? SentAt,
    string?   FailureReason,
    DateTime  CreatedAtUtc,
    DateTime  UpdatedAtUtc)
{
    public static TaskReminderDto From(TaskReminder r) => new(
        r.Id, r.TaskId, r.TenantId,
        r.ReminderType, r.RemindAt, r.Status,
        r.LastAttemptAt, r.SentAt, r.FailureReason,
        r.CreatedAtUtc, r.UpdatedAtUtc);
}

public record ReminderProcessResult(
    int Processed,
    int Sent,
    int Failed,
    int Skipped,
    DateTime ProcessedAt);
