using System.Text.Json;
using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class LienWorkflowConfigService : ILienWorkflowConfigService
{
    private const string ProductCode = "SYNQ_LIENS";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly ILienWorkflowConfigRepository      _repo;
    private readonly ILiensTaskServiceClient            _taskClient;
    private readonly IAuditPublisher                    _audit;
    private readonly ILogger<LienWorkflowConfigService> _logger;

    public LienWorkflowConfigService(
        ILienWorkflowConfigRepository      repo,
        ILiensTaskServiceClient            taskClient,
        IAuditPublisher                    audit,
        ILogger<LienWorkflowConfigService> logger)
    {
        _repo       = repo;
        _taskClient = taskClient;
        _audit      = audit;
        _logger     = logger;
    }

    // ── GetByTenantAsync — dual-read for stages ──────────────────────────────────

    public async Task<WorkflowConfigResponse?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);
        if (entity is null) return null;

        await EnsureDefaultTransitionsAsync(entity, ct);

        // TASK-MIG-03: try to load stage list from Task service first
        var stages = await TryLoadStagesFromTaskServiceAsync(tenantId, entity, ct);
        return MapToResponseWithStages(entity, stages);
    }

    // ── GetStageForRuntimeAsync — dual-read for single stage ─────────────────────

    public async Task<WorkflowStageResponse?> GetStageForRuntimeAsync(
        Guid tenantId, Guid stageId, CancellationToken ct = default)
    {
        // 1. Try Task service
        try
        {
            var dto = await _taskClient.GetStageAsync(tenantId, stageId, ct);
            if (dto is not null)
            {
                _logger.LogDebug(
                    "stage_source=task_service StageId={StageId} TenantId={TenantId}", stageId, tenantId);
                return MapFromTaskServiceStage(dto, workflowConfigId: null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "stage_source=task_service_error StageId={StageId} TenantId={TenantId}; falling back to Liens DB.",
                stageId, tenantId);
        }

        // 2. Fall back to Liens DB
        var stage = await _repo.GetStageGlobalAsync(stageId, ct);
        if (stage is not null)
        {
            _logger.LogDebug(
                "stage_source=liens_fallback StageId={StageId} TenantId={TenantId}", stageId, tenantId);
            return MapStageToResponse(stage);
        }

        return null;
    }

    public async Task<WorkflowConfigResponse> CreateAsync(
        Guid tenantId, Guid actingUserId, CreateWorkflowConfigRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.WorkflowName))
            errors.Add("workflowName", ["WorkflowName is required."]);
        if (!WorkflowUpdateSources.All.Contains(request.UpdateSource))
            errors.Add("updateSource", [$"Invalid updateSource '{request.UpdateSource}'."]);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        var existing = await _repo.GetByTenantProductAsync(tenantId, ProductCode, ct);
        if (existing is not null)
            throw new ConflictException(
                $"Workflow config for tenant already exists. Use PUT to update.",
                "WORKFLOW_CONFIG_EXISTS");

        var entity = LienWorkflowConfig.Create(
            tenantId:      tenantId,
            productCode:   ProductCode,
            workflowName:  request.WorkflowName,
            updateSource:  request.UpdateSource,
            createdByUserId: actingUserId,
            updatedByName: request.UpdatedByName);

        await _repo.AddAsync(entity, ct);

        _logger.LogInformation("WorkflowConfig created: {ConfigId} Tenant={TenantId}", entity.Id, tenantId);

        _audit.Publish(
            eventType:   "liens.workflow_config.created",
            action:      "create",
            description: $"Workflow '{entity.WorkflowName}' created from {request.UpdateSource}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowConfig",
            entityId:    entity.Id.ToString());

        return MapToResponseWithStages(entity, null);
    }

    public async Task<WorkflowConfigResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId, UpdateWorkflowConfigRequest request, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);

        if (entity.Version != request.Version)
            throw new ConflictException(
                $"Stale version — expected {entity.Version}, got {request.Version}. Reload and retry.",
                "WORKFLOW_CONFIG_VERSION_CONFLICT");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.WorkflowName))
            errors.Add("workflowName", ["WorkflowName is required."]);
        if (!WorkflowUpdateSources.All.Contains(request.UpdateSource))
            errors.Add("updateSource", [$"Invalid updateSource '{request.UpdateSource}'."]);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        entity.Update(request.WorkflowName, request.IsActive, request.UpdateSource, actingUserId, request.UpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.workflow_config.updated",
            action:      "update",
            description: $"Workflow '{entity.WorkflowName}' updated from {request.UpdateSource}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowConfig",
            entityId:    entity.Id.ToString());

        return MapToResponseWithStages(entity, null);
    }

    public async Task<WorkflowConfigResponse> AddStageAsync(
        Guid tenantId, Guid id, Guid actingUserId, AddWorkflowStageRequest request, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.StageName))
            errors.Add("stageName", ["StageName is required."]);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        var stage = LienWorkflowStage.Create(
            entity.Id, request.StageName, request.StageOrder, actingUserId,
            request.Description, request.DefaultOwnerRole, request.SlaMetadata);

        await _repo.AddStageAsync(stage, ct);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.workflow_stage.added",
            action:      "create",
            description: $"Stage '{stage.StageName}' added to workflow '{entity.WorkflowName}'",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowStage",
            entityId:    stage.Id.ToString());

        // TASK-MIG-03: best-effort write-through to Task service
        await TrySyncStageToTaskServiceAsync(tenantId, actingUserId, stage, ct);

        var updated = await _repo.GetByIdAsync(tenantId, id, ct);
        return MapToResponseWithStages(updated!, null);
    }

    public async Task<WorkflowConfigResponse> UpdateStageAsync(
        Guid tenantId, Guid id, Guid stageId, Guid actingUserId, UpdateWorkflowStageRequest request, CancellationToken ct = default)
    {
        await RequireConfig(tenantId, id, ct);
        var stage = await _repo.GetStageByIdAsync(id, stageId, ct)
            ?? throw new NotFoundException($"Stage '{stageId}' not found.");

        if (string.IsNullOrWhiteSpace(request.StageName))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
            {
                ["stageName"] = ["StageName is required."]
            });

        stage.Update(request.StageName, request.StageOrder, request.IsActive, actingUserId,
            request.Description, request.DefaultOwnerRole, request.SlaMetadata);

        await _repo.UpdateStageAsync(stage, ct);

        _audit.Publish(
            eventType:   "liens.workflow_stage.updated",
            action:      "update",
            description: $"Stage '{stage.StageName}' updated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowStage",
            entityId:    stage.Id.ToString());

        // TASK-MIG-03: best-effort write-through to Task service
        await TrySyncStageToTaskServiceAsync(tenantId, actingUserId, stage, ct);

        var updated = await _repo.GetByIdAsync(tenantId, id, ct);
        return MapToResponseWithStages(updated!, null);
    }

    public async Task<WorkflowConfigResponse> RemoveStageAsync(
        Guid tenantId, Guid id, Guid stageId, Guid actingUserId, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);
        var stage  = await _repo.GetStageByIdAsync(id, stageId, ct)
            ?? throw new NotFoundException($"Stage '{stageId}' not found.");

        stage.Deactivate(actingUserId);
        await _repo.UpdateStageAsync(stage, ct);

        entity.Update(entity.WorkflowName, entity.IsActive, entity.LastUpdatedSource, actingUserId, entity.LastUpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.workflow_stage.deactivated",
            action:      "update",
            description: $"Stage '{stage.StageName}' deactivated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowStage",
            entityId:    stage.Id.ToString());

        // TASK-MIG-03: best-effort write-through to Task service (stage is now inactive)
        await TrySyncStageToTaskServiceAsync(tenantId, actingUserId, stage, ct);

        var updated = await _repo.GetByIdAsync(tenantId, id, ct);
        return MapToResponseWithStages(updated!, null);
    }

    public async Task<WorkflowConfigResponse> ReorderStagesAsync(
        Guid tenantId, Guid id, Guid actingUserId, ReorderStagesRequest request, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);

        var reorderedStages = new List<LienWorkflowStage>();

        foreach (var entry in request.Stages)
        {
            var stage = await _repo.GetStageByIdAsync(id, entry.StageId, ct);
            if (stage is null) continue;
            stage.Update(stage.StageName, entry.StageOrder, stage.IsActive, actingUserId,
                stage.Description, stage.DefaultOwnerRole, stage.SlaMetadata);
            await _repo.UpdateStageAsync(stage, ct);
            reorderedStages.Add(stage);
        }

        entity.Update(entity.WorkflowName, entity.IsActive, entity.LastUpdatedSource, actingUserId, entity.LastUpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.workflow_stage.reordered",
            action:      "update",
            description: $"Workflow '{entity.WorkflowName}' stages reordered",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowConfig",
            entityId:    entity.Id.ToString());

        // TASK-MIG-03: best-effort write-through for each reordered stage
        foreach (var stage in reorderedStages)
            await TrySyncStageToTaskServiceAsync(tenantId, actingUserId, stage, ct);

        var updated = await _repo.GetByIdAsync(tenantId, id, ct);
        return MapToResponseWithStages(updated!, null);
    }

    // ── Transition management (LS-LIENS-FLOW-005) ─────────────────────────────

    public async Task<IReadOnlyList<WorkflowTransitionResponse>> GetTransitionsAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);
        var transitions = await _repo.GetActiveTransitionsAsync(entity.Id, ct);
        return transitions.Select(t => new WorkflowTransitionResponse
        {
            Id               = t.Id,
            WorkflowConfigId = t.WorkflowConfigId,
            FromStageId      = t.FromStageId,
            ToStageId        = t.ToStageId,
            IsActive         = t.IsActive,
            SortOrder        = t.SortOrder,
            CreatedAtUtc     = t.CreatedAtUtc,
            UpdatedAtUtc     = t.UpdatedAtUtc,
        }).ToList();
    }

    public async Task<WorkflowConfigResponse> AddTransitionAsync(
        Guid tenantId, Guid id, Guid actingUserId, AddWorkflowTransitionRequest request, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);

        ValidateStagesBelongToWorkflow(entity, request.FromStageId, request.ToStageId);

        if (request.FromStageId == request.ToStageId)
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
            {
                ["toStageId"] = ["A transition cannot point to the same stage (self-transition)."]
            });

        var exists = await _repo.TransitionExistsAsync(id, request.FromStageId, request.ToStageId, ct);
        if (exists)
            throw new ConflictException(
                "An active transition between these two stages already exists.",
                "TRANSITION_ALREADY_EXISTS");

        var transition = LienWorkflowTransition.Create(
            workflowConfigId: id,
            fromStageId:      request.FromStageId,
            toStageId:        request.ToStageId,
            createdByUserId:  actingUserId,
            sortOrder:        request.SortOrder);

        await _repo.AddTransitionAsync(transition, ct);

        entity.BumpVersion(actingUserId, entity.LastUpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.workflow_transition.created",
            action:      "create",
            description: $"Task stage transition {request.FromStageId} → {request.ToStageId} added to workflow '{entity.WorkflowName}'",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowTransition",
            entityId:    transition.Id.ToString());

        // TASK-MIG-04: best-effort write-through to Task service
        await TrySyncTransitionsToTaskServiceAsync(tenantId, actingUserId, id, ct);

        var updated = await _repo.GetByIdAsync(tenantId, id, ct);
        return MapToResponseWithStages(updated!, null);
    }

    public async Task<WorkflowConfigResponse> DeactivateTransitionAsync(
        Guid tenantId, Guid id, Guid transitionId, Guid actingUserId, CancellationToken ct = default)
    {
        var entity     = await RequireConfig(tenantId, id, ct);
        var transition = await _repo.GetTransitionByIdAsync(id, transitionId, ct)
            ?? throw new NotFoundException($"Transition '{transitionId}' not found.");

        transition.Deactivate(actingUserId);
        await _repo.UpdateTransitionAsync(transition, ct);

        entity.BumpVersion(actingUserId, entity.LastUpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _audit.Publish(
            eventType:   "liens.workflow_transition.deactivated",
            action:      "update",
            description: $"Task stage transition '{transitionId}' deactivated in workflow '{entity.WorkflowName}'",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowTransition",
            entityId:    transitionId.ToString());

        // TASK-MIG-04: best-effort write-through to Task service
        await TrySyncTransitionsToTaskServiceAsync(tenantId, actingUserId, id, ct);

        var updated = await _repo.GetByIdAsync(tenantId, id, ct);
        return MapToResponseWithStages(updated!, null);
    }

    public async Task<IReadOnlyList<WorkflowTransitionResponse>> SaveTransitionsAsync(
        Guid tenantId, Guid id, Guid actingUserId, SaveWorkflowTransitionsRequest request, CancellationToken ct = default)
    {
        var entity = await RequireConfig(tenantId, id, ct);

        var errors = new Dictionary<string, string[]>();
        if (!string.IsNullOrWhiteSpace(request.UpdateSource)
            && !WorkflowUpdateSources.All.Contains(request.UpdateSource))
            errors.Add("updateSource", [$"Invalid updateSource '{request.UpdateSource}'."]);

        foreach (var entry in request.Transitions)
        {
            if (entry.FromStageId == entry.ToStageId)
            {
                errors.Add("transitions", ["A transition cannot point to the same stage."]);
                break;
            }
        }

        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        // Deactivate all current active transitions
        await _repo.DeactivateAllTransitionsAsync(id, ct);

        // Re-create the desired set
        var toCreate = new List<LienWorkflowTransition>();
        var seen     = new HashSet<(Guid, Guid)>();

        foreach (var entry in request.Transitions)
        {
            var key = (entry.FromStageId, entry.ToStageId);
            if (seen.Contains(key)) continue;
            seen.Add(key);

            toCreate.Add(LienWorkflowTransition.Create(
                workflowConfigId: id,
                fromStageId:      entry.FromStageId,
                toStageId:        entry.ToStageId,
                createdByUserId:  actingUserId,
                sortOrder:        entry.SortOrder));
        }

        if (toCreate.Count > 0)
            await _repo.AddTransitionsAsync(toCreate, ct);

        var updateSource = !string.IsNullOrWhiteSpace(request.UpdateSource)
            ? request.UpdateSource
            : entity.LastUpdatedSource;

        entity.Update(entity.WorkflowName, entity.IsActive, updateSource, actingUserId, request.UpdatedByName ?? entity.LastUpdatedByName);
        await _repo.UpdateAsync(entity, ct);

        _logger.LogInformation(
            "Transitions batch-saved for workflow {WorkflowId}: {Count} active transitions",
            id, toCreate.Count);

        _audit.Publish(
            eventType:   "liens.workflow_transition.saved",
            action:      "update",
            description: $"Workflow '{entity.WorkflowName}' task stage transitions replaced: {toCreate.Count} active",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienWorkflowConfig",
            entityId:    entity.Id.ToString());

        // TASK-MIG-04: best-effort write-through to Task service
        await TrySyncTransitionsToTaskServiceAsync(tenantId, actingUserId, id, ct);

        return toCreate.Select(t => new WorkflowTransitionResponse
        {
            Id               = t.Id,
            WorkflowConfigId = t.WorkflowConfigId,
            FromStageId      = t.FromStageId,
            ToStageId        = t.ToStageId,
            IsActive         = t.IsActive,
            SortOrder        = t.SortOrder,
            CreatedAtUtc     = t.CreatedAtUtc,
            UpdatedAtUtc     = t.UpdatedAtUtc,
        }).ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<LienWorkflowConfig> RequireConfig(Guid tenantId, Guid id, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct);
        if (entity is null)
            throw new NotFoundException($"WorkflowConfig '{id}' not found.");
        return entity;
    }

    private static void ValidateStagesBelongToWorkflow(LienWorkflowConfig entity, Guid fromStageId, Guid toStageId)
    {
        var stageIds = entity.Stages.Select(s => s.Id).ToHashSet();
        var errors   = new Dictionary<string, string[]>();

        if (!stageIds.Contains(fromStageId))
            errors.Add("fromStageId", [$"Stage '{fromStageId}' does not belong to this workflow."]);
        if (!stageIds.Contains(toStageId))
            errors.Add("toStageId",   [$"Stage '{toStageId}' does not belong to this workflow."]);

        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);
    }

    /// <summary>
    /// Lazy default initialization: if a workflow has stages but no active transitions,
    /// auto-generate linear transitions based on stage order and persist them.
    /// </summary>
    private async Task EnsureDefaultTransitionsAsync(LienWorkflowConfig entity, CancellationToken ct)
    {
        var activeStages = entity.Stages
            .Where(s => s.IsActive)
            .OrderBy(s => s.StageOrder)
            .ToList();

        if (activeStages.Count < 2) return;

        var existingTransitions = await _repo.GetActiveTransitionsAsync(entity.Id, ct);
        if (existingTransitions.Count > 0) return;

        _logger.LogInformation(
            "Auto-initializing linear transitions for workflow {WorkflowId} ({Count} stages)",
            entity.Id, activeStages.Count);

        var defaultTransitions = new List<LienWorkflowTransition>();
        for (int i = 0; i < activeStages.Count - 1; i++)
        {
            defaultTransitions.Add(LienWorkflowTransition.Create(
                workflowConfigId: entity.Id,
                fromStageId:      activeStages[i].Id,
                toStageId:        activeStages[i + 1].Id,
                createdByUserId:  entity.CreatedByUserId ?? Guid.Empty,
                sortOrder:        i));
        }

        await _repo.AddTransitionsAsync(defaultTransitions, ct);

        // Reload transitions into the in-memory entity for the response
        entity.Transitions.AddRange(defaultTransitions);

        _audit.Publish(
            eventType:   "liens.workflow_transition.initialized",
            action:      "create",
            description: $"Workflow '{entity.WorkflowName}' auto-initialized with {defaultTransitions.Count} linear task stage transitions",
            tenantId:    entity.TenantId,
            actorUserId: entity.CreatedByUserId,
            entityType:  "LienWorkflowConfig",
            entityId:    entity.Id.ToString());
    }

    // ── TASK-MIG-03 — Private helpers ────────────────────────────────────────────

    /// <summary>
    /// Attempts to load the stage list from the Task service.
    /// On success returns the mapped WorkflowStageResponse list.
    /// On empty result or failure returns null (caller falls back to entity.Stages).
    /// </summary>
    private async Task<List<WorkflowStageResponse>?> TryLoadStagesFromTaskServiceAsync(
        Guid tenantId, LienWorkflowConfig entity, CancellationToken ct)
    {
        try
        {
            var dtos = await _taskClient.GetAllStagesAsync(tenantId, ProductCode, ct);
            if (dtos.Count == 0)
                return null;

            _logger.LogDebug(
                "stage_list_source=task_service TenantId={TenantId} Count={Count}", tenantId, dtos.Count);

            return dtos
                .Select(d => MapFromTaskServiceStage(d, workflowConfigId: entity.Id))
                .OrderBy(s => s.StageOrder)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "stage_list_source=task_service_error TenantId={TenantId}; falling back to Liens DB stages.",
                tenantId);
            return null;
        }
    }

    /// <summary>
    /// Best-effort sync of a single Liens stage to the Task service.
    /// Failures are logged but never propagate to the caller.
    /// </summary>
    private async System.Threading.Tasks.Task TrySyncStageToTaskServiceAsync(
        Guid tenantId, Guid actingUserId, LienWorkflowStage stage, CancellationToken ct)
    {
        try
        {
            var payload = BuildStageUpsertPayload(stage);
            await _taskClient.UpsertStageFromSourceAsync(tenantId, actingUserId, payload, ct);
            _logger.LogInformation(
                "stage_sync=ok StageId={StageId} TenantId={TenantId}", stage.Id, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "stage_sync=failed StageId={StageId} TenantId={TenantId}; Liens DB remains authoritative.",
                stage.Id, tenantId);
        }
    }

    /// <summary>
    /// Builds the Task service upsert payload from a Liens stage entity.
    /// Serializes Liens-specific fields (Description, DefaultOwnerRole, SlaMetadata) into ProductSettingsJson.
    /// </summary>
    public static TaskServiceStageUpsertRequest BuildStageUpsertPayload(LienWorkflowStage stage)
    {
        var extensions = new LiensStageExtensions
        {
            Description      = stage.Description,
            DefaultOwnerRole = stage.DefaultOwnerRole,
            SlaMetadata      = stage.SlaMetadata,
        };

        return new TaskServiceStageUpsertRequest
        {
            Id                  = stage.Id,
            SourceProductCode   = ProductCode,
            Name                = stage.StageName,
            DisplayOrder        = stage.StageOrder,
            IsActive            = stage.IsActive,
            ProductSettingsJson = JsonSerializer.Serialize(extensions, _json),
        };
    }

    /// <summary>Maps a Task service stage response to a Liens WorkflowStageResponse.</summary>
    private static WorkflowStageResponse MapFromTaskServiceStage(
        TaskServiceStageResponse dto,
        Guid? workflowConfigId)
    {
        var ext = DeserializeStageExtensions(dto.ProductSettingsJson);

        return new WorkflowStageResponse
        {
            Id               = dto.Id,
            WorkflowConfigId = workflowConfigId ?? Guid.Empty,
            StageName        = dto.Name,
            StageOrder       = dto.DisplayOrder,
            Description      = ext.Description,
            IsActive         = dto.IsActive,
            DefaultOwnerRole = ext.DefaultOwnerRole,
            SlaMetadata      = ext.SlaMetadata,
            CreatedAtUtc     = dto.CreatedAtUtc,
            UpdatedAtUtc     = dto.UpdatedAtUtc,
        };
    }

    /// <summary>Maps a Liens stage entity to a WorkflowStageResponse.</summary>
    private static WorkflowStageResponse MapStageToResponse(LienWorkflowStage s) => new()
    {
        Id               = s.Id,
        WorkflowConfigId = s.WorkflowConfigId,
        StageName        = s.StageName,
        StageOrder       = s.StageOrder,
        Description      = s.Description,
        IsActive         = s.IsActive,
        DefaultOwnerRole = s.DefaultOwnerRole,
        SlaMetadata      = s.SlaMetadata,
        CreatedAtUtc     = s.CreatedAtUtc,
        UpdatedAtUtc     = s.UpdatedAtUtc,
    };

    private static LiensStageExtensions DeserializeStageExtensions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new LiensStageExtensions();

        try
        {
            return JsonSerializer.Deserialize<LiensStageExtensions>(json, _json)
                   ?? new LiensStageExtensions();
        }
        catch
        {
            return new LiensStageExtensions();
        }
    }

    /// <summary>
    /// Maps entity to WorkflowConfigResponse.
    /// <paramref name="stages"/> overrides entity.Stages when non-null (Task-service dual-read path).
    /// Falls back to entity.Stages when null (Liens DB fallback).
    /// Transitions always come from entity (not yet migrated to Task service).
    /// </summary>
    private static WorkflowConfigResponse MapToResponseWithStages(
        LienWorkflowConfig entity,
        List<WorkflowStageResponse>? stages)
    {
        var stageList = stages ?? entity.Stages
            .OrderBy(s => s.StageOrder)
            .Select(MapStageToResponse)
            .ToList();

        return new WorkflowConfigResponse
        {
            Id                  = entity.Id,
            TenantId            = entity.TenantId,
            ProductCode         = entity.ProductCode,
            WorkflowName        = entity.WorkflowName,
            Version             = entity.Version,
            IsActive            = entity.IsActive,
            LastUpdatedAt       = entity.LastUpdatedAt,
            LastUpdatedByUserId = entity.LastUpdatedByUserId,
            LastUpdatedByName   = entity.LastUpdatedByName,
            LastUpdatedSource   = entity.LastUpdatedSource,
            CreatedAtUtc        = entity.CreatedAtUtc,
            UpdatedAtUtc        = entity.UpdatedAtUtc,
            Stages = stageList,
            Transitions = entity.Transitions
                .Where(t => t.IsActive)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.CreatedAtUtc)
                .Select(t => new WorkflowTransitionResponse
                {
                    Id               = t.Id,
                    WorkflowConfigId = t.WorkflowConfigId,
                    FromStageId      = t.FromStageId,
                    ToStageId        = t.ToStageId,
                    IsActive         = t.IsActive,
                    SortOrder        = t.SortOrder,
                    CreatedAtUtc     = t.CreatedAtUtc,
                    UpdatedAtUtc     = t.UpdatedAtUtc,
                }).ToList(),
        };
    }

    private static WorkflowConfigResponse MapToResponse(LienWorkflowConfig entity)
        => MapToResponseWithStages(entity, null);

    // ── TASK-MIG-04 — Transition write-through helper ────────────────────────────

    /// <summary>
    /// Reads all active transitions from Liens DB and pushes the full set to the Task service.
    /// This is a batch-replace: the Task service's transition set for (TenantId, ProductCode)
    /// is replaced with the current Liens DB state.
    /// Failures are logged but never propagated to the caller.
    /// </summary>
    private async System.Threading.Tasks.Task TrySyncTransitionsToTaskServiceAsync(
        Guid tenantId, Guid actingUserId, Guid workflowConfigId, CancellationToken ct)
    {
        try
        {
            var current = await _repo.GetActiveTransitionsAsync(workflowConfigId, ct);

            var payload = new TaskServiceTransitionsUpsertRequest
            {
                SourceProductCode = ProductCode,
                Transitions = current.Select(t => new TaskServiceTransitionsUpsertRequest.TransitionEntryDto
                {
                    FromStageId = t.FromStageId,
                    ToStageId   = t.ToStageId,
                    SortOrder   = t.SortOrder,
                }).ToList(),
            };

            await _taskClient.UpsertTransitionsFromSourceAsync(tenantId, actingUserId, payload, ct);

            _logger.LogInformation(
                "transition_sync=ok WorkflowConfigId={WorkflowConfigId} TenantId={TenantId} Count={Count}",
                workflowConfigId, tenantId, current.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "transition_sync=failed WorkflowConfigId={WorkflowConfigId} TenantId={TenantId}; Liens DB remains authoritative.",
                workflowConfigId, tenantId);
        }
    }
}

