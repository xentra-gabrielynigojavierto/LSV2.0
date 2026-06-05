namespace Liens.Application.DTOs;

public sealed class TaskGovernanceSettingsResponse
{
    public Guid    Id          { get; init; }
    public Guid    TenantId    { get; init; }
    public string  ProductCode { get; init; } = string.Empty;

    public bool RequireAssigneeOnCreate      { get; init; }
    public bool RequireCaseLinkOnCreate      { get; init; }
    public bool AllowMultipleAssignees       { get; init; }
    public bool RequireWorkflowStageOnCreate { get; init; }

    public string DefaultStartStageMode  { get; init; } = string.Empty;
    public Guid?  ExplicitStartStageId   { get; init; }

    public int      Version             { get; init; }
    public DateTime LastUpdatedAt       { get; init; }
    public Guid?    LastUpdatedByUserId { get; init; }
    public string?  LastUpdatedByName   { get; init; }
    public string   LastUpdatedSource   { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class UpdateTaskGovernanceSettingsRequest
{
    public bool   RequireAssigneeOnCreate      { get; init; }
    public bool   RequireCaseLinkOnCreate      { get; init; }
    public bool   AllowMultipleAssignees       { get; init; }
    public bool   RequireWorkflowStageOnCreate { get; init; }
    public string DefaultStartStageMode        { get; init; } = "FIRST_ACTIVE_STAGE";
    public Guid?  ExplicitStartStageId         { get; init; }
    public string UpdateSource                 { get; init; } = "TENANT_PRODUCT_SETTINGS";
    public int    Version                      { get; init; }
    public string? UpdatedByName               { get; init; }
}
