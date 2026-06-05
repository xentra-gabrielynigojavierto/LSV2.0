using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;

namespace Liens.Api.Endpoints;

/// <summary>
/// LS-LIENS-FLOW-003 — Task Generation Rule endpoints.
/// Event-driven task automation for Synq Liens.
/// Tenant-scoped and admin passthrough surfaces share the same underlying data.
/// </summary>
public static class TaskGenerationRuleEndpoints
{
    public static void MapTaskGenerationRuleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/task-generation-rules")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("TaskGenerationRules");

        group.MapGet("/", ListRules)
            .RequirePermission(LiensPermissions.TaskAutomationManage);

        group.MapGet("/{id:guid}", GetRule)
            .RequirePermission(LiensPermissions.TaskAutomationManage);

        group.MapPost("/", CreateRule)
            .RequirePermission(LiensPermissions.TaskAutomationManage);

        group.MapPut("/{id:guid}", UpdateRule)
            .RequirePermission(LiensPermissions.TaskAutomationManage);

        group.MapPost("/{id:guid}/activate", ActivateRule)
            .RequirePermission(LiensPermissions.TaskAutomationManage);

        group.MapPost("/{id:guid}/deactivate", DeactivateRule)
            .RequirePermission(LiensPermissions.TaskAutomationManage);

        // Trigger endpoint for workflow stage-change events
        app.MapPost("/api/liens/task-generation/trigger", TriggerGeneration)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequirePermission(LiensPermissions.TaskRead)
            .WithTags("TaskGenerationRules");

        // Admin passthrough
        var adminGroup = app.MapGroup("/api/liens/admin/task-generation-rules")
            .RequireAuthorization(Policies.PlatformOrTenantAdmin)
            .WithTags("TaskGenerationRules");

        adminGroup.MapGet("/tenants/{tenantId:guid}", AdminListRules);
        adminGroup.MapGet("/tenants/{tenantId:guid}/{id:guid}", AdminGetRule);
        adminGroup.MapPost("/tenants/{tenantId:guid}", AdminCreateRule);
        adminGroup.MapPut("/tenants/{tenantId:guid}/{id:guid}", AdminUpdateRule);
        adminGroup.MapPost("/tenants/{tenantId:guid}/{id:guid}/activate", AdminActivateRule);
        adminGroup.MapPost("/tenants/{tenantId:guid}/{id:guid}/deactivate", AdminDeactivateRule);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    // ── Tenant-scoped ─────────────────────────────────────────────────────────

    private static async Task<IResult> ListRules(
        ILienTaskGenerationRuleService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        return Results.Ok(await svc.GetByTenantAsync(tenantId, ct));
    }

    private static async Task<IResult> GetRule(
        Guid id,
        ILienTaskGenerationRuleService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await svc.GetByIdAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateRule(
        CreateTaskGenerationRuleRequest request,
        ILienTaskGenerationRuleService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await svc.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/task-generation-rules/{result.Id}", result);
    }

    private static async Task<IResult> UpdateRule(
        Guid id,
        UpdateTaskGenerationRuleRequest request,
        ILienTaskGenerationRuleService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.UpdateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> ActivateRule(
        Guid id,
        ActivateDeactivateRuleRequest request,
        ILienTaskGenerationRuleService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.ActivateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> DeactivateRule(
        Guid id,
        ActivateDeactivateRuleRequest request,
        ILienTaskGenerationRuleService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.DeactivateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> TriggerGeneration(
        TriggerTaskGenerationRequest request,
        ILienTaskGenerationEngine engine,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId   = RequireTenantId(ctx);
        var userId     = ctx.UserId;

        if (!TaskGenerationEventType.StageEvents.Contains(request.EventType))
            return Results.BadRequest(new { error = $"Trigger endpoint only supports stage-change events. Got: {request.EventType}" });

        if (!request.CaseId.HasValue && !request.LienId.HasValue)
            return Results.BadRequest(new { error = "At least one of caseId or lienId must be provided." });

        var entityType = request.LienId.HasValue ? "LIEN" : "CASE";
        var entityId   = (request.LienId ?? request.CaseId)!.Value;

        var context = new TaskGenerationContext(
            TenantId:       tenantId,
            EventType:      request.EventType,
            EntityType:     entityType,
            EntityId:       entityId,
            CaseId:         request.CaseId,
            LienId:         request.LienId,
            WorkflowStageId: request.WorkflowStageId,
            ActorUserId:    userId,
            ActorName:      request.ActorName);

        var result = await engine.TriggerAsync(context, ct);
        return Results.Ok(new TriggerTaskGenerationResponse
        {
            TasksGenerated = result.TasksGenerated,
            TasksSkipped   = result.TasksSkipped,
        });
    }

    // ── Admin passthrough ─────────────────────────────────────────────────────

    private static async Task<IResult> AdminListRules(
        Guid tenantId,
        ILienTaskGenerationRuleService svc,
        CancellationToken ct = default)
    {
        return Results.Ok(await svc.GetByTenantAsync(tenantId, ct));
    }

    private static async Task<IResult> AdminGetRule(
        Guid tenantId, Guid id,
        ILienTaskGenerationRuleService svc,
        CancellationToken ct = default)
    {
        var result = await svc.GetByIdAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> AdminCreateRule(
        Guid tenantId,
        CreateTaskGenerationRuleRequest request,
        ILienTaskGenerationRuleService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        var result = await svc.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/admin/task-generation-rules/tenants/{tenantId}/{result.Id}", result);
    }

    private static async Task<IResult> AdminUpdateRule(
        Guid tenantId, Guid id,
        UpdateTaskGenerationRuleRequest request,
        ILienTaskGenerationRuleService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.UpdateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> AdminActivateRule(
        Guid tenantId, Guid id,
        ActivateDeactivateRuleRequest request,
        ILienTaskGenerationRuleService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.ActivateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> AdminDeactivateRule(
        Guid tenantId, Guid id,
        ActivateDeactivateRuleRequest request,
        ILienTaskGenerationRuleService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.DeactivateAsync(tenantId, id, userId, request, ct));
    }
}
