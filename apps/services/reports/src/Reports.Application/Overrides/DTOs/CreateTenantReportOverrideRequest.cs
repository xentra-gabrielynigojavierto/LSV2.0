namespace Reports.Application.Overrides.DTOs;

public sealed class CreateTenantReportOverrideRequest
{
    public string TenantId { get; set; } = string.Empty;
    public string? NameOverride { get; set; }
    public string? DescriptionOverride { get; set; }
    public string? LayoutConfigJson { get; set; }
    public string? ColumnConfigJson { get; set; }
    public string? FilterConfigJson { get; set; }
    public string? FormulaConfigJson { get; set; }
    public string? HeaderConfigJson { get; set; }
    public string? FooterConfigJson { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RequiredFeatureCode { get; set; }
    public string? MinimumTierCode { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
}
