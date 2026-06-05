using BuildingBlocks.Authorization;
using BuildingBlocks.FlowClient;
using Microsoft.AspNetCore.Mvc;

namespace Liens.Api.Endpoints;

/// <summary>
/// LS-FLOW-MERGE-P4 — SynqLien-side bridge to Flow workflows. Routes are
/// scoped to a lien case and delegate to the shared <see cref="IFlowClient"/>.
///
/// <para>
/// Routes:
/// <list type="bullet">
///   <item><c>POST /api/liens/cases/{id}/workflows</c> — start a workflow.</item>
///   <item><c>GET  /api/liens/cases/{id}/workflows</c> — list workflows for the case.</item>
/// </list>
/// </para>
/// </summary>
public static class WorkflowEndpoints
{
    private const string ProductSlug = "synqlien";
    private const string SourceEntityType = "lien_case";

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
        // E8.1 — slim definitions list for the tenant-portal "Start workflow"
        // modal. Read-only passthrough to Flow; product key is fixed to
        // SynqLien so the endpoint cannot be used to enumerate other products'
        // definitions from this BFF.
        app.MapGet("/api/liens/workflow-definitions", async (
            [FromQuery] string? productKey,
            IFlowClient flow,
            CancellationToken ct) =>
        {
            var key = string.IsNullOrWhiteSpace(productKey) ? ProductSlug : productKey!;
            if (!string.Equals(key, ProductSlug, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = $"productKey must be '{ProductSlug}' on this endpoint." });
            }

            try
            {
                var rows = await flow.ListDefinitionsAsync(ProductSlug, ct);
                return Results.Ok(rows);
            }
            catch (Exception ex) when (ex is FlowClientUnavailableException or HttpRequestException)
            {
                return FlowEndpointResults.MapFailure(ex);
            }
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .WithTags("Workflows");

        var group = app.MapGroup("/api/liens/cases/{id:guid}/workflows")
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

                return Results.Created($"/api/liens/cases/{id}/workflows/{result.Id}", result);
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
