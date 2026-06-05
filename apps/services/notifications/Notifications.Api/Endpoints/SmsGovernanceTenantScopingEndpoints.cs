using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;

namespace Notifications.Api.Endpoints;

/// <summary>
/// LS-NOTIF-SMS-023: Per-tenant governance rule pack scoping admin endpoints.
/// All endpoints require PlatformAdmin authorization.
/// No raw phones, credentials, or message bodies exposed.
/// </summary>
public static class SmsGovernanceTenantScopingEndpoints
{
    public static IEndpointRouteBuilder MapSmsGovernanceTenantScopingEndpoints(
        this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/notifications/v1/admin/sms/governance/tenant-scoping")
                     .RequireAuthorization(BuildingBlocks.Authorization.Policies.AdminOnly)
                     .WithTags("SMS Governance – Tenant Scoping");

        // ── Assignments ───────────────────────────────────────────────────────

        grp.MapGet("/tenant-assignments", ListAssignments)
           .WithName("ListTenantAssignments")
           .Produces<PaginatedAssignmentResult>()
           .Produces(503);

        grp.MapPost("/tenant-assignments", CreateAssignment)
           .WithName("CreateTenantAssignment")
           .Produces<AssignmentOperationResult>(201)
           .Produces(400).Produces(503);

        grp.MapPost("/tenant-assignments/{id:guid}/activate", ActivateAssignment)
           .WithName("ActivateTenantAssignment")
           .Produces<AssignmentOperationResult>()
           .Produces(404).Produces(503);

        grp.MapPost("/tenant-assignments/{id:guid}/deactivate", DeactivateAssignment)
           .WithName("DeactivateTenantAssignment")
           .Produces<AssignmentOperationResult>()
           .Produces(404).Produces(503);

        grp.MapPost("/tenant-assignments/{id:guid}/rollback", RollbackAssignment)
           .WithName("RollbackTenantAssignment")
           .Produces<AssignmentOperationResult>()
           .Produces(404).Produces(503);

        // ── Overlays ──────────────────────────────────────────────────────────

        grp.MapGet("/tenant-overlays", ListOverlays)
           .WithName("ListTenantOverlays")
           .Produces<PaginatedOverlayResult>()
           .Produces(503);

        grp.MapPost("/tenant-overlays", CreateOverlay)
           .WithName("CreateTenantOverlay")
           .Produces<OverlayOperationResult>(201)
           .Produces(400).Produces(503);

        grp.MapPost("/tenant-overlays/{id:guid}/activate", ActivateOverlay)
           .WithName("ActivateTenantOverlay")
           .Produces<OverlayOperationResult>()
           .Produces(404).Produces(503);

        grp.MapPost("/tenant-overlays/{id:guid}/disable", DisableOverlay)
           .WithName("DisableTenantOverlay")
           .Produces<OverlayOperationResult>()
           .Produces(404).Produces(503);

        // ── Resolution / graph / explain / isolation ──────────────────────────

        grp.MapGet("/tenant-resolution/{tenantId:guid}", GetResolution)
           .WithName("GetTenantResolution")
           .Produces<EffectiveGovernanceGraphDto>()
           .Produces(503);

        grp.MapGet("/tenant-resolution/{tenantId:guid}/explain", ExplainResolution)
           .WithName("ExplainTenantResolution")
           .Produces<ResolutionExplanationDto>()
           .Produces(503);

        grp.MapGet("/tenant-isolation/{tenantId:guid}", ValidateIsolation)
           .WithName("ValidateTenantIsolation")
           .Produces<IsolationValidationResult>()
           .Produces(503);

        // ── Audit ─────────────────────────────────────────────────────────────

        grp.MapGet("/tenant-assignment-audit", GetAuditTrail)
           .WithName("GetTenantAssignmentAudit")
           .Produces<IReadOnlyList<TenantAssignmentAuditEventDto>>()
           .Produces(503);

        return app;
    }

    // ── Assignment handlers ───────────────────────────────────────────────────

    private static async Task<IResult> ListAssignments(
        [FromQuery] Guid?   tenantId,
        [FromQuery] Guid?   rulePackId,
        [FromQuery] string? state,
        [FromQuery] string? mode,
        [FromQuery] Guid?   rolloutPlanId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 50,
        [FromServices] ISmsGovernanceTenantAssignmentService svc = null!,
        [FromServices] IOptions<SmsGovernanceTenantScopingOptions> opts = null!,
        CancellationToken ct = default)
    {
        if (!opts.Value.Enabled)
            return Results.Problem("Tenant scoping is disabled.", statusCode: 503);

        var result = await svc.ListAssignmentsAsync(
            new TenantAssignmentQuery(tenantId, rulePackId, state, mode, rolloutPlanId, page, Math.Min(pageSize, 200)), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateAssignment(
        [FromBody] CreateAssignmentBody body,
        [FromServices] ISmsGovernanceTenantAssignmentService svc = null!,
        [FromServices] IOptions<SmsGovernanceTenantScopingOptions> opts = null!,
        CancellationToken ct = default)
    {
        if (!opts.Value.Enabled)
            return Results.Problem("Tenant scoping is disabled.", statusCode: 503);
        if (body.TenantId == Guid.Empty || body.RulePackId == Guid.Empty)
            return Results.BadRequest(new { error = "TenantId and RulePackId are required." });

        var request = new AssignRulePackRequest(
            body.TenantId, body.RulePackId,
            body.AssignmentMode ?? "inherited",
            body.Priority ?? 100,
            body.EffectiveFrom, body.EffectiveTo,
            body.RolloutPlanId, body.RolloutStageId, body.ReleasePackageId,
            body.AssignedBy);

        var result = await svc.AssignRulePackAsync(request, ct);
        return result.Success
            ? Results.Created($"/notifications/v1/admin/sms/governance/tenant-scoping/tenant-assignments/{result.AssignmentId}", result)
            : Results.BadRequest(result);
    }

    private static async Task<IResult> ActivateAssignment(
        Guid id,
        [FromBody] SimpleRequestBody? body,
        [FromServices] ISmsGovernanceTenantAssignmentService svc = null!,
        CancellationToken ct = default)
    {
        var result = await svc.ActivateAssignmentAsync(id, body?.RequestedBy ?? "api", ct);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> DeactivateAssignment(
        Guid id,
        [FromBody] ReasonBody? body,
        [FromServices] ISmsGovernanceTenantAssignmentService svc = null!,
        CancellationToken ct = default)
    {
        var result = await svc.DeactivateAssignmentAsync(id, body?.RequestedBy ?? "api", body?.Reason, ct);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> RollbackAssignment(
        Guid id,
        [FromBody] ReasonBody? body,
        [FromServices] ISmsGovernanceTenantAssignmentService svc = null!,
        CancellationToken ct = default)
    {
        var result = await svc.RollbackAssignmentAsync(id, body?.RequestedBy ?? "api", body?.Reason, ct);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    // ── Overlay handlers ──────────────────────────────────────────────────────

    private static async Task<IResult> ListOverlays(
        [FromQuery] Guid?   tenantId,
        [FromQuery] Guid?   rulePackId,
        [FromQuery] Guid?   ruleId,
        [FromQuery] string? overlayType,
        [FromQuery] string? overlayState,
        [FromQuery] bool?   enabled,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 50,
        [FromServices] ISmsGovernanceTenantAssignmentService svc = null!,
        [FromServices] IOptions<SmsGovernanceTenantScopingOptions> opts = null!,
        CancellationToken ct = default)
    {
        if (!opts.Value.Enabled)
            return Results.Problem("Tenant scoping is disabled.", statusCode: 503);

        var result = await svc.ListOverlaysAsync(
            new TenantOverlayQuery(tenantId, rulePackId, ruleId, overlayType, overlayState, enabled, page, Math.Min(pageSize, 200)), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateOverlay(
        [FromBody] CreateOverlayBody body,
        [FromServices] ISmsGovernanceTenantAssignmentService svc = null!,
        [FromServices] IOptions<SmsGovernanceTenantScopingOptions> opts = null!,
        CancellationToken ct = default)
    {
        if (!opts.Value.Enabled)
            return Results.Problem("Tenant scoping is disabled.", statusCode: 503);
        if (body.TenantId == Guid.Empty)
            return Results.BadRequest(new { error = "TenantId is required." });

        var request = new CreateTenantOverlayRequest(
            body.TenantId, body.OverlayType ?? "disable_rule",
            body.RulePackId, body.RuleId,
            body.OverrideJson, body.Priority ?? 100,
            body.EffectiveFrom, body.EffectiveTo, body.CreatedBy);

        var result = await svc.CreateOverlayAsync(request, ct);
        return result.Success
            ? Results.Created($"/notifications/v1/admin/sms/governance/tenant-scoping/tenant-overlays/{result.OverlayId}", result)
            : Results.BadRequest(result);
    }

    private static async Task<IResult> ActivateOverlay(
        Guid id,
        [FromBody] SimpleRequestBody? body,
        [FromServices] ISmsGovernanceTenantAssignmentService svc = null!,
        CancellationToken ct = default)
    {
        var result = await svc.ActivateOverlayAsync(id, body?.RequestedBy ?? "api", ct);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> DisableOverlay(
        Guid id,
        [FromBody] ReasonBody? body,
        [FromServices] ISmsGovernanceTenantAssignmentService svc = null!,
        CancellationToken ct = default)
    {
        var result = await svc.DisableOverlayAsync(id, body?.RequestedBy ?? "api", body?.Reason, ct);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    // ── Resolution / graph / explain / isolation handlers ────────────────────

    private static async Task<IResult> GetResolution(
        Guid tenantId,
        [FromServices] ISmsGovernanceTenantResolutionService svc = null!,
        [FromServices] IOptions<SmsGovernanceTenantScopingOptions> opts = null!,
        CancellationToken ct = default)
    {
        if (!opts.Value.Enabled)
            return Results.Problem("Tenant scoping is disabled.", statusCode: 503);

        var graph = await svc.GetEffectiveGovernanceGraphAsync(tenantId, ct);
        return Results.Ok(graph);
    }

    private static async Task<IResult> ExplainResolution(
        Guid tenantId,
        [FromServices] ISmsGovernanceTenantResolutionService svc = null!,
        [FromServices] IOptions<SmsGovernanceTenantScopingOptions> opts = null!,
        CancellationToken ct = default)
    {
        if (!opts.Value.Enabled)
            return Results.Problem("Tenant scoping is disabled.", statusCode: 503);

        var explanation = await svc.ExplainResolutionAsync(tenantId, ct);
        return Results.Ok(explanation);
    }

    private static async Task<IResult> ValidateIsolation(
        Guid tenantId,
        [FromServices] ISmsGovernanceTenantIsolationValidator validator = null!,
        [FromServices] IOptions<SmsGovernanceTenantScopingOptions> opts = null!,
        CancellationToken ct = default)
    {
        if (!opts.Value.Enabled)
            return Results.Problem("Tenant scoping is disabled.", statusCode: 503);

        var result = await validator.ValidateTenantIsolationAsync(tenantId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAuditTrail(
        [FromQuery] Guid?   tenantId,
        [FromQuery] Guid?   assignmentId,
        [FromQuery] Guid?   overlayId,
        [FromQuery] string? eventType,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 100,
        [FromServices] ISmsGovernanceTenantAssignmentService svc = null!,
        CancellationToken ct = default)
    {
        var result = await svc.GetAuditTrailAsync(
            new TenantAuditQuery(tenantId, assignmentId, overlayId, eventType, page, Math.Min(pageSize, 500)), ct);
        return Results.Ok(result);
    }

    // ── Request body records ──────────────────────────────────────────────────

    private record CreateAssignmentBody(
        Guid      TenantId,
        Guid      RulePackId,
        string?   AssignmentMode   = null,
        int?      Priority         = null,
        DateTime? EffectiveFrom    = null,
        DateTime? EffectiveTo      = null,
        Guid?     RolloutPlanId    = null,
        Guid?     RolloutStageId   = null,
        Guid?     ReleasePackageId = null,
        string?   AssignedBy       = null);

    private record CreateOverlayBody(
        Guid      TenantId,
        string?   OverlayType  = null,
        Guid?     RulePackId   = null,
        Guid?     RuleId       = null,
        string?   OverrideJson = null,
        int?      Priority     = null,
        DateTime? EffectiveFrom = null,
        DateTime? EffectiveTo   = null,
        string?   CreatedBy    = null);

    private record SimpleRequestBody(string? RequestedBy = null);
    private record ReasonBody(string? RequestedBy = null, string? Reason = null);
}
