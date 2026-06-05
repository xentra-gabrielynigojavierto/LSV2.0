using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Task.Application.DTOs;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

public static class TaskGovernanceEndpoints
{
    public static void MapTaskGovernanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks/governance")
            .RequireAuthorization("AuthenticatedUserOrService")
            .WithTags("TaskGovernance");

        group.MapGet("/",  GetGovernance);
        group.MapPost("/", UpsertGovernance).RequireAuthorization("InternalService");
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async System.Threading.Tasks.Task<IResult> GetGovernance(
        ITaskGovernanceService governanceService,
        ICurrentRequestContext ctx,
        string?                sourceProductCode = null,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await governanceService.GetAsync(tenantId, sourceProductCode, ct);
        return result is null ? Results.NoContent() : Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> UpsertGovernance(
        UpsertTaskGovernanceRequest request,
        ITaskGovernanceService      governanceService,
        ICurrentRequestContext      ctx,
        CancellationToken           ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await governanceService.UpsertAsync(tenantId, userId, request, ct);
        return Results.Ok(result);
    }
}
