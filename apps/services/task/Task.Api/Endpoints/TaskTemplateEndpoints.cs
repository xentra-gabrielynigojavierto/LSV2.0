using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Task.Application.DTOs;
using Task.Application.Interfaces;

namespace Task.Api.Endpoints;

public static class TaskTemplateEndpoints
{
    public static void MapTaskTemplateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks/templates")
            .RequireAuthorization("AuthenticatedUserOrService")
            .WithTags("TaskTemplates");

        group.MapGet("/",          ListTemplates);
        group.MapGet("/{id:guid}", GetTemplateById);
        group.MapPost("/",         CreateTemplate).RequireAuthorization(Policies.PlatformOrTenantAdmin);
        group.MapPut("/{id:guid}", UpdateTemplate).RequireAuthorization(Policies.PlatformOrTenantAdmin);
        group.MapPost("/{id:guid}/activate",   ActivateTemplate).RequireAuthorization(Policies.PlatformOrTenantAdmin);
        group.MapPost("/{id:guid}/deactivate", DeactivateTemplate).RequireAuthorization(Policies.PlatformOrTenantAdmin);
        group.MapPost("/{id:guid}/create-task", CreateTaskFromTemplate);

        // TASK-MIG-02 — source-product sync endpoint (internal service only)
        group.MapPost("/from-source", UpsertFromSource)
            .RequireAuthorization("InternalService");
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async System.Threading.Tasks.Task<IResult> ListTemplates(
        ITaskTemplateService   templateService,
        ICurrentRequestContext ctx,
        string?                sourceProductCode = null,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await templateService.ListAsync(tenantId, sourceProductCode, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> GetTemplateById(
        Guid                   id,
        ITaskTemplateService   templateService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await templateService.GetByIdAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> CreateTemplate(
        CreateTaskTemplateRequest request,
        ITaskTemplateService      templateService,
        ICurrentRequestContext    ctx,
        CancellationToken         ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await templateService.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/tasks/templates/{result.Id}", result);
    }

    private static async System.Threading.Tasks.Task<IResult> UpdateTemplate(
        Guid                      id,
        UpdateTaskTemplateRequest request,
        ITaskTemplateService      templateService,
        ICurrentRequestContext    ctx,
        CancellationToken         ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await templateService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> ActivateTemplate(
        Guid                   id,
        ITaskTemplateService   templateService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await templateService.ActivateAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> DeactivateTemplate(
        Guid                   id,
        ITaskTemplateService   templateService,
        ICurrentRequestContext ctx,
        CancellationToken      ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await templateService.DeactivateAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async System.Threading.Tasks.Task<IResult> CreateTaskFromTemplate(
        Guid                          id,
        CreateTaskFromTemplateRequest request,
        ITaskTemplateService          templateService,
        ICurrentRequestContext        ctx,
        CancellationToken             ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await templateService.CreateTaskFromTemplateAsync(tenantId, userId, id, request, ct);
        return Results.Created($"/api/tasks/{result.Id}", result);
    }

    private static async System.Threading.Tasks.Task<IResult> UpsertFromSource(
        UpsertFromSourceTemplateRequest request,
        ITaskTemplateService            templateService,
        ICurrentRequestContext          ctx,
        CancellationToken               ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await templateService.UpsertFromSourceAsync(tenantId, userId, request, ct);
        return Results.Ok(result);
    }
}
