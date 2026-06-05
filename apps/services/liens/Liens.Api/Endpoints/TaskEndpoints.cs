using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/tasks")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .WithTags("Tasks");

        group.MapGet("/", ListTasks)
            .RequirePermission(LiensPermissions.TaskRead);

        group.MapGet("/{id:guid}", GetTaskById)
            .RequirePermission(LiensPermissions.TaskRead);

        group.MapPost("/", CreateTask)
            .RequirePermission(LiensPermissions.TaskCreate);

        group.MapPut("/{id:guid}", UpdateTask)
            .RequirePermission(LiensPermissions.TaskEditAll);

        group.MapPost("/{id:guid}/assign", AssignTask)
            .RequirePermission(LiensPermissions.TaskAssign);

        group.MapPost("/{id:guid}/status", UpdateStatus)
            .RequirePermission(LiensPermissions.TaskEditOwn);

        group.MapPost("/{id:guid}/complete", CompleteTask)
            .RequirePermission(LiensPermissions.TaskComplete);

        group.MapPost("/{id:guid}/cancel", CancelTask)
            .RequirePermission(LiensPermissions.TaskCancel);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> ListTasks(
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        string? search = null,
        string? status = null,
        string? priority = null,
        Guid? assignedUserId = null,
        Guid? caseId = null,
        Guid? lienId = null,
        Guid? workflowStageId = null,
        string? assignmentScope = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var tenantId      = RequireTenantId(ctx);
        var currentUserId = ctx.UserId;
        var result = await taskService.SearchAsync(
            tenantId, search, status, priority, assignedUserId, caseId, lienId,
            workflowStageId, assignmentScope, currentUserId, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTaskById(
        Guid id,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result   = await taskService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Task '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateTask(
        CreateTaskRequest request,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.CreateAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/tasks/{result.Id}", result);
    }

    private static async Task<IResult> UpdateTask(
        Guid id,
        UpdateTaskRequest request,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AssignTask(
        Guid id,
        AssignTaskRequest request,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.AssignAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateStatus(
        Guid id,
        UpdateTaskStatusRequest request,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.UpdateStatusAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CompleteTask(
        Guid id,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.CompleteAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CancelTask(
        Guid id,
        ILienTaskService taskService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId   = RequireUserId(ctx);
        var result   = await taskService.CancelAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }
}
