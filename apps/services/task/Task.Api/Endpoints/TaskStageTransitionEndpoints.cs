using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Task.Application.DTOs;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

/// <summary>
/// TASK-MIG-04 — Task-board stage transition endpoints.
/// Consumed by the Liens service (and any other product source) during migration and runtime.
/// </summary>
public static class TaskStageTransitionEndpoints
{
    public static IEndpointRouteBuilder MapTaskStageTransitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks/stage-transitions")
            .RequireAuthorization("AuthenticatedUserOrService");

        group.MapGet("", GetActiveTransitions)
            .WithName("GetActiveStageTransitions")
            .WithTags("StageTransitions");

        group.MapPost("/from-source", UpsertFromSource)
            .RequireAuthorization("InternalService")
            .WithName("UpsertStageTransitionsFromSource")
            .WithTags("StageTransitions");

        return app;
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> GetActiveTransitions(
        ICurrentRequestContext      ctx,
        ITaskStageTransitionService svc,
        string                      productCode,
        CancellationToken           ct)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await svc.GetActiveTransitionsAsync(tenantId, productCode, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpsertFromSource(
        ICurrentRequestContext             ctx,
        ITaskStageTransitionService        svc,
        UpsertFromSourceTransitionsRequest request,
        CancellationToken                  ct)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        await svc.UpsertFromSourceAsync(tenantId, userId, request, ct);
        return Results.NoContent();
    }
}
