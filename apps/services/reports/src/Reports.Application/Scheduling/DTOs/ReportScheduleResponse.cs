namespace Reports.Application.Scheduling.DTOs;

public sealed class ReportScheduleResponse
{
    public Guid Id { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid ReportTemplateId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string OrganizationType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public string FrequencyType { get; init; } = string.Empty;
    public string? FrequencyConfigJson { get; init; }
    public string Timezone { get; init; } = string.Empty;
    public DateTimeOffset? NextRunAtUtc { get; init; }
    public DateTimeOffset? LastRunAtUtc { get; init; }
    public bool UseOverride { get; init; }
    public Guid? ViewId { get; init; }
    public string ExportFormat { get; init; } = string.Empty;
    public string DeliveryMethod { get; init; } = string.Empty;
    public string? DeliveryConfigJson { get; init; }
    public string? ParametersJson { get; init; }
    public string? RequiredFeatureCode { get; init; }
    public string? MinimumTierCode { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public string? UpdatedByUserId { get; init; }
}
