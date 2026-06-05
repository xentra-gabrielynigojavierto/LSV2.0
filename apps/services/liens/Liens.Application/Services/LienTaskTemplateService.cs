using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// TASK-MIG-09 — Final Cleanup: Task service is the sole authoritative owner of templates.
///
/// Read authority  : Task service only. No Liens DB fallback.
///                  If the Task service is unavailable, reads fail (correct behavior
///                  for a single authoritative owner).
///
/// Write authority : Task service only. No Liens DB mirrors.
///                  template_write_owner=task_service (invariant)
///
/// Removed in MIG-09:
///   - ILienTaskTemplateRepository / Liens DB fallback reads
///   - TryMirrorCreateToLiensDbAsync / TryMirrorUpdateToLiensDbAsync
///   - RequireTemplate (Liens DB load) → replaced by RequireFromTaskServiceAsync
///   - Version conflict check against Liens DB → now uses Task service version
///
/// LienTaskTemplate domain entity is retained as an in-memory value object for
/// CreateAsync only: ID generation + field validation + payload building.
/// It is never persisted to Liens DB.
/// </summary>
public sealed class LienTaskTemplateService : ILienTaskTemplateService
{
    private const string ProductCode  = "SYNQ_LIENS";
    private const string DefaultScope = "GENERAL";

    private readonly ILiensTaskServiceClient          _taskClient;
    private readonly IAuditPublisher                  _audit;
    private readonly ILogger<LienTaskTemplateService> _logger;

    public LienTaskTemplateService(
        ILiensTaskServiceClient          taskClient,
        IAuditPublisher                  audit,
        ILogger<LienTaskTemplateService> logger)
    {
        _taskClient = taskClient;
        _audit      = audit;
        _logger     = logger;
    }

    // ── Read methods: Task service only ──────────────────────────────────────────

    public async Task<List<TaskTemplateResponse>> GetByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var all = await _taskClient.GetAllTemplatesAsync(tenantId, ProductCode, ct);
        _logger.LogDebug(
            "template_read_owner=task_service GetByTenant TenantId={TenantId} Count={Count}",
            tenantId, all.Count);
        return all.Select(MapFromTaskServiceDto).ToList();
    }

    public async Task<List<TaskTemplateResponse>> GetContextualAsync(
        Guid tenantId, string? contextType, Guid? workflowStageId, CancellationToken ct = default)
    {
        var all = await _taskClient.GetAllTemplatesAsync(tenantId, ProductCode, ct);

        _logger.LogDebug(
            "template_read_owner=task_service GetContextual TenantId={TenantId} contextType={ContextType} stageId={StageId} Total={Count}",
            tenantId, contextType, workflowStageId, all.Count);

        var filtered = all
            .Select(dto => new { dto, ext = LiensTemplateExtensions.Deserialize(dto.ProductSettingsJson) })
            .Where(x => IsContextualMatch(x.dto, x.ext, contextType, workflowStageId))
            .OrderBy(x => ContextualSortOrder(x.ext.ContextType, contextType))
            .ThenBy(x => x.dto.Name)
            .Select(x => MapFromTaskServiceDto(x.dto))
            .ToList();

        return filtered;
    }

    public async Task<TaskTemplateResponse?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var dto = await _taskClient.GetTemplateAsync(tenantId, id, ct);
        if (dto is null)
        {
            _logger.LogDebug(
                "template_read_owner=task_service GetById TemplateId={TemplateId} TenantId={TenantId} — not found",
                id, tenantId);
            return null;
        }

        _logger.LogDebug(
            "template_read_owner=task_service GetById TemplateId={TemplateId} TenantId={TenantId}",
            id, tenantId);
        return MapFromTaskServiceDto(dto);
    }

    public async Task<TaskTemplateResponse?> GetForGenerationAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var dto = await _taskClient.GetTemplateAsync(tenantId, id, ct);
        if (dto is null)
        {
            _logger.LogDebug(
                "template_read_owner=task_service GetForGeneration TemplateId={TemplateId} TenantId={TenantId} — not found",
                id, tenantId);
            return null;
        }

        _logger.LogDebug(
            "template_read_owner=task_service GetForGeneration TemplateId={TemplateId} TenantId={TenantId}",
            id, tenantId);
        return MapFromTaskServiceDto(dto);
    }

    // ── Write methods: Task service only ─────────────────────────────────────────

    public async Task<TaskTemplateResponse> CreateAsync(
        Guid tenantId, Guid actingUserId, CreateTaskTemplateRequest request, CancellationToken ct = default)
    {
        var errors = Validate(request.Name, request.DefaultTitle, request.ContextType, request.UpdateSource);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        // Build the domain entity in-memory: ID generation + field validation only.
        // Never persisted to Liens DB (MIG-09).
        var entity = LienTaskTemplate.Create(
            tenantId:                  tenantId,
            name:                      request.Name,
            defaultTitle:              request.DefaultTitle,
            defaultPriority:           request.DefaultPriority,
            contextType:               request.ContextType,
            updateSource:              request.UpdateSource,
            createdByUserId:           actingUserId,
            description:               request.Description,
            defaultDescription:        request.DefaultDescription,
            defaultDueOffsetDays:      request.DefaultDueOffsetDays,
            defaultRoleId:             request.DefaultRoleId,
            applicableWorkflowStageId: request.ApplicableWorkflowStageId,
            updatedByName:             request.UpdatedByName);

        // PRIMARY WRITE — Task service (sole authority)
        var payload = MapToUpsertPayload(entity);
        await _taskClient.UpsertTemplateFromSourceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "template_write_owner=task_service Created TemplateId={TemplateId} TenantId={TenantId}",
            entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_template.created",
            action:      "create",
            description: $"Task template '{entity.Name}' created",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    entity.Id.ToString());

        return MapToResponse(entity);
    }

    public async Task<TaskTemplateResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId, UpdateTaskTemplateRequest request, CancellationToken ct = default)
    {
        var errors = Validate(request.Name, request.DefaultTitle, request.ContextType, request.UpdateSource);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        // Load from Task service — authoritative version source post-MIG-09.
        var existing = await RequireFromTaskServiceAsync(tenantId, id, ct);

        if (existing.Version != request.Version)
            throw new ConflictException(
                $"Stale version — expected {existing.Version}, got {request.Version}. Reload and retry.",
                "TASK_TEMPLATE_VERSION_CONFLICT");

        // Build upsert payload directly from request fields.
        var payload = BuildUpdatePayload(id, existing.IsActive, request);
        await _taskClient.UpsertTemplateFromSourceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "template_write_owner=task_service Updated TemplateId={TemplateId} TenantId={TenantId}",
            id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_template.updated",
            action:      "update",
            description: $"Task template '{request.Name}' updated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    id.ToString());

        return BuildResponseFromUpdate(id, tenantId, actingUserId, request, existing);
    }

    public async Task<TaskTemplateResponse> ActivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateTemplateRequest request, CancellationToken ct = default)
    {
        if (!WorkflowUpdateSources.All.Contains(request.UpdateSource))
            throw new ValidationException("Validation failed.",
                new Dictionary<string, string[]> { { "updateSource", [$"Invalid updateSource '{request.UpdateSource}'."] } });

        var existing = await RequireFromTaskServiceAsync(tenantId, id, ct);

        var payload = BuildActivationPayload(id, existing, isActive: true);
        await _taskClient.UpsertTemplateFromSourceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "template_write_owner=task_service Activated TemplateId={TemplateId} TenantId={TenantId}",
            id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_template.activated",
            action:      "activate",
            description: $"Task template '{existing.Name}' activated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    id.ToString());

        return BuildResponseFromExisting(id, tenantId, actingUserId, request, existing, isActive: true);
    }

    public async Task<TaskTemplateResponse> DeactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, ActivateDeactivateTemplateRequest request, CancellationToken ct = default)
    {
        if (!WorkflowUpdateSources.All.Contains(request.UpdateSource))
            throw new ValidationException("Validation failed.",
                new Dictionary<string, string[]> { { "updateSource", [$"Invalid updateSource '{request.UpdateSource}'."] } });

        var existing = await RequireFromTaskServiceAsync(tenantId, id, ct);

        var payload = BuildActivationPayload(id, existing, isActive: false);
        await _taskClient.UpsertTemplateFromSourceAsync(tenantId, actingUserId, payload, ct);

        _logger.LogInformation(
            "template_write_owner=task_service Deactivated TemplateId={TemplateId} TenantId={TenantId}",
            id, tenantId);

        _audit.Publish(
            eventType:   "liens.task_template.deactivated",
            action:      "deactivate",
            description: $"Task template '{existing.Name}' deactivated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTaskTemplate",
            entityId:    id.ToString());

        return BuildResponseFromExisting(id, tenantId, actingUserId, request, existing, isActive: false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<TaskServiceTemplateResponse> RequireFromTaskServiceAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var dto = await _taskClient.GetTemplateAsync(tenantId, id, ct);
        if (dto is null)
            throw new NotFoundException($"Task template '{id}' not found.");
        return dto;
    }

    private static bool IsContextualMatch(
        TaskServiceTemplateResponse dto,
        LiensTemplateExtensions     ext,
        string?                     contextType,
        Guid?                       workflowStageId)
    {
        if (!dto.IsActive) return false;
        if (string.IsNullOrWhiteSpace(contextType)) return true;

        var ct2 = ext.ContextType;
        return ct2 == TaskTemplateContextType.General
            || ct2 == contextType
            || (ct2 == TaskTemplateContextType.Stage
                && workflowStageId.HasValue
                && ext.ApplicableWorkflowStageId == workflowStageId);
    }

    private static int ContextualSortOrder(string ctxType, string? requestedContextType)
    {
        if (ctxType == TaskTemplateContextType.Stage)  return 0;
        if (ctxType == requestedContextType)           return 1;
        return 2;
    }

    private static Dictionary<string, string[]> Validate(
        string name, string defaultTitle, string contextType, string updateSource)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(name))
            errors.Add("name", ["Name is required."]);
        if (string.IsNullOrWhiteSpace(defaultTitle))
            errors.Add("defaultTitle", ["DefaultTitle is required."]);
        if (!TaskTemplateContextType.All.Contains(contextType))
            errors.Add("contextType", [$"Invalid contextType '{contextType}'."]);
        if (!WorkflowUpdateSources.All.Contains(updateSource))
            errors.Add("updateSource", [$"Invalid updateSource '{updateSource}'."]);
        return errors;
    }

    // ── Payload builders ──────────────────────────────────────────────────────────

    public static TaskServiceTemplateUpsertRequest MapToUpsertPayload(LienTaskTemplate entity)
    {
        var ext = new LiensTemplateExtensions
        {
            ContextType               = entity.ContextType,
            ApplicableWorkflowStageId = entity.ApplicableWorkflowStageId,
            DefaultRoleId             = entity.DefaultRoleId,
        };

        return new TaskServiceTemplateUpsertRequest
        {
            Id                  = entity.Id,
            Code                = entity.Id.ToString("N").ToUpperInvariant(),
            Name                = entity.Name,
            DefaultTitle        = entity.DefaultTitle,
            SourceProductCode   = ProductCode,
            Description         = entity.Description,
            DefaultDescription  = entity.DefaultDescription,
            DefaultPriority     = entity.DefaultPriority,
            DefaultScope        = DefaultScope,
            DefaultDueInDays    = entity.DefaultDueOffsetDays,
            DefaultStageId      = null,
            IsActive            = entity.IsActive,
            ProductSettingsJson = ext.Serialize(),
        };
    }

    private static TaskServiceTemplateUpsertRequest BuildUpdatePayload(
        Guid id, bool currentIsActive, UpdateTaskTemplateRequest request)
    {
        var ext = new LiensTemplateExtensions
        {
            ContextType               = request.ContextType,
            ApplicableWorkflowStageId = request.ApplicableWorkflowStageId,
            DefaultRoleId             = request.DefaultRoleId,
        };

        return new TaskServiceTemplateUpsertRequest
        {
            Id                  = id,
            Code                = id.ToString("N").ToUpperInvariant(),
            Name                = request.Name,
            DefaultTitle        = request.DefaultTitle,
            SourceProductCode   = ProductCode,
            Description         = request.Description,
            DefaultDescription  = request.DefaultDescription,
            DefaultPriority     = request.DefaultPriority,
            DefaultScope        = DefaultScope,
            DefaultDueInDays    = request.DefaultDueOffsetDays,
            DefaultStageId      = null,
            IsActive            = currentIsActive,
            ProductSettingsJson = ext.Serialize(),
        };
    }

    private static TaskServiceTemplateUpsertRequest BuildActivationPayload(
        Guid id, TaskServiceTemplateResponse existing, bool isActive)
    {
        return new TaskServiceTemplateUpsertRequest
        {
            Id                  = id,
            Code                = id.ToString("N").ToUpperInvariant(),
            Name                = existing.Name,
            DefaultTitle        = existing.DefaultTitle,
            SourceProductCode   = ProductCode,
            Description         = existing.Description,
            DefaultDescription  = existing.DefaultDescription,
            DefaultPriority     = existing.DefaultPriority,
            DefaultScope        = DefaultScope,
            DefaultDueInDays    = existing.DefaultDueInDays,
            DefaultStageId      = null,
            IsActive            = isActive,
            ProductSettingsJson = existing.ProductSettingsJson,
        };
    }

    // ── Response builders ─────────────────────────────────────────────────────────

    private static TaskTemplateResponse MapFromTaskServiceDto(TaskServiceTemplateResponse dto)
    {
        var ext = LiensTemplateExtensions.Deserialize(dto.ProductSettingsJson);
        return new TaskTemplateResponse
        {
            Id                        = dto.Id,
            TenantId                  = dto.TenantId,
            ProductCode               = ProductCode,
            Name                      = dto.Name,
            Description               = dto.Description,
            DefaultTitle              = dto.DefaultTitle,
            DefaultDescription        = dto.DefaultDescription,
            DefaultPriority           = dto.DefaultPriority,
            DefaultDueOffsetDays      = dto.DefaultDueInDays,
            DefaultRoleId             = ext.DefaultRoleId,
            ContextType               = ext.ContextType,
            ApplicableWorkflowStageId = ext.ApplicableWorkflowStageId,
            IsActive                  = dto.IsActive,
            Version                   = dto.Version,
            LastUpdatedAt             = dto.UpdatedAtUtc,
            LastUpdatedByUserId       = null,
            LastUpdatedByName         = null,
            LastUpdatedSource         = WorkflowUpdateSources.TaskServiceSync,
            CreatedAtUtc              = dto.CreatedAtUtc,
            UpdatedAtUtc              = dto.UpdatedAtUtc,
        };
    }

    private static TaskTemplateResponse MapToResponse(LienTaskTemplate e) => new()
    {
        Id                        = e.Id,
        TenantId                  = e.TenantId,
        ProductCode               = e.ProductCode,
        Name                      = e.Name,
        Description               = e.Description,
        DefaultTitle              = e.DefaultTitle,
        DefaultDescription        = e.DefaultDescription,
        DefaultPriority           = e.DefaultPriority,
        DefaultDueOffsetDays      = e.DefaultDueOffsetDays,
        DefaultRoleId             = e.DefaultRoleId,
        ContextType               = e.ContextType,
        ApplicableWorkflowStageId = e.ApplicableWorkflowStageId,
        IsActive                  = e.IsActive,
        Version                   = e.Version,
        LastUpdatedAt             = e.LastUpdatedAt,
        LastUpdatedByUserId       = e.LastUpdatedByUserId,
        LastUpdatedByName         = e.LastUpdatedByName,
        LastUpdatedSource         = e.LastUpdatedSource,
        CreatedAtUtc              = e.CreatedAtUtc,
        UpdatedAtUtc              = e.UpdatedAtUtc,
    };

    private static TaskTemplateResponse BuildResponseFromUpdate(
        Guid id, Guid tenantId, Guid actingUserId,
        UpdateTaskTemplateRequest request, TaskServiceTemplateResponse existing)
    {
        var now = DateTime.UtcNow;
        return new TaskTemplateResponse
        {
            Id                        = id,
            TenantId                  = tenantId,
            ProductCode               = ProductCode,
            Name                      = request.Name,
            Description               = request.Description,
            DefaultTitle              = request.DefaultTitle,
            DefaultDescription        = request.DefaultDescription,
            DefaultPriority           = request.DefaultPriority,
            DefaultDueOffsetDays      = request.DefaultDueOffsetDays,
            DefaultRoleId             = request.DefaultRoleId,
            ContextType               = request.ContextType,
            ApplicableWorkflowStageId = request.ApplicableWorkflowStageId,
            IsActive                  = existing.IsActive,
            Version                   = existing.Version + 1,
            LastUpdatedAt             = now,
            LastUpdatedByUserId       = actingUserId,
            LastUpdatedByName         = request.UpdatedByName,
            LastUpdatedSource         = request.UpdateSource,
            CreatedAtUtc              = existing.CreatedAtUtc,
            UpdatedAtUtc              = now,
        };
    }

    private static TaskTemplateResponse BuildResponseFromExisting(
        Guid id, Guid tenantId, Guid actingUserId,
        ActivateDeactivateTemplateRequest request,
        TaskServiceTemplateResponse existing, bool isActive)
    {
        var ext = LiensTemplateExtensions.Deserialize(existing.ProductSettingsJson);
        var now = DateTime.UtcNow;
        return new TaskTemplateResponse
        {
            Id                        = id,
            TenantId                  = tenantId,
            ProductCode               = ProductCode,
            Name                      = existing.Name,
            Description               = existing.Description,
            DefaultTitle              = existing.DefaultTitle,
            DefaultDescription        = existing.DefaultDescription,
            DefaultPriority           = existing.DefaultPriority,
            DefaultDueOffsetDays      = existing.DefaultDueInDays,
            DefaultRoleId             = ext.DefaultRoleId,
            ContextType               = ext.ContextType,
            ApplicableWorkflowStageId = ext.ApplicableWorkflowStageId,
            IsActive                  = isActive,
            Version                   = existing.Version + 1,
            LastUpdatedAt             = now,
            LastUpdatedByUserId       = actingUserId,
            LastUpdatedByName         = request.UpdatedByName,
            LastUpdatedSource         = request.UpdateSource,
            CreatedAtUtc              = existing.CreatedAtUtc,
            UpdatedAtUtc              = now,
        };
    }
}
