namespace Reports.Domain.Entities;

public sealed class ReportScheduleRun
{
    public Guid Id { get; set; }
    public Guid ReportScheduleId { get; set; }
    public Guid? ExecutionId { get; set; }
    public Guid? ExportId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset ScheduledForUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string? DeliveryResultJson { get; set; }
    public string? GeneratedFileName { get; set; }
    public long? GeneratedFileSize { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ReportSchedule? ReportSchedule { get; set; }
}
