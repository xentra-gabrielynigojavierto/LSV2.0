namespace Reports.Application.Scheduling.DTOs;

public sealed class ReportScheduleRunResponse
{
    public Guid Id { get; init; }
    public Guid ReportScheduleId { get; init; }
    public Guid? ExecutionId { get; init; }
    public Guid? ExportId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset ScheduledForUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset? DeliveredAtUtc { get; init; }
    public string? FailureReason { get; init; }
    public string? DeliveryResultJson { get; init; }
    public string? GeneratedFileName { get; init; }
    public long? GeneratedFileSize { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}
