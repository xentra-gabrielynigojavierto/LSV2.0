namespace Reports.Application.Execution.DTOs;

public sealed class ReportExecutionResponse
{
    public Guid ExecutionId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid TemplateId { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public int PublishedVersionNumber { get; init; }
    public int? BaseTemplateVersionNumber { get; init; }
    public bool HasOverride { get; init; }
    public Guid? ViewId { get; init; }
    public string? ViewName { get; init; }
    public List<ReportColumnResponse> Columns { get; init; } = new();
    public List<ReportRowResponse> Rows { get; init; } = new();
    public int RowCount { get; init; }
    public DateTimeOffset ExecutedAtUtc { get; init; }
    public string ExecutedByUserId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
