using System.Net;
using System.Text.Json;
using Flow.Application.Exceptions;

namespace Flow.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            NotFoundException notFound => (
                HttpStatusCode.NotFound,
                new ErrorResponse { Error = notFound.Message }
            ),
            ValidationException validation => (
                HttpStatusCode.BadRequest,
                new ErrorResponse { Error = "Validation failed.", Errors = validation.Errors }
            ),
            InvalidStateTransitionException transition => (
                HttpStatusCode.UnprocessableEntity,
                new ErrorResponse { Error = transition.Message }
            ),
            // LS-FLOW-E11.4 — task lifecycle CAS race. Map to 409 so
            // callers can safely re-read and retry; aligns with the
            // standard "concurrent modification" REST convention and is
            // the contract the lifecycle service documents.
            WorkflowTaskConcurrencyException concurrency => (
                HttpStatusCode.Conflict,
                new ErrorResponse { Error = concurrency.Message }
            ),
            // LS-FLOW-E11.7 — workflow engine refused the requested
            // transition (stale step, instance not active, ambiguous
            // outbound transition, no matching transition, …). Map to
            // 409 to match the existing per-controller behaviour in
            // WorkflowInstancesController / ProductWorkflowExecutionController
            // so the new task-completion ↔ workflow-advance surface
            // returns the same code as direct workflow-instance calls.
            // The exception's `Code` field is preserved in the message
            // for client-side disambiguation.
            InvalidWorkflowTransitionException workflowTransition => (
                HttpStatusCode.Conflict,
                new ErrorResponse { Error = workflowTransition.Message }
            ),
            // LS-FLOW-E14.2 — claim / reassign rule violation
            // (state, target shape, missing reason, mode mismatch,
            // already-assigned, …). Mapped to 422 so clients can
            // distinguish a semantically-invalid request from a
            // racing-write 409. The exception's stable `Code` is
            // already embedded in the message for client dispatch.
            AssignmentRuleException assignmentRule => (
                HttpStatusCode.UnprocessableEntity,
                new ErrorResponse { Error = assignmentRule.Message }
            ),
            // LS-FLOW-E14.2 — caller is authenticated but ineligible
            // for the requested assignment action (queue membership,
            // supervisor role, …). Mapped to 403 to keep cross-tenant
            // id-probing impossible: out-of-scope task ids that the
            // tenant filter would already hide return 404 instead.
            AssignmentForbiddenException assignmentForbidden => (
                HttpStatusCode.Forbidden,
                new ErrorResponse { Error = assignmentForbidden.Message }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse { Error = "An unexpected error occurred." }
            )
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public IReadOnlyList<string>? Errors { get; set; }
}
