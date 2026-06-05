namespace Reports.Application.Assignments.DTOs;

public sealed class TenantTemplateCatalogItemResponse
{
    public Guid TemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string OrganizationType { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    public int? PublishedVersionNumber { get; set; }
    public string AssignmentScope { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
