namespace Liens.Application.DTOs;

public sealed class CaseNoteResponse
{
    public Guid      Id              { get; init; }
    public Guid      CaseId          { get; init; }
    public string    Content         { get; init; } = string.Empty;
    public string    Category        { get; init; } = "general";
    public bool      IsPinned        { get; init; }
    public Guid      CreatedByUserId { get; init; }
    public string    CreatedByName   { get; init; } = string.Empty;
    public bool      IsEdited        { get; init; }
    public DateTime  CreatedAtUtc    { get; init; }
    public DateTime? UpdatedAtUtc    { get; init; }
}

public sealed class CreateCaseNoteRequest
{
    public string Content       { get; init; } = string.Empty;
    public string Category      { get; init; } = "general";
    public string CreatedByName { get; init; } = string.Empty;
}

public sealed class UpdateCaseNoteRequest
{
    public string  Content  { get; init; } = string.Empty;
    public string? Category { get; init; }
}
