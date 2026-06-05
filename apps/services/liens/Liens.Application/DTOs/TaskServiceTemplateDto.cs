using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liens.Application.DTOs;

// ── TASK-MIG-02 — DTOs for Task service template round-trip ─────────────────

/// <summary>
/// SYNQ_LIENS-specific extensions stored in tasks_Templates.ProductSettingsJson.
/// Fields that have no generic Task equivalent are stored here.
/// </summary>
public sealed class LiensTemplateExtensions
{
    [JsonPropertyName("contextType")]
    public string  ContextType               { get; set; } = "GENERAL";

    [JsonPropertyName("applicableWorkflowStageId")]
    public Guid?   ApplicableWorkflowStageId { get; set; }

    [JsonPropertyName("defaultRoleId")]
    public string? DefaultRoleId             { get; set; }

    private static readonly JsonSerializerOptions _opts =
        new(JsonSerializerDefaults.Web);

    public static LiensTemplateExtensions Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new LiensTemplateExtensions();
        try
        {
            return JsonSerializer.Deserialize<LiensTemplateExtensions>(json, _opts)
                   ?? new LiensTemplateExtensions();
        }
        catch
        {
            return new LiensTemplateExtensions();
        }
    }

    public string Serialize() => JsonSerializer.Serialize(this, _opts);
}

/// <summary>
/// Wire type matching the TaskTemplateDto returned by Task service API.
/// </summary>
public sealed class TaskServiceTemplateResponse
{
    public Guid    Id                  { get; set; }
    public Guid    TenantId            { get; set; }
    public string? SourceProductCode   { get; set; }
    public string  Code                { get; set; } = string.Empty;
    public string  Name                { get; set; } = string.Empty;
    public string? Description         { get; set; }
    public string  DefaultTitle        { get; set; } = string.Empty;
    public string? DefaultDescription  { get; set; }
    public string  DefaultPriority     { get; set; } = "MEDIUM";
    public string  DefaultScope        { get; set; } = "GENERAL";
    public int?    DefaultDueInDays    { get; set; }
    public Guid?   DefaultStageId      { get; set; }
    public bool    IsActive            { get; set; } = true;
    public int     Version             { get; set; } = 1;
    public string? ProductSettingsJson { get; set; }
    public DateTime CreatedAtUtc       { get; set; }
    public DateTime UpdatedAtUtc       { get; set; }
}

/// <summary>
/// Payload sent to POST /api/tasks/templates/from-source.
/// </summary>
public sealed class TaskServiceTemplateUpsertRequest
{
    public Guid    Id                  { get; set; }
    public string  Code                { get; set; } = string.Empty;
    public string  Name                { get; set; } = string.Empty;
    public string  DefaultTitle        { get; set; } = string.Empty;
    public string  SourceProductCode   { get; set; } = string.Empty;
    public string? Description         { get; set; }
    public string? DefaultDescription  { get; set; }
    public string  DefaultPriority     { get; set; } = "MEDIUM";
    public string  DefaultScope        { get; set; } = "GENERAL";
    public int?    DefaultDueInDays    { get; set; }
    public Guid?   DefaultStageId      { get; set; }
    public bool    IsActive            { get; set; } = true;
    public string? ProductSettingsJson { get; set; }
}
