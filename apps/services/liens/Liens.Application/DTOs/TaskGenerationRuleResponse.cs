namespace Liens.Application.DTOs;

public sealed class TaskGenerationRuleResponse
{
    public Guid   Id                        { get; init; }
    public Guid   TenantId                  { get; init; }
    public string ProductCode               { get; init; } = string.Empty;
    public string Name                      { get; init; } = string.Empty;
    public string? Description              { get; init; }
    public string EventType                 { get; init; } = string.Empty;
    public Guid   TaskTemplateId            { get; init; }
    public string ContextType               { get; init; } = string.Empty;
    public Guid?  ApplicableWorkflowStageId { get; init; }
    public string DuplicatePreventionMode   { get; init; } = string.Empty;
    public string AssignmentMode            { get; init; } = string.Empty;
    public string DueDateMode               { get; init; } = string.Empty;
    public int?   DueDateOffsetDays         { get; init; }
    public bool   IsActive                  { get; init; }
    public int    Version                   { get; init; }
    public DateTime LastUpdatedAt           { get; init; }
    public Guid?  LastUpdatedByUserId       { get; init; }
    public string? LastUpdatedByName        { get; init; }
    public string LastUpdatedSource         { get; init; } = string.Empty;
    public Guid?  CreatedByUserId           { get; init; }
    public DateTime CreatedAtUtc            { get; init; }
    public DateTime UpdatedAtUtc            { get; init; }
}

public sealed class TriggerTaskGenerationResponse
{
    public int TasksGenerated  { get; init; }
    public int TasksSkipped    { get; init; }
}
