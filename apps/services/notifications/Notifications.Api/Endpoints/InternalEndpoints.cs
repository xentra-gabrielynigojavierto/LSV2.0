using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Services;

namespace Notifications.Api.Endpoints;

public static class InternalEndpoints
{
    public static void MapInternalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal").WithTags("Internal");

        group.MapPost("/send-email", async (InternalEmailService service, InternalSendEmailDto request) =>
        {
            var result = await service.SendAsync(request);
            return result.Success ? Results.Ok(result) : Results.Json(result, statusCode: 502);
        });

        // ─── Membership cache invalidation ──────────────────────────────────
        // Called by identity (fire-and-observe) when it emits a relevant
        // change event — identity.role.assigned, identity.role.removed,
        // identity.user.role.*, identity.group.role.*,
        // identity.group.member.*, identity.membership.changed,
        // identity.user.deactivated. Bumps the per-tenant cache version so
        // the next role/org fan-out reflects the new membership instead of
        // waiting for the configured TTL (default 60 s) to elapse.
        //
        // Authentication is handled by the global InternalTokenMiddleware
        // (X-Internal-Service-Token) — the same gate applied to every
        // /internal route in this service.
        group.MapPost("/membership-cache/invalidate", (
            IRoleMembershipProvider             provider,
            ILogger<InvalidateMembershipLogger> logger,
            MembershipCacheInvalidationDto      body) =>
        {
            if (body is null || body.TenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId is required." });

            provider.InvalidateTenant(body.TenantId);
            logger.LogInformation(
                "Membership cache invalidated for tenant {TenantId} (reason={Reason}, eventType={EventType}).",
                body.TenantId, body.Reason ?? "(none)", body.EventType ?? "(none)");
            return Results.Accepted();
        });

        // ─── Membership cache stats ─────────────────────────────────────────
        // Operator-facing snapshot of cache hits / misses / invalidations.
        // Lets ops verify identity → notifications wiring is healthy in a
        // given environment without grepping logs:
        //   • identityConfigured=false  → wrong/missing IdentityService:BaseUrl.
        //   • hits=misses=0             → no role/org fan-outs have happened yet.
        //   • invalidations=0 with a    → identity isn't reaching this service
        //     non-zero miss count         (token mismatch, network, etc.).
        group.MapGet("/membership-cache/stats", (IMembershipCacheDiagnostics diagnostics) =>
            Results.Ok(diagnostics.GetSnapshot()));
    }

    /// <summary>Marker type used as the logger category for the invalidation endpoint.</summary>
    public sealed class InvalidateMembershipLogger { }
}

/// <summary>
/// Payload accepted by <c>POST /internal/membership-cache/invalidate</c>.
/// <see cref="TenantId"/> is required; other fields are diagnostic only and
/// help operators correlate invalidations with the originating identity event.
/// </summary>
public sealed class MembershipCacheInvalidationDto
{
    public Guid    TenantId  { get; set; }
    public string? EventType { get; set; }
    public string? Reason    { get; set; }
}
