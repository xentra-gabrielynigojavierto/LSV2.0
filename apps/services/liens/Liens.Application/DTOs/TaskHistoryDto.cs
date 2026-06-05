namespace Liens.Application.DTOs;

public sealed class TaskHistoryEventDto
{
    public Guid     Id                { get; init; }
    public Guid     TaskId            { get; init; }
    public string   Action            { get; init; } = string.Empty;
    public string?  Details           { get; init; }
    public Guid     PerformedByUserId { get; init; }
    public DateTime CreatedAtUtc      { get; init; }
}
