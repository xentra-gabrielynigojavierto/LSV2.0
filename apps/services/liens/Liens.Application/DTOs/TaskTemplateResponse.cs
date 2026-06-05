namespace Liens.Application.DTOs;

public sealed class TaskTemplateResponse
{
    public Guid    Id                        { get; init; }
    public Guid    TenantId                  { get; init; }
    public string  ProductCode               { get; init; } = string.Empty;
    public string  Name                      { get; init; } = string.Empty;
    public string? Description               { get; init; }
    public string  DefaultTitle              { get; init; } = string.Empty;
    public string? DefaultDescription        { get; init; }
    public string  DefaultPriority           { get; init; } = "MEDIUM";
    public int?    DefaultDueOffsetDays      { get; init; }
    public string? DefaultRoleId             { get; init; }
    public string  ContextType               { get; init; } = "GENERAL";
    public Guid?   ApplicableWorkflowStageId { get; init; }
    public bool    IsActive                  { get; init; }
    public int     Version                   { get; init; }
    public DateTime LastUpdatedAt            { get; init; }
    public Guid?   LastUpdatedByUserId       { get; init; }
    public string? LastUpdatedByName         { get; init; }
    public string  LastUpdatedSource         { get; init; } = string.Empty;
    public DateTime CreatedAtUtc             { get; init; }
    public DateTime UpdatedAtUtc             { get; init; }
}
