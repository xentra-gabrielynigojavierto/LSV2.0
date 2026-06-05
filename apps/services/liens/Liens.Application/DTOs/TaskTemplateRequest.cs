namespace Liens.Application.DTOs;

public sealed class CreateTaskTemplateRequest
{
    public string  Name                      { get; init; } = string.Empty;
    public string? Description               { get; init; }
    public string  DefaultTitle              { get; init; } = string.Empty;
    public string? DefaultDescription        { get; init; }
    public string  DefaultPriority           { get; init; } = "MEDIUM";
    public int?    DefaultDueOffsetDays      { get; init; }
    public string? DefaultRoleId             { get; init; }
    public string  ContextType               { get; init; } = "GENERAL";
    public Guid?   ApplicableWorkflowStageId { get; init; }
    public string  UpdateSource              { get; init; } = string.Empty;
    public string? UpdatedByName             { get; init; }
}

public sealed class UpdateTaskTemplateRequest
{
    public string  Name                      { get; init; } = string.Empty;
    public string? Description               { get; init; }
    public string  DefaultTitle              { get; init; } = string.Empty;
    public string? DefaultDescription        { get; init; }
    public string  DefaultPriority           { get; init; } = "MEDIUM";
    public int?    DefaultDueOffsetDays      { get; init; }
    public string? DefaultRoleId             { get; init; }
    public string  ContextType               { get; init; } = "GENERAL";
    public Guid?   ApplicableWorkflowStageId { get; init; }
    public string  UpdateSource              { get; init; } = string.Empty;
    public string? UpdatedByName             { get; init; }
    public int     Version                   { get; init; }
}

public sealed class ActivateDeactivateTemplateRequest
{
    public string  UpdateSource  { get; init; } = string.Empty;
    public string? UpdatedByName { get; init; }
}

public sealed class ContextualTemplateQuery
{
    public string? ContextType               { get; init; }
    public Guid?   WorkflowStageId           { get; init; }
}
