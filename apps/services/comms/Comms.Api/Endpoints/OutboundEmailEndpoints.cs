using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Domain;

namespace Comms.Api.Endpoints;

public static class OutboundEmailEndpoints
{
    public static void MapOutboundEmailEndpoints(this WebApplication app)
    {
        app.MapPost("/api/comms/email/send", SendOutbound)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.EmailSend);

        app.MapPost("/api/comms/internal/delivery-status", ProcessInternalDeliveryStatus)
            .AllowAnonymous();

        app.MapPost("/api/comms/email/delivery-status", ProcessDeliveryStatus)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.EmailDeliveryUpdate);

        app.MapGet("/api/comms/conversations/{conversationId:guid}/email-delivery", ListDeliveryStates)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.ConversationRead);

        app.MapGet("/api/comms/conversations/{conversationId:guid}/reply-all-preview", GetReplyAllPreview)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.ConversationRead);
    }

    private static async Task<IResult> SendOutbound(
        SendOutboundEmailRequest request,
        IOutboundEmailService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var orgId = ctx.OrgId ?? throw new UnauthorizedAccessException("Organization context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.SendOutboundAsync(request, tenantId, orgId, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ProcessInternalDeliveryStatus(
        DeliveryStatusUpdateRequest request,
        IOutboundEmailService service,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        if (httpContext.Items["InternalServiceAuth"] is not true)
            return Results.Unauthorized();

        if (!Guid.TryParse(request.NotificationsRequestId, out _) &&
            string.IsNullOrWhiteSpace(request.ProviderMessageId) &&
            string.IsNullOrWhiteSpace(request.InternetMessageId))
        {
            return Results.BadRequest(new { error = new { code = "bad_request", message = "At least one correlation identifier (ProviderMessageId, InternetMessageId, or NotificationsRequestId) is required." } });
        }

        var tenantIdHeader = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (!Guid.TryParse(tenantIdHeader, out var tenantId))
            return Results.BadRequest(new { error = new { code = "bad_request", message = "X-Tenant-Id header is required." } });

        var matched = await service.ProcessDeliveryStatusAsync(request, tenantId, ct);
        return matched ? Results.Ok(new { status = "updated" }) : Results.NotFound(new { status = "not_matched" });
    }

    private static async Task<IResult> ProcessDeliveryStatus(
        DeliveryStatusUpdateRequest request,
        IOutboundEmailService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var matched = await service.ProcessDeliveryStatusAsync(request, tenantId, ct);
        return matched ? Results.Ok(new { status = "updated" }) : Results.NotFound(new { status = "not_matched" });
    }

    private static async Task<IResult> ListDeliveryStates(
        Guid conversationId,
        IOutboundEmailService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.ListDeliveryStatesAsync(tenantId, conversationId, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetReplyAllPreview(
        Guid conversationId,
        IOutboundEmailService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.GetReplyAllPreviewAsync(tenantId, conversationId, userId, ct);
        return Results.Ok(result);
    }
}
