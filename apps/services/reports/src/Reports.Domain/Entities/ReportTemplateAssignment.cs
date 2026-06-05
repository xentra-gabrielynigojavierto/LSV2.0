namespace Reports.Domain.Entities;

public sealed class ReportTemplateAssignment
{
    public Guid Id { get; set; }
    public Guid ReportTemplateId { get; set; }
    public string AssignmentScope { get; set; } = "Global";
    public string ProductCode { get; set; } = string.Empty;
    public string OrganizationType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? RequiredFeatureCode { get; set; }
    public string? MinimumTierCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedByUserId { get; set; }

    public ReportTemplate? ReportTemplate { get; set; }
    public ICollection<ReportTemplateAssignmentTenant> TenantTargets { get; set; } = new List<ReportTemplateAssignmentTenant>();
}
