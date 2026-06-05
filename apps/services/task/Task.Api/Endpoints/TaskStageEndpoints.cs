using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Task.Application.DTOs;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

public static class TaskStageEndpoints
{
    public static void MapTaskStageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks/stages")
            .RequireAuthorization("AuthenticatedUserOrService")
            .WithTags("TaskStages");

        group.MapGet("/",               ListStages);
        group.MapGet("/{id:guid}",      GetStageById);
        group.MapPost("/",              CreateStage).RequireAuthorization(Policies.PlatformOrTenantAdmin);
        group.MapPut("/{id:guid}",      UpdateStage).RequireAuthorization(Policies.PlatformOrTenantAdmin);
        group.MapPost("/from-source",   UpsertFromSource).RequireAuthorization("InternalService");
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async System.Threading.Tasks.Task<IResult> ListStages(
        ITaskStageService      stageService,
        ICurrentRequestContext ctx,
        string?                sourceProductCode = null,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await stageService.ListAsync(tenantId, sourceProductCode, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetStageById(
        Guid                   id,
        ITaskStageService      stageService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await stageService.GetByIdAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> CreateStage(
        CreateTaskStageRequest request,
        ITaskStageService      stageService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await stageService.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/tasks/stages/{result.Id}", result);
    }

    private static async System.Threading.Tasks.Task<IResult> UpdateStage(
        Guid                   id,
        UpdateTaskStageRequest request,
        ITaskStageService      stageService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await stageService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> UpsertFromSource(
        UpsertFromSourceStageRequest request,
        ITaskStageService            stageService,
        ICurrentRequestContext       ctx,
        CancellationToken            ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await stageService.UpsertFromSourceAsync(tenantId, userId, request, ct);
        return Results.Ok(result);
    }
}
