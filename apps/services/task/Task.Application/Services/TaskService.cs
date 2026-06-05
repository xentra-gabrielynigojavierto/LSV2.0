using BuildingBlocks.Exceptions;
using Task.Application.DTOs;
using Task.Application.Interfaces;
using Task.Domain.Entities;
using Task.Domain.Validation;
using TaskStatus = Task.Domain.Enums.TaskStatus;
using Microsoft.Extensions.Logging;

namespace Task.Application.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository             _tasks;
    private readonly ITaskNoteRepository         _notes;
    private readonly ITaskHistoryRepository      _history;
    private readonly ITaskLinkedEntityRepository _linkedEntities;
    private readonly ITaskGovernanceService      _governance;
    private readonly ITaskReminderService        _reminders;
    private readonly ITaskNotificationClient     _notifications;
    private readonly ITaskAuditPublisher         _audit;
    private readonly IUnitOfWork                 _uow;
    private readonly ILogger<TaskService>        _logger;

    public TaskService(
        ITaskRepository             tasks,
        ITaskNoteRepository         notes,
        ITaskHistoryRepository      history,
        ITaskLinkedEntityRepository linkedEntities,
        ITaskGovernanceService      governance,
        ITaskReminderService        reminders,
        ITaskNotificationClient     notifications,
        ITaskAuditPublisher         audit,
        IUnitOfWork                 uow,
        ILogger<TaskService>        logger)
    {
        _tasks          = tasks;
        _notes          = notes;
        _history        = history;
        _linkedEntities = linkedEntities;
        _governance     = governance;
        _reminders      = reminders;
        _notifications  = notifications;
        _audit          = audit;
        _uow            = uow;
        _logger         = logger;
    }

    public async System.Threading.Tasks.Task<TaskDto> CreateAsync(
        Guid              tenantId,
        Guid              createdByUserId,
        CreateTaskRequest request,
        CancellationToken ct = default)
    {
        // TASK-B05 (TASK-014) — validate product code against canonical registry
        KnownProductCodes.ValidateOptional(request.SourceProductCode);

        var governance = await _governance.ResolveAsync(tenantId, request.SourceProductCode, ct);

        if (governance.RequireAssignee && request.AssignedUserId is null)
            throw new InvalidOperationException("Governance requires an assignee.");
        if (governance.RequireDueDate && request.DueAt is null)
            throw new InvalidOperationException("Governance requires a due date.");

        var task = PlatformTask.Create(
            tenantId,
            request.Title,
            createdByUserId,
            request.Description,
            request.Priority ?? governance.DefaultPriority,
            request.Scope    ?? governance.DefaultTaskScope,
            request.AssignedUserId,
            request.SourceProductCode,
            request.SourceEntityType,
            request.SourceEntityId,
            request.DueAt,
            externalId:           request.ExternalId,
            generationRuleId:     request.GenerationRuleId,
            generatingTemplateId: request.GeneratingTemplateId,
            // TASK-FLOW-02 — queue assignment fields forwarded from Flow on creation
            assignmentMode:       request.AssignmentMode,
            assignedRole:         request.AssignedRole,
            assignedOrgId:        request.AssignedOrgId,
            assignedBy:           request.AssignedBy,
            assignmentReason:     request.AssignmentReason);

        if (request.WorkflowInstanceId.HasValue)
            task.SetWorkflowLinkage(request.WorkflowInstanceId, request.WorkflowStepKey, createdByUserId);

        await _tasks.AddAsync(task, ct);

        await _history.AddAsync(
            TaskHistory.Record(task.Id, tenantId, TaskActions.Created, createdByUserId,
                $"Task '{task.Title}' created with scope {task.Scope}"), ct);

        await _uow.SaveChangesAsync(ct);

        if (task.DueAt.HasValue)
            await _reminders.SyncRemindersAsync(tenantId, task.Id, task.DueAt, ct);

        _audit.Publish("TASK_CREATED", TaskActions.Created,
            $"Task '{task.Title}' created", tenantId, createdByUserId,
            "PlatformTask", task.Id.ToString());

        _logger.LogInformation(
            "Task {TaskId} created by {UserId} in tenant {TenantId} (scope={Scope})",
            task.Id, createdByUserId, tenantId, task.Scope);

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskDto?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var task = await _tasks.GetByIdAsync(tenantId, id, ct);
        return task is null ? null : TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskListResponse> SearchAsync(
        Guid      tenantId,
        string?   search               = null,
        string?   status               = null,
        string?   priority             = null,
        string?   scope                = null,
        Guid?     assignedUserId       = null,
        string?   sourceProductCode    = null,
        Guid?     stageId              = null,
        DateTime? dueBefore            = null,
        DateTime? dueAfter             = null,
        Guid?     workflowInstanceId   = null,
        string?   sourceEntityType     = null,
        Guid?     sourceEntityId       = null,
        string?   linkedEntityType     = null,
        Guid?     linkedEntityId       = null,
        string?   assignmentScope      = null,
        Guid?     currentUserId        = null,
        Guid?     generationRuleId     = null,
        Guid?     generatingTemplateId = null,
        bool      excludeTerminal      = false,
        int       page                 = 1,
        int       pageSize             = 50,
        string?   assignmentMode       = null,
        string?   assignedRole         = null,
        string?   assignedOrgId        = null,
        string?   sort                 = null,
        CancellationToken ct           = default)
    {
        var (items, total) = await _tasks.SearchAsync(
            tenantId, search, status, priority, scope,
            assignedUserId, sourceProductCode, stageId,
            dueBefore, dueAfter, workflowInstanceId,
            sourceEntityType, sourceEntityId,
            linkedEntityType, linkedEntityId,
            assignmentScope, currentUserId,
            generationRuleId, generatingTemplateId, excludeTerminal,
            page, pageSize,
            assignmentMode: assignmentMode,
            assignedRole:   assignedRole,
            assignedOrgId:  assignedOrgId,
            sort:           sort,
            ct:             ct);
        return new TaskListResponse(items.Select(TaskDto.From).ToList(), total, page, pageSize);
    }

    // TASK-FLOW-02 — batch SLA state push from Flow's WorkflowTaskSlaEvaluator
    public async System.Threading.Tasks.Task<FlowSlaUpdateResult> UpdateFlowSlaStateAsync(
        Guid                 tenantId,
        FlowSlaUpdateRequest request,
        CancellationToken    ct = default)
    {
        var updated  = 0;
        var notFound = 0;

        foreach (var item in request.Updates)
        {
            var task = await _tasks.GetByIdAsync(tenantId, item.TaskId, ct);
            if (task is null)
            {
                notFound++;
                _logger.LogWarning(
                    "FlowSlaUpdate: task {TaskId} not found in tenant {TenantId}", item.TaskId, tenantId);
                continue;
            }

            task.SetSlaState(item.SlaStatus, item.SlaBreachedAt, item.EvaluatedAt);
            updated++;
        }

        if (updated > 0)
            await _uow.SaveChangesAsync(ct);

        return new FlowSlaUpdateResult(updated, notFound);
    }

    // TASK-FLOW-02 — queue assignment delegated from Flow's WorkflowTaskAssignmentService
    public async System.Threading.Tasks.Task<FlowQueueAssignResult> SetFlowQueueAssignmentAsync(
        Guid                   tenantId,
        Guid                   taskId,
        FlowQueueAssignRequest request,
        CancellationToken      ct = default)
    {
        var task = await _tasks.GetByIdAsync(tenantId, taskId, ct);
        if (task is null)
            return new FlowQueueAssignResult(false, $"Task {taskId} not found");

        task.SetFlowQueueAssignment(
            request.AssignmentMode,
            request.AssignedUserId,
            request.AssignedRole,
            request.AssignedOrgId,
            request.AssignedBy,
            request.AssignmentReason);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "FlowQueueAssign: task {TaskId} assigned mode={Mode} user={UserId} role={Role} org={Org}",
            taskId, request.AssignmentMode, request.AssignedUserId, request.AssignedRole, request.AssignedOrgId);

        return new FlowQueueAssignResult(true);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskDto>> GetMyTasksAsync(
        Guid      tenantId,
        Guid      userId,
        string?   productCode = null,
        string?   status      = null,
        int       page        = 1,
        int       pageSize    = 50,
        CancellationToken ct  = default)
    {
        var tasks = await _tasks.GetByAssignedUserAsync(
            tenantId, userId, productCode, status, page, pageSize, ct);
        return tasks.Select(TaskDto.From).ToList();
    }

    public async System.Threading.Tasks.Task<MyTaskSummaryResponse> GetMyTaskSummaryAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var now    = DateTime.UtcNow;

        // TASK-B05 (TASK-017) — replaced unbounded 5000-row in-memory loads with
        // server-side aggregation queries. Start both in parallel.
        var countTask   = _tasks.GetMyTaskCountsAsync(tenantId, userId, ct);
        var overdueTask = _tasks.GetOverdueCountForUserAsync(tenantId, userId, now, ct);
        await System.Threading.Tasks.Task.WhenAll(countTask, overdueTask);
        var counts       = countTask.Result;
        var overdueCount = overdueTask.Result;

        var openStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "OPEN", "IN_PROGRESS", "PENDING_REVIEW", "BLOCKED" };

        var totalOpen = counts
            .Where(c => openStatuses.Contains(c.Status))
            .Sum(c => c.Count);

        var summary = counts
            .Select(c => new TaskProductSummaryDto(c.ProductCode, c.Status, c.Count))
            .ToList()
            .AsReadOnly();

        return new MyTaskSummaryResponse(summary, totalOpen, overdueCount);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskDto>> GetByWorkflowInstanceAsync(
        Guid tenantId, Guid workflowInstanceId, CancellationToken ct = default)
    {
        var tasks = await _tasks.GetByWorkflowInstanceAsync(tenantId, workflowInstanceId, ct);
        return tasks.Select(TaskDto.From).ToList();
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskDto>> GetBySourceEntityAsync(
        Guid   tenantId,
        string entityType,
        Guid   entityId,
        CancellationToken ct = default)
    {
        var tasks = await _tasks.GetBySourceEntityAsync(tenantId, entityType, entityId, ct);
        return tasks.Select(TaskDto.From).ToList();
    }

    public async System.Threading.Tasks.Task<TaskWorkflowContextDto?> GetWorkflowContextAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var task = await _tasks.GetByIdAsync(tenantId, id, ct);
        if (task is null) return null;
        return new TaskWorkflowContextDto(
            task.Id, task.WorkflowInstanceId, task.WorkflowStepKey, task.WorkflowLinkageChangedAt);
    }

    public async System.Threading.Tasks.Task<TaskDto> UpdateWorkflowLinkageAsync(
        Guid                         tenantId,
        Guid                         id,
        Guid                         updatedByUserId,
        UpdateWorkflowLinkageRequest request,
        CancellationToken            ct = default)
    {
        var task = await RequireTaskAsync(tenantId, id, ct);
        var changed = task.SetWorkflowLinkage(request.WorkflowInstanceId, request.WorkflowStepKey, updatedByUserId);

        if (changed)
        {
            await _history.AddAsync(
                TaskHistory.Record(task.Id, tenantId, TaskActions.FlowLinkageUpdated, updatedByUserId,
                    $"WorkflowInstance={request.WorkflowInstanceId}, Step={request.WorkflowStepKey}"), ct);
            await _uow.SaveChangesAsync(ct);
        }

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<FlowCallbackResult> ProcessFlowCallbackAsync(
        FlowStepCallbackRequest request,
        CancellationToken       ct = default)
    {
        var linked = await _tasks.GetByWorkflowInstanceAsync(
            request.TenantId, request.WorkflowInstanceId, ct);

        var actorId   = request.UpdatedByUserId ?? Guid.Empty;
        var updated   = 0;
        var skipped   = 0;

        foreach (var task in linked)
        {
            var changed = task.SetWorkflowLinkage(
                request.WorkflowInstanceId, request.NewStepKey, actorId);

            if (changed)
            {
                await _history.AddAsync(
                    TaskHistory.Record(task.Id, request.TenantId,
                        TaskActions.FlowLinkageUpdated, actorId,
                        $"FlowCallback: step→{request.NewStepKey}"), ct);
                updated++;
            }
            else
            {
                skipped++;
            }
        }

        if (updated > 0)
            await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Flow callback for workflow {WorkflowId}: {Updated} updated, {Skipped} skipped",
            request.WorkflowInstanceId, updated, skipped);

        return new FlowCallbackResult(updated, skipped, request.WorkflowInstanceId, request.NewStepKey);
    }

    public async System.Threading.Tasks.Task<TaskDto> UpdateAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        UpdateTaskRequest request,
        CancellationToken ct = default)
    {
        var task       = await RequireTaskAsync(tenantId, id, ct);
        var governance = await _governance.ResolveAsync(tenantId, task.SourceProductCode, ct);

        if (governance.RequireDueDate && request.DueAt is null)
            throw new InvalidOperationException("Governance requires a due date.");

        var previousDueAt = task.DueAt;
        task.Update(request.Title, updatedByUserId, request.Description,
                    request.Priority, request.AssignedUserId, request.DueAt);

        await _history.AddAsync(
            TaskHistory.Record(task.Id, tenantId, TaskActions.Updated, updatedByUserId), ct);

        await _uow.SaveChangesAsync(ct);

        if (task.DueAt != previousDueAt)
            await _reminders.SyncRemindersAsync(tenantId, task.Id, task.DueAt, ct);

        _logger.LogInformation(
            "Task {TaskId} updated by {UserId} in tenant {TenantId}", id, updatedByUserId, tenantId);

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskDto> TransitionStatusAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        string            newStatus,
        CancellationToken ct = default)
    {
        var task       = await RequireTaskAsync(tenantId, id, ct);
        var governance = await _governance.ResolveAsync(tenantId, task.SourceProductCode, ct);

        if (newStatus == TaskStatus.Cancelled && !governance.AllowCancel)
            throw new InvalidOperationException("Governance does not allow cancellation of tasks.");

        if (newStatus == TaskStatus.Completed
            && !governance.AllowCompleteWithoutStage
            && task.CurrentStageId is null)
            throw new InvalidOperationException("Governance requires a stage to be set before completing a task.");

        task.TransitionStatus(newStatus, updatedByUserId);

        var action = newStatus switch
        {
            TaskStatus.Completed => TaskActions.Completed,
            TaskStatus.Cancelled => TaskActions.Cancelled,
            _                    => TaskActions.StatusChanged,
        };

        await _history.AddAsync(
            TaskHistory.Record(task.Id, tenantId, action, updatedByUserId,
                $"Status changed to {newStatus}"), ct);

        await _uow.SaveChangesAsync(ct);

        if (TaskStatus.IsTerminal(newStatus))
            await _reminders.CancelRemindersAsync(tenantId, task.Id, ct);

        _audit.Publish($"TASK_{action.ToUpperInvariant()}", action,
            $"Task status → {newStatus}", tenantId, updatedByUserId,
            "PlatformTask", task.Id.ToString());

        _logger.LogInformation(
            "Task {TaskId} status → {Status} by {UserId} in tenant {TenantId}",
            id, newStatus, updatedByUserId, tenantId);

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskDto> AssignAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        Guid?             assignedUserId,
        CancellationToken ct = default)
    {
        var task       = await RequireTaskAsync(tenantId, id, ct);
        var governance = await _governance.ResolveAsync(tenantId, task.SourceProductCode, ct);

        if (assignedUserId is null && !governance.AllowUnassign)
            throw new InvalidOperationException("Governance does not allow unassigning tasks.");

        var changeKind = task.Assign(assignedUserId, updatedByUserId);

        if (changeKind != AssignmentChangeKind.NoOp)
        {
            var (action, detail) = changeKind switch
            {
                AssignmentChangeKind.Assigned   => (TaskActions.Assigned,   $"Assigned to {assignedUserId}"),
                AssignmentChangeKind.Reassigned => (TaskActions.Reassigned, $"Reassigned to {assignedUserId}"),
                AssignmentChangeKind.Unassigned => (TaskActions.Unassigned, "Assignee removed"),
                _                               => (TaskActions.Updated,    string.Empty),
            };

            await _history.AddAsync(
                TaskHistory.Record(task.Id, tenantId, action, updatedByUserId, detail), ct);

            await _uow.SaveChangesAsync(ct);

            if (assignedUserId.HasValue)
            {
                try
                {
                    if (changeKind == AssignmentChangeKind.Assigned)
                        await _notifications.NotifyAssignedAsync(
                            tenantId, task.Id, task.Title,
                            assignedUserId.Value, task.SourceProductCode, ct);
                    else
                        await _notifications.NotifyReassignedAsync(
                            tenantId, task.Id, task.Title,
                            assignedUserId.Value, task.SourceProductCode, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Notification for task {TaskId} assignment could not be dispatched — continuing.",
                        task.Id);
                }
            }
        }

        _logger.LogInformation(
            "Task {TaskId} assignment change={Change} by {UserId} in tenant {TenantId}",
            id, changeKind, updatedByUserId, tenantId);

        return TaskDto.From(task);
    }

    public async System.Threading.Tasks.Task<TaskNoteDto> AddNoteAsync(
        Guid              tenantId,
        Guid              taskId,
        Guid              createdByUserId,
        string            note,
        CancellationToken ct         = default,
        string?           authorName = null)
    {
        var task       = await RequireTaskAsync(tenantId, taskId, ct);
        var governance = await _governance.ResolveAsync(tenantId, task.SourceProductCode, ct);

        if (TaskStatus.IsTerminal(task.Status) && !governance.AllowNotesOnClosedTasks)
            throw new InvalidOperationException("Governance does not allow notes on closed tasks.");

        var noteEntity = TaskNote.Create(taskId, tenantId, note, createdByUserId, authorName);
        await _notes.AddAsync(noteEntity, ct);

        await _history.AddAsync(
            TaskHistory.Record(taskId, tenantId, TaskActions.NoteAdded, createdByUserId), ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Note {NoteId} added to task {TaskId} by {UserId}", noteEntity.Id, taskId, createdByUserId);

        return TaskNoteDto.From(noteEntity);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskNoteDto>> GetNotesAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        await RequireTaskAsync(tenantId, taskId, ct);
        var notes = await _notes.GetByTaskAsync(tenantId, taskId, ct);
        return notes.Select(TaskNoteDto.From).ToList();
    }

    public async System.Threading.Tasks.Task<TaskNoteDto> EditNoteAsync(
        Guid              tenantId,
        Guid              taskId,
        Guid              noteId,
        Guid              editorUserId,
        string            newContent,
        CancellationToken ct = default)
    {
        await RequireTaskAsync(tenantId, taskId, ct);

        var note = await _notes.GetByIdAsync(tenantId, noteId, ct)
            ?? throw new InvalidOperationException($"Note '{noteId}' not found.");

        if (note.TaskId != taskId)
            throw new InvalidOperationException($"Note '{noteId}' does not belong to task '{taskId}'.");

        if (note.CreatedByUserId != editorUserId)
            throw new UnauthorizedAccessException("You can only edit your own notes.");

        note.Edit(newContent, editorUserId);
        await _notes.UpdateAsync(note, ct);

        await _history.AddAsync(
            TaskHistory.Record(taskId, tenantId, TaskActions.NoteAdded, editorUserId, "Note edited"), ct);

        await _uow.SaveChangesAsync(ct);

        return TaskNoteDto.From(note);
    }

    public async System.Threading.Tasks.Task DeleteNoteAsync(
        Guid              tenantId,
        Guid              taskId,
        Guid              noteId,
        Guid              deletedByUserId,
        CancellationToken ct = default)
    {
        await RequireTaskAsync(tenantId, taskId, ct);

        var note = await _notes.GetByIdAsync(tenantId, noteId, ct)
            ?? throw new InvalidOperationException($"Note '{noteId}' not found.");

        if (note.TaskId != taskId)
            throw new InvalidOperationException($"Note '{noteId}' does not belong to task '{taskId}'.");

        if (note.CreatedByUserId != deletedByUserId)
            throw new UnauthorizedAccessException("You can only delete your own notes.");

        if (!note.IsDeleted)
        {
            note.SoftDelete(deletedByUserId);
            await _notes.UpdateAsync(note, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskHistoryDto>> GetHistoryAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        await RequireTaskAsync(tenantId, taskId, ct);
        var entries = await _history.GetByTaskAsync(tenantId, taskId, ct);
        return entries.Select(TaskHistoryDto.From).ToList();
    }

    public async System.Threading.Tasks.Task<TaskLinkedEntityDto> AddLinkedEntityAsync(
        Guid                   tenantId,
        Guid                   taskId,
        Guid                   createdByUserId,
        AddLinkedEntityRequest request,
        CancellationToken      ct = default)
    {
        await RequireTaskAsync(tenantId, taskId, ct);

        var entity = TaskLinkedEntity.Create(
            taskId, tenantId,
            request.EntityType, request.EntityId,
            request.RelationshipType, request.SourceProductCode);

        await _linkedEntities.AddAsync(entity, ct);

        await _history.AddAsync(
            TaskHistory.Record(taskId, tenantId, TaskActions.LinkedEntityAdded, createdByUserId,
                $"{request.EntityType}:{request.EntityId} [{request.RelationshipType}]"), ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "LinkedEntity {EntityId} added to task {TaskId} by {UserId}",
            entity.Id, taskId, createdByUserId);

        return TaskLinkedEntityDto.From(entity);
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskLinkedEntityDto>> GetLinkedEntitiesAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default)
    {
        await RequireTaskAsync(tenantId, taskId, ct);
        var entities = await _linkedEntities.GetByTaskAsync(tenantId, taskId, ct);
        return entities.Select(TaskLinkedEntityDto.From).ToList();
    }

    public async System.Threading.Tasks.Task RemoveLinkedEntityAsync(
        Guid tenantId,
        Guid taskId,
        Guid linkedEntityId,
        Guid removedByUserId,
        CancellationToken ct = default)
    {
        await RequireTaskAsync(tenantId, taskId, ct);

        var entity = await _linkedEntities.GetByIdAsync(tenantId, linkedEntityId, ct);
        if (entity is null || entity.TaskId != taskId)
            throw new NotFoundException($"Linked entity {linkedEntityId} not found on task {taskId}.");

        _linkedEntities.Remove(entity);

        await _history.AddAsync(
            TaskHistory.Record(taskId, tenantId, TaskActions.LinkedEntityRemoved, removedByUserId,
                $"{entity.EntityType}:{entity.EntityId}"), ct);

        await _uow.SaveChangesAsync(ct);
    }

    // TASK-FLOW-03 — SLA batch read for Flow's WorkflowTaskSlaEvaluator
    public async System.Threading.Tasks.Task<Task.Application.DTOs.FlowSlaBatchResponse> GetFlowSlaBatchAsync(
        int               batchSize,
        DateTime          dueSoonHorizonUtc,
        CancellationToken ct = default)
    {
        var items = await _tasks.GetFlowSlaBatchAsync(
            Math.Max(1, batchSize), dueSoonHorizonUtc, ct);
        return new Task.Application.DTOs.FlowSlaBatchResponse(items);
    }

    private async System.Threading.Tasks.Task<PlatformTask> RequireTaskAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(tenantId, id, ct);
        if (task is null)
            throw new NotFoundException($"Task {id} not found.");
        return task;
    }
}
