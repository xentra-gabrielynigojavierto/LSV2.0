using BuildingBlocks.Authorization;
using BuildingBlocks.FlowClient;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// LS-FLOW-MERGE-P4 — CareConnect-side bridge to Flow workflows. Routes are
/// scoped to a referral and delegate to the shared <see cref="IFlowClient"/>.
///
/// <para>
/// Routes:
/// <list type="bullet">
///   <item><c>POST /api/referrals/{id}/workflows</c> — start a workflow.</item>
///   <item><c>GET  /api/referrals/{id}/workflows</c> — list workflows for the referral.</item>
/// </list>
/// </para>
/// </summary>
public static class WorkflowEndpoints
{
    private const string ProductSlug = "careconnect";
    private const string SourceEntityType = "referral";

    public sealed class StartWorkflowBody
    {
        public Guid WorkflowDefinitionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CorrelationKey { get; set; }
        public string? AssignedToUserId { get; set; }
        public string? AssignedToRoleKey { get; set; }
        public string? AssignedToOrgId { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public static void MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/referrals/{id:guid}/workflows")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .WithTags("Workflows");

        group.MapPost("/", async (
            Guid id,
            [FromBody] StartWorkflowBody body,
            IFlowClient flow,
            CancellationToken ct) =>
        {
            if (body.WorkflowDefinitionId == Guid.Empty || string.IsNullOrWhiteSpace(body.Title))
            {
                return Results.BadRequest(new { error = "WorkflowDefinitionId and Title are required." });
            }

            try
            {
                var result = await flow.StartWorkflowAsync(ProductSlug, new StartProductWorkflowRequest
                {
                    SourceEntityType = SourceEntityType,
                    SourceEntityId = id.ToString(),
                    WorkflowDefinitionId = body.WorkflowDefinitionId,
                    Title = body.Title,
                    Description = body.Description,
                    CorrelationKey = body.CorrelationKey,
                    AssignedToUserId = body.AssignedToUserId,
                    AssignedToRoleKey = body.AssignedToRoleKey,
                    AssignedToOrgId = body.AssignedToOrgId,
                    DueDate = body.DueDate
                }, ct);

                return Results.Created($"/api/referrals/{id}/workflows/{result.Id}", result);
            }
            catch (Exception ex) when (ex is FlowClientUnavailableException or HttpRequestException)
            {
                return FlowEndpointResults.MapFailure(ex);
            }
        });

        group.MapGet("/", async (
            Guid id,
            IFlowClient flow,
            CancellationToken ct) =>
        {
            try
            {
                var rows = await flow.ListBySourceEntityAsync(ProductSlug, SourceEntityType, id.ToString(), ct);
                return Results.Ok(rows);
            }
            catch (Exception ex) when (ex is FlowClientUnavailableException or HttpRequestException)
            {
                return FlowEndpointResults.MapFailure(ex);
            }
        });

        // LS-FLOW-MERGE-P5 — execution passthrough (get/advance/complete).
        group.MapFlowExecutionPassthrough(ProductSlug, SourceEntityType);
    }
}
