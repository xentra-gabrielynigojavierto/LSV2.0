namespace Reports.Application.Overrides.DTOs;

public sealed class TenantEffectiveReportResponse
{
    public Guid TemplateId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string OrganizationType { get; set; } = string.Empty;
    public int PublishedVersionNumber { get; set; }
    public int? BaseTemplateVersionNumber { get; set; }
    public bool HasOverride { get; set; }
    public Guid? OverrideId { get; set; }

    public string EffectiveName { get; set; } = string.Empty;
    public string? EffectiveDescription { get; set; }
    public string? EffectiveLayoutConfigJson { get; set; }
    public string? EffectiveColumnConfigJson { get; set; }
    public string? EffectiveFilterConfigJson { get; set; }
    public string? EffectiveFormulaConfigJson { get; set; }
    public string? EffectiveHeaderConfigJson { get; set; }
    public string? EffectiveFooterConfigJson { get; set; }

    public bool IsActive { get; set; }
    public string? RequiredFeatureCode { get; set; }
    public string? MinimumTierCode { get; set; }
}
