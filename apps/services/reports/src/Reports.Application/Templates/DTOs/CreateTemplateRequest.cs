namespace Reports.Application.Templates.DTOs;

public sealed class CreateTemplateRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string OrganizationType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
