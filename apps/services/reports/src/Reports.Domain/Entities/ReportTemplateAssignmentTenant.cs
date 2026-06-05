namespace Reports.Domain.Entities;

public sealed class ReportTemplateAssignmentTenant
{
    public Guid Id { get; set; }
    public Guid ReportTemplateAssignmentId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;

    public ReportTemplateAssignment? Assignment { get; set; }
}
