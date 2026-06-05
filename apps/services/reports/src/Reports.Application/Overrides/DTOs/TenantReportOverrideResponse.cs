namespace Reports.Application.Overrides.DTOs;

public sealed class TenantReportOverrideResponse
{
    public Guid OverrideId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public int BaseTemplateVersionNumber { get; set; }
    public string? NameOverride { get; set; }
    public string? DescriptionOverride { get; set; }
    public string? LayoutConfigJson { get; set; }
    public string? ColumnConfigJson { get; set; }
    public string? FilterConfigJson { get; set; }
    public string? FormulaConfigJson { get; set; }
    public string? HeaderConfigJson { get; set; }
    public string? FooterConfigJson { get; set; }
    public bool IsActive { get; set; }
    public string? RequiredFeatureCode { get; set; }
    public string? MinimumTierCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
