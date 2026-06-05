namespace Reports.Domain.Entities;

public sealed class ReportExecution
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public Guid ReportTemplateId { get; set; }
    public int TemplateVersionNumber { get; set; } = 1;
    public string Status { get; set; } = "Pending";
    public string? OutputDocumentId { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }

    public ReportTemplate? ReportTemplate { get; set; }
}
