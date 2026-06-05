using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class ServicingEndpoints
{
    public static void MapServicingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/servicing")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/", ListServicingItems)
            .RequirePermission(LiensPermissions.LienService);

        group.MapGet("/{id:guid}", GetServicingItemById)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPost("/", CreateServicingItem)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPut("/{id:guid}", UpdateServicingItem)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPut("/{id:guid}/status", UpdateServicingItemStatus)
            .RequirePermission(LiensPermissions.LienService);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx)
    {
        return ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");
    }

    private static Guid RequireUserId(ICurrentRequestContext ctx)
    {
        return ctx.UserId
            ?? throw new UnauthorizedAccessException("User context is required.");
    }

    private static Guid RequireOrgId(ICurrentRequestContext ctx)
    {
        return ctx.OrgId
            ?? throw new UnauthorizedAccessException("Organization context is required.");
    }

    private static async Task<IResult> ListServicingItems(
        IServicingItemService servicingService,
        ICurrentRequestContext ctx,
        string? search = null,
        string? status = null,
        string? priority = null,
        string? assignedTo = null,
        Guid? caseId = null,
        Guid? lienId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await servicingService.SearchAsync(
            tenantId, search, status, priority, assignedTo, caseId, lienId, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetServicingItemById(
        Guid id,
        IServicingItemService servicingService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await servicingService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Servicing item '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateServicingItem(
        CreateServicingItemRequest request,
        IServicingItemService servicingService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await servicingService.CreateAsync(tenantId, orgId, userId, request, ct);
        return Results.Created($"/api/liens/servicing/{result.Id}", result);
    }

    private static async Task<IResult> UpdateServicingItem(
        Guid id,
        UpdateServicingItemRequest request,
        IServicingItemService servicingService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await servicingService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateServicingItemStatus(
        Guid id,
        UpdateStatusRequest request,
        IServicingItemService servicingService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await servicingService.UpdateStatusAsync(
            tenantId, id, userId, request.Status, request.Resolution, ct);
        return Results.Ok(result);
    }
}

public sealed class UpdateStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? Resolution { get; init; }
}
