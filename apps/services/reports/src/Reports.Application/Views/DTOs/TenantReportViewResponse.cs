namespace Reports.Application.Views.DTOs;

public sealed class TenantReportViewResponse
{
    public Guid ViewId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid ReportTemplateId { get; init; }
    public int BaseTemplateVersionNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
    public string? LayoutConfigJson { get; init; }
    public string? ColumnConfigJson { get; init; }
    public string? FilterConfigJson { get; init; }
    public string? FormulaConfigJson { get; init; }
    public string? FormattingConfigJson { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public string? UpdatedByUserId { get; init; }
}
