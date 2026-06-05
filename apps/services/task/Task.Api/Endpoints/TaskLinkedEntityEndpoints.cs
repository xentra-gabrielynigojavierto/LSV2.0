using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Task.Application.DTOs;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

/// <summary>
/// CRUD endpoints for task linked entities.
///
/// GET    /api/tasks/{id}/linked-entities           — list all linked entities on a task
/// POST   /api/tasks/{id}/linked-entities           — add a linked entity
/// DELETE /api/tasks/{id}/linked-entities/{linkId}  — remove a linked entity
/// </summary>
public static class TaskLinkedEntityEndpoints
{
    public static void MapTaskLinkedEntityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks/{id:guid}/linked-entities")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .WithTags("Tasks - Linked Entities");

        group.MapGet("/",                   GetLinkedEntities);
        group.MapPost("/",                  AddLinkedEntity);
        group.MapDelete("/{linkId:guid}",   RemoveLinkedEntity);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async System.Threading.Tasks.Task<IResult> GetLinkedEntities(
        Guid                   id,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await taskService.GetLinkedEntitiesAsync(tenantId, id, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> AddLinkedEntity(
        Guid                   id,
        AddLinkedEntityRequest request,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.AddLinkedEntityAsync(tenantId, id, userId, request, ct);
        return Results.Created($"/api/tasks/{id}/linked-entities/{result.Id}", result);
    }

    private static async System.Threading.Tasks.Task<IResult> RemoveLinkedEntity(
        Guid                   id,
        Guid                   linkId,
        ITaskService           taskService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        await taskService.RemoveLinkedEntityAsync(tenantId, id, linkId, userId, ct);
        return Results.NoContent();
    }
}
