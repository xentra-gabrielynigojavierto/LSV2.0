using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Domain;

namespace Comms.Api.Endpoints;

public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/comms/queues")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode);

        group.MapPost("/", Create)
            .RequirePermission(CommsPermissions.QueueManage);

        group.MapGet("/", List)
            .RequirePermission(CommsPermissions.QueueRead);

        group.MapGet("/{id:guid}", GetById)
            .RequirePermission(CommsPermissions.QueueRead);

        group.MapPatch("/{id:guid}", Update)
            .RequirePermission(CommsPermissions.QueueManage);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> Create(
        CreateConversationQueueRequest request,
        IQueueService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/comms/queues/{result.Id}", result);
    }

    private static async Task<IResult> List(
        IQueueService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await service.ListAsync(tenantId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetById(
        Guid id,
        IQueueService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await service.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Queue '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateConversationQueueRequest request,
        IQueueService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }
}
