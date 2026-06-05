namespace Liens.Application.DTOs;

public sealed class CreateTaskGenerationRuleRequest
{
    public string  Name                      { get; init; } = string.Empty;
    public string? Description               { get; init; }
    public string  EventType                 { get; init; } = string.Empty;
    public Guid    TaskTemplateId            { get; init; }
    public string  ContextType               { get; init; } = "GENERAL";
    public Guid?   ApplicableWorkflowStageId { get; init; }
    public string  DuplicatePreventionMode   { get; init; } = "SAME_RULE_SAME_ENTITY_OPEN_TASK";
    public string  AssignmentMode            { get; init; } = "USE_TEMPLATE_DEFAULT";
    public string  DueDateMode               { get; init; } = "USE_TEMPLATE_DEFAULT";
    public int?    DueDateOffsetDays         { get; init; }
    public string  UpdateSource              { get; init; } = string.Empty;
    public string? UpdatedByName             { get; init; }
}

public sealed class UpdateTaskGenerationRuleRequest
{
    public string  Name                      { get; init; } = string.Empty;
    public string? Description               { get; init; }
    public string  EventType                 { get; init; } = string.Empty;
    public Guid    TaskTemplateId            { get; init; }
    public string  ContextType               { get; init; } = "GENERAL";
    public Guid?   ApplicableWorkflowStageId { get; init; }
    public string  DuplicatePreventionMode   { get; init; } = "SAME_RULE_SAME_ENTITY_OPEN_TASK";
    public string  AssignmentMode            { get; init; } = "USE_TEMPLATE_DEFAULT";
    public string  DueDateMode               { get; init; } = "USE_TEMPLATE_DEFAULT";
    public int?    DueDateOffsetDays         { get; init; }
    public string  UpdateSource              { get; init; } = string.Empty;
    public string? UpdatedByName             { get; init; }
    public int     Version                   { get; init; }
}

public sealed class ActivateDeactivateRuleRequest
{
    public string  UpdateSource  { get; init; } = string.Empty;
    public string? UpdatedByName { get; init; }
}

public sealed class TriggerTaskGenerationRequest
{
    public string EventType      { get; init; } = string.Empty;
    public Guid?  CaseId         { get; init; }
    public Guid?  LienId         { get; init; }
    public Guid?  WorkflowStageId { get; init; }
    public string? ActorName     { get; init; }
}
