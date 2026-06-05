using Flow.Application.Interfaces;

namespace Flow.Application.DTOs;

// ── E18 — Work Distribution Intelligence DTOs ──────────────────────────────
//
// All types in this file are additive. No existing DTO is modified.

/// <summary>
/// LS-FLOW-E18 — input query/body for
/// <c>GET /api/v1/workflow-tasks/{id}/recommend-assignee</c>.
///
/// <para>
/// <c>CandidateUserIds</c> is optional. When omitted the recommendation
/// engine derives candidates from workload history within the tenant:
/// RoleQueue → users who hold/held the task's role (by task association);
/// OrgQueue → users in the task's org. This is documented clearly in the
/// <see cref="RecommendAssigneeResult.CandidateSource"/> field.
/// </para>
/// </summary>
public sealed record RecommendAssigneeQuery
{
    /// <summary>
    /// Optional explicit candidate list. When supplied, only these
    /// user IDs are evaluated; workload-history derivation is skipped.
    /// </summary>
    public IReadOnlyList<string>? CandidateUserIds { get; init; }
}

/// <summary>
/// LS-FLOW-E18 — ranked candidate info returned per user by the
/// recommendation engine.
/// </summary>
public sealed record AssigneeCandidateInfo
{
    /// <summary>The candidate's user id.</summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Current active task count (Status = Open or InProgress,
    /// AssignedUserId = this user, within the caller's tenant).
    /// </summary>
    public int ActiveTaskCount { get; init; }

    /// <summary>
    /// True when <see cref="ActiveTaskCount"/> is strictly below
    /// the configured <c>SoftCapacityThreshold</c>.
    /// </summary>
    public bool IsWithinSoftThreshold { get; init; }

    /// <summary>
    /// True when <see cref="ActiveTaskCount"/> is strictly below
    /// the configured <c>MaxActiveTasksPerUser</c>.
    /// </summary>
    public bool IsWithinHardCap { get; init; }

    /// <summary>
    /// 1-based rank among all candidates (lower = better).
    /// Determined by capacity bucket then active count then UserId.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Human-readable note explaining this candidate's rank.
    /// Examples:
    /// "Below soft threshold (12/15 tasks) — lowest load in eligible set."
    /// "At or above soft threshold (17/15 tasks) but within hard cap."
    /// "Overloaded (22/20 tasks) — no other candidates available."
    /// </summary>
    public required string ExplanationNote { get; init; }
}

/// <summary>
/// LS-FLOW-E18 — full result returned by
/// <c>GET /api/v1/workflow-tasks/{id}/recommend-assignee</c>.
/// </summary>
public sealed record RecommendAssigneeResult
{
    /// <summary>The task that was evaluated.</summary>
    public Guid TaskId { get; init; }

    /// <summary>
    /// The recommended user id, or <c>null</c> if no recommendation
    /// can be made (no candidates, or feature disabled).
    /// </summary>
    public string? RecommendedUserId { get; init; }

    /// <summary>
    /// Human-readable summary of why this user was recommended (or
    /// why no recommendation was made). Mandatory — always set.
    /// Examples:
    /// "Recommended because user alice@example.com is eligible, below soft
    ///  capacity (8 active tasks), and has the lowest load in the eligible set.
    ///  Task is Overdue (Urgent priority) — high urgency considered."
    /// "No recommendation: no candidates found. Task is a RoleQueue task for
    ///  role 'LienManager' but no workload-history candidates exist in this
    ///  tenant. Supply candidateUserIds explicitly."
    /// </summary>
    public required string ExplanationSummary { get; init; }

    /// <summary>
    /// How candidates were sourced:
    /// "explicit" — caller provided candidateUserIds;
    /// "workload-history-role" — derived from tasks with matching role;
    /// "workload-history-org" — derived from tasks with matching org;
    /// "none" — task mode does not support auto-derivation.
    /// </summary>
    public required string CandidateSource { get; init; }

    /// <summary>SLA urgency of the task at evaluation time.</summary>
    public required string TaskSlaStatus { get; init; }

    /// <summary>Priority of the task at evaluation time.</summary>
    public required string TaskPriority { get; init; }

    /// <summary>
    /// Ranked candidate list. May be empty if no candidates were found.
    /// Always ordered rank-ascending (rank 1 = best).
    /// </summary>
    public IReadOnlyList<AssigneeCandidateInfo> Candidates { get; init; }
        = Array.Empty<AssigneeCandidateInfo>();
}

/// <summary>
/// LS-FLOW-E18 — request body for
/// <c>POST /api/v1/workflow-tasks/{id}/auto-assign</c>.
/// </summary>
public sealed record AutoAssignRequest
{
    /// <summary>
    /// Optional explicit candidate list. Same semantics as
    /// <see cref="RecommendAssigneeQuery.CandidateUserIds"/>.
    /// </summary>
    public IReadOnlyList<string>? CandidateUserIds { get; init; }

    /// <summary>
    /// Reason for the auto-assignment. Required, ≤ 500 chars.
    /// Forwarded verbatim to <c>ReassignAsync</c> and appears in the
    /// audit record alongside the system-generated recommendation explanation.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// LS-FLOW-E18 — response for
/// <c>POST /api/v1/workflow-tasks/{id}/auto-assign</c>.
/// </summary>
public sealed record AutoAssignResult
{
    /// <summary>Full assignment result from the governed assignment path.</summary>
    public required WorkflowTaskAssignmentResult Assignment { get; init; }

    /// <summary>The recommendation that drove the assignment decision.</summary>
    public required RecommendAssigneeResult Recommendation { get; init; }
}

/// <summary>
/// LS-FLOW-E18 — lightweight snapshot used internally by the
/// recommendation service when it needs to read a task's context
/// (assignment mode, role/org key, SLA status, priority).
/// </summary>
internal sealed record TaskRecommendationContext(
    Guid   Id,
    string AssignmentMode,
    string? AssignedRole,
    string? AssignedOrgId,
    string SlaStatus,
    string Priority);
