using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Domain;

namespace Comms.Api.Endpoints;

public static class ConversationEndpoints
{
    public static void MapConversationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/comms/conversations")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode);

        group.MapGet("/", ListByContext)
            .RequirePermission(CommsPermissions.ConversationRead);

        group.MapGet("/{id:guid}", GetById)
            .RequirePermission(CommsPermissions.ConversationRead);

        group.MapGet("/{id:guid}/thread", GetThread)
            .RequirePermission(CommsPermissions.ConversationRead);

        group.MapPost("/", Create)
            .RequirePermission(CommsPermissions.ConversationCreate);

        group.MapPatch("/{id:guid}/status", UpdateStatus)
            .RequirePermission(CommsPermissions.ConversationUpdate);

        group.MapPost("/{id:guid}/read", MarkRead)
            .RequirePermission(CommsPermissions.ConversationRead);

        group.MapPost("/{id:guid}/unread", MarkUnread)
            .RequirePermission(CommsPermissions.ConversationRead);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static Guid RequireOrgId(ICurrentRequestContext ctx) =>
        ctx.OrgId ?? throw new UnauthorizedAccessException("Organization context is required.");

    private static async Task<IResult> ListByContext(
        IConversationService service,
        ICurrentRequestContext ctx,
        string contextType,
        string contextId,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.ListByContextAsync(tenantId, contextType, contextId, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetById(
        Guid id,
        IConversationService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.GetByIdAsync(tenantId, id, userId, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Conversation '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetThread(
        Guid id,
        IConversationService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.GetThreadAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Create(
        CreateConversationRequest request,
        IConversationService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.CreateAsync(tenantId, orgId, userId, request, ct);
        return Results.Created($"/api/comms/conversations/{result.Id}", result);
    }

    private static async Task<IResult> UpdateStatus(
        Guid id,
        UpdateConversationStatusRequest request,
        IConversationService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.UpdateStatusAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> MarkRead(
        Guid id,
        IReadTrackingService readService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await readService.MarkReadAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> MarkUnread(
        Guid id,
        IReadTrackingService readService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await readService.MarkUnreadAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }
}
