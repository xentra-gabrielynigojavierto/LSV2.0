namespace Flow.Application.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.") { }
}

public class ValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public ValidationException(string error) : base(error)
    {
        Errors = new List<string> { error };
    }

    public ValidationException(IEnumerable<string> errors) : base("One or more validation errors occurred.")
    {
        Errors = errors.ToList();
    }
}

public class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(string from, string to)
        : base($"Invalid state transition from '{from}' to '{to}'.") { }
}

/// <summary>
/// LS-FLOW-MERGE-P5 — raised by the workflow engine when an
/// advance/complete/cancel call cannot be honoured because the instance
/// is not in the expected state, the transition does not exist, or the
/// instance has already reached a terminal status. Mapped to HTTP 409 by
/// the API surface.
/// </summary>
public class InvalidWorkflowTransitionException : Exception
{
    public string Code { get; }

    public InvalidWorkflowTransitionException(string message, string code = "invalid_transition")
        : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// LS-FLOW-E11.4 — raised by
/// <see cref="Interfaces.IWorkflowTaskLifecycleService"/> when an atomic
/// status compare-and-swap fails because the row was modified between
/// the pre-check read and the conditional UPDATE. Callers may safely
/// re-read the task and retry. Mapped to HTTP 409 by the API surface.
/// </summary>
public class WorkflowTaskConcurrencyException : Exception
{
    public Guid TaskId { get; }
    public string ExpectedStatus { get; }

    public WorkflowTaskConcurrencyException(Guid taskId, string expectedStatus)
        : base($"WorkflowTask '{taskId}' status changed concurrently (expected '{expectedStatus}'). " +
               "Re-read the task and retry the transition.")
    {
        TaskId = taskId;
        ExpectedStatus = expectedStatus;
    }
}

/// <summary>
/// LS-FLOW-E14.2 — raised by
/// <see cref="Interfaces.IWorkflowTaskAssignmentService"/> when a
/// claim or reassign request violates a state, target-shape, or
/// reason-required rule. Carries a stable
/// <see cref="Code"/> from
/// <see cref="Domain.Common.WorkflowTaskAssignmentErrorCodes"/> so
/// clients can dispatch on it without parsing the message. Mapped to
/// HTTP <b>422 Unprocessable Entity</b> by
/// <see cref="Api.Middleware.ExceptionHandlingMiddleware"/>.
/// </summary>
public class AssignmentRuleException : Exception
{
    public string Code { get; }

    public AssignmentRuleException(string code, string message)
        : base($"[{code}] {message}")
    {
        Code = code;
    }
}

/// <summary>
/// LS-FLOW-E14.2 — raised by
/// <see cref="Interfaces.IWorkflowTaskAssignmentService"/> when the
/// authenticated caller is not eligible for the requested assignment
/// action (e.g. claiming a role queue without holding the role,
/// claiming an org queue from a different org, reassigning without
/// supervisor authority). Distinct from
/// <see cref="UnauthorizedAccessException"/>: the caller is correctly
/// authenticated, just out-of-scope for this particular task.
/// Carries a stable <see cref="Code"/> (currently always
/// <see cref="Domain.Common.WorkflowTaskAssignmentErrorCodes.ForbiddenAssignmentAction"/>).
/// Mapped to HTTP <b>403 Forbidden</b> by
/// <see cref="Api.Middleware.ExceptionHandlingMiddleware"/>.
/// </summary>
public class AssignmentForbiddenException : Exception
{
    public string Code { get; }

    public AssignmentForbiddenException(string code, string message)
        : base($"[{code}] {message}")
    {
        Code = code;
    }
}
