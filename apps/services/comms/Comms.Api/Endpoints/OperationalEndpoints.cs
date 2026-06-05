using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Domain;

namespace Comms.Api.Endpoints;

public static class OperationalEndpoints
{
    public static void MapOperationalEndpoints(this WebApplication app)
    {
        var convGroup = app.MapGroup("/api/comms/conversations")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode);

        convGroup.MapPost("/{id:guid}/assign", Assign)
            .RequirePermission(CommsPermissions.AssignmentManage);

        convGroup.MapPost("/{id:guid}/reassign", Reassign)
            .RequirePermission(CommsPermissions.AssignmentManage);

        convGroup.MapPost("/{id:guid}/unassign", Unassign)
            .RequirePermission(CommsPermissions.AssignmentManage);

        convGroup.MapPost("/{id:guid}/accept", Accept)
            .RequirePermission(CommsPermissions.OperationalRead);

        convGroup.MapPatch("/{id:guid}/priority", UpdatePriority)
            .RequirePermission(CommsPermissions.AssignmentManage);

        convGroup.MapGet("/{id:guid}/operational", GetOperationalSummary)
            .RequirePermission(CommsPermissions.OperationalRead);

        var opsGroup = app.MapGroup("/api/comms/operations")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode);

        opsGroup.MapGet("/", ListOperational)
            .RequirePermission(CommsPermissions.OperationalRead);

        var inboxGroup = app.MapGroup("/api/comms/operational")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode);

        inboxGroup.MapGet("/conversations", QueryConversations)
            .RequirePermission(CommsPermissions.OperationalRead);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> Assign(
        Guid id,
        AssignConversationRequest request,
        IAssignmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.AssignAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Reassign(
        Guid id,
        ReassignConversationRequest request,
        IAssignmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.ReassignAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Unassign(
        Guid id,
        IAssignmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.UnassignAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Accept(
        Guid id,
        IAssignmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.AcceptAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdatePriority(
        Guid id,
        UpdateConversationPriorityRequest request,
        IOperationalService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.UpdatePriorityAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetOperationalSummary(
        Guid id,
        IOperationalService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await service.GetOperationalSummaryAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Conversation '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> ListOperational(
        IOperationalService service,
        ICurrentRequestContext ctx,
        Guid? queueId = null,
        Guid? assignedUserId = null,
        string? assignmentStatus = null,
        string? priority = null,
        bool? breachedFirstResponse = null,
        bool? breachedResolution = null,
        string? conversationStatus = null,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var query = new OperationalListQuery(
            queueId, assignedUserId, assignmentStatus,
            priority, breachedFirstResponse, breachedResolution,
            conversationStatus);
        var result = await service.ListOperationalAsync(tenantId, query, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> QueryConversations(
        IOperationalViewService service,
        ICurrentRequestContext ctx,
        Guid? queueId = null,
        Guid? assignedUserId = null,
        string? assignmentStatus = null,
        string? priority = null,
        string? operationalStatus = null,
        string? waitingState = null,
        bool? breachedFirstResponse = null,
        bool? breachedResolution = null,
        bool? hasWarnings = null,
        Guid? mentionedUserId = null,
        bool? unreadOnly = null,
        DateTime? updatedSince = null,
        DateTime? createdSince = null,
        int page = 1,
        int pageSize = 50,
        string sortBy = "lastActivityAtUtc",
        string sortDirection = "desc",
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        var request = new OperationalQueryRequest(
            queueId, assignedUserId, assignmentStatus,
            priority, operationalStatus, waitingState,
            breachedFirstResponse, breachedResolution, hasWarnings,
            mentionedUserId, unreadOnly,
            updatedSince, createdSince,
            page, pageSize, sortBy, sortDirection);

        var result = await service.QueryConversationsAsync(tenantId, userId, request, ct);
        return Results.Ok(result);
    }
}
