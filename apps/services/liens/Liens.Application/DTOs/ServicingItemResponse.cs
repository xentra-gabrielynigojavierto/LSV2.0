namespace Liens.Application.DTOs;

public sealed class ServicingItemResponse
{
    public Guid Id { get; init; }
    public string TaskNumber { get; init; } = string.Empty;
    public string TaskType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string AssignedTo { get; init; } = string.Empty;
    public Guid? AssignedToUserId { get; init; }
    public Guid? CaseId { get; init; }
    public Guid? LienId { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? Notes { get; init; }
    public string? Resolution { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime? EscalatedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
