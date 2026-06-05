using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P5 / LS-FLOW-HARDEN-A1 — minimal-API helper that maps
/// the three execution passthrough endpoints onto a product's existing
/// <c>/api/.../{id:guid}/workflows</c> route group:
/// <list type="bullet">
///   <item><c>GET  ./{workflowInstanceId:guid}</c></item>
///   <item><c>POST ./{workflowInstanceId:guid}/advance</c></item>
///   <item><c>POST ./{workflowInstanceId:guid}/complete</c></item>
/// </list>
///
/// <para>
/// <b>HARDEN-A1 change.</b> The original P5 implementation enforced
/// parent-ownership in the product process by calling
/// <c>ListBySourceEntityAsync</c> first and then the execution endpoint
/// — two upstream requests, with a TOCTOU window between them. This
/// helper now forwards directly to Flow's atomic ownership-aware
/// endpoints (<c>/product-workflows/{product}/{sourceEntityType}/{sourceEntityId}/{workflowInstanceId}/...</c>),
/// which validate tenant + product + parent + workflow ownership in a
/// single database read before mutating state. Product services stay
/// thin and orchestration-only.
/// </para>
///
/// <para>Errors funnel through <see cref="FlowEndpointResults.MapFailure"/>:
/// 503 on Flow downtime; 4xx (incl. 404 <c>workflow_instance_not_owned</c>)
/// propagated from the upstream.</para>
/// </summary>
public static class FlowExecutionEndpoints
{
    public sealed class AdvanceWorkflowBody
    {
        public string ExpectedCurrentStepKey { get; set; } = string.Empty;
        public string? ToStepKey { get; set; }
        public Dictionary<string, string>? Payload { get; set; }
    }

    public static RouteGroupBuilder MapFlowExecutionPassthrough(
        this RouteGroupBuilder group,
        string productSlug,
        string sourceEntityType)
    {
        group.MapGet("/{workflowInstanceId:guid}", async (
            Guid id,
            Guid workflowInstanceId,
            IFlowClient flow,
            CancellationToken ct) =>
        {
            try
            {
                var dto = await flow.GetProductWorkflowAsync(
                    productSlug, sourceEntityType, id.ToString(), workflowInstanceId, ct);
                return Results.Ok(dto);
            }
            catch (Exception ex) when (ex is FlowClientUnavailableException or HttpRequestException)
            {
                return FlowEndpointResults.MapFailure(ex);
            }
        });

        group.MapPost("/{workflowInstanceId:guid}/advance", async (
            Guid id,
            Guid workflowInstanceId,
            [FromBody] AdvanceWorkflowBody body,
            IFlowClient flow,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.ExpectedCurrentStepKey))
            {
                return Results.BadRequest(new { error = "ExpectedCurrentStepKey is required." });
            }

            try
            {
                var dto = await flow.AdvanceProductWorkflowAsync(
                    productSlug, sourceEntityType, id.ToString(), workflowInstanceId,
                    new FlowAdvanceWorkflowRequest
                    {
                        ExpectedCurrentStepKey = body.ExpectedCurrentStepKey,
                        ToStepKey = body.ToStepKey,
                        Payload = body.Payload
                    }, ct);
                return Results.Ok(dto);
            }
            catch (Exception ex) when (ex is FlowClientUnavailableException or HttpRequestException)
            {
                return FlowEndpointResults.MapFailure(ex);
            }
        });

        group.MapPost("/{workflowInstanceId:guid}/complete", async (
            Guid id,
            Guid workflowInstanceId,
            IFlowClient flow,
            CancellationToken ct) =>
        {
            try
            {
                var dto = await flow.CompleteProductWorkflowAsync(
                    productSlug, sourceEntityType, id.ToString(), workflowInstanceId, ct);
                return Results.Ok(dto);
            }
            catch (Exception ex) when (ex is FlowClientUnavailableException or HttpRequestException)
            {
                return FlowEndpointResults.MapFailure(ex);
            }
        });

        return group;
    }
}
