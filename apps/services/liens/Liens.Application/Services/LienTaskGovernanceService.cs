using System.Text.Json;
using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// TASK-MIG-09 — Final Cleanup: Task service is the sole authoritative owner of governance settings.
///
/// Read authority  : Task service only. No Liens DB fallback.
/// Write authority : Task service only. No Liens DB mirrors.
///                  governance_write_owner=task_service (invariant)
///
/// Removed in MIG-09:
///   - ILienTaskGovernanceSettingsRepository / Liens DB fallback reads
///   - TryMirrorCreateToLiensDbAsync / TryMirrorUpdateToLiensDbAsync
///   - Version conflict check against Liens DB → now uses Task service version
///
/// LienTaskGovernanceSettings domain entity is retained as an in-memory value object
/// for GetOrCreateAsync create path only: default-value factory + payload building.
/// It is never persisted to Liens DB.
/// </summary>
public sealed class LienTaskGovernanceService : ILienTaskGovernanceService
{
    private const string ProductCode = LiensPermissions.ProductCode; // "SYNQ_LIENS"

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly ILiensTaskServiceClient               _taskClient;
    private readonly IAuditPublisher                       _audit;
    private readonly ILogger<LienTaskGovernanceService>    _logger;

    public LienTaskGovernanceService(
        ILiensTaskServiceClient               taskClient,
        IAuditPublisher                       audit,
        ILogger<LienTaskGovernanceService>    logger)
    {
        _taskClient = taskClient;
        _audit      = audit;
        _logger     = logger;
    }

    // ── GetAsync — Task service only ─────────────────────────────────────────────

    public async Task<TaskGovernanceSettingsResponse?> GetAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var dto = await _taskClient.GetGovernanceAsync(tenantId, ProductCode, ct);
        if (dto is null)
        {
            _logger.LogDebug(
                "governance_read_owner=task_service GetAsync TenantId={TenantId} — not found", tenantId);
            return null;
        }

        _logger.LogDebug(
            "governance_read_owner=task_service GetAsync TenantId={TenantId}", tenantId);
        return MapFromTaskServiceDto(dto);
    }

    // ── GetOrCreateAsync — Task service only ─────────────────────────────────────

    public async Task<TaskGovernanceSettingsResponse> GetOrCreateAsync(
        Guid tenantId, Guid actingUserId, string updateSource, CancellationToken ct = default)
    {
        // 1. Task service (sole authority)
        var dto = await _taskClient.GetGovernanceAsync(tenantId, ProductCode, ct);
        if (dto is not null)
        {
            _logger.LogDebug(
                "governance_read_owner=task_service GetOrCreateAsync TenantId={TenantId}", tenantId);
            return MapFromTaskServiceDto(dto);
        }

        // 2. Not found — create defaults in Task service (sole authority)
        var newEntity = LienTaskGovernanceSettings.CreateDefault(
            tenantId:        tenantId,
            productCode:     ProductCode,
            updateSource:    updateSource,
            createdByUserId: actingUserId);

        var payload = BuildUpsertPayload(newEntity);
        await _taskClient.UpsertGovernanceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "governance_write_owner=task_service Created defaults TenantId={TenantId}", tenantId);

        _audit.Publish(
            eventType:   "liens.task_governance.created",
            action:      "create",
            description: "Task governance settings created with defaults",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskGovernanceSettings",
            entityId:    newEntity.Id.ToString());

        return MapToResponse(newEntity);
    }

    // ── UpdateAsync — Task service only ──────────────────────────────────────────

    public async Task<TaskGovernanceSettingsResponse> UpdateAsync(
        Guid tenantId, Guid actingUserId,
        UpdateTaskGovernanceSettingsRequest request, CancellationToken ct = default)
    {
        // Load from Task service — authoritative version source post-MIG-09.
        var existing = await _taskClient.GetGovernanceAsync(tenantId, ProductCode, ct);

        if (existing is null)
        {
            // First-time upsert: no version conflict possible.
            var newEntity = LienTaskGovernanceSettings.CreateDefault(
                tenantId:        tenantId,
                productCode:     ProductCode,
                updateSource:    request.UpdateSource,
                createdByUserId: actingUserId);

            newEntity.Update(
                requireAssigneeOnCreate:      request.RequireAssigneeOnCreate,
                requireCaseLinkOnCreate:      request.RequireCaseLinkOnCreate,
                allowMultipleAssignees:       request.AllowMultipleAssignees,
                requireWorkflowStageOnCreate: request.RequireWorkflowStageOnCreate,
                defaultStartStageMode:        request.DefaultStartStageMode,
                explicitStartStageId:         request.ExplicitStartStageId,
                updateSource:                 request.UpdateSource,
                updatedByUserId:              actingUserId,
                updatedByName:                request.UpdatedByName);

            var payload = BuildUpsertPayload(newEntity);
            await _taskClient.UpsertGovernanceAsync(tenantId, actingUserId, payload, ct);

            _logger.LogInformation(
                "governance_write_owner=task_service Created+Updated TenantId={TenantId} by UserId={UserId}",
                tenantId, actingUserId);

            _audit.Publish(
                eventType:   "liens.task_governance.updated",
                action:      "update",
                description: "Task governance settings updated (first-time create)",
                tenantId:    tenantId,
                actorUserId: actingUserId,
                entityType:  "LienTaskGovernanceSettings",
                entityId:    newEntity.Id.ToString());

            return MapToResponse(newEntity);
        }

        // Version check against Task service version (authoritative post-MIG-09).
        if (existing.Version != request.Version)
        {
            throw new ConflictException(
                $"Governance settings were modified by another user (expected version {request.Version}, current {existing.Version}). Please reload and try again.");
        }

        // Build upsert payload from request fields.
        var updatePayload = BuildUpdatePayload(request);
        await _taskClient.UpsertGovernanceAsync(tenantId, actingUserId, updatePayload, ct);

        _logger.LogInformation(
            "governance_write_owner=task_service Updated TenantId={TenantId} by UserId={UserId}",
            tenantId, actingUserId);

        _audit.Publish(
            eventType:   "liens.task_governance.updated",
            action:      "update",
            description: "Task governance settings updated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskGovernanceSettings",
            entityId:    existing.Id.ToString());

        return BuildResponseFromUpdate(existing, actingUserId, request);
    }

    // ── Payload builders ──────────────────────────────────────────────────────────

    private static TaskServiceGovernanceUpsertRequest BuildUpsertPayload(
        LienTaskGovernanceSettings entity)
    {
        var extensions = new LiensGovernanceExtensions
        {
            RequireCaseLinkOnCreate = entity.RequireCaseLinkOnCreate,
            AllowMultipleAssignees  = entity.AllowMultipleAssignees,
            DefaultStartStageMode   = entity.DefaultStartStageMode,
            ExplicitStartStageId    = entity.ExplicitStartStageId,
        };

        return new TaskServiceGovernanceUpsertRequest
        {
            RequireAssignee           = entity.RequireAssigneeOnCreate,
            RequireDueDate            = false,
            RequireStage              = entity.RequireWorkflowStageOnCreate,
            AllowUnassign             = true,
            AllowCancel               = true,
            AllowCompleteWithoutStage = !entity.RequireWorkflowStageOnCreate,
            AllowNotesOnClosedTasks   = false,
            DefaultPriority           = "MEDIUM",
            DefaultTaskScope          = "GENERAL",
            SourceProductCode         = ProductCode,
            ExpectedVersion           = 0,
            ProductSettingsJson       = JsonSerializer.Serialize(extensions, _json),
        };
    }

    private static TaskServiceGovernanceUpsertRequest BuildUpdatePayload(
        UpdateTaskGovernanceSettingsRequest request)
    {
        var extensions = new LiensGovernanceExtensions
        {
            RequireCaseLinkOnCreate = request.RequireCaseLinkOnCreate,
            AllowMultipleAssignees  = request.AllowMultipleAssignees,
            DefaultStartStageMode   = request.DefaultStartStageMode,
            ExplicitStartStageId    = request.ExplicitStartStageId,
        };

        return new TaskServiceGovernanceUpsertRequest
        {
            RequireAssignee           = request.RequireAssigneeOnCreate,
            RequireDueDate            = false,
            RequireStage              = request.RequireWorkflowStageOnCreate,
            AllowUnassign             = true,
            AllowCancel               = true,
            AllowCompleteWithoutStage = !request.RequireWorkflowStageOnCreate,
            AllowNotesOnClosedTasks   = false,
            DefaultPriority           = "MEDIUM",
            DefaultTaskScope          = "GENERAL",
            SourceProductCode         = ProductCode,
            ExpectedVersion           = 0,
            ProductSettingsJson       = JsonSerializer.Serialize(extensions, _json),
        };
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────────

    private static TaskGovernanceSettingsResponse MapFromTaskServiceDto(TaskServiceGovernanceResponse dto)
    {
        var extensions = DeserializeExtensions(dto.ProductSettingsJson);
        return new TaskGovernanceSettingsResponse
        {
            Id                           = dto.Id,
            TenantId                     = dto.TenantId,
            ProductCode                  = ProductCode,
            RequireAssigneeOnCreate      = dto.RequireAssignee,
            RequireCaseLinkOnCreate      = extensions.RequireCaseLinkOnCreate,
            AllowMultipleAssignees       = extensions.AllowMultipleAssignees,
            RequireWorkflowStageOnCreate = dto.RequireStage,
            DefaultStartStageMode        = extensions.DefaultStartStageMode,
            ExplicitStartStageId         = extensions.ExplicitStartStageId,
            Version                      = dto.Version,
            LastUpdatedAt                = dto.UpdatedAtUtc,
            LastUpdatedByUserId          = null,
            LastUpdatedByName            = null,
            LastUpdatedSource            = WorkflowUpdateSources.TaskServiceSync,
            CreatedAtUtc                 = dto.CreatedAtUtc,
            UpdatedAtUtc                 = dto.UpdatedAtUtc,
        };
    }

    private static TaskGovernanceSettingsResponse MapToResponse(LienTaskGovernanceSettings e) =>
        new()
        {
            Id                           = e.Id,
            TenantId                     = e.TenantId,
            ProductCode                  = e.ProductCode,
            RequireAssigneeOnCreate      = e.RequireAssigneeOnCreate,
            RequireCaseLinkOnCreate      = e.RequireCaseLinkOnCreate,
            AllowMultipleAssignees       = e.AllowMultipleAssignees,
            RequireWorkflowStageOnCreate = e.RequireWorkflowStageOnCreate,
            DefaultStartStageMode        = e.DefaultStartStageMode,
            ExplicitStartStageId         = e.ExplicitStartStageId,
            Version                      = e.Version,
            LastUpdatedAt                = e.LastUpdatedAt,
            LastUpdatedByUserId          = e.LastUpdatedByUserId,
            LastUpdatedByName            = e.LastUpdatedByName,
            LastUpdatedSource            = e.LastUpdatedSource,
            CreatedAtUtc                 = e.CreatedAtUtc,
            UpdatedAtUtc                 = e.UpdatedAtUtc,
        };

    private static TaskGovernanceSettingsResponse BuildResponseFromUpdate(
        TaskServiceGovernanceResponse existing, Guid actingUserId,
        UpdateTaskGovernanceSettingsRequest request)
    {
        var now = DateTime.UtcNow;
        return new TaskGovernanceSettingsResponse
        {
            Id                           = existing.Id,
            TenantId                     = existing.TenantId,
            ProductCode                  = ProductCode,
            RequireAssigneeOnCreate      = request.RequireAssigneeOnCreate,
            RequireCaseLinkOnCreate      = request.RequireCaseLinkOnCreate,
            AllowMultipleAssignees       = request.AllowMultipleAssignees,
            RequireWorkflowStageOnCreate = request.RequireWorkflowStageOnCreate,
            DefaultStartStageMode        = request.DefaultStartStageMode,
            ExplicitStartStageId         = request.ExplicitStartStageId,
            Version                      = existing.Version + 1,
            LastUpdatedAt                = now,
            LastUpdatedByUserId          = actingUserId,
            LastUpdatedByName            = request.UpdatedByName,
            LastUpdatedSource            = request.UpdateSource,
            CreatedAtUtc                 = existing.CreatedAtUtc,
            UpdatedAtUtc                 = now,
        };
    }

    private static LiensGovernanceExtensions DeserializeExtensions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new LiensGovernanceExtensions();

        try
        {
            return JsonSerializer.Deserialize<LiensGovernanceExtensions>(json, _json)
                   ?? new LiensGovernanceExtensions();
        }
        catch
        {
            return new LiensGovernanceExtensions();
        }
    }
}
