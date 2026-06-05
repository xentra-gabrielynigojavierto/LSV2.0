namespace Flow.Domain.Common;

/// <summary>
/// LS-FLOW-E14.2 — stable client-facing error codes for the
/// <c>claim</c> / <c>reassign</c> assignment transition APIs.
///
/// <para>
/// Mirrors the existing string-constant convention in this codebase
/// (e.g. <see cref="WorkflowTaskStatus"/>) so the wire payload values
/// are a single source of truth and grep-able. Every code is included
/// in the <c>Error</c> message of the structured exception that
/// surfaces it (<see cref="Application.Exceptions.AssignmentRuleException"/>
/// and <see cref="Application.Exceptions.AssignmentForbiddenException"/>),
/// so callers can dispatch on substring without parsing free-form
/// text.
/// </para>
///
/// <para>
/// HTTP mapping (handled by <c>ExceptionHandlingMiddleware</c>):
///   <list type="bullet">
///     <item><b>422 Unprocessable Entity</b> — <see cref="TaskNotClaimable"/>,
///       <see cref="TaskNotReassignable"/>, <see cref="AssignmentTargetInvalid"/>,
///       <see cref="AssignmentModeInvalid"/>, <see cref="TaskAlreadyAssigned"/>,
///       <see cref="TaskStateInvalid"/>, <see cref="MissingAssignmentReason"/>.</item>
///     <item><b>403 Forbidden</b> — <see cref="ForbiddenAssignmentAction"/>.</item>
///     <item><b>409 Conflict</b> — <c>concurrent_assignment_change</c> is
///       carried by the existing
///       <see cref="Application.Exceptions.WorkflowTaskConcurrencyException"/>
///       (no new code needed; the concurrency primitive is shared with
///       the lifecycle service).</item>
///   </list>
/// </para>
/// </summary>
public static class WorkflowTaskAssignmentErrorCodes
{
    public const string TaskNotClaimable          = "task_not_claimable";
    public const string TaskNotReassignable       = "task_not_reassignable";
    public const string AssignmentTargetInvalid   = "assignment_target_invalid";
    public const string AssignmentModeInvalid     = "assignment_mode_invalid";
    public const string TaskAlreadyAssigned       = "task_already_assigned";
    public const string TaskStateInvalid          = "task_state_invalid";
    public const string MissingAssignmentReason   = "missing_assignment_reason";
    public const string ForbiddenAssignmentAction = "forbidden_assignment_action";
}
