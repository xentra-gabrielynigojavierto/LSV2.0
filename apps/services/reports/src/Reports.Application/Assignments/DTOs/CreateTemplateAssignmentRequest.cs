namespace Reports.Application.Assignments.DTOs;

public sealed class CreateTemplateAssignmentRequest
{
    public string AssignmentScope { get; set; } = string.Empty;
    public List<string>? TenantIds { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? RequiredFeatureCode { get; set; }
    public string? MinimumTierCode { get; set; }
}
