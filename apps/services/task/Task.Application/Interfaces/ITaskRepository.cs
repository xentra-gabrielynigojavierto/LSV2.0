using Task.Domain.Entities;

namespace Task.Application.Interfaces;

public interface ITaskRepository
{
    System.Threading.Tasks.Task<PlatformTask?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default);

    System.Threading.Tasks.Task<(IReadOnlyList<PlatformTask> Items, int Total)> SearchAsync(
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
        // TASK-FLOW-02 — queue filter params
        string?   assignmentMode       = null,
        string?   assignedRole         = null,
        string?   assignedOrgId        = null,
        string?   sort                 = null,
        CancellationToken ct           = default);

    System.Threading.Tasks.Task<IReadOnlyList<PlatformTask>> GetByAssignedUserAsync(
        Guid      tenantId,
        Guid      userId,
        string?   productCode = null,
        string?   status      = null,
        int       page        = 1,
        int       pageSize    = 200,
        CancellationToken ct  = default);

    System.Threading.Tasks.Task<IReadOnlyList<PlatformTask>> GetByWorkflowInstanceAsync(
        Guid tenantId, Guid workflowInstanceId, CancellationToken ct = default);

    System.Threading.Tasks.Task<IReadOnlyList<PlatformTask>> GetBySourceEntityAsync(
        Guid   tenantId,
        string entityType,
        Guid   entityId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns grouped task counts by (SourceProductCode, Status) for the given user.
    /// Used by the cross-product my-task summary widget.
    /// </summary>
    System.Threading.Tasks.Task<IReadOnlyList<(string? ProductCode, string Status, int Count)>> GetMyTaskCountsAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// TASK-B05 (TASK-017) — server-side count of tasks that are overdue for a user.
    /// Replaces the previous in-memory approach of loading up to 5 000 rows.
    /// </summary>
    System.Threading.Tasks.Task<int> GetOverdueCountForUserAsync(
        Guid tenantId, Guid userId, DateTime asOf, CancellationToken ct = default);

    System.Threading.Tasks.Task AddAsync(PlatformTask task, CancellationToken ct = default);

    /// <summary>
    /// TASK-FLOW-03 — Returns a cross-tenant batch of active tasks with
    /// <c>DueAt</c> set and SLA status potentially needing re-evaluation.
    /// Ordered by <c>DueAt</c> ascending (nearest deadline first).
    /// Used exclusively by Flow's <c>WorkflowTaskSlaEvaluator</c>.
    /// </summary>
    System.Threading.Tasks.Task<IReadOnlyList<Task.Application.DTOs.FlowSlaBatchItem>> GetFlowSlaBatchAsync(
        int               batchSize,
        DateTime          dueSoonHorizonUtc,
        CancellationToken ct = default);
}
