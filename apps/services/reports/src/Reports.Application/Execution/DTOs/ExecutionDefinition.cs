namespace Reports.Application.Execution.DTOs;

public sealed class ExecutionDefinition
{
    public Guid TemplateId { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string EffectiveName { get; init; } = string.Empty;
    public string? EffectiveDescription { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string OrganizationType { get; init; } = string.Empty;
    public int PublishedVersionNumber { get; init; }
    public string? TemplateBody { get; init; }
    public bool HasOverride { get; init; }
    public int? BaseTemplateVersionNumber { get; init; }
    public Guid? OverrideId { get; init; }
    public string? LayoutConfigJson { get; init; }
    public string? ColumnConfigJson { get; init; }
    public string? FilterConfigJson { get; init; }
    public Guid? ViewId { get; init; }
    public string? ViewName { get; init; }
    public string? FormulaConfigJson { get; init; }
    public string? FormattingConfigJson { get; init; }
}
