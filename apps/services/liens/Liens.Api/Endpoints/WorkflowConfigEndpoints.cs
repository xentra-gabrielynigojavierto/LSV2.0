using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

/// <summary>
/// LS-LIENS-FLOW-001 — Workflow Configuration endpoints.
/// LS-LIENS-FLOW-005 — Workflow Transition endpoints.
/// Manages the single-source-of-truth workflow config for Synq Liens.
/// Both tenant-side Product Settings and admin-side Control Center use the same endpoints.
/// The caller distinguishes itself via the UpdateSource field in the request body.
/// </summary>
public static class WorkflowConfigEndpoints
{
    public static void MapWorkflowConfigEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/workflow-config")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("WorkflowConfig");

        group.MapGet("/", GetWorkflowConfig)
            .RequirePermission(LiensPermissions.WorkflowManage);

        group.MapPost("/", CreateWorkflowConfig)
            .RequirePermission(LiensPermissions.WorkflowManage);

        group.MapPut("/{id:guid}", UpdateWorkflowConfig)
            .RequirePermission(LiensPermissions.WorkflowManage);

        group.MapPost("/{id:guid}/stages", AddStage)
            .RequirePermission(LiensPermissions.WorkflowManage);

        group.MapPut("/{id:guid}/stages/{stageId:guid}", UpdateStage)
            .RequirePermission(LiensPermissions.WorkflowManage);

        group.MapDelete("/{id:guid}/stages/{stageId:guid}", RemoveStage)
            .RequirePermission(LiensPermissions.WorkflowManage);

        group.MapPost("/{id:guid}/stages/reorder", ReorderStages)
            .RequirePermission(LiensPermissions.WorkflowManage);

        // ── Transition endpoints (LS-LIENS-FLOW-005) ──────────────────────────
        group.MapGet("/{id:guid}/transitions", GetTransitions)
            .RequirePermission(LiensPermissions.WorkflowManage);

        group.MapPost("/{id:guid}/transitions", AddTransition)
            .RequirePermission(LiensPermissions.WorkflowManage);

        group.MapDelete("/{id:guid}/transitions/{transitionId:guid}", DeactivateTransition)
            .RequirePermission(LiensPermissions.WorkflowManage);

        group.MapPost("/{id:guid}/transitions/save", SaveTransitions)
            .RequirePermission(LiensPermissions.WorkflowManage);

        // Admin-only passthrough — platform admin can also manage from control center
        // without needing the tenant's workflow:manage permission.
        var adminGroup = app.MapGroup("/api/liens/admin/workflow-config")
            .RequireAuthorization(Policies.PlatformOrTenantAdmin)
            .WithTags("WorkflowConfig");

        adminGroup.MapGet("/tenants/{tenantId:guid}", AdminGetWorkflowConfig);
        adminGroup.MapPost("/tenants/{tenantId:guid}", AdminCreateWorkflowConfig);
        adminGroup.MapPut("/tenants/{tenantId:guid}/{id:guid}", AdminUpdateWorkflowConfig);
        adminGroup.MapPost("/tenants/{tenantId:guid}/{id:guid}/stages", AdminAddStage);
        adminGroup.MapPut("/tenants/{tenantId:guid}/{id:guid}/stages/{stageId:guid}", AdminUpdateStage);
        adminGroup.MapDelete("/tenants/{tenantId:guid}/{id:guid}/stages/{stageId:guid}", AdminRemoveStage);
        adminGroup.MapPost("/tenants/{tenantId:guid}/{id:guid}/stages/reorder", AdminReorderStages);

        // ── Admin transition endpoints (LS-LIENS-FLOW-005) ────────────────────
        adminGroup.MapGet("/tenants/{tenantId:guid}/{id:guid}/transitions", AdminGetTransitions);

        adminGroup.MapPost("/tenants/{tenantId:guid}/{id:guid}/transitions", AdminAddTransition);
        adminGroup.MapDelete("/tenants/{tenantId:guid}/{id:guid}/transitions/{transitionId:guid}", AdminDeactivateTransition);
        adminGroup.MapPost("/tenants/{tenantId:guid}/{id:guid}/transitions/save", AdminSaveTransitions);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    // ── Tenant-scoped endpoints ───────────────────────────────────────────────

    private static async Task<IResult> GetWorkflowConfig(
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await svc.GetByTenantAsync(tenantId, ct);
        return result is null ? Results.NoContent() : Results.Ok(result);
    }

    private static async Task<IResult> CreateWorkflowConfig(
        CreateWorkflowConfigRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await svc.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/workflow-config/{result.Id}", result);
    }

    private static async Task<IResult> UpdateWorkflowConfig(
        Guid id,
        UpdateWorkflowConfigRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.UpdateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> AddStage(
        Guid id,
        AddWorkflowStageRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.AddStageAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> UpdateStage(
        Guid id, Guid stageId,
        UpdateWorkflowStageRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.UpdateStageAsync(tenantId, id, stageId, userId, request, ct));
    }

    private static async Task<IResult> RemoveStage(
        Guid id, Guid stageId,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.RemoveStageAsync(tenantId, id, stageId, userId, ct));
    }

    private static async Task<IResult> ReorderStages(
        Guid id,
        ReorderStagesRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.ReorderStagesAsync(tenantId, id, userId, request, ct));
    }

    // ── Transition endpoints (tenant-scoped) ──────────────────────────────────

    private static async Task<IResult> GetTransitions(
        Guid id,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        return Results.Ok(await svc.GetTransitionsAsync(tenantId, id, ct));
    }

    private static async Task<IResult> AddTransition(
        Guid id,
        AddWorkflowTransitionRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.AddTransitionAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> DeactivateTransition(
        Guid id, Guid transitionId,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.DeactivateTransitionAsync(tenantId, id, transitionId, userId, ct));
    }

    private static async Task<IResult> SaveTransitions(
        Guid id,
        SaveWorkflowTransitionsRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.SaveTransitionsAsync(tenantId, id, userId, request, ct));
    }

    // ── Admin-scoped endpoints (explicit tenantId in path) ────────────────────

    private static async Task<IResult> AdminGetWorkflowConfig(
        Guid tenantId,
        ILienWorkflowConfigService svc,
        CancellationToken ct = default)
    {
        var result = await svc.GetByTenantAsync(tenantId, ct);
        return result is null ? Results.NoContent() : Results.Ok(result);
    }

    private static async Task<IResult> AdminCreateWorkflowConfig(
        Guid tenantId,
        CreateWorkflowConfigRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        var result = await svc.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/admin/workflow-config/tenants/{tenantId}/{result.Id}", result);
    }

    private static async Task<IResult> AdminUpdateWorkflowConfig(
        Guid tenantId, Guid id,
        UpdateWorkflowConfigRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.UpdateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> AdminAddStage(
        Guid tenantId, Guid id,
        AddWorkflowStageRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.AddStageAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> AdminUpdateStage(
        Guid tenantId, Guid id, Guid stageId,
        UpdateWorkflowStageRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.UpdateStageAsync(tenantId, id, stageId, userId, request, ct));
    }

    private static async Task<IResult> AdminRemoveStage(
        Guid tenantId, Guid id, Guid stageId,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.RemoveStageAsync(tenantId, id, stageId, userId, ct));
    }

    private static async Task<IResult> AdminReorderStages(
        Guid tenantId, Guid id,
        ReorderStagesRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.ReorderStagesAsync(tenantId, id, userId, request, ct));
    }

    // ── Admin transition endpoints ─────────────────────────────────────────────

    private static async Task<IResult> AdminGetTransitions(
        Guid tenantId, Guid id,
        ILienWorkflowConfigService svc,
        CancellationToken ct = default)
    {
        return Results.Ok(await svc.GetTransitionsAsync(tenantId, id, ct));
    }

    private static async Task<IResult> AdminAddTransition(
        Guid tenantId, Guid id,
        AddWorkflowTransitionRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.AddTransitionAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> AdminDeactivateTransition(
        Guid tenantId, Guid id, Guid transitionId,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.DeactivateTransitionAsync(tenantId, id, transitionId, userId, ct));
    }

    private static async Task<IResult> AdminSaveTransitions(
        Guid tenantId, Guid id,
        SaveWorkflowTransitionsRequest request,
        ILienWorkflowConfigService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.SaveTransitionsAsync(tenantId, id, userId, request, ct));
    }
}
