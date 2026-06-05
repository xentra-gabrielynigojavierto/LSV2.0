namespace Liens.Application.DTOs;

public sealed class UpdateServicingItemRequest
{
    public string TaskType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string AssignedTo { get; init; } = string.Empty;
    public Guid? AssignedToUserId { get; init; }
    public string? Priority { get; init; }
    public string? Status { get; init; }
    public Guid? CaseId { get; init; }
    public Guid? LienId { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? Notes { get; init; }
    public string? Resolution { get; init; }
}
