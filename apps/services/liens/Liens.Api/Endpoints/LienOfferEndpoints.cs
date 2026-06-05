using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class LienOfferEndpoints
{
    public static void MapLienOfferEndpoints(this WebApplication app)
    {
        var offersGroup = app.MapGroup("/api/liens/offers")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        offersGroup.MapGet("/", SearchOffers)
            .RequirePermission(LiensPermissions.LienRead);

        offersGroup.MapGet("/{id:guid}", GetOfferById)
            .RequirePermission(LiensPermissions.LienRead);

        offersGroup.MapPost("/", CreateOffer)
            .RequirePermission(LiensPermissions.LienOffer)
            .RequireSellMode();

        offersGroup.MapPost("/{offerId:guid}/accept", AcceptOffer)
            .RequirePermission(LiensPermissions.LienUpdate)
            .RequireSellMode();

        var lienOffersGroup = app.MapGroup("/api/liens/liens/{lienId:guid}/offers")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        lienOffersGroup.MapGet("/", GetOffersByLienId)
            .RequirePermission(LiensPermissions.LienRead);
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

    private static async Task<IResult> SearchOffers(
        ILienOfferService offerService,
        ICurrentRequestContext ctx,
        Guid? lienId = null,
        string? status = null,
        Guid? buyerOrgId = null,
        Guid? sellerOrgId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await offerService.SearchAsync(
            tenantId, lienId, status, buyerOrgId, sellerOrgId, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetOfferById(
        Guid id,
        ILienOfferService offerService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await offerService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"LienOffer '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetOffersByLienId(
        Guid lienId,
        ILienOfferService offerService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await offerService.GetByLienIdAsync(tenantId, lienId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateOffer(
        CreateLienOfferRequest request,
        ILienOfferService offerService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var buyerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await offerService.CreateAsync(tenantId, buyerOrgId, userId, request, ct);
        return Results.Created($"/api/liens/offers/{result.Id}", result);
    }

    private static async Task<IResult> AcceptOffer(
        Guid offerId,
        ILienSaleService saleService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await saleService.AcceptOfferAsync(tenantId, offerId, userId, ct);
        return Results.Ok(result);
    }
}
