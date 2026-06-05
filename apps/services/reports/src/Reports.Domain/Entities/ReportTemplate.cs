namespace Reports.Domain.Entities;

public sealed class ReportTemplate
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string OrganizationType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int CurrentVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ReportTemplateVersion> Versions { get; set; } = new List<ReportTemplateVersion>();
    public ICollection<ReportTemplateAssignment> Assignments { get; set; } = new List<ReportTemplateAssignment>();
}
