namespace Reports.Application.Scheduling.DTOs;

public sealed class CreateReportScheduleRequest
{
    public string TenantId { get; init; } = string.Empty;
    public Guid ReportTemplateId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string OrganizationType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public string FrequencyType { get; init; } = string.Empty;
    public string? FrequencyConfigJson { get; init; }
    public string Timezone { get; init; } = "UTC";
    public bool UseOverride { get; init; }
    public Guid? ViewId { get; init; }
    public string ExportFormat { get; init; } = "CSV";
    public string DeliveryMethod { get; init; } = "OnScreen";
    public string? DeliveryConfigJson { get; init; }
    public string? ParametersJson { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public string? RequiredFeatureCode { get; init; }
    public string? MinimumTierCode { get; init; }
}
