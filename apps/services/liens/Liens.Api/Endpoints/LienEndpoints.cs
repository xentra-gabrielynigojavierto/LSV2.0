using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class LienEndpoints
{
    public static void MapLienEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/liens")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/", ListLiens)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapGet("/{id:guid}", GetLienById)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapGet("/by-number/{lienNumber}", GetLienByNumber)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapPost("/", CreateLien)
            .RequirePermission(LiensPermissions.LienCreate);

        group.MapPut("/{id:guid}", UpdateLien)
            .RequirePermission(LiensPermissions.LienUpdate);
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

    private static async Task<IResult> ListLiens(
        ILienService lienService,
        ICurrentRequestContext ctx,
        string? search = null,
        string? status = null,
        string? lienType = null,
        Guid? caseId = null,
        Guid? facilityId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await lienService.SearchAsync(
            tenantId, search, status, lienType, caseId, facilityId, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetLienById(
        Guid id,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await lienService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Lien '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetLienByNumber(
        string lienNumber,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await lienService.GetByLienNumberAsync(tenantId, lienNumber, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Lien with number '{lienNumber}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateLien(
        CreateLienRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await lienService.CreateAsync(tenantId, orgId, userId, request, ct);
        return Results.Created($"/api/liens/liens/{result.Id}", result);
    }

    private static async Task<IResult> UpdateLien(
        Guid id,
        UpdateLienRequest request,
        ILienService lienService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await lienService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }
}
