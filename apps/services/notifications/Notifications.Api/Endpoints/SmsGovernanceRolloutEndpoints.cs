using BuildingBlocks.Authorization;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-022: Governance rollout plan management endpoints.
/// All endpoints require PlatformAdmin authorization (Policies.AdminOnly).
/// No raw phone numbers, message content, credentials, or provider payloads are returned.
/// </summary>
public static class SmsGovernanceRolloutEndpoints
{
    public static IEndpointRouteBuilder MapSmsGovernanceRolloutEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin/sms/governance")
            .RequireAuthorization(Policies.AdminOnly);

        // ── GET /v1/admin/sms/governance/rollouts ──────────────────────────────

        group.MapGet("/rollouts", async (
            ISmsGovernanceRolloutService            svc,
            IOptions<SmsGovernanceRolloutsOptions>  opts,
            Guid?   releasePackageId = null,
            Guid?   tenantId         = null,
            string? state            = null,
            string? strategy         = null,
            int     page             = 1,
            int     pageSize         = 50) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            var result = await svc.ListRolloutsAsync(
                new RolloutListQuery(releasePackageId, tenantId, state, strategy, page, pageSize));
            return Results.Ok(result);
        });

        // ── GET /v1/admin/sms/governance/rollouts/{id} ─────────────────────────

        group.MapGet("/rollouts/{id:guid}", async (
            Guid id,
            ISmsGovernanceRolloutService           svc,
            IOptions<SmsGovernanceRolloutsOptions> opts) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            var detail = await svc.GetRolloutAsync(id);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        // ── POST /v1/admin/sms/governance/rollouts ─────────────────────────────

        group.MapPost("/rollouts", async (
            CreateRolloutRequest                   request,
            ISmsGovernanceRolloutService           svc,
            IOptions<SmsGovernanceRolloutsOptions> opts) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            try
            {
                var plan = await svc.CreateRolloutAsync(request);
                return Results.Created($"/v1/admin/sms/governance/rollouts/{plan.Id}", plan);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // ── POST /v1/admin/sms/governance/rollouts/{id}/stages ─────────────────

        group.MapPost("/rollouts/{id:guid}/stages", async (
            Guid id,
            AddRolloutStageRequest                 request,
            ISmsGovernanceRolloutService           svc,
            IOptions<SmsGovernanceRolloutsOptions> opts) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            try
            {
                var stage = await svc.AddStageAsync(id, request);
                return Results.Created($"/v1/admin/sms/governance/rollouts/{id}/stages/{stage.Id}", stage);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // ── POST /v1/admin/sms/governance/rollouts/{id}/cohorts ────────────────

        group.MapPost("/rollouts/{id:guid}/cohorts", async (
            Guid id,
            AddTenantCohortRequest                 request,
            ISmsGovernanceRolloutService           svc,
            IOptions<SmsGovernanceRolloutsOptions> opts) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            try
            {
                var cohort = await svc.AddCohortTenantAsync(id, request);
                return Results.Created($"/v1/admin/sms/governance/rollouts/{id}/cohorts/{cohort.Id}", cohort);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // ── POST /v1/admin/sms/governance/rollouts/{id}/start ─────────────────

        group.MapPost("/rollouts/{id:guid}/start", async (
            Guid id,
            ISmsGovernanceRolloutService           svc,
            IOptions<SmsGovernanceRolloutsOptions> opts,
            HttpContext                             ctx) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            var actor = ctx.User.Identity?.Name ?? "unknown";
            try
            {
                var result = await svc.StartRolloutAsync(id, actor);
                return result.Success ? Results.Ok(result) : Results.BadRequest(result);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        // ── POST /v1/admin/sms/governance/rollouts/{id}/pause ─────────────────

        group.MapPost("/rollouts/{id:guid}/pause", async (
            Guid id,
            PauseRolloutRequest                    request,
            ISmsGovernanceRolloutService           svc,
            IOptions<SmsGovernanceRolloutsOptions> opts,
            HttpContext                             ctx) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            var actor = ctx.User.Identity?.Name ?? "unknown";
            try
            {
                var result = await svc.PauseRolloutAsync(id, actor, request.Reason);
                return result.Success ? Results.Ok(result) : Results.BadRequest(result);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        // ── POST /v1/admin/sms/governance/rollouts/{id}/resume ────────────────

        group.MapPost("/rollouts/{id:guid}/resume", async (
            Guid id,
            ISmsGovernanceRolloutService           svc,
            IOptions<SmsGovernanceRolloutsOptions> opts,
            HttpContext                             ctx) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            var actor = ctx.User.Identity?.Name ?? "unknown";
            try
            {
                var result = await svc.ResumeRolloutAsync(id, actor);
                return result.Success ? Results.Ok(result) : Results.BadRequest(result);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        // ── POST /v1/admin/sms/governance/rollouts/{id}/rollback ──────────────

        group.MapPost("/rollouts/{id:guid}/rollback", async (
            Guid id,
            RollbackRolloutRequest                 request,
            ISmsGovernanceRolloutService           svc,
            IOptions<SmsGovernanceRolloutsOptions> opts,
            HttpContext                             ctx) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            var actor = ctx.User.Identity?.Name ?? "unknown";
            try
            {
                var result = await svc.RollbackRolloutAsync(id, actor, request.Reason);
                return result.Success ? Results.Ok(result) : Results.BadRequest(result);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        // ── POST /v1/admin/sms/governance/rollouts/{id}/advance ───────────────

        group.MapPost("/rollouts/{id:guid}/advance", async (
            Guid id,
            ISmsGovernanceRolloutService           svc,
            IOptions<SmsGovernanceRolloutsOptions> opts,
            HttpContext                             ctx) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            var actor = ctx.User.Identity?.Name ?? "unknown";
            try
            {
                var result = await svc.AdvanceStageAsync(id, actor);
                return result.Success ? Results.Ok(result) : Results.BadRequest(result);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        });

        // ── GET /v1/admin/sms/governance/rollouts/{id}/analytics ──────────────

        group.MapGet("/rollouts/{id:guid}/analytics", async (
            Guid id,
            ISmsGovernanceRolloutAnalyticsService  analytics,
            IOptions<SmsGovernanceRolloutsOptions> opts) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            var result = await analytics.GetRolloutAnalyticsAsync(id);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        // ── GET /v1/admin/sms/governance/rollouts/{id}/audit ──────────────────

        group.MapGet("/rollouts/{id:guid}/audit", async (
            Guid id,
            ISmsGovernanceRolloutService           svc,
            IOptions<SmsGovernanceRolloutsOptions> opts) =>
        {
            if (!opts.Value.Enabled) return Results.StatusCode(503);
            var events = await svc.GetAuditTrailAsync(id);
            return Results.Ok(events);
        });

        return app;
    }
}

// ── Request body models ───────────────────────────────────────────────────────

public record PauseRolloutRequest(string? Reason);
public record RollbackRolloutRequest(string? Reason);
