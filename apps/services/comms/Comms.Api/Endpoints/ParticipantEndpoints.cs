using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Domain;

namespace Comms.Api.Endpoints;

public static class ParticipantEndpoints
{
    public static void MapParticipantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/comms/conversations/{conversationId:guid}/participants")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode);

        group.MapGet("/", ListByConversation)
            .RequirePermission(CommsPermissions.ParticipantRead);

        group.MapPost("/", AddParticipant)
            .RequirePermission(CommsPermissions.ParticipantManage);

        group.MapDelete("/{participantId:guid}", DeactivateParticipant)
            .RequirePermission(CommsPermissions.ParticipantManage);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static Guid RequireOrgId(ICurrentRequestContext ctx) =>
        ctx.OrgId ?? throw new UnauthorizedAccessException("Organization context is required.");

    private static async Task<IResult> ListByConversation(
        Guid conversationId,
        IParticipantService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await service.ListByConversationAsync(tenantId, conversationId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AddParticipant(
        Guid conversationId,
        AddParticipantRequest request,
        IParticipantService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.AddAsync(tenantId, orgId, userId, conversationId, request, ct);
        return Results.Created($"/api/comms/conversations/{conversationId}/participants/{result.Id}", result);
    }

    private static async Task<IResult> DeactivateParticipant(
        Guid conversationId,
        Guid participantId,
        IParticipantService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        await service.DeactivateAsync(tenantId, conversationId, participantId, userId, ct);
        return Results.NoContent();
    }
}
