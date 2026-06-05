namespace Liens.Application.DTOs;

/// <summary>
/// TASK-MIG-01 — response shape returned by the Task service governance API.
/// Matches Task.Application.DTOs.TaskGovernanceDto (camelCase over HTTP).
/// </summary>
public sealed class TaskServiceGovernanceResponse
{
    public Guid    Id                        { get; init; }
    public Guid    TenantId                  { get; init; }
    public string? SourceProductCode         { get; init; }
    public bool    RequireAssignee           { get; init; }
    public bool    RequireDueDate            { get; init; }
    public bool    RequireStage              { get; init; }
    public bool    AllowUnassign             { get; init; }
    public bool    AllowCancel               { get; init; }
    public bool    AllowCompleteWithoutStage { get; init; }
    public bool    AllowNotesOnClosedTasks   { get; init; }
    public string  DefaultPriority           { get; init; } = "MEDIUM";
    public string  DefaultTaskScope          { get; init; } = "GENERAL";
    public int     Version                   { get; init; }
    public DateTime CreatedAtUtc             { get; init; }
    public DateTime UpdatedAtUtc             { get; init; }

    /// <summary>
    /// JSON blob for product-specific extensions.
    /// For SYNQ_LIENS contains RequireCaseLinkOnCreate, AllowMultipleAssignees,
    /// DefaultStartStageMode, and ExplicitStartStageId.
    /// </summary>
    public string? ProductSettingsJson { get; init; }
}

/// <summary>
/// TASK-MIG-01 — request body sent to POST /api/tasks/governance to upsert
/// governance settings from Liens into the Task service.
/// </summary>
public sealed class TaskServiceGovernanceUpsertRequest
{
    public bool    RequireAssignee           { get; init; }
    public bool    RequireDueDate            { get; init; }
    public bool    RequireStage              { get; init; }
    public bool    AllowUnassign             { get; init; }
    public bool    AllowCancel               { get; init; }
    public bool    AllowCompleteWithoutStage { get; init; }
    public bool    AllowNotesOnClosedTasks   { get; init; }
    public string  DefaultPriority           { get; init; } = "MEDIUM";
    public string  DefaultTaskScope          { get; init; } = "GENERAL";
    public string? SourceProductCode         { get; init; }
    public int     ExpectedVersion           { get; init; }
    public string? ProductSettingsJson       { get; init; }
}

/// <summary>
/// TASK-MIG-01 — Liens-specific governance extensions stored in ProductSettingsJson.
/// Serialized as JSON and written to tasks_GovernanceSettings.ProductSettingsJson
/// for rows with SourceProductCode = "SYNQ_LIENS".
/// </summary>
public sealed class LiensGovernanceExtensions
{
    public bool   RequireCaseLinkOnCreate  { get; set; } = true;
    public bool   AllowMultipleAssignees   { get; set; } = false;
    public string DefaultStartStageMode   { get; set; } = "FIRST_ACTIVE_STAGE";
    public Guid?  ExplicitStartStageId    { get; set; }
}
