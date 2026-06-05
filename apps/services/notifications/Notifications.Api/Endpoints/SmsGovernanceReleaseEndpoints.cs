using BuildingBlocks.Authorization;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-021: Governance release package management endpoints.
/// LS-NOTIF-SMS-021-HARDENING: Added /validation, /integrity, /locks endpoints.
/// All endpoints require PlatformAdmin authorization.
/// No raw phone numbers, message content, credentials, or provider payloads are returned.
/// </summary>
public static class SmsGovernanceReleaseEndpoints
{
    public static IEndpointRouteBuilder MapSmsGovernanceReleaseEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin/sms/governance")
            .RequireAuthorization(Policies.AdminOnly);

        // ── Release packages ──────────────────────────────────────────────────

        // GET /v1/admin/sms/governance/releases
        group.MapGet("/releases", async (
            ISmsGovernanceReleaseService svc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            Guid?   tenantId    = null,
            string? state       = null,
            string? releaseType = null,
            int     page        = 1,
            int     pageSize    = 50) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var result = await svc.ListReleasesAsync(
                new ReleaseListQuery(tenantId, state, releaseType, page, pageSize));
            return Results.Ok(result);
        });

        // GET /v1/admin/sms/governance/releases/{id}
        group.MapGet("/releases/{id:guid}", async (
            Guid id,
            ISmsGovernanceReleaseService svc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var detail = await svc.GetReleaseAsync(id);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        // POST /v1/admin/sms/governance/releases
        group.MapPost("/releases", async (
            CreateReleaseRequest request,
            ISmsGovernanceReleaseService svc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            HttpContext ctx) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            try
            {
                var actor   = ctx.User.FindFirst("sub")?.Value ?? "unknown";
                var reqWithActor = request with { RequestedBy = actor };
                var pkg     = await svc.CreateReleaseAsync(reqWithActor);
                return Results.Created($"/v1/admin/sms/governance/releases/{pkg.Id}", pkg);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        // ── Release items ─────────────────────────────────────────────────────

        // POST /v1/admin/sms/governance/releases/{id}/items
        group.MapPost("/releases/{id:guid}/items", async (
            Guid id,
            AddReleaseItemRequest request,
            ISmsGovernanceReleaseService svc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            HttpContext ctx) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            try
            {
                var actor      = ctx.User.FindFirst("sub")?.Value ?? "unknown";
                var reqWithActor = request with { RequestedBy = actor };
                var item       = await svc.AddReleaseItemAsync(id, reqWithActor);
                return Results.Created($"/v1/admin/sms/governance/releases/{id}/items/{item.Id}", item);
            }
            catch (KeyNotFoundException ex)     { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (ArgumentException ex)         { return Results.BadRequest(new { error = ex.Message }); }
        });

        // DELETE /v1/admin/sms/governance/releases/{id}/items/{itemId}
        group.MapDelete("/releases/{id:guid}/items/{itemId:guid}", async (
            Guid id,
            Guid itemId,
            ISmsGovernanceReleaseService svc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            HttpContext ctx) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var actor  = ctx.User.FindFirst("sub")?.Value ?? "unknown";
            try
            {
                var result = await svc.RemoveReleaseItemAsync(id, itemId, actor);
                return result.Success ? Results.NoContent() : Results.BadRequest(new { error = result.ErrorMessage });
            }
            catch (KeyNotFoundException ex)     { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        // ── State transitions ─────────────────────────────────────────────────

        // POST /v1/admin/sms/governance/releases/{id}/submit-review
        group.MapPost("/releases/{id:guid}/submit-review", async (
            Guid id,
            ISmsGovernanceReleaseService svc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            HttpContext ctx) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var actor  = ctx.User.FindFirst("sub")?.Value ?? "unknown";
            try
            {
                var result = await svc.SubmitForReviewAsync(id, actor);
                return result.Success ? Results.Ok(new { message = "Submitted for review." })
                                      : Results.BadRequest(new { error = result.ErrorMessage });
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        });

        // POST /v1/admin/sms/governance/releases/{id}/approve
        group.MapPost("/releases/{id:guid}/approve", async (
            Guid id,
            ApproveReleaseRequest request,
            ISmsGovernanceApprovalWorkflowService approvalSvc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            HttpContext ctx) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var actor   = ctx.User.FindFirst("sub")?.Value ?? "unknown";
            var role    = ctx.User.FindFirst("role")?.Value;
            var reqWithActor = request with { DecidedBy = actor, DecidedByRole = role };
            var result  = await approvalSvc.ApproveAsync(id, reqWithActor);
            return result.Success ? Results.Ok(new { message = "Approval recorded." })
                                  : Results.BadRequest(new { error = result.ErrorMessage });
        });

        // POST /v1/admin/sms/governance/releases/{id}/reject
        group.MapPost("/releases/{id:guid}/reject", async (
            Guid id,
            RejectReleaseRequest request,
            ISmsGovernanceApprovalWorkflowService approvalSvc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            HttpContext ctx) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var actor   = ctx.User.FindFirst("sub")?.Value ?? "unknown";
            var role    = ctx.User.FindFirst("role")?.Value;
            var reqWithActor = request with { DecidedBy = actor, DecidedByRole = role };
            var result  = await approvalSvc.RejectAsync(id, reqWithActor);
            return result.Success ? Results.Ok(new { message = "Release rejected." })
                                  : Results.BadRequest(new { error = result.ErrorMessage });
        });

        // POST /v1/admin/sms/governance/releases/{id}/schedule
        group.MapPost("/releases/{id:guid}/schedule", async (
            Guid id,
            ScheduleReleaseBody body,
            ISmsGovernanceReleaseService svc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            HttpContext ctx) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var actor  = ctx.User.FindFirst("sub")?.Value ?? "unknown";
            try
            {
                var result = await svc.ScheduleActivationAsync(id, body.ActivateAtUtc, actor);
                return result.Success ? Results.Ok(new { message = "Scheduled.", activateAt = body.ActivateAtUtc })
                                      : Results.BadRequest(new { error = result.ErrorMessage });
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        });

        // POST /v1/admin/sms/governance/releases/{id}/activate
        group.MapPost("/releases/{id:guid}/activate", async (
            Guid id,
            ISmsGovernanceReleaseService svc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            HttpContext ctx) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var actor  = ctx.User.FindFirst("sub")?.Value ?? "unknown";
            try
            {
                var result = await svc.ActivateAsync(id, actor);
                return result.Success ? Results.Ok(new { message = "Release activated." })
                                      : Results.BadRequest(new { error = result.ErrorMessage });
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        });

        // POST /v1/admin/sms/governance/releases/{id}/archive
        group.MapPost("/releases/{id:guid}/archive", async (
            Guid id,
            ArchiveReleaseBody body,
            ISmsGovernanceReleaseService svc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            HttpContext ctx) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var actor  = ctx.User.FindFirst("sub")?.Value ?? "unknown";
            try
            {
                var result = await svc.ArchiveAsync(id, actor, body.Reason);
                return result.Success ? Results.Ok(new { message = "Release archived." })
                                      : Results.BadRequest(new { error = result.ErrorMessage });
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        });

        // ── Audit trail ───────────────────────────────────────────────────────

        // GET /v1/admin/sms/governance/releases/{id}/audit
        group.MapGet("/releases/{id:guid}/audit", async (
            Guid id,
            ISmsGovernanceReleaseService svc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var trail = await svc.GetAuditTrailAsync(id);
            return Results.Ok(new { releaseId = id, events = trail });
        });

        // ── Pending approvals ─────────────────────────────────────────────────

        // GET /v1/admin/sms/governance/approvals/pending
        group.MapGet("/approvals/pending", async (
            ISmsGovernanceApprovalWorkflowService approvalSvc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts,
            string? approverRole = null,
            int     page         = 1,
            int     pageSize     = 50) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var result = await approvalSvc.GetPendingApprovalsAsync(
                new ApprovalQuery(approverRole, page, pageSize));
            return Results.Ok(result);
        });

        // ── LS-NOTIF-SMS-021-HARDENING: Read-only hardening endpoints ─────────

        // GET /v1/admin/sms/governance/releases/{id}/validation
        group.MapGet("/releases/{id:guid}/validation", async (
            Guid id,
            ISmsGovernanceReleaseIntegrityService integritySvc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var report = await integritySvc.ValidateReleaseItemsAsync(id);
            return Results.Ok(report);
        });

        // GET /v1/admin/sms/governance/releases/{id}/integrity
        group.MapGet("/releases/{id:guid}/integrity", async (
            Guid id,
            ISmsGovernanceReleaseIntegrityService integritySvc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var report = await integritySvc.ValidateReleaseIntegrityAsync(id);
            return Results.Ok(report);
        });

        // GET /v1/admin/sms/governance/releases/{id}/locks
        group.MapGet("/releases/{id:guid}/locks", async (
            Guid id,
            ISmsGovernanceReleaseIntegrityService integritySvc,
            IOptions<SmsGovernanceReleaseManagementOptions> opts) =>
        {
            if (!opts.Value.Enabled)
                return Results.StatusCode(503);

            var status = await integritySvc.GetActivationLockStatusAsync(id);
            return Results.Ok(status);
        });

        return app;
    }

    // ── Local request body types ──────────────────────────────────────────────

    private sealed record ScheduleReleaseBody(DateTime ActivateAtUtc);
    private sealed record ArchiveReleaseBody(string? Reason);
}
