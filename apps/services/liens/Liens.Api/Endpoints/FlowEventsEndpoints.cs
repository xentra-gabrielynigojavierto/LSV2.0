using Liens.Application.Events;
using Liens.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Liens.Api.Endpoints;

/// <summary>
/// LS-LIENS-FLOW-009 — internal event ingestion endpoint for Flow step-change notifications.
///
/// Protected by a shared secret (<c>FLOW_SERVICE_TOKEN_SECRET</c>) carried in the
/// <c>X-Internal-Service-Token</c> request header. Not a public API; not discoverable
/// through the standard auth/API surface.
///
/// Auth design: the endpoint calls <c>AllowAnonymous()</c> because it uses its own
/// shared-secret scheme rather than the user JWT scheme registered for the service.
/// The token check is enforced inside the handler — the request is rejected before
/// any business logic runs if the header is absent or incorrect.
/// </summary>
public static class FlowEventsEndpoints
{
    private const string InternalTokenHeader = "X-Internal-Service-Token";
    private const string ExpectedEventType   = "workflow.step.changed";
    private const string ExpectedProductCode = "SYNQ_LIENS";

    public static IEndpointRouteBuilder MapFlowEventsEndpoints(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/liens/internal/flow-events", HandleFlowEvent)
            .AllowAnonymous();

        return routes;
    }

    private static async Task<IResult> HandleFlowEvent(
        HttpContext            httpContext,
        FlowStepChangedEvent?  body,
        IFlowEventHandler      handler,
        IConfiguration         config,
        ILoggerFactory         loggerFactory,
        CancellationToken      ct)
    {
        var logger = loggerFactory.CreateLogger("FlowEventsEndpoints");

        // ── Token validation ───────────────────────────────────────────────────────
        var expectedToken = config["FLOW_SERVICE_TOKEN_SECRET"]
                         ?? Environment.GetEnvironmentVariable("FLOW_SERVICE_TOKEN_SECRET");

        var suppliedToken = httpContext.Request.Headers[InternalTokenHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(expectedToken) || string.IsNullOrWhiteSpace(suppliedToken)
            || suppliedToken != expectedToken)
        {
            logger.LogWarning(
                "FlowEventsEndpoints: Rejected request — invalid or missing {Header}.",
                InternalTokenHeader);
            return Results.Unauthorized();
        }

        // ── Payload validation ─────────────────────────────────────────────────────
        if (body is null)
            return Results.BadRequest(new { error = "Request body is required." });

        if (!string.Equals(body.EventType, ExpectedEventType, StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new
            {
                error = $"Unsupported eventType '{body.EventType}'. Expected '{ExpectedEventType}'."
            });

        if (!string.Equals(body.ProductCode, ExpectedProductCode, StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new
            {
                error = $"Unsupported productCode '{body.ProductCode}'. Expected '{ExpectedProductCode}'."
            });

        if (body.TenantId == Guid.Empty)
            return Results.BadRequest(new { error = "tenantId is required." });

        if (body.WorkflowInstanceId == Guid.Empty)
            return Results.BadRequest(new { error = "workflowInstanceId is required." });

        if (string.IsNullOrWhiteSpace(body.CurrentStepKey))
            return Results.BadRequest(new { error = "currentStepKey is required." });

        // ── Dispatch ───────────────────────────────────────────────────────────────
        var result = await handler.HandleStepChangedAsync(body, ct);

        return Results.Ok(new { processed = result.Processed, noOp = result.NoOp });
    }
}
