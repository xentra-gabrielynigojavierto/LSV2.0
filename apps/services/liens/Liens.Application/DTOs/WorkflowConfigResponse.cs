namespace Liens.Application.DTOs;

public sealed class WorkflowConfigResponse
{
    public Guid   Id                  { get; init; }
    public Guid   TenantId            { get; init; }
    public string ProductCode         { get; init; } = string.Empty;
    public string WorkflowName        { get; init; } = string.Empty;
    public int    Version             { get; init; }
    public bool   IsActive            { get; init; }
    public DateTime LastUpdatedAt     { get; init; }
    public Guid?  LastUpdatedByUserId { get; init; }
    public string? LastUpdatedByName  { get; init; }
    public string LastUpdatedSource   { get; init; } = string.Empty;
    public List<WorkflowStageResponse>      Stages      { get; init; } = [];
    public List<WorkflowTransitionResponse> Transitions { get; init; } = [];
    public DateTime CreatedAtUtc      { get; init; }
    public DateTime UpdatedAtUtc      { get; init; }
}

public sealed class WorkflowStageResponse
{
    public Guid   Id               { get; init; }
    public Guid   WorkflowConfigId { get; init; }
    public string StageName        { get; init; } = string.Empty;
    public int    StageOrder       { get; init; }
    public string? Description     { get; init; }
    public bool   IsActive         { get; init; }
    public string? DefaultOwnerRole { get; init; }
    public string? SlaMetadata      { get; init; }
    public DateTime CreatedAtUtc   { get; init; }
    public DateTime UpdatedAtUtc   { get; init; }
}

public sealed class WorkflowTransitionResponse
{
    public Guid    Id               { get; init; }
    public Guid    WorkflowConfigId { get; init; }
    public Guid    FromStageId      { get; init; }
    public Guid    ToStageId        { get; init; }
    public bool    IsActive         { get; init; }
    public int     SortOrder        { get; init; }
    public DateTime CreatedAtUtc    { get; init; }
    public DateTime UpdatedAtUtc    { get; init; }
}
