namespace Reports.Application.Assignments.DTOs;

public sealed class TemplateAssignmentResponse
{
    public Guid AssignmentId { get; set; }
    public Guid TemplateId { get; set; }
    public string AssignmentScope { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string OrganizationType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<string> TenantIds { get; set; } = new();
    public string? RequiredFeatureCode { get; set; }
    public string? MinimumTierCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
