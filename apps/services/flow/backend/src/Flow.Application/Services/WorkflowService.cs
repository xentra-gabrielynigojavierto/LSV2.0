using Flow.Application.DTOs;
using Flow.Application.Events;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IFlowDbContext _db;
    private readonly IFlowEventDispatcher _events;
    private readonly IFlowUserContext _user;

    public WorkflowService(IFlowDbContext db, IFlowEventDispatcher events, IFlowUserContext user)
    {
        _db = db;
        _events = events;
        _user = user;
    }

    public async Task<List<WorkflowDefinitionSummaryResponse>> ListAsync(string? productKey = null, CancellationToken cancellationToken = default)
    {
        // LS-FLOW-020-A — when productKey is omitted, return all products in tenant (transitional default).
        var q = _db.FlowDefinitions
            .AsNoTracking()
            .Include(w => w.Stages)
            .Include(w => w.Transitions)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(productKey))
        {
            if (!ProductKeys.IsValid(productKey))
                throw new ValidationException($"Unsupported productKey: {productKey}");
            q = q.Where(w => w.ProductKey == productKey);
        }

        var workflows = await q.OrderBy(w => w.Name).ToListAsync(cancellationToken);

        return workflows.Select(w => new WorkflowDefinitionSummaryResponse
        {
            Id = w.Id,
            Name = w.Name,
            Description = w.Description,
            Version = w.Version,
            Status = w.Status,
            ProductKey = w.ProductKey,
            StageCount = w.Stages.Count,
            TransitionCount = w.Transitions.Count
        }).ToList();
    }

    public async Task<WorkflowDefinitionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.FlowDefinitions
            .AsNoTracking()
            .Include(w => w.Stages.OrderBy(s => s.Order))
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (workflow is null)
            throw new NotFoundException("WorkflowDefinition", id);

        return MapToResponse(workflow);
    }

    public async Task<WorkflowDefinitionResponse> CreateAsync(CreateWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Name is required");

        // LS-FLOW-020-A — Default to FLOW_GENERIC if omitted (transitional behavior). Reject unknown values.
        var productKey = ResolveProductKeyOrDefault(request.ProductKey);

        var entity = new FlowDefinition
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Version = "1.0",
            Status = FlowStatus.Draft,
            ProductKey = productKey,
            CreatedAt = DateTime.UtcNow
        };

        _db.FlowDefinitions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        // LS-FLOW-MERGE-P3 — emit creation event for audit/notifications fan-out.
        await _events.PublishAsync(new WorkflowCreatedEvent(
            entity.Id, entity.Name, entity.ProductKey,
            _user.TenantId, _user.UserId, DateTime.UtcNow), cancellationToken);

        return MapToResponse(entity);
    }

    public async Task<WorkflowDefinitionResponse> UpdateAsync(Guid id, UpdateWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Name is required");

        var workflow = await _db.FlowDefinitions
            .Include(w => w.Stages.OrderBy(s => s.Order))
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (workflow is null)
            throw new NotFoundException("WorkflowDefinition", id);

        var previousStatus = workflow.Status;

        workflow.Name = request.Name.Trim();
        workflow.Description = request.Description?.Trim();
        if (request.Status.HasValue)
            workflow.Status = request.Status.Value;
        // LS-FLOW-020-A — only update ProductKey when explicitly provided. Reject unknown values.
        // Also refuse to change a workflow's ProductKey while linked tasks or automation hooks
        // still reference it, since cascading the change is out of scope for this task and
        // silently desyncing them would violate the workflow↔task and workflow↔hook invariants.
        if (!string.IsNullOrWhiteSpace(request.ProductKey))
        {
            var trimmedNewKey = request.ProductKey.Trim();
            if (!ProductKeys.IsValid(trimmedNewKey))
                throw new ValidationException($"Unsupported productKey: {trimmedNewKey}");

            if (!string.Equals(trimmedNewKey, workflow.ProductKey, StringComparison.Ordinal))
            {
                var linkedTaskCount = await _db.TaskItems
                    .CountAsync(t => t.FlowDefinitionId == id, cancellationToken);
                var linkedHookCount = await _db.AutomationHooks
                    .CountAsync(h => h.WorkflowDefinitionId == id, cancellationToken);
                if (linkedTaskCount > 0 || linkedHookCount > 0)
                {
                    throw new ValidationException(
                        $"Cannot change workflow productKey while {linkedTaskCount} task(s) and {linkedHookCount} automation hook(s) reference it.");
                }
                workflow.ProductKey = trimmedNewKey;
            }
        }
        workflow.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // LS-FLOW-MERGE-P3 — emit state change + completion events when status moved.
        if (request.Status.HasValue && previousStatus != workflow.Status)
        {
            await _events.PublishAsync(new WorkflowStateChangedEvent(
                workflow.Id, previousStatus.ToString(), workflow.Status.ToString(),
                _user.TenantId, _user.UserId, DateTime.UtcNow), cancellationToken);

            if (workflow.Status == FlowStatus.Completed)
            {
                await _events.PublishAsync(new WorkflowCompletedEvent(
                    workflow.Id, _user.TenantId, _user.UserId, DateTime.UtcNow),
                    cancellationToken);
            }
        }

        return MapToResponse(workflow);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.FlowDefinitions
            .Include(w => w.Stages)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (workflow is null)
            throw new NotFoundException("WorkflowDefinition", id);

        var tasksUsingWorkflow = await _db.TaskItems
            .AnyAsync(t => t.FlowDefinitionId == id, cancellationToken);

        if (tasksUsingWorkflow)
            throw new ValidationException("Cannot delete workflow: it is assigned to one or more tasks");

        _db.WorkflowTransitions.RemoveRange(workflow.Transitions);
        _db.WorkflowStages.RemoveRange(workflow.Stages);
        _db.FlowDefinitions.Remove(workflow);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowStageResponse> AddStageAsync(Guid workflowId, CreateStageRequest request, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.FlowDefinitions
            .Include(w => w.Stages)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        if (workflow is null)
            throw new NotFoundException("WorkflowDefinition", workflowId);

        if (string.IsNullOrWhiteSpace(request.Key))
            throw new ValidationException("Stage key is required");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Stage name is required");

        if (workflow.Stages.Any(s => s.Key == request.Key.Trim()))
            throw new ValidationException($"Stage with key '{request.Key.Trim()}' already exists");

        if (request.IsInitial && workflow.Stages.Any(s => s.IsInitial))
            throw new ValidationException("Workflow already has an initial stage");

        var stage = new WorkflowStage
        {
            WorkflowDefinitionId = workflowId,
            Key = request.Key.Trim(),
            Name = request.Name.Trim(),
            MappedStatus = request.MappedStatus,
            Order = request.Order,
            IsInitial = request.IsInitial,
            IsTerminal = request.IsTerminal,
            CanvasX = request.CanvasX,
            CanvasY = request.CanvasY
        };

        _db.WorkflowStages.Add(stage);
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapStageResponse(stage);
    }

    public async Task<WorkflowStageResponse> UpdateStageAsync(Guid workflowId, Guid stageId, UpdateStageRequest request, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.FlowDefinitions
            .Include(w => w.Stages)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        if (workflow is null)
            throw new NotFoundException("WorkflowDefinition", workflowId);

        var stage = workflow.Stages.FirstOrDefault(s => s.Id == stageId);
        if (stage is null)
            throw new NotFoundException("WorkflowStage", stageId);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Stage name is required");

        if (request.IsInitial && workflow.Stages.Any(s => s.IsInitial && s.Id != stageId))
            throw new ValidationException("Workflow already has an initial stage");

        stage.Name = request.Name.Trim();
        stage.MappedStatus = request.MappedStatus;
        stage.Order = request.Order;
        stage.IsInitial = request.IsInitial;
        stage.IsTerminal = request.IsTerminal;
        stage.CanvasX = request.CanvasX;
        stage.CanvasY = request.CanvasY;

        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapStageResponse(stage);
    }

    public async Task DeleteStageAsync(Guid workflowId, Guid stageId, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.FlowDefinitions
            .Include(w => w.Stages)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        if (workflow is null)
            throw new NotFoundException("WorkflowDefinition", workflowId);

        var stage = workflow.Stages.FirstOrDefault(s => s.Id == stageId);
        if (stage is null)
            throw new NotFoundException("WorkflowStage", stageId);

        var usedInTransitions = workflow.Transitions.Any(t => t.FromStageId == stageId || t.ToStageId == stageId);
        if (usedInTransitions)
            throw new ValidationException("Cannot delete stage: it is used in one or more transitions. Remove the transitions first.");

        _db.WorkflowStages.Remove(stage);
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowTransitionResponse> AddTransitionAsync(Guid workflowId, CreateTransitionRequest request, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.FlowDefinitions
            .Include(w => w.Stages)
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        if (workflow is null)
            throw new NotFoundException("WorkflowDefinition", workflowId);

        if (!workflow.Stages.Any(s => s.Id == request.FromStageId))
            throw new ValidationException("From stage does not exist in this workflow");

        if (!workflow.Stages.Any(s => s.Id == request.ToStageId))
            throw new ValidationException("To stage does not exist in this workflow");

        if (request.FromStageId == request.ToStageId)
            throw new ValidationException("From and To stages must be different");

        if (workflow.Transitions.Any(t => t.FromStageId == request.FromStageId && t.ToStageId == request.ToStageId))
            throw new ValidationException("A transition between these stages already exists");

        var validatedRulesJson = ValidateAndNormalizeRulesJson(request.RulesJson);

        var transition = new WorkflowTransition
        {
            WorkflowDefinitionId = workflowId,
            FromStageId = request.FromStageId,
            ToStageId = request.ToStageId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Transition" : request.Name.Trim(),
            IsActive = true,
            RulesJson = validatedRulesJson
        };

        _db.WorkflowTransitions.Add(transition);
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapTransitionResponse(transition);
    }

    public async Task<WorkflowTransitionResponse> UpdateTransitionAsync(Guid workflowId, Guid transitionId, UpdateTransitionRequest request, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.FlowDefinitions
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        if (workflow is null)
            throw new NotFoundException("WorkflowDefinition", workflowId);

        var transition = workflow.Transitions.FirstOrDefault(t => t.Id == transitionId);
        if (transition is null)
            throw new NotFoundException("WorkflowTransition", transitionId);

        transition.Name = string.IsNullOrWhiteSpace(request.Name) ? transition.Name : request.Name.Trim();
        transition.IsActive = request.IsActive;
        transition.RulesJson = ValidateAndNormalizeRulesJson(request.RulesJson);

        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapTransitionResponse(transition);
    }

    public async Task DeleteTransitionAsync(Guid workflowId, Guid transitionId, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.FlowDefinitions
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        if (workflow is null)
            throw new NotFoundException("WorkflowDefinition", workflowId);

        var transition = workflow.Transitions.FirstOrDefault(t => t.Id == transitionId);
        if (transition is null)
            throw new NotFoundException("WorkflowTransition", transitionId);

        _db.WorkflowTransitions.Remove(transition);
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string ResolveProductKeyOrDefault(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return ProductKeys.FlowGeneric;
        var trimmed = requested.Trim();
        if (!ProductKeys.IsValid(trimmed))
            throw new ValidationException($"Unsupported productKey: {trimmed}");
        return trimmed;
    }

    private static WorkflowDefinitionResponse MapToResponse(FlowDefinition workflow)
    {
        return new WorkflowDefinitionResponse
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            Version = workflow.Version,
            Status = workflow.Status,
            ProductKey = workflow.ProductKey,
            Stages = workflow.Stages.OrderBy(s => s.Order).Select(MapStageResponse).ToList(),
            Transitions = workflow.Transitions.Select(MapTransitionResponse).ToList(),
            CreatedAt = workflow.CreatedAt,
            CreatedBy = workflow.CreatedBy,
            UpdatedAt = workflow.UpdatedAt,
            UpdatedBy = workflow.UpdatedBy
        };
    }

    private static WorkflowStageResponse MapStageResponse(WorkflowStage s) => new()
    {
        Id = s.Id,
        Key = s.Key,
        Name = s.Name,
        MappedStatus = s.MappedStatus,
        Order = s.Order,
        IsInitial = s.IsInitial,
        IsTerminal = s.IsTerminal,
        CanvasX = s.CanvasX,
        CanvasY = s.CanvasY
    };

    private static string? ValidateAndNormalizeRulesJson(string? rulesJson)
    {
        if (string.IsNullOrWhiteSpace(rulesJson))
            return null;

        var parsed = WorkflowRuleEvaluator.ParseRules(rulesJson);
        if (parsed is null)
            throw new ValidationException("Invalid RulesJson format. Expected JSON with boolean fields: requireTitle, requireDescription, requireAssignment, requireDueDate.");

        return WorkflowRuleEvaluator.SerializeRules(parsed);
    }

    public async Task<List<AutomationHookResponse>> ListAutomationHooksAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.FlowDefinitions.AnyAsync(w => w.Id == workflowId, cancellationToken);
        if (!exists) throw new NotFoundException("WorkflowDefinition", workflowId);

        var hooks = await _db.AutomationHooks
            .AsNoTracking()
            .Include(h => h.Actions)
            .Where(h => h.WorkflowDefinitionId == workflowId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        return hooks.Select(MapHookResponse).ToList();
    }

    public async Task<AutomationHookResponse> AddAutomationHookAsync(Guid workflowId, CreateAutomationHookRequest request, CancellationToken cancellationToken = default)
    {
        var workflow = await _db.FlowDefinitions
            .Include(w => w.Transitions)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken);

        if (workflow is null)
            throw new NotFoundException("WorkflowDefinition", workflowId);

        if (!workflow.Transitions.Any(t => t.Id == request.WorkflowTransitionId))
            throw new ValidationException("Transition does not belong to this workflow");

        if (!TriggerEventTypes.All.Contains(request.TriggerEventType))
            throw new ValidationException($"Unsupported trigger event type: {request.TriggerEventType}");

        // LS-FLOW-020-A — derive ProductKey from parent workflow; if caller supplied a value, it must match.
        ValidateHookProductKeyMatchesWorkflow(request.ProductKey, workflow.ProductKey);

        var normalizedActions = NormalizeAndValidateActions(request.Actions, request.ActionType, request.ConfigJson);

        var hook = new WorkflowAutomationHook
        {
            WorkflowDefinitionId = workflowId,
            WorkflowTransitionId = request.WorkflowTransitionId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Automation Hook" : request.Name.Trim(),
            TriggerEventType = request.TriggerEventType,
            ProductKey = workflow.ProductKey,
            // Mirror Actions[0] into legacy columns for backward compatibility.
            ActionType = normalizedActions[0].ActionType,
            ConfigJson = normalizedActions[0].ConfigJson,
            IsActive = true
        };

        foreach (var a in normalizedActions)
        {
            hook.Actions.Add(new AutomationAction
            {
                ActionType = a.ActionType,
                ConfigJson = a.ConfigJson,
                ConditionJson = a.ConditionJson,
                Order = a.Order ?? 0,
                RetryCount = a.RetryCount ?? 0,
                RetryDelaySeconds = a.RetryDelaySeconds,
                StopOnFailure = a.StopOnFailure ?? false
            });
        }

        _db.AutomationHooks.Add(hook);
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return MapHookResponse(hook);
    }

    public async Task<AutomationHookResponse> UpdateAutomationHookAsync(Guid workflowId, Guid hookId, UpdateAutomationHookRequest request, CancellationToken cancellationToken = default)
    {
        var hook = await _db.AutomationHooks
            .Include(h => h.Actions)
            .Include(h => h.WorkflowDefinition)
            .FirstOrDefaultAsync(h => h.Id == hookId && h.WorkflowDefinitionId == workflowId, cancellationToken);

        if (hook is null)
            throw new NotFoundException("WorkflowAutomationHook", hookId);

        // LS-FLOW-020-A — hook ProductKey is always the workflow's ProductKey.
        // If the caller supplied one explicitly it must match.
        ValidateHookProductKeyMatchesWorkflow(request.ProductKey, hook.WorkflowDefinition.ProductKey);
        hook.ProductKey = hook.WorkflowDefinition.ProductKey;

        var normalizedActions = NormalizeAndValidateActions(request.Actions, request.ActionType, request.ConfigJson);

        hook.Name = string.IsNullOrWhiteSpace(request.Name) ? hook.Name : request.Name.Trim();
        hook.IsActive = request.IsActive;
        // Keep legacy single-action columns synced with the first action.
        hook.ActionType = normalizedActions[0].ActionType;
        hook.ConfigJson = normalizedActions[0].ConfigJson;

        // Replace existing actions with the normalized set. Delete the old
        // rows in a separate SaveChanges call so they don't collide with the
        // new rows on the (HookId, Order) unique index. The whole operation
        // (hook field update + action delete + action insert) is wrapped in
        // a transaction so a mid-flight failure cannot leave the hook with a
        // partial action set. The MySQL retrying execution strategy requires
        // the entire transactional unit to be invoked through CreateExecutionStrategy().
        var strategy = _db.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.BeginTransactionAsync(cancellationToken);

            var existingActions = hook.Actions.ToList();
            if (existingActions.Count > 0)
            {
                _db.AutomationActions.RemoveRange(existingActions);
                await _db.SaveChangesAsync(cancellationToken);
            }

            foreach (var a in normalizedActions)
            {
                _db.AutomationActions.Add(new AutomationAction
                {
                    HookId = hook.Id,
                    ActionType = a.ActionType,
                    ConfigJson = a.ConfigJson,
                    ConditionJson = a.ConditionJson,
                    Order = a.Order ?? 0,
                    RetryCount = a.RetryCount ?? 0,
                    RetryDelaySeconds = a.RetryDelaySeconds,
                    StopOnFailure = a.StopOnFailure ?? false
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });

        // Refresh the navigation collection from tracked state so MapHookResponse
        // sees the newly inserted actions.
        hook.Actions.Clear();
        var fresh = await _db.AutomationActions
            .Where(a => a.HookId == hook.Id)
            .OrderBy(a => a.Order)
            .ToListAsync(cancellationToken);
        foreach (var f in fresh) hook.Actions.Add(f);
        return MapHookResponse(hook);
    }

    public async Task DeleteAutomationHookAsync(Guid workflowId, Guid hookId, CancellationToken cancellationToken = default)
    {
        var hook = await _db.AutomationHooks
            .FirstOrDefaultAsync(h => h.Id == hookId && h.WorkflowDefinitionId == workflowId, cancellationToken);

        if (hook is null)
            throw new NotFoundException("WorkflowAutomationHook", hookId);

        _db.AutomationHooks.Remove(hook);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AutomationExecutionLogResponse>> GetExecutionLogsAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var logs = await _db.AutomationExecutionLogs
            .AsNoTracking()
            .Where(l => l.TaskId == taskId)
            .Include(l => l.AutomationHook)
            .OrderByDescending(l => l.ExecutedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        // ActionType is an immutable snapshot captured at execution time,
        // so the log stays accurate even if the underlying AutomationAction
        // or hook is later edited/deleted. For pre-019-A rows whose snapshot
        // is empty, fall back to the hook's legacy ActionType.
        return logs.Select(l => new AutomationExecutionLogResponse
        {
            Id = l.Id,
            TaskId = l.TaskId,
            WorkflowAutomationHookId = l.WorkflowAutomationHookId,
            ActionId = l.ActionId,
            HookName = l.AutomationHook?.Name ?? "Unknown",
            ActionType = string.IsNullOrEmpty(l.ActionType)
                ? (l.AutomationHook?.ActionType ?? "Unknown")
                : l.ActionType,
            Status = l.Status,
            Message = l.Message,
            Attempts = l.Attempts,
            ExecutedAt = l.ExecutedAt
        }).ToList();
    }

    // Resolves the incoming request shape (new-style Actions list OR legacy single-action
    // fields) to a validated, order-assigned list. Throws ValidationException on any issue.
    private static List<AutomationActionDto> NormalizeAndValidateActions(
        List<AutomationActionDto>? actions,
        string? legacyActionType,
        string? legacyConfigJson)
    {
        // If the new-style list is provided and non-empty, it wins. Otherwise fall
        // back to the legacy single-action fields. If neither path yields an action, reject.
        List<AutomationActionDto> raw;
        if (actions is not null && actions.Count > 0)
        {
            raw = actions.Select(a => new AutomationActionDto
            {
                ActionType = a.ActionType,
                ConfigJson = a.ConfigJson,
                Order = a.Order,
                ConditionJson = a.ConditionJson,
                RetryCount = a.RetryCount,
                RetryDelaySeconds = a.RetryDelaySeconds,
                StopOnFailure = a.StopOnFailure
            }).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(legacyActionType))
        {
            raw = new List<AutomationActionDto>
            {
                new() { ActionType = legacyActionType!, ConfigJson = legacyConfigJson, Order = 0 }
            };
        }
        else
        {
            throw new ValidationException("At least one action is required");
        }

        // Per-action validation: type whitelist + per-type config validation.
        foreach (var a in raw)
        {
            if (string.IsNullOrWhiteSpace(a.ActionType))
                throw new ValidationException("Action type is required");
            if (!ActionTypes.All.Contains(a.ActionType))
                throw new ValidationException($"Unsupported action type: {a.ActionType}");
            AutomationExecutor.ValidateConfig(a.ActionType, a.ConfigJson);
            if (a.Order.HasValue && a.Order.Value < 0)
                throw new ValidationException("Action order must be >= 0");
            // Optional condition: parse + validate up-front so runtime can trust persisted shape.
            if (!string.IsNullOrWhiteSpace(a.ConditionJson))
                AutomationConditionEvaluator.Parse(a.ConditionJson);
            // LS-FLOW-019-C — retry policy validation.
            if (a.RetryCount.HasValue && a.RetryCount.Value < 0)
                throw new ValidationException("retryCount must be >= 0");
            if (a.RetryDelaySeconds.HasValue && a.RetryDelaySeconds.Value < 0)
                throw new ValidationException("retryDelaySeconds must be >= 0");
        }

        // Assign sequential order where not provided, preserving submitted sequence for
        // items without an Order. Items with an explicit Order keep it.
        var normalized = new List<AutomationActionDto>(raw.Count);
        var autoIndex = 0;
        foreach (var a in raw)
        {
            var order = a.Order ?? autoIndex;
            if (!a.Order.HasValue) autoIndex++;
            else autoIndex = Math.Max(autoIndex, a.Order.Value + 1);
            normalized.Add(new AutomationActionDto
            {
                ActionType = a.ActionType,
                ConfigJson = a.ConfigJson,
                Order = order,
                ConditionJson = string.IsNullOrWhiteSpace(a.ConditionJson) ? null : a.ConditionJson,
                RetryCount = a.RetryCount ?? 0,
                RetryDelaySeconds = a.RetryDelaySeconds,
                StopOnFailure = a.StopOnFailure ?? false
            });
        }

        // Duplicate-order check across the final normalized set.
        var orderSet = new HashSet<int>();
        foreach (var a in normalized)
        {
            if (!orderSet.Add(a.Order!.Value))
                throw new ValidationException($"Duplicate action order {a.Order} in hook");
        }

        // Return sorted by Order so persistence and downstream iteration match.
        return normalized.OrderBy(a => a.Order).ToList();
    }

    private static WorkflowTransitionResponse MapTransitionResponse(WorkflowTransition t) => new()
    {
        Id = t.Id,
        FromStageId = t.FromStageId,
        ToStageId = t.ToStageId,
        Name = t.Name,
        IsActive = t.IsActive,
        RulesJson = t.RulesJson
    };

    private static void ValidateHookProductKeyMatchesWorkflow(string? requested, string workflowProductKey)
    {
        if (string.IsNullOrWhiteSpace(requested)) return;
        var trimmed = requested.Trim();
        if (!ProductKeys.IsValid(trimmed))
            throw new ValidationException($"Unsupported productKey: {trimmed}");
        if (!string.Equals(trimmed, workflowProductKey, StringComparison.Ordinal))
            throw new ValidationException("Automation hook productKey must match workflow productKey");
    }

    private static AutomationHookResponse MapHookResponse(WorkflowAutomationHook h)
    {
        // Always expose an Actions list. If the hook has no child rows (legacy hook
        // that predates LS-FLOW-019-A and hasn't been touched yet), synthesize a
        // single-item list from the legacy ActionType/ConfigJson columns.
        var actionDtos = h.Actions is { Count: > 0 }
            ? h.Actions
                .OrderBy(a => a.Order)
                .Select(a => new AutomationActionDto
                {
                    Id = a.Id,
                    ActionType = a.ActionType,
                    ConfigJson = a.ConfigJson,
                    ConditionJson = a.ConditionJson,
                    Order = a.Order,
                    RetryCount = a.RetryCount,
                    RetryDelaySeconds = a.RetryDelaySeconds,
                    StopOnFailure = a.StopOnFailure
                })
                .ToList()
            : new List<AutomationActionDto>
            {
                // Synthetic single-action fallback for legacy hooks (no child rows).
                // Normalize all 019-B/019-C policy fields to their defaults so the
                // response shape stays stable regardless of vintage.
                new()
                {
                    ActionType = h.ActionType,
                    ConfigJson = h.ConfigJson,
                    Order = 0,
                    ConditionJson = null,
                    RetryCount = 0,
                    RetryDelaySeconds = null,
                    StopOnFailure = false
                }
            };

        return new AutomationHookResponse
        {
            Id = h.Id,
            WorkflowDefinitionId = h.WorkflowDefinitionId,
            WorkflowTransitionId = h.WorkflowTransitionId,
            Name = h.Name,
            TriggerEventType = h.TriggerEventType,
            ProductKey = string.IsNullOrWhiteSpace(h.ProductKey) ? ProductKeys.FlowGeneric : h.ProductKey,
            ActionType = actionDtos[0].ActionType,
            ConfigJson = actionDtos[0].ConfigJson,
            Actions = actionDtos,
            IsActive = h.IsActive,
            CreatedAt = h.CreatedAt,
            UpdatedAt = h.UpdatedAt
        };
    }
}
