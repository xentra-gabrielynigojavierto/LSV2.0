namespace Reports.Application.Templates.DTOs;

public sealed class TemplateVersionResponse
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public int VersionNumber { get; set; }
    public string? TemplateBody { get; set; }
    public string OutputFormat { get; set; } = string.Empty;
    public string? ChangeNotes { get; set; }
    public bool IsActive { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
}
