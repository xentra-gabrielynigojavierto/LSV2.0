namespace Flow.Application.DTOs;

/// <summary>
/// LS-FLOW-E11.5 — UI-friendly projection of a
/// <see cref="Domain.Entities.WorkflowTask"/> for the operator portal's
/// "My Tasks" surface, widened in **LS-FLOW-E15** to also serve the
/// Role Queue, Org Queue, and task-detail surfaces.
///
/// <para>
/// Originally narrow (E11.5): only the fields needed to render a "task
/// assigned directly to me" row. E15 added the assignment-context
/// fields (<c>AssignmentMode</c>, <c>AssignedRole</c>,
/// <c>AssignedOrgId</c>, <c>AssignedAt</c>, <c>AssignedBy</c>,
/// <c>AssignmentReason</c>) so the same DTO can drive the queue lists
/// and the task-detail drawer without introducing a parallel
/// <c>TaskDetailDto</c>. All new fields are nullable; pre-E15 JSON
/// consumers are unaffected.
/// </para>
///
/// <para>
/// Internal engine fields (<c>MetadataJson</c>, <c>CorrelationKey</c>,
/// audit columns beyond the four lifecycle timestamps) remain
/// excluded so the contract still does not couple the UI to engine
/// internals.
/// </para>
/// </summary>
public sealed record MyTaskDto
{
    public Guid TaskId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string StepKey { get; init; } = string.Empty;

    // ---------------- Assignment shape (LS-FLOW-E15) -----------------
    /// <summary>
    /// Stable string from <see cref="Domain.Common.WorkflowTaskAssignmentMode"/>:
    /// <c>DirectUser</c>, <c>RoleQueue</c>, <c>OrgQueue</c>, or
    /// <c>Unassigned</c>. Always set on rows returned by E15+ queries.
    /// </summary>
    public string AssignmentMode { get; init; } = string.Empty;

    /// <summary>The current direct assignee, when <c>AssignmentMode = DirectUser</c>.</summary>
    public string? AssignedUserId { get; init; }

    /// <summary>The role key that owns this task, when <c>AssignmentMode = RoleQueue</c>.</summary>
    public string? AssignedRole { get; init; }

    /// <summary>The org id that owns this task, when <c>AssignmentMode = OrgQueue</c>.</summary>
    public string? AssignedOrgId { get; init; }

    /// <summary>UTC timestamp of the most recent assignment event. Null for <c>Unassigned</c>.</summary>
    public DateTime? AssignedAt { get; init; }

    /// <summary>Actor (user id) who performed the most recent assignment. Null for <c>Unassigned</c>.</summary>
    public string? AssignedBy { get; init; }

    /// <summary>Free-form note recorded with the most recent assignment. Null when none was supplied.</summary>
    public string? AssignmentReason { get; init; }

    // ---------------- Lifecycle timestamps -----------------
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CancelledAt { get; init; }

    // ---------------- SLA / Timer (LS-FLOW-E10.3 task slice) -----------------
    /// <summary>
    /// UTC deadline for this task. Null when no SLA applied at
    /// creation (legacy rows or disabled SLA).
    /// </summary>
    public DateTime? DueAt { get; init; }

    /// <summary>
    /// One of <see cref="Domain.Common.WorkflowSlaStatus"/>: OnTrack,
    /// DueSoon (= "At Risk" in the UI), or Overdue. Defaults to
    /// OnTrack on rows the evaluator has not yet visited.
    /// </summary>
    public string SlaStatus { get; init; } = Domain.Common.WorkflowSlaStatus.OnTrack;

    /// <summary>First-observation breach timestamp; null until the task is observed Overdue.</summary>
    public DateTime? SlaBreachedAt { get; init; }

    // ---------------- Minimal workflow context -----------------
    /// <summary>Owning workflow instance — opaque to the UI, useful as a deep-link target.</summary>
    public Guid WorkflowInstanceId { get; init; }
    /// <summary>Optional human-readable workflow name (FlowDefinition.Name). Null if join missed.</summary>
    public string? WorkflowName { get; init; }
    /// <summary>Optional product key the instance belongs to. Null if join missed.</summary>
    public string? ProductKey { get; init; }
}

/// <summary>
/// LS-FLOW-E11.5 — server-side query parameters for the My Tasks
/// endpoint. Populated from the controller's query string. Intentionally
/// narrow: no arbitrary user / role / org / context filters — the surface
/// is hard-scoped to the calling user.
/// </summary>
public sealed record MyTasksQuery
{
    /// <summary>Optional status filter; multiple values may be passed (<c>?status=Open&amp;status=InProgress</c>).</summary>
    public IReadOnlyList<string>? Status { get; init; }

    /// <summary>1-based page index. Defaults to 1; values &lt; 1 are normalised to 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Page size. Defaults to 25; clamped to <see cref="MyTasksDefaults.MaxPageSize"/>.</summary>
    public int PageSize { get; init; } = MyTasksDefaults.DefaultPageSize;
}

/// <summary>
/// LS-FLOW-E15 — server-side query parameters for the Role Queue
/// surface. The set of eligible roles is **never** taken from the
/// caller; it is always derived server-side from
/// <see cref="Domain.Interfaces.IFlowUserContext.Roles"/>. The query
/// only carries pagination so cross-role injection is impossible by
/// API shape.
/// </summary>
public sealed record RoleQueueQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = MyTasksDefaults.DefaultPageSize;
}

/// <summary>
/// LS-FLOW-E15 — server-side query parameters for the Org Queue
/// surface. Same rationale as <see cref="RoleQueueQuery"/>: the
/// caller's org id is server-derived and never accepted from the
/// request.
/// </summary>
public sealed record OrgQueueQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = MyTasksDefaults.DefaultPageSize;
}

/// <summary>
/// LS-FLOW-E11.5 — pagination + safety constants for the My Tasks
/// endpoint. Centralised so the controller, service, and report can
/// reference identical values.
/// </summary>
public static class MyTasksDefaults
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
}
