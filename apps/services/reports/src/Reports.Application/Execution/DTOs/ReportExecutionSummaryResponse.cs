namespace Reports.Application.Execution.DTOs;

public sealed class ReportExecutionSummaryResponse
{
    public Guid ExecutionId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid TemplateId { get; init; }
    public int TemplateVersionNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? FailureReason { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
}
