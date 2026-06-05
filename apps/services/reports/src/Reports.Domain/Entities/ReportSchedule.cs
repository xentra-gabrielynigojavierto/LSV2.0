namespace Reports.Domain.Entities;

public sealed class ReportSchedule
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid ReportTemplateId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string OrganizationType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string FrequencyType { get; set; } = string.Empty;
    public string? FrequencyConfigJson { get; set; }
    public string Timezone { get; set; } = "UTC";
    public DateTimeOffset? NextRunAtUtc { get; set; }
    public DateTimeOffset? LastRunAtUtc { get; set; }
    public bool UseOverride { get; set; }
    public Guid? ViewId { get; set; }
    public string ExportFormat { get; set; } = "CSV";
    public string DeliveryMethod { get; set; } = "OnScreen";
    public string? DeliveryConfigJson { get; set; }
    public string? ParametersJson { get; set; }
    public string? RequiredFeatureCode { get; set; }
    public string? MinimumTierCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }

    public ReportTemplate? ReportTemplate { get; set; }
    public ICollection<ReportScheduleRun> Runs { get; set; } = new List<ReportScheduleRun>();
}
