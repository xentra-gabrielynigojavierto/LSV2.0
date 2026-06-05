using Flow.Application.DTOs;
using Flow.Application.Events;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Domain.Interfaces;
using Flow.Domain.Rules;
using Microsoft.EntityFrameworkCore;
using ProductKeys = Flow.Domain.Common.ProductKeys;

namespace Flow.Application.Services;

public class TaskService : ITaskService
{
    private readonly IFlowDbContext _db;
    private readonly IAutomationExecutor _automationExecutor;
    private readonly INotificationService _notificationService;
    private readonly IFlowEventDispatcher _events;
    private readonly IFlowUserContext _user;

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdAt", "dueDate", "title", "status", "updatedAt"
    };

    public TaskService(
        IFlowDbContext db,
        IAutomationExecutor automationExecutor,
        INotificationService notificationService,
        IFlowEventDispatcher events,
        IFlowUserContext user)
    {
        _db = db;
        _automationExecutor = automationExecutor;
        _notificationService = notificationService;
        _events = events;
        _user = user;
    }

    public async Task<TaskResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.TaskItems
            .AsNoTracking()
            .Include(t => t.WorkflowDefinition)
            .Include(t => t.WorkflowStage)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            throw new NotFoundException("TaskItem", id);
        return await MapToResponseAsync(entity, cancellationToken);
    }

    public async Task<PagedResponse<TaskResponse>> ListAsync(TaskListQuery query, CancellationToken cancellationToken = default)
    {
        var q = _db.TaskItems
            .AsNoTracking()
            .Include(t => t.WorkflowDefinition)
            .Include(t => t.WorkflowStage)
            .AsQueryable();

        if (query.Status.HasValue)
            q = q.Where(t => t.Status == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.AssignedToUserId))
            q = q.Where(t => t.AssignedToUserId == query.AssignedToUserId);
        if (!string.IsNullOrWhiteSpace(query.AssignedToRoleKey))
            q = q.Where(t => t.AssignedToRoleKey == query.AssignedToRoleKey);
        if (!string.IsNullOrWhiteSpace(query.AssignedToOrgId))
            q = q.Where(t => t.AssignedToOrgId == query.AssignedToOrgId);
        if (!string.IsNullOrWhiteSpace(query.ContextType))
            q = q.Where(t => t.Context != null && t.Context.ContextType == query.ContextType);
        if (!string.IsNullOrWhiteSpace(query.ContextId))
            q = q.Where(t => t.Context != null && t.Context.ContextId == query.ContextId);

        // LS-FLOW-020-A — when productKey is omitted, return all products in tenant (transitional default).
        if (!string.IsNullOrWhiteSpace(query.ProductKey))
        {
            if (!ProductKeys.IsValid(query.ProductKey))
                throw new ValidationException($"Unsupported productKey: {query.ProductKey}");
            q = q.Where(t => t.ProductKey == query.ProductKey);
        }

        var totalCount = await q.CountAsync(cancellationToken);

        q = ApplySorting(q, query.SortBy, query.SortDirection);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        q = q.Skip((page - 1) * pageSize).Take(pageSize);

        var entities = await q.ToListAsync(cancellationToken);

        var items = new List<TaskResponse>();
        foreach (var entity in entities)
        {
            items.Add(await MapToResponseAsync(entity, cancellationToken));
        }

        return new PagedResponse<TaskResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        Guid? workflowStageId = null;
        var initialStatus = TaskItemStatus.Open;
        string? workflowProductKey = null;

        if (request.FlowDefinitionId.HasValue)
        {
            // LS-FLOW-020-A — load workflow's ProductKey to enforce match with task.
            var workflow = await _db.FlowDefinitions
                .AsNoTracking()
                .Where(w => w.Id == request.FlowDefinitionId.Value)
                .Select(w => new { w.Id, w.ProductKey })
                .FirstOrDefaultAsync(cancellationToken);

            if (workflow == null)
                throw new ValidationException($"Workflow {request.FlowDefinitionId.Value} not found.");
            workflowProductKey = workflow.ProductKey;

            var initialStage = await _db.WorkflowStages
                .AsNoTracking()
                .Where(s => s.WorkflowDefinitionId == request.FlowDefinitionId.Value && s.IsInitial)
                .FirstOrDefaultAsync(cancellationToken);

            if (initialStage != null)
            {
                workflowStageId = initialStage.Id;
                initialStatus = initialStage.MappedStatus;
            }
        }

        // LS-FLOW-020-A — resolve product key. Precedence:
        //   1) explicit value in request (validated against allowed set + workflow match)
        //   2) inferred from linked workflow when present
        //   3) FLOW_GENERIC fallback
        var productKey = ResolveTaskProductKey(request.ProductKey, workflowProductKey);

        var entity = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = initialStatus,
            ProductKey = productKey,
            FlowDefinitionId = request.FlowDefinitionId,
            WorkflowStageId = workflowStageId,
            AssignedToUserId = request.AssignedToUserId?.Trim(),
            AssignedToRoleKey = request.AssignedToRoleKey?.Trim(),
            AssignedToOrgId = request.AssignedToOrgId?.Trim(),
            DueDate = request.DueDate,
            Context = request.Context is not null
                ? new ContextReference
                {
                    ContextType = request.Context.ContextType.Trim(),
                    ContextId = request.Context.ContextId.Trim(),
                    Label = request.Context.Label?.Trim()
                }
                : null
        };

        _db.TaskItems.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        // LS-FLOW-MERGE-P3 — if the task was created already-assigned, fan out
        // a TaskAssigned event (audit + notification).
        var hasAssignment = !string.IsNullOrWhiteSpace(entity.AssignedToUserId)
            || !string.IsNullOrWhiteSpace(entity.AssignedToRoleKey)
            || !string.IsNullOrWhiteSpace(entity.AssignedToOrgId);
        if (hasAssignment)
        {
            await _events.PublishAsync(new TaskAssignedEvent(
                entity.Id, entity.AssignedToUserId, entity.AssignedToRoleKey, entity.AssignedToOrgId,
                _user.TenantId, _user.UserId, DateTime.UtcNow, entity.Title), cancellationToken);
        }

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TaskResponse> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            throw new NotFoundException("TaskItem", id);

        ValidateUpdateRequest(request);

        entity.Title = request.Title.Trim();
        entity.Description = request.Description?.Trim();
        entity.AssignedToUserId = request.AssignedToUserId?.Trim();
        entity.AssignedToRoleKey = request.AssignedToRoleKey?.Trim();
        entity.AssignedToOrgId = request.AssignedToOrgId?.Trim();
        entity.DueDate = request.DueDate;

        var workflowChanged = entity.FlowDefinitionId != request.FlowDefinitionId;
        entity.FlowDefinitionId = request.FlowDefinitionId;

        // LS-FLOW-020-A — resolve product context. We need the (possibly new)
        // workflow's ProductKey for validation when either the workflow link
        // changes OR a productKey is explicitly supplied.
        string? workflowProductKey = null;
        if (request.FlowDefinitionId.HasValue)
        {
            var workflow = await _db.FlowDefinitions
                .AsNoTracking()
                .Where(w => w.Id == request.FlowDefinitionId.Value)
                .Select(w => new { w.Id, w.ProductKey })
                .FirstOrDefaultAsync(cancellationToken);
            if (workflow == null)
                throw new ValidationException($"Workflow {request.FlowDefinitionId.Value} not found.");
            workflowProductKey = workflow.ProductKey;
        }

        if (!string.IsNullOrWhiteSpace(request.ProductKey))
        {
            // Explicit override: validate + cross-check with workflow if linked.
            entity.ProductKey = ResolveTaskProductKey(request.ProductKey, workflowProductKey);
        }
        else if (workflowChanged && workflowProductKey != null)
        {
            // Workflow link changed and no explicit override: inherit from new workflow.
            entity.ProductKey = workflowProductKey;
        }
        else if (workflowProductKey != null && !string.Equals(entity.ProductKey, workflowProductKey, StringComparison.Ordinal))
        {
            // Workflow unchanged but task's existing ProductKey conflicts (legacy data).
            throw new ValidationException("Task productKey must match workflow productKey");
        }

        if (workflowChanged)
        {
            if (request.FlowDefinitionId.HasValue)
            {
                var initialStage = await _db.WorkflowStages
                    .AsNoTracking()
                    .Where(s => s.WorkflowDefinitionId == request.FlowDefinitionId.Value && s.IsInitial)
                    .FirstOrDefaultAsync(cancellationToken);

                if (initialStage != null)
                {
                    entity.WorkflowStageId = initialStage.Id;
                    entity.Status = initialStage.MappedStatus;
                }
                else
                {
                    entity.WorkflowStageId = null;
                }
            }
            else
            {
                entity.WorkflowStageId = null;
            }
        }

        if (request.Context is not null)
        {
            entity.Context = new ContextReference
            {
                ContextType = request.Context.ContextType.Trim(),
                ContextId = request.Context.ContextId.Trim(),
                Label = request.Context.Label?.Trim()
            };
        }
        else
        {
            entity.Context = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TaskResponse> UpdateStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            throw new NotFoundException("TaskItem", id);

        if (entity.WorkflowStageId.HasValue)
        {
            var currentStage = await _db.WorkflowStages
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == entity.WorkflowStageId.Value, cancellationToken);

            if (currentStage != null)
            {
                var activeTransitions = await _db.WorkflowTransitions
                    .AsNoTracking()
                    .Where(t => t.WorkflowDefinitionId == currentStage.WorkflowDefinitionId
                        && t.FromStageId == currentStage.Id
                        && t.IsActive)
                    .Include(t => t.ToStage)
                    .ToListAsync(cancellationToken);

                var validTransition = activeTransitions
                    .FirstOrDefault(t => t.ToStage.MappedStatus == request.Status);

                if (validTransition == null)
                {
                    throw new InvalidStateTransitionException(
                        entity.Status.ToString(),
                        request.Status.ToString());
                }

                var rules = WorkflowRuleEvaluator.ParseRules(validTransition.RulesJson);
                var ruleResult = WorkflowRuleEvaluator.Evaluate(rules, entity);
                if (!ruleResult.IsValid)
                {
                    throw new ValidationException(ruleResult.Errors);
                }

                var fromStageName = currentStage.Name;
                entity.Status = request.Status;
                entity.WorkflowStageId = validTransition.ToStageId;
                await _db.SaveChangesAsync(cancellationToken);

                // LS-FLOW-MERGE-P3 — also emit TaskCompleted on the workflow-
                // transition path when the resulting status is terminal. The
                // non-workflow branch below covers the same emit for tasks
                // not bound to a workflow stage.
                if (request.Status == TaskItemStatus.Done)
                {
                    await _events.PublishAsync(new TaskCompletedEvent(
                        entity.Id, _user.TenantId, _user.UserId, DateTime.UtcNow, entity.Title), cancellationToken);
                }

                var automationResults = await _automationExecutor.ExecuteTransitionHooksAsync(
                    validTransition.Id, entity, cancellationToken);

                var toStageName = validTransition.ToStage.Name;
                await _notificationService.CreateNotificationAsync(
                    Domain.Entities.NotificationType.TaskTransitioned,
                    Domain.Entities.NotificationSourceType.WorkflowTransition,
                    $"Task transitioned: {entity.Title}",
                    $"Moved from \"{fromStageName}\" to \"{toStageName}\"",
                    taskId: entity.Id,
                    workflowDefinitionId: entity.FlowDefinitionId,
                    targetUserId: entity.AssignedToUserId,
                    targetRoleKey: entity.AssignedToRoleKey,
                    targetOrgId: entity.AssignedToOrgId,
                    cancellationToken: cancellationToken);

                foreach (var ar in automationResults)
                {
                    var notifType = ar.Status == "Succeeded"
                        ? Domain.Entities.NotificationType.AutomationSucceeded
                        : Domain.Entities.NotificationType.AutomationFailed;
                    await _notificationService.CreateNotificationAsync(
                        notifType,
                        Domain.Entities.NotificationSourceType.AutomationHook,
                        $"Automation {ar.Status.ToLower()}: {ar.HookName}",
                        ar.Message ?? $"Hook \"{ar.HookName}\" {ar.Status.ToLower()} on task \"{entity.Title}\"",
                        taskId: entity.Id,
                        workflowDefinitionId: entity.FlowDefinitionId,
                        targetUserId: entity.AssignedToUserId,
                        targetRoleKey: entity.AssignedToRoleKey,
                        targetOrgId: entity.AssignedToOrgId,
                        cancellationToken: cancellationToken);
                }

                return await GetByIdAsync(entity.Id, cancellationToken);
            }
        }

        if (!TaskStateTransitions.IsValidTransition(entity.Status, request.Status))
        {
            throw new InvalidStateTransitionException(
                entity.Status.ToString(),
                request.Status.ToString());
        }

        entity.Status = request.Status;
        await _db.SaveChangesAsync(cancellationToken);

        // LS-FLOW-MERGE-P3 — emit TaskCompleted on terminal status.
        if (request.Status == TaskItemStatus.Done)
        {
            await _events.PublishAsync(new TaskCompletedEvent(
                entity.Id, _user.TenantId, _user.UserId, DateTime.UtcNow, entity.Title), cancellationToken);
        }

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<TaskResponse> AssignAsync(Guid id, AssignTaskRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null)
            throw new NotFoundException("TaskItem", id);

        var hadPriorAssignment = !string.IsNullOrWhiteSpace(entity.AssignedToUserId)
            || !string.IsNullOrWhiteSpace(entity.AssignedToRoleKey)
            || !string.IsNullOrWhiteSpace(entity.AssignedToOrgId);

        entity.AssignedToUserId = request.AssignedToUserId?.Trim();
        entity.AssignedToRoleKey = request.AssignedToRoleKey?.Trim();
        entity.AssignedToOrgId = request.AssignedToOrgId?.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        var hasNewAssignment = !string.IsNullOrWhiteSpace(entity.AssignedToUserId)
            || !string.IsNullOrWhiteSpace(entity.AssignedToRoleKey)
            || !string.IsNullOrWhiteSpace(entity.AssignedToOrgId);

        if (hasNewAssignment)
        {
            // LS-FLOW-MERGE-P3 — fan out via dispatcher (audit + notification).
            await _events.PublishAsync(new TaskAssignedEvent(
                entity.Id, entity.AssignedToUserId, entity.AssignedToRoleKey, entity.AssignedToOrgId,
                _user.TenantId, _user.UserId, DateTime.UtcNow, entity.Title), cancellationToken);

            var notifType = hadPriorAssignment
                ? Domain.Entities.NotificationType.TaskReassigned
                : Domain.Entities.NotificationType.TaskAssigned;
            var target = entity.AssignedToUserId ?? entity.AssignedToRoleKey ?? entity.AssignedToOrgId ?? "unknown";
            await _notificationService.CreateNotificationAsync(
                notifType,
                Domain.Entities.NotificationSourceType.Assignment,
                hadPriorAssignment ? $"Task reassigned: {entity.Title}" : $"Task assigned: {entity.Title}",
                $"Assigned to {target}",
                taskId: entity.Id,
                workflowDefinitionId: entity.FlowDefinitionId,
                targetUserId: entity.AssignedToUserId,
                targetRoleKey: entity.AssignedToRoleKey,
                targetOrgId: entity.AssignedToOrgId,
                cancellationToken: cancellationToken);
        }

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    private async Task<List<TaskItemStatus>> GetAllowedNextStatusesAsync(TaskItem entity, CancellationToken cancellationToken)
    {
        if (entity.WorkflowStageId.HasValue)
        {
            var transitions = await _db.WorkflowTransitions
                .AsNoTracking()
                .Where(t => t.FromStageId == entity.WorkflowStageId.Value && t.IsActive)
                .Include(t => t.ToStage)
                .ToListAsync(cancellationToken);

            return transitions
                .Select(t => t.ToStage.MappedStatus)
                .Distinct()
                .ToList();
        }

        return TaskStateTransitions.GetAllowedTransitions(entity.Status).ToList();
    }

    private async Task<Dictionary<string, TransitionRuleHints>?> GetTransitionRuleHintsAsync(TaskItem entity, CancellationToken cancellationToken)
    {
        if (!entity.WorkflowStageId.HasValue)
            return null;

        var transitions = await _db.WorkflowTransitions
            .AsNoTracking()
            .Where(t => t.FromStageId == entity.WorkflowStageId.Value && t.IsActive && t.RulesJson != null)
            .Include(t => t.ToStage)
            .ToListAsync(cancellationToken);

        if (transitions.Count == 0)
            return null;

        var hints = new Dictionary<string, TransitionRuleHints>();
        foreach (var t in transitions)
        {
            var rules = WorkflowRuleEvaluator.ParseRules(t.RulesJson);
            if (rules is null) continue;

            var statusKey = t.ToStage.MappedStatus.ToString();
            hints[statusKey] = new TransitionRuleHints
            {
                RequireTitle = rules.RequireTitle,
                RequireDescription = rules.RequireDescription,
                RequireAssignment = rules.RequireAssignment,
                RequireDueDate = rules.RequireDueDate
            };
        }

        return hints.Count > 0 ? hints : null;
    }

    private static IQueryable<TaskItem> ApplySorting(IQueryable<TaskItem> query, string sortBy, string sortDirection)
    {
        var isDesc = sortDirection.Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);
        var field = AllowedSortFields.Contains(sortBy.Trim()) ? sortBy.Trim() : "createdAt";

        var ordered = field.ToLowerInvariant() switch
        {
            "title" => isDesc ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
            "status" => isDesc ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            "duedate" => isDesc ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate),
            "updatedat" => isDesc ? query.OrderByDescending(t => t.UpdatedAt) : query.OrderBy(t => t.UpdatedAt),
            _ => isDesc ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt)
        };

        return ordered.ThenBy(t => t.Id);
    }

    private static void ValidateCreateRequest(CreateTaskRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Title))
            errors.Add("Title is required.");
        else if (request.Title.Trim().Length > 512)
            errors.Add("Title must not exceed 512 characters.");

        if (request.Description is not null && request.Description.Trim().Length > 4096)
            errors.Add("Description must not exceed 4096 characters.");

        if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow.Date)
            errors.Add("DueDate cannot be in the past.");

        ValidateContext(request.Context, errors);

        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static void ValidateUpdateRequest(UpdateTaskRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Title))
            errors.Add("Title is required.");
        else if (request.Title.Trim().Length > 512)
            errors.Add("Title must not exceed 512 characters.");

        if (request.Description is not null && request.Description.Trim().Length > 4096)
            errors.Add("Description must not exceed 4096 characters.");

        ValidateContext(request.Context, errors);

        if (errors.Count > 0)
            throw new ValidationException(errors);
    }

    private static void ValidateContext(ContextReferenceDto? context, List<string> errors)
    {
        if (context is null) return;

        if (string.IsNullOrWhiteSpace(context.ContextType))
            errors.Add("Context.ContextType is required when Context is provided.");
        if (string.IsNullOrWhiteSpace(context.ContextId))
            errors.Add("Context.ContextId is required when Context is provided.");
    }

    /// <summary>
    /// LS-FLOW-020-A — task product key resolution.
    /// Precedence: explicit request → inferred from workflow → FLOW_GENERIC.
    /// Throws on unknown keys or when an explicit value disagrees with the linked workflow.
    /// </summary>
    private static string ResolveTaskProductKey(string? requested, string? workflowProductKey)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var trimmed = requested.Trim();
            if (!ProductKeys.IsValid(trimmed))
                throw new ValidationException($"Unsupported productKey: {trimmed}");
            if (workflowProductKey != null && !string.Equals(trimmed, workflowProductKey, StringComparison.Ordinal))
                throw new ValidationException("Task productKey must match workflow productKey");
            return trimmed;
        }

        if (!string.IsNullOrWhiteSpace(workflowProductKey))
            return workflowProductKey!;

        return ProductKeys.FlowGeneric;
    }

    private async Task<TaskResponse> MapToResponseAsync(TaskItem entity, CancellationToken cancellationToken)
    {
        var allowedNext = await GetAllowedNextStatusesAsync(entity, cancellationToken);
        var ruleHints = await GetTransitionRuleHintsAsync(entity, cancellationToken);

        return new TaskResponse
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Status = entity.Status,
            ProductKey = string.IsNullOrWhiteSpace(entity.ProductKey) ? ProductKeys.FlowGeneric : entity.ProductKey,
            FlowDefinitionId = entity.FlowDefinitionId,
            WorkflowStageId = entity.WorkflowStageId,
            WorkflowName = entity.WorkflowDefinition?.Name,
            WorkflowStageName = entity.WorkflowStage?.Name,
            AllowedNextStatuses = allowedNext.Count > 0 ? allowedNext : null,
            AllowedTransitionRules = ruleHints,
            AssignedToUserId = entity.AssignedToUserId,
            AssignedToRoleKey = entity.AssignedToRoleKey,
            AssignedToOrgId = entity.AssignedToOrgId,
            DueDate = entity.DueDate,
            Context = entity.Context is not null
                ? new ContextReferenceDto
                {
                    ContextType = entity.Context.ContextType,
                    ContextId = entity.Context.ContextId,
                    Label = entity.Context.Label
                }
                : null,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }
}
