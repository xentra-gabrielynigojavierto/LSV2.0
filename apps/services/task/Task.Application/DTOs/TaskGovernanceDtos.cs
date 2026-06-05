using Task.Domain.Entities;

namespace Task.Application.DTOs;

public record UpsertTaskGovernanceRequest(
    bool    RequireAssignee,
    bool    RequireDueDate,
    bool    RequireStage,
    bool    AllowUnassign,
    bool    AllowCancel,
    bool    AllowCompleteWithoutStage,
    bool    AllowNotesOnClosedTasks,
    string  DefaultPriority,
    string  DefaultTaskScope,
    string? SourceProductCode    = null,
    int     ExpectedVersion      = 0,
    string? ProductSettingsJson  = null);

public record TaskGovernanceDto(
    Guid    Id,
    Guid    TenantId,
    string? SourceProductCode,
    bool    RequireAssignee,
    bool    RequireDueDate,
    bool    RequireStage,
    bool    AllowUnassign,
    bool    AllowCancel,
    bool    AllowCompleteWithoutStage,
    bool    AllowNotesOnClosedTasks,
    string  DefaultPriority,
    string  DefaultTaskScope,
    int     Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? ProductSettingsJson = null)
{
    public static TaskGovernanceDto From(TaskGovernanceSettings s) => new(
        s.Id, s.TenantId, s.SourceProductCode,
        s.RequireAssignee, s.RequireDueDate, s.RequireStage,
        s.AllowUnassign, s.AllowCancel, s.AllowCompleteWithoutStage,
        s.AllowNotesOnClosedTasks, s.DefaultPriority, s.DefaultTaskScope,
        s.Version, s.CreatedAtUtc, s.UpdatedAtUtc,
        s.ProductSettingsJson);
}

/// <summary>Resolved governance after applying the priority lookup chain.</summary>
public record ResolvedGovernance(
    bool    RequireAssignee,
    bool    RequireDueDate,
    bool    RequireStage,
    bool    AllowUnassign,
    bool    AllowCancel,
    bool    AllowCompleteWithoutStage,
    bool    AllowNotesOnClosedTasks,
    string  DefaultPriority,
    string  DefaultTaskScope)
{
    public static ResolvedGovernance From(TaskGovernanceSettings s) => new(
        s.RequireAssignee, s.RequireDueDate, s.RequireStage,
        s.AllowUnassign, s.AllowCancel, s.AllowCompleteWithoutStage,
        s.AllowNotesOnClosedTasks, s.DefaultPriority, s.DefaultTaskScope);

    public static ResolvedGovernance Fallback() => new(
        GovernanceFallback.RequireAssignee,
        GovernanceFallback.RequireDueDate,
        GovernanceFallback.RequireStage,
        GovernanceFallback.AllowUnassign,
        GovernanceFallback.AllowCancel,
        GovernanceFallback.AllowCompleteWithoutStage,
        GovernanceFallback.AllowNotesOnClosedTasks,
        GovernanceFallback.DefaultPriority,
        GovernanceFallback.DefaultTaskScope);
}
