using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// TASK-B04 — LienTaskService post-cutover: all task runtime is delegated to the canonical
/// Task service via <see cref="ILiensTaskServiceClient"/>.
/// Liens retains governance validation, audit publishing, notification publishing,
/// and Flow workflow instance resolution. It no longer owns task DB rows.
/// </summary>
public sealed class LienTaskService : ILienTaskService
{
    private readonly ILiensTaskServiceClient              _taskClient;
    private readonly ILienRepository                      _lienRepo;
    private readonly IAuditPublisher                      _audit;
    private readonly INotificationPublisher               _notifications;
    private readonly ILienWorkflowConfigRepository        _workflowRepo;
    private readonly ILienWorkflowConfigService           _workflowConfigService;
    private readonly IWorkflowTransitionValidationService _transitionValidator;
    private readonly ILienTaskGovernanceService           _governanceService;
    private readonly IFlowInstanceResolver                _flowResolver;
    private readonly ILogger<LienTaskService>             _logger;

    public LienTaskService(
        ILiensTaskServiceClient               taskClient,
        ILienRepository                       lienRepo,
        IAuditPublisher                       audit,
        INotificationPublisher                notifications,
        ILienWorkflowConfigRepository         workflowRepo,
        ILienWorkflowConfigService            workflowConfigService,
        IWorkflowTransitionValidationService  transitionValidator,
        ILienTaskGovernanceService            governanceService,
        IFlowInstanceResolver                 flowResolver,
        ILogger<LienTaskService>              logger)
    {
        _taskClient            = taskClient;
        _lienRepo              = lienRepo;
        _audit                 = audit;
        _notifications         = notifications;
        _workflowRepo          = workflowRepo;
        _workflowConfigService = workflowConfigService;
        _transitionValidator   = transitionValidator;
        _governanceService     = governanceService;
        _flowResolver          = flowResolver;
        _logger                = logger;
    }

    // ── Search ───────────────────────────────────────────────────────────────────

    public async Task<PaginatedResult<TaskResponse>> SearchAsync(
        Guid    tenantId,
        string? search,
        string? status,
        string? priority,
        Guid?   assignedUserId,
        Guid?   caseId,
        Guid?   lienId,
        Guid?   workflowStageId,
        string? assignmentScope,
        Guid?   currentUserId,
        int     page,
        int     pageSize,
        CancellationToken ct = default)
    {
        if (page < 1)     page     = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        return await _taskClient.SearchTasksAsync(
            tenantId, search, status, priority, assignedUserId,
            caseId, lienId, workflowStageId,
            assignmentScope, currentUserId, page, pageSize, ct);
    }

    // ── Get by ID ────────────────────────────────────────────────────────────────

    public Task<TaskResponse?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
        => _taskClient.GetTaskAsync(tenantId, id, ct);

    // ── Create ───────────────────────────────────────────────────────────────────

    public async Task<TaskResponse> CreateAsync(
        Guid              tenantId,
        Guid              actingUserId,
        CreateTaskRequest request,
        CancellationToken ct = default)
    {
        // ── Basic validation ───────────────────────────────────────────────────
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
            errors.Add("title", ["Title is required."]);
        if (request.Priority is not null && !TaskPriorities.All.Contains(request.Priority))
            errors.Add("priority", [$"Invalid priority '{request.Priority}'."]);
        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing.", errors);

        // ── LS-LIENS-FLOW-006 / TASK-MIG-01: Governance enforcement (dual-read) ──
        var governance = await _governanceService.GetAsync(tenantId, ct);

        var effectiveAssignedUserId  = request.AssignedUserId;
        var effectiveCaseId          = request.CaseId;
        var effectiveWorkflowStageId = request.WorkflowStageId;

        if (governance is not null)
        {
            if (governance.RequireAssigneeOnCreate && !effectiveAssignedUserId.HasValue)
                errors.Add("assignedUserId", ["Task assignee is required."]);

            if (governance.RequireCaseLinkOnCreate && !effectiveCaseId.HasValue)
                errors.Add("caseId", ["Task must be linked to a case."]);

            if (governance.RequireWorkflowStageOnCreate && !effectiveWorkflowStageId.HasValue)
            {
                effectiveWorkflowStageId = await DeriveStartStageAsync(tenantId, governance, ct);
                if (!effectiveWorkflowStageId.HasValue)
                    errors.Add("workflowStageId", [
                        "A valid workflow stage is required for task creation. " +
                        "Configure at least one active stage in your workflow settings."]);
            }

            if (errors.Count > 0)
                throw new ValidationException("Task creation does not satisfy governance requirements.", errors);
        }

        // ── LS-LIENS-FLOW-007: Resolve Flow workflow instance ─────────────────
        Guid?   flowInstanceId = null;
        string? flowStepKey    = null;

        if (effectiveCaseId.HasValue)
        {
            var (instanceId, stepKey) = await _flowResolver.ResolveAsync(effectiveCaseId.Value, ct);
            if (instanceId.HasValue)
            {
                flowInstanceId = instanceId.Value;
                flowStepKey    = stepKey;
            }
        }

        // ── Delegate creation to Task service ─────────────────────────────────
        var externalId      = Guid.NewGuid();
        var effectiveRequest = new CreateTaskRequest
        {
            Title                = request.Title,
            Description          = request.Description,
            Priority             = request.Priority ?? "MEDIUM",
            AssignedUserId       = effectiveAssignedUserId,
            CaseId               = effectiveCaseId,
            LienIds              = request.LienIds,
            WorkflowStageId      = effectiveWorkflowStageId,
            DueDate              = request.DueDate,
            SourceType           = request.SourceType,
            GenerationRuleId     = request.GenerationRuleId,
            GeneratingTemplateId = request.GeneratingTemplateId,
        };

        var result = await _taskClient.CreateTaskAsync(
            tenantId, actingUserId, externalId, effectiveRequest, ct);

        _logger.LogInformation(
            "Task created via Task service: TaskId={TaskId} Tenant={TenantId}", result.Id, tenantId);

        // ── Audit ─────────────────────────────────────────────────────────────
        _audit.Publish(
            eventType:   "liens.task.created",
            action:      "create",
            description: $"Task '{result.Title}' created",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTask",
            entityId:    result.Id.ToString());

        if (effectiveCaseId.HasValue)
        {
            _audit.Publish(
                eventType:   "liens.task.created",
                action:      "create",
                description: $"Task '{result.Title}' created",
                tenantId:    tenantId,
                actorUserId: actingUserId,
                entityType:  "Case",
                entityId:    effectiveCaseId.Value.ToString());
        }

        // ── Notifications ─────────────────────────────────────────────────────
        if (effectiveAssignedUserId.HasValue)
        {
            _ = _notifications.PublishAsync("liens.task.created_assigned", tenantId, new Dictionary<string, string>
            {
                ["tenantId"]        = tenantId.ToString(),
                ["taskId"]          = result.Id.ToString(),
                ["taskTitle"]       = result.Title,
                ["assignedTo"]      = effectiveAssignedUserId.Value.ToString(),
                ["assignedBy"]      = actingUserId.ToString(),
                ["caseId"]          = effectiveCaseId?.ToString() ?? string.Empty,
                ["lienIds"]         = string.Join(",", request.LienIds),
                ["priority"]        = result.Priority,
                ["workflowStageId"] = effectiveWorkflowStageId?.ToString() ?? string.Empty,
                ["dueDate"]         = result.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ["sourceType"]      = request.SourceType ?? string.Empty,
            }, ct);
        }

        var reporterIsAlsoAssignee = effectiveAssignedUserId.HasValue
            && effectiveAssignedUserId.Value == actingUserId;

        if (!reporterIsAlsoAssignee)
        {
            _ = _notifications.PublishAsync("liens.task.created_reporter", tenantId, new Dictionary<string, string>
            {
                ["tenantId"]    = tenantId.ToString(),
                ["taskId"]      = result.Id.ToString(),
                ["taskTitle"]   = result.Title,
                ["reporterId"]  = actingUserId.ToString(),
                ["assignedTo"]  = effectiveAssignedUserId?.ToString() ?? string.Empty,
                ["caseId"]      = effectiveCaseId?.ToString() ?? string.Empty,
                ["priority"]    = result.Priority,
                ["dueDate"]     = result.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            }, ct);
        }

        return result;
    }

    // ── Update ───────────────────────────────────────────────────────────────────

    public async Task<TaskResponse> UpdateAsync(
        Guid              tenantId,
        Guid              id,
        Guid              actingUserId,
        UpdateTaskRequest request,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
            errors.Add("title", ["Title is required."]);
        if (request.Priority is not null && !TaskPriorities.All.Contains(request.Priority))
            errors.Add("priority", [$"Invalid priority '{request.Priority}'."]);
        if (errors.Count > 0)
            throw new ValidationException("Validation failed.", errors);

        // ── Workflow stage transition validation ──────────────────────────────
        // Fetch current task to compare stage IDs
        var existing = await _taskClient.GetTaskAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Task '{id}' not found.");

        if (existing.WorkflowStageId.HasValue
            && request.WorkflowStageId.HasValue
            && existing.WorkflowStageId.Value != request.WorkflowStageId.Value)
        {
            // TASK-MIG-03: stage lookup uses dual-read (Task service first, Liens DB fallback)
            var fromStageResp = await _workflowConfigService.GetStageForRuntimeAsync(
                tenantId, existing.WorkflowStageId.Value, ct);
            if (fromStageResp is not null)
            {
                var allowed = await _transitionValidator.IsTransitionAllowedAsync(
                    tenantId,
                    fromStageResp.WorkflowConfigId,
                    existing.WorkflowStageId.Value,
                    request.WorkflowStageId.Value, ct);

                if (!allowed)
                {
                    var toStageResp = await _workflowConfigService.GetStageForRuntimeAsync(
                        tenantId, request.WorkflowStageId.Value, ct);
                    var fromName = fromStageResp.StageName;
                    var toName   = toStageResp?.StageName ?? request.WorkflowStageId.Value.ToString();
                    throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
                    {
                        ["workflowStageId"] = [
                            $"Transition from '{fromName}' to '{toName}' is not allowed by the workflow configuration."]
                    });
                }
            }
        }

        var result = await _taskClient.UpdateTaskAsync(tenantId, id, actingUserId, request, ct);

        _audit.Publish(
            eventType:   "liens.task.updated",
            action:      "update",
            description: $"Task '{result.Title}' updated",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTask",
            entityId:    id.ToString());

        if (result.CaseId.HasValue)
        {
            _audit.Publish(
                eventType:   "liens.task.updated",
                action:      "update",
                description: $"Task '{result.Title}' updated",
                tenantId:    tenantId,
                actorUserId: actingUserId,
                entityType:  "Case",
                entityId:    result.CaseId.Value.ToString());
        }

        return result;
    }

    // ── Assign ───────────────────────────────────────────────────────────────────

    public async Task<TaskResponse> AssignAsync(
        Guid              tenantId,
        Guid              id,
        Guid              actingUserId,
        AssignTaskRequest request,
        CancellationToken ct = default)
    {
        var existing = await _taskClient.GetTaskAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Task '{id}' not found.");

        var previousAssignee = existing.AssignedUserId;
        var isReassignment   = previousAssignee.HasValue
                               && request.AssignedUserId.HasValue
                               && previousAssignee.Value != request.AssignedUserId.Value;

        var result = await _taskClient.AssignTaskAsync(
            tenantId, id, actingUserId, request.AssignedUserId, ct);

        _audit.Publish(
            eventType:   "liens.task.assigned",
            action:      "update",
            description: $"Task '{result.Title}' {(isReassignment ? "reassigned" : "assigned")}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTask",
            entityId:    id.ToString());

        if (result.CaseId.HasValue)
        {
            _audit.Publish(
                eventType:   "liens.task.assigned",
                action:      "update",
                description: $"Task '{result.Title}' {(isReassignment ? "reassigned" : "assigned")}",
                tenantId:    tenantId,
                actorUserId: actingUserId,
                entityType:  "Case",
                entityId:    result.CaseId.Value.ToString());
        }

        if (request.AssignedUserId.HasValue)
        {
            var notifKey = isReassignment ? "liens.task.reassigned" : "liens.task.assigned";
            _ = _notifications.PublishAsync(notifKey, tenantId, new Dictionary<string, string>
            {
                ["tenantId"]           = tenantId.ToString(),
                ["taskId"]             = id.ToString(),
                ["taskTitle"]          = result.Title,
                ["assignedTo"]         = request.AssignedUserId.Value.ToString(),
                ["assignedBy"]         = actingUserId.ToString(),
                ["previousAssigneeId"] = previousAssignee?.ToString() ?? string.Empty,
                ["caseId"]             = result.CaseId?.ToString() ?? string.Empty,
                ["priority"]           = result.Priority,
                ["dueDate"]            = result.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            }, ct);
        }

        return result;
    }

    // ── Status transitions ────────────────────────────────────────────────────────

    public async Task<TaskResponse> UpdateStatusAsync(
        Guid                   tenantId,
        Guid                   id,
        Guid                   actingUserId,
        UpdateTaskStatusRequest request,
        CancellationToken       ct = default)
    {
        if (!TaskStatuses.All.Contains(request.Status))
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
            {
                ["status"] = [$"Invalid status '{request.Status}'."]
            });

        var result = await _taskClient.TransitionStatusAsync(
            tenantId, id, actingUserId, request.Status, ct);

        _audit.Publish(
            eventType:   "liens.task.status_changed",
            action:      "update",
            description: $"Task '{result.Title}' status changed to {request.Status}",
            tenantId:    tenantId,
            actorUserId: actingUserId,
            entityType:  "LienTask",
            entityId:    id.ToString());

        if (result.CaseId.HasValue)
        {
            _audit.Publish(
                eventType:   "liens.task.status_changed",
                action:      "update",
                description: $"Task '{result.Title}' status → {request.Status}",
                tenantId:    tenantId,
                actorUserId: actingUserId,
                entityType:  "Case",
                entityId:    result.CaseId.Value.ToString());
        }

        return result;
    }

    public Task<TaskResponse> CompleteAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
        => UpdateStatusAsync(tenantId, id, actingUserId, new UpdateTaskStatusRequest { Status = "COMPLETED" }, ct);

    public Task<TaskResponse> CancelAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default)
        => UpdateStatusAsync(tenantId, id, actingUserId, new UpdateTaskStatusRequest { Status = "CANCELLED" }, ct);

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<Guid?> DeriveStartStageAsync(
        Guid                          tenantId,
        TaskGovernanceSettingsResponse governance,
        CancellationToken              ct)
    {
        if (governance.DefaultStartStageMode == StartStageMode.ExplicitStage
            && governance.ExplicitStartStageId.HasValue)
        {
            // TASK-MIG-03: dual-read (Task service first, Liens DB fallback)
            var explicit_ = await _workflowConfigService.GetStageForRuntimeAsync(
                tenantId, governance.ExplicitStartStageId.Value, ct);
            if (explicit_ is { IsActive: true })
                return explicit_.Id;

            _logger.LogWarning(
                "Governance ExplicitStartStageId {StageId} is inactive or missing; falling back to FIRST_ACTIVE_STAGE.",
                governance.ExplicitStartStageId.Value);
        }

        // TASK-MIG-03: dual-read config (Task service stages first, Liens DB fallback)
        var config = await _workflowConfigService.GetByTenantAsync(tenantId, ct);
        if (config is null) return null;

        var firstStage = config.Stages
            .Where(s => s.IsActive)
            .OrderBy(s => s.StageOrder)
            .FirstOrDefault();

        return firstStage?.Id;
    }
}
