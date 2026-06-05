using Task.Application.DTOs;

namespace Task.Application.Interfaces;

public interface ITaskService
{
    System.Threading.Tasks.Task<TaskDto> CreateAsync(
        Guid              tenantId,
        Guid              createdByUserId,
        CreateTaskRequest request,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskDto?> GetByIdAsync(
        Guid              tenantId,
        Guid              id,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskListResponse> SearchAsync(
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
        // TASK-FLOW-02
        string?   assignmentMode       = null,
        string?   assignedRole         = null,
        string?   assignedOrgId        = null,
        string?   sort                 = null,
        CancellationToken ct           = default);

    // TASK-FLOW-02 — internal SLA push from Flow's WorkflowTaskSlaEvaluator
    System.Threading.Tasks.Task<FlowSlaUpdateResult> UpdateFlowSlaStateAsync(
        Guid                tenantId,
        FlowSlaUpdateRequest request,
        CancellationToken   ct = default);

    // TASK-FLOW-02 — internal queue assignment from Flow's WorkflowTaskAssignmentService
    System.Threading.Tasks.Task<FlowQueueAssignResult> SetFlowQueueAssignmentAsync(
        Guid                   tenantId,
        Guid                   taskId,
        FlowQueueAssignRequest request,
        CancellationToken      ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskDto>> GetMyTasksAsync(
        Guid      tenantId,
        Guid      userId,
        string?   productCode = null,
        string?   status      = null,
        int       page        = 1,
        int       pageSize    = 50,
        CancellationToken ct  = default);

    System.Threading.Tasks.Task<MyTaskSummaryResponse> GetMyTaskSummaryAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskDto>> GetByWorkflowInstanceAsync(
        Guid tenantId, Guid workflowInstanceId, CancellationToken ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskDto>> GetBySourceEntityAsync(
        Guid   tenantId,
        string entityType,
        Guid   entityId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskWorkflowContextDto?> GetWorkflowContextAsync(
        Guid tenantId, Guid id, CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskDto> UpdateWorkflowLinkageAsync(
        Guid                         tenantId,
        Guid                         id,
        Guid                         updatedByUserId,
        UpdateWorkflowLinkageRequest request,
        CancellationToken            ct = default);

    System.Threading.Tasks.Task<FlowCallbackResult> ProcessFlowCallbackAsync(
        FlowStepCallbackRequest request,
        CancellationToken       ct = default);

    System.Threading.Tasks.Task<TaskDto> UpdateAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        UpdateTaskRequest request,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskDto> TransitionStatusAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        string            newStatus,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskDto> AssignAsync(
        Guid              tenantId,
        Guid              id,
        Guid              updatedByUserId,
        Guid?             assignedUserId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskNoteDto> AddNoteAsync(
        Guid              tenantId,
        Guid              taskId,
        Guid              createdByUserId,
        string            note,
        CancellationToken ct         = default,
        string?           authorName = null);

    System.Threading.Tasks.Task<IReadOnlyList<TaskNoteDto>> GetNotesAsync(
        Guid              tenantId,
        Guid              taskId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskNoteDto> EditNoteAsync(
        Guid              tenantId,
        Guid              taskId,
        Guid              noteId,
        Guid              editorUserId,
        string            newContent,
        CancellationToken ct = default);

    System.Threading.Tasks.Task DeleteNoteAsync(
        Guid              tenantId,
        Guid              taskId,
        Guid              noteId,
        Guid              deletedByUserId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskHistoryDto>> GetHistoryAsync(
        Guid              tenantId,
        Guid              taskId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task<TaskLinkedEntityDto> AddLinkedEntityAsync(
        Guid                    tenantId,
        Guid                    taskId,
        Guid                    createdByUserId,
        AddLinkedEntityRequest  request,
        CancellationToken       ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskLinkedEntityDto>> GetLinkedEntitiesAsync(
        Guid tenantId, Guid taskId, CancellationToken ct = default);

    System.Threading.Tasks.Task RemoveLinkedEntityAsync(
        Guid tenantId,
        Guid taskId,
        Guid linkedEntityId,
        Guid removedByUserId,
        CancellationToken ct = default);

    /// <summary>
    /// TASK-FLOW-03 — Returns a cross-tenant batch of active tasks with
    /// <c>DueAt</c> set and SLA status potentially needing re-evaluation.
    /// Called by Flow's <c>WorkflowTaskSlaEvaluator</c> after the shadow
    /// table (<c>flow_workflow_tasks</c>) was dropped.
    /// </summary>
    System.Threading.Tasks.Task<Task.Application.DTOs.FlowSlaBatchResponse> GetFlowSlaBatchAsync(
        int               batchSize,
        DateTime          dueSoonHorizonUtc,
        CancellationToken ct = default);
}
