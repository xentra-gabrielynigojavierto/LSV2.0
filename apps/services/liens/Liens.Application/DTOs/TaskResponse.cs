namespace Liens.Application.DTOs;

public sealed class TaskResponse
{
    public Guid   Id                    { get; init; }
    public Guid   TenantId              { get; init; }
    public string Title                 { get; init; } = string.Empty;
    public string? Description          { get; init; }
    public string Status                { get; init; } = string.Empty;
    public string Priority              { get; init; } = string.Empty;
    public Guid?  AssignedUserId        { get; init; }
    public Guid?  CaseId               { get; init; }
    public Guid?  WorkflowStageId      { get; init; }
    public DateTime? DueDate           { get; init; }
    public DateTime? CompletedAt       { get; init; }
    public Guid?  ClosedByUserId       { get; init; }
    public List<TaskLienLinkResponse> LinkedLiens { get; init; } = [];
    public Guid?  CreatedByUserId      { get; init; }
    public DateTime CreatedAtUtc       { get; init; }
    public DateTime UpdatedAtUtc       { get; init; }
    public string SourceType           { get; init; } = "MANUAL";
    public bool   IsSystemGenerated    { get; init; }
    public Guid?  GenerationRuleId     { get; init; }
    public Guid?  GeneratingTemplateId { get; init; }

    // LS-LIENS-FLOW-007 — Flow workflow instance linked at task creation time
    public Guid?   WorkflowInstanceId { get; init; }
    public string? WorkflowStepKey    { get; init; }
}

public sealed class TaskLienLinkResponse
{
    public Guid TaskId     { get; init; }
    public Guid LienId     { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
