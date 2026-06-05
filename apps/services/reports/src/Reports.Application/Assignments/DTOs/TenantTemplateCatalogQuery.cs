namespace Reports.Application.Assignments.DTOs;

public sealed class TenantTemplateCatalogQuery
{
    public string TenantId { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string OrganizationType { get; set; } = string.Empty;
}
