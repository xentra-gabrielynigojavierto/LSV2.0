namespace Liens.Application.DTOs;

public sealed class CreateTaskRequest
{
    public string Title               { get; init; } = string.Empty;
    public string? Description        { get; init; }
    public string? Priority           { get; init; }
    public Guid?  AssignedUserId      { get; init; }
    public Guid?  CaseId              { get; init; }
    public List<Guid> LienIds         { get; init; } = [];
    public Guid?  WorkflowStageId     { get; init; }
    public DateTime? DueDate          { get; init; }
    public string? SourceType         { get; init; }
    public Guid?  GenerationRuleId    { get; init; }
    public Guid?  GeneratingTemplateId { get; init; }
}

public sealed class UpdateTaskRequest
{
    public string Title           { get; init; } = string.Empty;
    public string? Description    { get; init; }
    public string? Priority       { get; init; }
    public Guid?  CaseId          { get; init; }
    public List<Guid> LienIds     { get; init; } = [];
    public Guid?  WorkflowStageId { get; init; }
    public DateTime? DueDate      { get; init; }
}

public sealed class AssignTaskRequest
{
    public Guid? AssignedUserId { get; init; }
}

public sealed class UpdateTaskStatusRequest
{
    public string Status { get; init; } = string.Empty;
}
