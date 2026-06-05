namespace Flow.Application.Interfaces;

// ── Projection DTOs returned by read paths ─────────────────────────────────

/// <summary>Minimal task projection returned by Task service read calls.</summary>
public record TaskServiceTaskDto(
    Guid      TaskId,
    Guid      TenantId,
    string    Title,
    string?   Description,
    string    Status,
    string    Priority,
    string?   WorkflowStepKey,
    // TASK-FLOW-02 — queue assignment
    string?   AssignmentMode,
    string?   AssignedUserId,
    string?   AssignedRole,
    string?   AssignedOrgId,
    DateTime? AssignedAt,
    string?   AssignedBy,
    string?   AssignmentReason,
    // Lifecycle
    DateTime  CreatedAtUtc,
    DateTime  UpdatedAtUtc,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    // SLA
    DateTime? DueAt,
    string    SlaStatus,
    DateTime? SlaBreachedAt,
    // Workflow linkage
    Guid?     WorkflowInstanceId);

/// <summary>Paged result returned by Task service list endpoints.</summary>
public record TaskServicePageResult(
    IReadOnlyList<TaskServiceTaskDto> Items,
    int Total,
    int Page,
    int PageSize);

// ── Client interface ────────────────────────────────────────────────────────

/// <summary>
/// TASK-FLOW-01 / TASK-FLOW-02 — HTTP client interface for the canonical Task service.
/// Flow delegates task creation, lifecycle mutations, and (Phase 2) read paths to this client.
///
/// <para>
/// Auth model: the implementation forwards the calling user's bearer token
/// via <c>FlowTaskServiceAuthDelegatingHandler</c>. All Task service user-facing
/// endpoints require <c>AuthenticatedUser</c> policy. Internal endpoints use the
/// service token sent via <c>FlowTaskServiceInternalAuthHandler</c>.
/// </para>
///
/// <para>
/// Dual-write contract (Phase 1 / TASK-FLOW-01): callers invoke Task service
/// first (making it the write authority) and then mirror the change to
/// <c>flow_workflow_tasks</c> via the local EF context. If the Task service call
/// fails, the local write is NOT performed and the error propagates to the caller.
/// </para>
///
/// <para>
/// Phase 2 (TASK-FLOW-02) adds read paths: list/get from Task service, SLA push,
/// and full queue-assignment delegation for all modes (not just DirectUser).
/// </para>
/// </summary>
public interface IFlowTaskServiceClient
{
    // ── Write: create ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new task in the Task service for the given workflow step.
    /// Returns the canonical Task service ID assigned to the new task.
    /// Tenant context is derived from the forwarded bearer token.
    /// TASK-FLOW-02 — now also accepts queue metadata so all assignment modes
    /// are fully represented on the Task service record at creation time.
    /// </summary>
    Task<Guid> CreateWorkflowTaskAsync(
        Guid      workflowInstanceId,
        string    stepKey,
        string    title,
        string    priority,
        DateTime? dueAt,
        string?   assignedUserId,
        Guid?     externalId           = null,
        string?   assignmentMode       = null,
        string?   assignedRole         = null,
        string?   assignedOrgId        = null,
        string?   assignedBy           = null,
        string?   assignmentReason     = null,
        CancellationToken ct = default);

    // ── Write: lifecycle transitions ─────────────────────────────────────────

    /// <summary>Transitions a task to <c>IN_PROGRESS</c> (Open → InProgress).</summary>
    Task StartTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Transitions a task to <c>COMPLETED</c> (InProgress → Completed).</summary>
    Task CompleteTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Transitions a task to <c>CANCELLED</c> (Open|InProgress → Cancelled).</summary>
    Task CancelTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Assigns a task to a specific user (DirectUser mode).
    /// Pass <c>null</c> to clear the assignment (Unassigned).
    /// </summary>
    Task AssignUserAsync(Guid taskId, Guid? assignedUserId, CancellationToken ct = default);

    // ── Write: internal — queue assignment (TASK-FLOW-02) ────────────────────

    /// <summary>
    /// TASK-FLOW-02 — Pushes full queue assignment metadata to the Task service
    /// via the internal <c>POST /api/tasks/internal/flow-queue-assign/{tenantId}/{taskId}</c>
    /// endpoint. Used by all assignment modes (DirectUser, RoleQueue, OrgQueue, Unassigned).
    /// Uses the service-token auth handler (not the user bearer token).
    /// </summary>
    Task SetQueueAssignmentAsync(
        Guid      tenantId,
        Guid      taskId,
        string?   assignmentMode,
        Guid?     assignedUserId,
        string?   assignedRole,
        string?   assignedOrgId,
        string?   assignedBy,
        string?   assignmentReason,
        CancellationToken ct = default);

    // ── Write: internal — SLA push (TASK-FLOW-02) ────────────────────────────

    /// <summary>
    /// TASK-FLOW-02 — Pushes a batch of SLA state updates to the Task service
    /// via the internal <c>POST /api/tasks/internal/flow-sla-update</c> endpoint.
    /// Called by <see cref="Flow.Infrastructure.Outbox.WorkflowTaskSlaEvaluator"/>
    /// after computing per-task SLA transitions.
    /// Uses the service-token auth handler.
    /// </summary>
    Task UpdateSlaStateAsync(
        Guid tenantId,
        IReadOnlyList<(Guid TaskId, string SlaStatus, DateTime? SlaBreachedAt, DateTime EvaluatedAt)> updates,
        CancellationToken ct = default);

    // ── Read paths (TASK-FLOW-02) ────────────────────────────────────────────

    /// <summary>
    /// TASK-FLOW-02 — Lists tasks from the Task service with queue-aware filters.
    /// Equivalent to <c>GET /api/tasks</c> with query parameters.
    /// </summary>
    Task<TaskServicePageResult> ListTasksAsync(
        string?   assignedUserId  = null,
        string?   status          = null,
        string?   assignmentMode  = null,
        string?   assignedRole    = null,
        string?   assignedOrgId   = null,
        string?   sort            = null,
        int       page            = 1,
        int       pageSize        = 50,
        CancellationToken ct      = default);

    /// <summary>
    /// TASK-FLOW-02 — Retrieves a single task by ID.
    /// Returns <c>null</c> when the task is not found (404).
    /// </summary>
    Task<TaskServiceTaskDto?> GetTaskByIdAsync(Guid taskId, CancellationToken ct = default);

    // ── Read: analytics (TASK-FLOW-04) ────────────────────────────────────────

    /// <summary>
    /// TASK-FLOW-04 — Returns pre-computed SLA analytics for <paramref name="tenantId"/>
    /// from the Task service. Replaces the <c>_db.WorkflowTasks</c> queries in
    /// <c>FlowAnalyticsService.GetSlaSummaryAsync</c>.
    /// </summary>
    Task<TaskSlaAnalyticsResult> GetSlaAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        CancellationToken ct = default);

    /// <summary>
    /// TASK-FLOW-04 — Returns pre-computed queue and workload analytics for
    /// <paramref name="tenantId"/>. Replaces <c>GetQueueSummaryAsync</c> shadow reads.
    /// </summary>
    Task<TaskQueueAnalyticsResult> GetQueueAnalyticsAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// TASK-FLOW-04 — Returns pre-computed assignment analytics for
    /// <paramref name="tenantId"/>. Replaces <c>GetAssignmentSummaryAsync</c> shadow reads.
    /// </summary>
    Task<TaskAssignmentAnalyticsResult> GetAssignmentAnalyticsAsync(
        Guid      tenantId,
        DateTime  windowStart,
        DateTime  windowEnd,
        CancellationToken ct = default);

    /// <summary>
    /// TASK-FLOW-04 — Returns cross-tenant active task and SLA counts.
    /// Replaces the <c>IgnoreQueryFilters()</c> queries in <c>GetPlatformSummaryAsync</c>.
    /// </summary>
    Task<TaskPlatformAnalyticsResult> GetPlatformAnalyticsAsync(
        CancellationToken ct = default);

    // ── Workload / dedup (TASK-FLOW-03) ───────────────────────────────────────

    /// <summary>
    /// TASK-FLOW-03 — Returns active task counts per user for the supplied
    /// <paramref name="userIds"/> within <paramref name="tenantId"/>.
    /// Replaces <c>WorkloadService.GetActiveTaskCountsAsync</c> shadow query.
    /// Users with zero active tasks are omitted from the result.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetWorkloadCountsAsync(
        Guid                tenantId,
        IEnumerable<string> userIds,
        CancellationToken   ct = default);

    /// <summary>
    /// TASK-FLOW-03 — Returns user IDs who have at least one active task
    /// in <paramref name="role"/> for <paramref name="tenantId"/>.
    /// Replaces <c>WorkloadService.GetUserIdsForRoleAsync</c> shadow query.
    /// </summary>
    Task<IReadOnlyList<string>> GetWorkloadUsersByRoleAsync(
        Guid              tenantId,
        string            role,
        int               max = 20,
        CancellationToken ct  = default);

    /// <summary>
    /// TASK-FLOW-03 — Returns user IDs who have at least one active task
    /// in <paramref name="orgId"/> for <paramref name="tenantId"/>.
    /// Replaces <c>WorkloadService.GetUserIdsForOrgAsync</c> shadow query.
    /// </summary>
    Task<IReadOnlyList<string>> GetWorkloadUsersByOrgAsync(
        Guid              tenantId,
        string            orgId,
        int               max = 20,
        CancellationToken ct  = default);

    /// <summary>
    /// TASK-FLOW-03 — Returns true when an active (Open or InProgress) task
    /// already exists in the Task service for the given
    /// <paramref name="workflowInstanceId"/> + <paramref name="stepKey"/>.
    /// Replaces <c>WorkflowTaskFromWorkflowFactory</c> shadow dedup check.
    /// </summary>
    Task<bool> HasActiveStepTaskAsync(
        Guid              tenantId,
        Guid              workflowInstanceId,
        string            stepKey,
        CancellationToken ct = default);

    /// <summary>
    /// TASK-FLOW-03 — Returns a cross-tenant batch of active tasks with
    /// <c>DueAt</c> set and SLA status potentially needing re-evaluation.
    /// Replaces <c>WorkflowTaskSlaEvaluator</c>'s shadow table read.
    /// </summary>
    Task<IReadOnlyList<FlowSlaBatchItem>> GetTasksForSlaEvaluationAsync(
        int               batchSize,
        int               dueSoonThresholdMinutes,
        CancellationToken ct = default);
}

/// <summary>
/// TASK-FLOW-03 — Minimal projection returned by the Task service SLA
/// batch read endpoint. Contains only the fields needed by
/// <see cref="Flow.Infrastructure.Outbox.WorkflowTaskSlaEvaluator"/>
/// to compute SLA status transitions.
/// </summary>
public record FlowSlaBatchItem(
    Guid      TaskId,
    Guid      TenantId,
    DateTime? DueAt,
    string    SlaStatus,
    DateTime? SlaBreachedAt);

// ── Analytics result records (TASK-FLOW-04) ───────────────────────────────────

public record TaskSlaAnalyticsResult(
    IReadOnlyList<SlaStatusCount>  SlaGroups,
    double?                        AvgOverdueAgeDays,
    int                            BreachedInWindow,
    int                            CompletedInWindow,
    int                            CompletedOnTimeInWindow,
    IReadOnlyList<QueueOverdueItem> RoleOverdueGroups,
    IReadOnlyList<QueueOverdueItem> OrgOverdueGroups);

public record SlaStatusCount(string SlaStatus, int Count);
public record QueueOverdueItem(string QueueKey, int OverdueCount);

public record TaskQueueAnalyticsResult(
    IReadOnlyList<QueueGroupItem> RoleGroups,
    IReadOnlyList<QueueGroupItem> OrgGroups,
    int                           UnassignedCount,
    double?                       OldestQueueAgeHours,
    double?                       MedianQueueAgeHours,
    int                           ActiveUserCount,
    int                           OverloadedUserCount);

public record QueueGroupItem(string Key, string Status, string SlaStatus, int Count);

public record TaskAssignmentAnalyticsResult(
    IReadOnlyList<ModeCount>       ModeGroups,
    int                            AssignedInWindow,
    IReadOnlyList<UserStatusCount> UserStatusGroups);

public record ModeCount(string? Mode, int Count);
public record UserStatusCount(string UserId, string Status, int Count);

public record TaskPlatformAnalyticsResult(
    int                             TotalActiveTasks,
    int                             TotalOverdueTasks,
    IReadOnlyList<TenantSlaCount>   TenantSlaGroups);

public record TenantSlaCount(Guid TenantId, string SlaStatus, int Count);
