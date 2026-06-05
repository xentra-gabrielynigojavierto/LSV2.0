namespace Reports.Domain.Entities;

public sealed class TenantReportOverride
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid ReportTemplateId { get; set; }
    public int BaseTemplateVersionNumber { get; set; }
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
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }

    public ReportTemplate? ReportTemplate { get; set; }
}
