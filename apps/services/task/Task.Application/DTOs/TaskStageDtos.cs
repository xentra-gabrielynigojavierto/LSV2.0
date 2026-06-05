using Task.Domain.Entities;

namespace Task.Application.DTOs;

public record CreateTaskStageRequest(
    string  Code,
    string  Name,
    int     DisplayOrder,
    string? SourceProductCode   = null,
    string? ProductSettingsJson = null);

public record UpdateTaskStageRequest(
    string  Name,
    int     DisplayOrder,
    bool    IsActive,
    string? ProductSettingsJson = null);

/// <summary>
/// Idempotent create-or-update request from a source product (e.g. SYNQ_LIENS).
/// Caller supplies the explicit ID to preserve identity across services.
/// Code is derived from Id: Id.ToString("N").ToUpperInvariant().
/// </summary>
public record UpsertFromSourceStageRequest(
    Guid    Id,
    string  SourceProductCode,
    string  Name,
    int     DisplayOrder,
    bool    IsActive,
    string? ProductSettingsJson = null);

public record TaskStageDto(
    Guid    Id,
    Guid    TenantId,
    string? SourceProductCode,
    string  Code,
    string  Name,
    int     DisplayOrder,
    bool    IsActive,
    string? ProductSettingsJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static TaskStageDto From(TaskStageConfig s) => new(
        s.Id, s.TenantId, s.SourceProductCode,
        s.Code, s.Name, s.DisplayOrder, s.IsActive,
        s.ProductSettingsJson,
        s.CreatedAtUtc, s.UpdatedAtUtc);
}
