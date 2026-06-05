namespace Flow.Application.Interfaces;

/// <summary>
/// LS-FLOW-E14.2 — single, dedicated entry point for user-driven
/// assignment transitions on a <see cref="Domain.Entities.WorkflowTask"/>.
///
/// <para>
/// Spec rule: every claim / reassign must flow through this service.
/// Controllers and other application services MUST NOT mutate the
/// assignment columns (<c>AssignmentMode</c>, <c>AssignedUserId</c>,
/// <c>AssignedRole</c>, <c>AssignedOrgId</c>, <c>AssignedAt</c>,
/// <c>AssignedBy</c>, <c>AssignmentReason</c>) directly. The
/// engine-driven producer path
/// (<see cref="IWorkflowTaskFromWorkflowFactory"/>) remains the only
/// other writer; it stamps assignment at task creation time and is
/// not in scope here.
/// </para>
///
/// <para>
/// All methods are tenant-scoped via the <c>WorkflowTask</c> global
/// query filter — cross-tenant ids surface as
/// <see cref="Application.Exceptions.NotFoundException"/> ⇒ 404,
/// identical to a missing task (no cross-tenant id leakage).
/// </para>
/// </summary>
public interface IWorkflowTaskAssignmentService
{
    /// <summary>
    /// Self-claim: the authenticated caller becomes the
    /// <c>DirectUser</c> assignee of <paramref name="taskId"/>.
    /// </summary>
    /// <param name="reason">
    /// Optional free-form note. When omitted, the service stamps a
    /// deterministic default ("claimed from queue") so the audit
    /// row is never blank.
    /// </param>
    Task<WorkflowTaskAssignmentResult> ClaimAsync(
        Guid taskId,
        string? reason,
        CancellationToken ct = default);

    /// <summary>
    /// Supervisor-driven reassignment of <paramref name="taskId"/>
    /// to a new direct user, role queue, org queue, or back to
    /// unassigned.
    /// </summary>
    Task<WorkflowTaskAssignmentResult> ReassignAsync(
        Guid taskId,
        ReassignTaskRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// LS-FLOW-E14.2 — request shape for
/// <see cref="IWorkflowTaskAssignmentService.ReassignAsync"/>.
/// Validation rules:
///   <list type="bullet">
///     <item><see cref="TargetMode"/> required, one of
///       <c>DirectUser</c>, <c>RoleQueue</c>, <c>OrgQueue</c>,
///       <c>Unassigned</c>.</item>
///     <item>The (<see cref="AssignedUserId"/>, <see cref="AssignedRole"/>,
///       <see cref="AssignedOrgId"/>) tuple must match
///       <see cref="TargetMode"/> exactly — see
///       <see cref="Domain.Entities.WorkflowTask.EnsureValid"/> for the
///       enforced single-mode rule.</item>
///     <item><see cref="Reason"/> required, non-whitespace, ≤ 500 chars.</item>
///   </list>
/// </summary>
public sealed record ReassignTaskRequest(
    string TargetMode,
    string? AssignedUserId,
    string? AssignedRole,
    string? AssignedOrgId,
    string Reason);

/// <summary>
/// LS-FLOW-E14.2 — projection of the post-write task state returned
/// by both <see cref="IWorkflowTaskAssignmentService.ClaimAsync"/>
/// and <see cref="IWorkflowTaskAssignmentService.ReassignAsync"/>.
/// Lets callers update local UI state without a follow-up GET.
/// </summary>
public sealed record WorkflowTaskAssignmentResult(
    Guid TaskId,
    Guid WorkflowInstanceId,
    string Status,
    string AssignmentMode,
    string? AssignedUserId,
    string? AssignedRole,
    string? AssignedOrgId,
    DateTime? AssignedAt,
    string? AssignedBy,
    string? AssignmentReason,
    DateTime OccurredAtUtc);
