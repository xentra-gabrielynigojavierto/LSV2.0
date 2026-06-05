using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;

namespace Liens.Api.Endpoints;

public static class LookupEndpoints
{
    public static void MapLookupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/lookups")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/categories", GetCategories)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapGet("/all", GetAll)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapGet("/{category}", GetByCategory)
            .RequirePermission(LiensPermissions.LienRead);

        group.MapGet("/{category}/{code}", GetByCode)
            .RequirePermission(LiensPermissions.LienRead);
    }

    private static async Task<IResult> GetCategories(
        ILookupValueService lookupService,
        CancellationToken ct = default)
    {
        var categories = await lookupService.GetCategoriesAsync(ct);
        return Results.Ok(categories);
    }

    private static async Task<IResult> GetAll(
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var result = await lookupService.GetAllAsync(ctx.TenantId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByCategory(
        string category,
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        if (!LookupCategory.All.Contains(category))
            return Results.NotFound(new { error = new { code = "not_found", message = $"Category '{category}' is not a valid lookup category." } });

        var result = await lookupService.GetByCategoryAsync(ctx.TenantId, category, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByCode(
        string category,
        string code,
        ILookupValueService lookupService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        if (!LookupCategory.All.Contains(category))
            return Results.NotFound(new { error = new { code = "not_found", message = $"Category '{category}' is not a valid lookup category." } });

        var result = await lookupService.GetByCodeAsync(ctx.TenantId, category, code, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Lookup '{category}/{code}' not found." } })
            : Results.Ok(result);
    }
}
