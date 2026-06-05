namespace Liens.Application.DTOs;

/// <summary>
/// TASK-MIG-03 — Liens-specific stage extensions stored in tasks_StageConfigs.ProductSettingsJson
/// for rows with SourceProductCode = "SYNQ_LIENS".
/// JSON shape: { description, defaultOwnerRole, slaMetadata }
/// </summary>
public sealed class LiensStageExtensions
{
    public string? Description     { get; set; }
    public string? DefaultOwnerRole { get; set; }
    public string? SlaMetadata     { get; set; }
}

/// <summary>
/// TASK-MIG-03 — response shape returned by GET /api/tasks/stages/{id} or
/// GET /api/tasks/stages?sourceProductCode=SYNQ_LIENS.
/// Matches Task.Application.DTOs.TaskStageDto (camelCase over HTTP).
/// </summary>
public sealed class TaskServiceStageResponse
{
    public Guid    Id                  { get; init; }
    public Guid    TenantId            { get; init; }
    public string? SourceProductCode   { get; init; }
    public string  Code                { get; init; } = string.Empty;
    public string  Name                { get; init; } = string.Empty;
    public int     DisplayOrder        { get; init; }
    public bool    IsActive            { get; init; }
    public string? ProductSettingsJson { get; init; }
    public DateTime CreatedAtUtc       { get; init; }
    public DateTime UpdatedAtUtc       { get; init; }
}

/// <summary>
/// TASK-MIG-03 — request body sent to POST /api/tasks/stages/from-source to upsert
/// a Liens stage into the Task service.
/// </summary>
public sealed class TaskServiceStageUpsertRequest
{
    public Guid    Id                  { get; init; }
    public string  SourceProductCode   { get; init; } = string.Empty;
    public string  Name                { get; init; } = string.Empty;
    public int     DisplayOrder        { get; init; }
    public bool    IsActive            { get; init; }
    public string? ProductSettingsJson { get; init; }
}
