namespace Liens.Application.DTOs;

public sealed class TaskNoteResponse
{
    public Guid      Id              { get; init; }
    public Guid      TaskId          { get; init; }
    public string    Content         { get; init; } = string.Empty;
    public Guid      CreatedByUserId { get; init; }
    public string    CreatedByName   { get; init; } = string.Empty;
    public bool      IsEdited        { get; init; }
    public DateTime  CreatedAtUtc    { get; init; }
    public DateTime? UpdatedAtUtc    { get; init; }
}

public sealed class CreateTaskNoteRequest
{
    public string Content       { get; init; } = string.Empty;
    public string CreatedByName { get; init; } = string.Empty;
}

public sealed class UpdateTaskNoteRequest
{
    public string Content { get; init; } = string.Empty;
}
