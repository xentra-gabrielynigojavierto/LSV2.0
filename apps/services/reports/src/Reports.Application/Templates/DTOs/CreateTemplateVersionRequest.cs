namespace Reports.Application.Templates.DTOs;

public sealed class CreateTemplateVersionRequest
{
    public string TemplateBody { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = "PDF";
    public string? ChangeNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
}
