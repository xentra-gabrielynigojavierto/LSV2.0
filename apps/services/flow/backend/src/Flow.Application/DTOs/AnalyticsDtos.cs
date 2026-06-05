using System.Text.Json.Serialization;

namespace Flow.Application.DTOs;

// ── Time Window ──────────────────────────────────────────────────────────────

/// <summary>
/// E19 — supported reporting windows for analytics queries.
/// All windows use UTC boundaries.
/// </summary>
public enum AnalyticsWindow
{
    Today    = 0,
    Last7Days  = 7,
    Last30Days = 30,
}

// ── SLA Analytics ────────────────────────────────────────────────────────────

/// <summary>
/// E19 — SLA summary for a single time window and scope (tenant or platform).
///
/// Source-of-truth:
///   Active/overdue/at-risk counts → WorkflowTask.Status + WorkflowTask.SlaStatus (current state).
///   BreachedInWindow              → WorkflowTask.SlaBreachedAt within [windowStart, now].
///   OnTimeCompletions             → WorkflowTask.CompletedAt + WorkflowTask.SlaStatus = OnTrack.
///   AvgOverdueAgeDays             → (UtcNow − WorkflowTask.SlaBreachedAt) for current Overdue tasks.
/// </summary>
public sealed record SlaSummaryDto
{
    /// <summary>Tasks in Open/InProgress that have SlaStatus = OnTrack.</summary>
    public int ActiveOnTrackCount { get; init; }

    /// <summary>Tasks in Open/InProgress that have SlaStatus = DueSoon.</summary>
    public int ActiveAtRiskCount { get; init; }

    /// <summary>Tasks in Open/InProgress that have SlaStatus = Overdue.</summary>
    public int ActiveOverdueCount { get; init; }

    /// <summary>Total active tasks (Open + InProgress).</summary>
    public int TotalActiveCount { get; init; }

    /// <summary>
    /// Percentage of active tasks that are overdue. 0 when TotalActiveCount = 0.
    /// </summary>
    public double OverduePercentage { get; init; }

    /// <summary>
    /// Tasks where SlaBreachedAt falls within the reporting window.
    /// Measures new breaches (not cumulative).
    /// </summary>
    public int BreachedInWindow { get; init; }

    /// <summary>
    /// Tasks completed (CompletedAt in window) where SlaStatus was OnTrack at completion.
    /// Approximates on-time completion rate within the window.
    /// </summary>
    public int CompletedOnTimeInWindow { get; init; }

    /// <summary>Tasks completed within the window (any SLA status).</summary>
    public int CompletedInWindow { get; init; }

    /// <summary>
    /// Average age in days of currently overdue tasks (measured from SlaBreachedAt to UtcNow).
    /// Null when no overdue tasks exist.
    /// </summary>
    public double? AvgOverdueAgeDays { get; init; }

    /// <summary>UTC start of the reporting window.</summary>
    public DateTime WindowStart { get; init; }

    /// <summary>UTC end of the reporting window (always UtcNow).</summary>
    public DateTime WindowEnd { get; init; }

    /// <summary>Human-readable label for the window.</summary>
    public string WindowLabel { get; init; } = string.Empty;

    /// <summary>
    /// Top overdue queues by count — role or org id with count of overdue tasks.
    /// Max 10 entries.
    /// </summary>
    public List<QueueOverdueBreakdownDto> TopOverdueQueues { get; init; } = new();
}

/// <summary>E19 — single queue's overdue task count for the SLA top-overdue list.</summary>
public sealed record QueueOverdueBreakdownDto
{
    public string QueueKey  { get; init; } = string.Empty;
    public string QueueType { get; init; } = string.Empty; // "Role" | "Org"
    public int    OverdueCount { get; init; }
}

// ── Queue / Workload Analytics ───────────────────────────────────────────────

/// <summary>
/// E19 — queue backlog and workload analytics.
///
/// Source-of-truth:
///   Role/OrgQueue backlog → WorkflowTask.AssignmentMode + Status ∈ {Open, InProgress}.
///   QueueAgeHours         → (UtcNow − WorkflowTask.CreatedAt) for queued tasks.
///   ActivePerUser         → WorkflowTask.AssignedUserId GROUP BY, Status ∈ {Open, InProgress}.
///   OverloadedUsers       → users where active count ≥ OverloadThreshold (default 10).
/// </summary>
public sealed record QueueSummaryDto
{
    /// <summary>Total tasks in RoleQueue assignment mode with Status ∈ {Open, InProgress}.</summary>
    public int RoleQueueBacklog { get; init; }

    /// <summary>Total tasks in OrgQueue assignment mode with Status ∈ {Open, InProgress}.</summary>
    public int OrgQueueBacklog { get; init; }

    /// <summary>Total tasks in Unassigned mode with Status ∈ {Open, InProgress}.</summary>
    public int UnassignedBacklog { get; init; }

    /// <summary>Age in hours of the oldest queued (non-DirectUser) task.</summary>
    public double? OldestQueuedTaskAgeHours { get; init; }

    /// <summary>Median age in hours of currently queued tasks.</summary>
    public double? MedianQueueAgeHours { get; init; }

    /// <summary>Number of distinct users with at least one active task.</summary>
    public int ActiveUserCount { get; init; }

    /// <summary>Number of users with active task count ≥ overload threshold.</summary>
    public int OverloadedUserCount { get; init; }

    /// <summary>Threshold used to determine overloaded users.</summary>
    public int OverloadThreshold { get; init; }

    /// <summary>Per-role-queue breakdown — max 20 entries, sorted by backlog desc.</summary>
    public List<RoleQueueBacklogDto> RoleQueueBreakdown { get; init; } = new();

    /// <summary>Per-org-queue breakdown — max 20 entries, sorted by backlog desc.</summary>
    public List<OrgQueueBacklogDto> OrgQueueBreakdown { get; init; } = new();

    /// <summary>UTC snapshot timestamp.</summary>
    public DateTime AsOf { get; init; }
}

/// <summary>E19 — backlog count for one role queue.</summary>
public sealed record RoleQueueBacklogDto
{
    public string Role         { get; init; } = string.Empty;
    public int    OpenCount    { get; init; }
    public int    InProgressCount { get; init; }
    public int    TotalCount   { get; init; }
    public int    OverdueCount { get; init; }
}

/// <summary>E19 — backlog count for one org queue.</summary>
public sealed record OrgQueueBacklogDto
{
    public string OrgId        { get; init; } = string.Empty;
    public int    OpenCount    { get; init; }
    public int    InProgressCount { get; init; }
    public int    TotalCount   { get; init; }
    public int    OverdueCount { get; init; }
}

// ── Workflow Throughput Analytics ────────────────────────────────────────────

/// <summary>
/// E19 — workflow throughput analytics for a time window.
///
/// Source-of-truth:
///   Started   → WorkflowInstance.CreatedAt within window.
///   Completed → WorkflowInstance.CompletedAt within window + Status = Completed.
///   Cancelled → WorkflowInstance.CompletedAt (or UpdatedAt) within window + Status = Cancelled.
///   Active    → WorkflowInstance.Status = Active (current state, no window).
///   CycleTime → WorkflowInstance.CompletedAt − WorkflowInstance.CreatedAt (when both exist).
/// </summary>
public sealed record WorkflowThroughputDto
{
    /// <summary>Instances with CreatedAt within the window.</summary>
    public int StartedInWindow { get; init; }

    /// <summary>Instances with CompletedAt within the window and Status = Completed.</summary>
    public int CompletedInWindow { get; init; }

    /// <summary>Instances with Status = Cancelled and UpdatedAt within the window.</summary>
    public int CancelledInWindow { get; init; }

    /// <summary>Instances with Status = Failed and UpdatedAt within the window.</summary>
    public int FailedInWindow { get; init; }

    /// <summary>Current count of active (Status = Active) instances regardless of window.</summary>
    public int CurrentlyActiveCount { get; init; }

    /// <summary>
    /// Average cycle time in hours for instances completed in the window.
    /// Calculated as (CompletedAt − CreatedAt). Null if no completions.
    /// </summary>
    public double? AvgCycleTimeHours { get; init; }

    /// <summary>
    /// Median cycle time in hours for instances completed in the window.
    /// Null if fewer than 2 completions.
    /// </summary>
    public double? MedianCycleTimeHours { get; init; }

    /// <summary>Breakdown by product key — top 10 by started count.</summary>
    public List<WorkflowProductBreakdownDto> ByProduct { get; init; } = new();

    /// <summary>UTC start of the reporting window.</summary>
    public DateTime WindowStart { get; init; }

    /// <summary>UTC end of the reporting window.</summary>
    public DateTime WindowEnd { get; init; }

    public string WindowLabel { get; init; } = string.Empty;
}

/// <summary>E19 — per-product-key throughput counts.</summary>
public sealed record WorkflowProductBreakdownDto
{
    public string ProductKey     { get; init; } = string.Empty;
    public int    StartedCount   { get; init; }
    public int    CompletedCount { get; init; }
    public int    ActiveCount    { get; init; }
}

// ── Assignment / Intelligence Analytics ─────────────────────────────────────

/// <summary>
/// E19 — assignment and workload fairness analytics.
///
/// Source-of-truth:
///   DirectUserCount     → WorkflowTask.AssignmentMode = DirectUser (current state).
///   RoleQueueCount      → AssignmentMode = RoleQueue.
///   OrgQueueCount       → AssignmentMode = OrgQueue.
///   UnassignedCount     → AssignmentMode = Unassigned.
///   AssignedInWindow    → WorkflowTask.AssignedAt within window.
///   TopAssignees        → GROUP BY AssignedUserId for active tasks.
///
/// NOTE: Claim/reassign/auto-assign volumes require the audit layer to
/// be fully instrumented with typed events per assignment trigger. In E19,
/// these are approximated from WorkflowTask fields only (see AssumptionNote).
/// </summary>
public sealed record AssignmentSummaryDto
{
    /// <summary>Tasks currently in DirectUser assignment mode (any status).</summary>
    public int DirectUserCount { get; init; }

    /// <summary>Tasks currently in RoleQueue assignment mode (any status).</summary>
    public int RoleQueueCount { get; init; }

    /// <summary>Tasks currently in OrgQueue assignment mode (any status).</summary>
    public int OrgQueueCount { get; init; }

    /// <summary>Tasks currently Unassigned (any status).</summary>
    public int UnassignedCount { get; init; }

    /// <summary>Tasks where AssignedAt falls within the reporting window.</summary>
    public int AssignedInWindow { get; init; }

    /// <summary>
    /// Top users by active task count — workload fairness view.
    /// Max 20 entries, sorted by active task count descending.
    /// </summary>
    public List<UserWorkloadDto> TopAssigneesByActiveLoad { get; init; } = new();

    /// <summary>
    /// Documented assumption: auto-assign, claim, and reassign volumes cannot
    /// be distinguished from WorkflowTask fields alone in E19. AssignmentReason
    /// is a free-text field. Full split requires typed audit events.
    /// </summary>
    public string AssumptionNote { get; init; } =
        "Claim/reassign/auto-assign volumes are not individually distinguishable " +
        "from WorkflowTask fields in E19. AssignedAt counts all assignment transitions. " +
        "Typed volumes require future audit-event instrumentation.";

    public DateTime WindowStart { get; init; }
    public DateTime WindowEnd   { get; init; }
    public string   WindowLabel { get; init; } = string.Empty;
}

/// <summary>E19 — one user's active task workload for fairness view.</summary>
public sealed record UserWorkloadDto
{
    public string UserId          { get; init; } = string.Empty;
    public int    ActiveTaskCount { get; init; }
    public int    OpenCount       { get; init; }
    public int    InProgressCount { get; init; }
}

// ── Operations / Outbox Analytics ────────────────────────────────────────────

/// <summary>
/// E19 — outbox health analytics, building on the E17 summary.
///
/// Source-of-truth: OutboxMessage.Status, OutboxMessage.EventType,
/// OutboxMessage.CreatedAt, OutboxMessage.AttemptCount.
///
/// This DTO extends the AdminOutboxSummary (E17) with trend and event-type
/// breakdown that were not in the E17 summary card.
/// </summary>
public sealed record OutboxAnalyticsSummaryDto
{
    // ── Current State ───────────────────────────────────────────────────
    public int PendingCount      { get; init; }
    public int ProcessingCount   { get; init; }
    public int FailedCount       { get; init; }
    public int DeadLetteredCount { get; init; }
    public int SucceededCount    { get; init; }

    /// <summary>Total currently not-yet-succeeded (Pending + Processing + Failed + DeadLettered).</summary>
    public int UnhealthyCount { get; init; }

    // ── Window-Scoped ───────────────────────────────────────────────────

    /// <summary>Messages created within the window.</summary>
    public int CreatedInWindow { get; init; }

    /// <summary>Messages that reached DeadLettered within the window (ProcessedAt in window + Status = DeadLettered).</summary>
    public int DeadLetteredInWindow { get; init; }

    /// <summary>Messages that reached Failed within the window.</summary>
    public int FailedInWindow { get; init; }

    /// <summary>Messages that reached Succeeded within the window.</summary>
    public int SucceededInWindow { get; init; }

    // ── Breakdown ───────────────────────────────────────────────────────

    /// <summary>Failed + DeadLettered breakdown by event type — max 20 entries.</summary>
    public List<OutboxEventTypeBreakdownDto> FailedByEventType { get; init; } = new();

    public DateTime WindowStart { get; init; }
    public DateTime WindowEnd   { get; init; }
    public string   WindowLabel { get; init; } = string.Empty;
    public DateTime AsOf        { get; init; }
}

/// <summary>E19 — outbox failure count per event type.</summary>
public sealed record OutboxEventTypeBreakdownDto
{
    public string EventType      { get; init; } = string.Empty;
    public int    FailedCount    { get; init; }
    public int    DeadLettered   { get; init; }
    public int    TotalUnhealthy { get; init; }
}

// ── Platform-Scoped Cross-Tenant ─────────────────────────────────────────────

/// <summary>
/// E19 — cross-tenant analytics summary for platform admins.
/// Each property represents platform-wide aggregations or tenant-ranked views.
/// </summary>
public sealed record PlatformAnalyticsSummaryDto
{
    /// <summary>Total active workflow instances across all tenants.</summary>
    public int TotalActiveWorkflows { get; init; }

    /// <summary>Total active tasks (Open + InProgress) across all tenants.</summary>
    public int TotalActiveTasks { get; init; }

    /// <summary>Total overdue tasks across all tenants.</summary>
    public int TotalOverdueTasks { get; init; }

    /// <summary>Total dead-lettered outbox messages across all tenants.</summary>
    public int TotalDeadLettered { get; init; }

    /// <summary>Total failed outbox messages across all tenants.</summary>
    public int TotalFailedOutbox { get; init; }

    /// <summary>Top tenants by overdue task count — max 10.</summary>
    public List<TenantOverdueRankDto> TopTenantsByOverdue { get; init; } = new();

    /// <summary>Top tenants by active workflow count — max 10.</summary>
    public List<TenantWorkflowRankDto> TopTenantsByActiveWorkflows { get; init; } = new();

    /// <summary>Outbox health by tenant — max 20 tenants.</summary>
    public List<TenantOutboxHealthDto> OutboxHealthByTenant { get; init; } = new();

    public DateTime AsOf        { get; init; }
    public string   WindowLabel { get; init; } = string.Empty;
}

/// <summary>E19 — one tenant's overdue count for the platform ranking.</summary>
public sealed record TenantOverdueRankDto
{
    public string TenantId     { get; init; } = string.Empty;
    public int    OverdueCount { get; init; }
    public double OverdueRate  { get; init; }
}

/// <summary>E19 — one tenant's active workflow count for the platform ranking.</summary>
public sealed record TenantWorkflowRankDto
{
    public string TenantId    { get; init; } = string.Empty;
    public int    ActiveCount { get; init; }
}

/// <summary>E19 — one tenant's outbox health metrics for the platform view.</summary>
public sealed record TenantOutboxHealthDto
{
    public string TenantId      { get; init; } = string.Empty;
    public int    FailedCount   { get; init; }
    public int    DeadLettered  { get; init; }
}

// ── Unified Summary Card ─────────────────────────────────────────────────────

/// <summary>
/// E19 — top-level dashboard summary combining key metrics from all domains.
/// Returned by GET /api/v1/admin/analytics/summary.
/// </summary>
public sealed record AnalyticsDashboardSummaryDto
{
    public SlaSummaryDto            Sla        { get; init; } = new();
    public QueueSummaryDto          Queue      { get; init; } = new();
    public WorkflowThroughputDto    Workflows  { get; init; } = new();
    public AssignmentSummaryDto     Assignment { get; init; } = new();
    public OutboxAnalyticsSummaryDto Outbox    { get; init; } = new();

    public DateTime GeneratedAt  { get; init; }
    public string   WindowLabel  { get; init; } = string.Empty;
}
