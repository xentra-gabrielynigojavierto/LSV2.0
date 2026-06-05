using BuildingBlocks.Authorization;
using Notifications.Api.Authorization;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-CORE-008 — Platform-admin cross-tenant notification endpoints.
/// All routes require the <c>PlatformAdmin</c> role (enforced via JWT policy).
/// When <c>tenantId</c> query parameter is omitted, queries span all tenants.
/// </summary>
public static class AdminNotificationEndpoints
{
    public static void MapAdminNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin/notifications")
                       .WithTags("Admin — Notifications")
                       .RequireAuthorization(Policies.AdminOnly);

        // ── GET /v1/admin/notifications/stats ─────────────────────────────────
        // Must be before /{id:guid} to avoid routing ambiguity
        group.MapGet("/stats", async (
            HttpContext context,
            INotificationService service,
            string? tenantId,
            DateTime? from,
            DateTime? to,
            string? channel,
            string? status,
            string? provider,
            string? productKey) =>
        {
            var actorUserId  = context.GetUserContext().UserId;
            var tenantFilter = ParseTenantId(tenantId);

            var query = new NotificationStatsQuery
            {
                From       = from,
                To         = to,
                Channel    = channel,
                Status     = status,
                Provider   = provider,
                ProductKey = productKey,
            };

            var result = await service.AdminGetStatsAsync(tenantFilter, query, actorUserId);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/notifications/{id} ──────────────────────────────────
        group.MapGet("/{id:guid}", async (
            HttpContext context,
            INotificationService service,
            Guid id) =>
        {
            var actorUserId = context.GetUserContext().UserId;
            var result      = await service.AdminGetByIdAsync(id, actorUserId);
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        // ── GET /v1/admin/notifications ───────────────────────────────────────
        group.MapGet("/", async (
            HttpContext context,
            INotificationService service,
            string? tenantId,
            int? page,
            int? pageSize,
            string? status,
            string? channel,
            string? provider,
            string? recipient,
            string? productKey,
            DateTime? from,
            DateTime? to,
            string? sortBy,
            string? sortDirection) =>
        {
            var actorUserId  = context.GetUserContext().UserId;
            var tenantFilter = ParseTenantId(tenantId);

            var query = new NotificationListQuery
            {
                Page          = page ?? 1,
                PageSize      = pageSize ?? 50,
                Status        = status,
                Channel       = channel,
                Provider      = provider,
                Recipient     = recipient,
                ProductKey    = productKey,
                From          = from,
                To            = to,
                SortBy        = sortBy,
                SortDirection = sortDirection,
            };

            var result = await service.AdminListPagedAsync(tenantFilter, query, actorUserId);
            return Results.Ok(result);
        });

        // ── GET /v1/admin/notifications/{id}/events ───────────────────────────
        group.MapGet("/{id:guid}/events", async (
            HttpContext context,
            INotificationService service,
            Guid id) =>
        {
            var actorUserId = context.GetUserContext().UserId;
            var result      = await service.AdminGetEventsAsync(id, actorUserId);
            // AdminGetEventsAsync always synthesizes at least a "created" event when
            // the notification exists. Empty list means notification not found.
            if (result.Count == 0) return Results.NotFound();
            return Results.Ok(result);
        });

        // ── GET /v1/admin/notifications/{id}/issues ───────────────────────────
        group.MapGet("/{id:guid}/issues", async (
            HttpContext context,
            INotificationService service,
            Guid id) =>
        {
            var actorUserId = context.GetUserContext().UserId;
            var result      = await service.AdminGetIssuesAsync(id, actorUserId);
            return Results.Ok(result);
        });

        // ── POST /v1/admin/notifications/{id}/retry ───────────────────────────
        group.MapPost("/{id:guid}/retry", async (
            HttpContext context,
            INotificationService service,
            Guid id) =>
        {
            var actorUserId = context.GetUserContext().UserId;
            var result      = await service.AdminRetryAsync(id, actorUserId);
            if (result == null) return Results.NotFound();

            if (result.FailureCategory == "not_retryable")
                return Results.Json(result, statusCode: 422);

            return Results.Ok(result);
        });

        // ── POST /v1/admin/notifications/{id}/resend ──────────────────────────
        group.MapPost("/{id:guid}/resend", async (
            HttpContext context,
            INotificationService service,
            Guid id) =>
        {
            var actorUserId = context.GetUserContext().UserId;
            var result      = await service.AdminResendAsync(id, actorUserId);
            if (result == null) return Results.NotFound();
            return Results.Created($"/v1/notifications/{result.NewNotificationId}", result);
        });
    }

    private static Guid? ParseTenantId(string? raw)
        => !string.IsNullOrEmpty(raw) && Guid.TryParse(raw, out var id) ? id : null;
}
