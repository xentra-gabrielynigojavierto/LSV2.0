using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

/// <summary>
/// LS-LIENS-FLOW-002 — Task Template endpoints.
/// Contextual task templates for Synq Liens.
/// Tenant-scoped and admin passthrough surfaces share the same underlying data.
/// </summary>
public static class TaskTemplateEndpoints
{
    public static void MapTaskTemplateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/task-templates")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("TaskTemplates");

        group.MapGet("/", ListTemplates)
            .RequirePermission(LiensPermissions.TaskTemplateManage);

        group.MapGet("/contextual", GetContextualTemplates)
            .RequirePermission(LiensPermissions.TaskRead);

        group.MapGet("/{id:guid}", GetTemplate)
            .RequirePermission(LiensPermissions.TaskTemplateManage);

        group.MapPost("/", CreateTemplate)
            .RequirePermission(LiensPermissions.TaskTemplateManage);

        group.MapPut("/{id:guid}", UpdateTemplate)
            .RequirePermission(LiensPermissions.TaskTemplateManage);

        group.MapPost("/{id:guid}/activate", ActivateTemplate)
            .RequirePermission(LiensPermissions.TaskTemplateManage);

        group.MapPost("/{id:guid}/deactivate", DeactivateTemplate)
            .RequirePermission(LiensPermissions.TaskTemplateManage);

        // Admin passthrough — platform/tenant admin can manage templates without product permission
        var adminGroup = app.MapGroup("/api/liens/admin/task-templates")
            .RequireAuthorization(Policies.PlatformOrTenantAdmin)
            .WithTags("TaskTemplates");

        adminGroup.MapGet("/tenants/{tenantId:guid}", AdminListTemplates);
        adminGroup.MapGet("/tenants/{tenantId:guid}/{id:guid}", AdminGetTemplate);
        adminGroup.MapPost("/tenants/{tenantId:guid}", AdminCreateTemplate);
        adminGroup.MapPut("/tenants/{tenantId:guid}/{id:guid}", AdminUpdateTemplate);
        adminGroup.MapPost("/tenants/{tenantId:guid}/{id:guid}/activate", AdminActivateTemplate);
        adminGroup.MapPost("/tenants/{tenantId:guid}/{id:guid}/deactivate", AdminDeactivateTemplate);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    // ── Tenant-scoped ─────────────────────────────────────────────────────────

    private static async Task<IResult> ListTemplates(
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        return Results.Ok(await svc.GetByTenantAsync(tenantId, ct));
    }

    private static async Task<IResult> GetContextualTemplates(
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        string? contextType = null,
        Guid?   workflowStageId = null,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        return Results.Ok(await svc.GetContextualAsync(tenantId, contextType, workflowStageId, ct));
    }

    private static async Task<IResult> GetTemplate(
        Guid id,
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await svc.GetByIdAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateTemplate(
        CreateTaskTemplateRequest request,
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await svc.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/task-templates/{result.Id}", result);
    }

    private static async Task<IResult> UpdateTemplate(
        Guid id,
        UpdateTaskTemplateRequest request,
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.UpdateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> ActivateTemplate(
        Guid id,
        ActivateDeactivateTemplateRequest request,
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.ActivateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> DeactivateTemplate(
        Guid id,
        ActivateDeactivateTemplateRequest request,
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        return Results.Ok(await svc.DeactivateAsync(tenantId, id, userId, request, ct));
    }

    // ── Admin passthrough ─────────────────────────────────────────────────────

    private static async Task<IResult> AdminListTemplates(
        Guid tenantId,
        ILienTaskTemplateService svc,
        CancellationToken ct = default)
    {
        return Results.Ok(await svc.GetByTenantAsync(tenantId, ct));
    }

    private static async Task<IResult> AdminGetTemplate(
        Guid tenantId, Guid id,
        ILienTaskTemplateService svc,
        CancellationToken ct = default)
    {
        var result = await svc.GetByIdAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> AdminCreateTemplate(
        Guid tenantId,
        CreateTaskTemplateRequest request,
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        var result = await svc.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/admin/task-templates/tenants/{tenantId}/{result.Id}", result);
    }

    private static async Task<IResult> AdminUpdateTemplate(
        Guid tenantId, Guid id,
        UpdateTaskTemplateRequest request,
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.UpdateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> AdminActivateTemplate(
        Guid tenantId, Guid id,
        ActivateDeactivateTemplateRequest request,
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.ActivateAsync(tenantId, id, userId, request, ct));
    }

    private static async Task<IResult> AdminDeactivateTemplate(
        Guid tenantId, Guid id,
        ActivateDeactivateTemplateRequest request,
        ILienTaskTemplateService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var userId = RequireUserId(ctx);
        return Results.Ok(await svc.DeactivateAsync(tenantId, id, userId, request, ct));
    }
}
