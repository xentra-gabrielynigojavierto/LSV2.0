using System.Net;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P4 — shared failure mapper for product-side endpoints that
/// proxy to Flow via <see cref="IFlowClient"/>.
///
/// <list type="bullet">
///   <item><see cref="FlowClientUnavailableException"/> → <c>503</c></item>
///   <item><see cref="HttpRequestException"/> with a known
///         <c>StatusCode</c> → that status code (so upstream
///         <c>400/401/403/404/409/422</c> reach the caller intact).</item>
///   <item>Any other <see cref="HttpRequestException"/> → <c>502</c>.</item>
/// </list>
/// </summary>
public static class FlowEndpointResults
{
    public static IResult MapFailure(Exception ex)
    {
        switch (ex)
        {
            case FlowClientUnavailableException fcu:
                return Results.Json(
                    new { error = "Flow service unavailable", code = FlowErrorCodes.FlowUnavailable, detail = fcu.Message },
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            case HttpRequestException httpEx when httpEx.StatusCode is HttpStatusCode upstream:
                var status = (int)upstream;
                if (status >= 500)
                {
                    return Results.Json(
                        new { error = "Flow service error", code = FlowErrorCodes.FlowUpstreamError, upstreamStatus = status, detail = httpEx.Message },
                        statusCode: StatusCodes.Status502BadGateway);
                }
                // 4xx from Flow — propagate the upstream status verbatim. The body
                // already carries Flow's own structured `code` (e.g.
                // workflow_instance_not_owned, expected_step_mismatch); we don't
                // override it here.
                return Results.Json(
                    new { error = "Flow rejected the request", code = FlowErrorCodes.FlowUpstreamError, upstreamStatus = status, detail = httpEx.Message },
                    statusCode: status);

            case HttpRequestException httpEx:
                return Results.Json(
                    new { error = "Flow service error", code = FlowErrorCodes.FlowUpstreamError, detail = httpEx.Message },
                    statusCode: StatusCodes.Status502BadGateway);

            default:
                throw ex;
        }
    }
}
