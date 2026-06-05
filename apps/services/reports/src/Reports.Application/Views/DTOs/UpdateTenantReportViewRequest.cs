namespace Reports.Application.Views.DTOs;

public sealed class UpdateTenantReportViewRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool? IsDefault { get; init; }
    public bool? IsActive { get; init; }
    public string? LayoutConfigJson { get; init; }
    public string? ColumnConfigJson { get; init; }
    public string? FilterConfigJson { get; init; }
    public string? FormulaConfigJson { get; init; }
    public string? FormattingConfigJson { get; init; }
    public string UpdatedByUserId { get; init; } = string.Empty;
}
