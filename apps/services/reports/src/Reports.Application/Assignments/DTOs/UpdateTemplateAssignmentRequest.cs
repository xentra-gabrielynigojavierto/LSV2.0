namespace Reports.Application.Assignments.DTOs;

public sealed class UpdateTemplateAssignmentRequest
{
    public List<string>? TenantIds { get; set; }
    public bool IsActive { get; set; } = true;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string? RequiredFeatureCode { get; set; }
    public string? MinimumTierCode { get; set; }
}
