using Notifications.Api.Middleware;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Api.Endpoints;

public static class BrandingEndpoints
{
    public static void MapBrandingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/branding").WithTags("Branding");

        group.MapGet("/", async (HttpContext context, ITenantBrandingRepository repo) =>
        {
            var tenantId = context.GetTenantId();
            var items = await repo.GetByTenantAsync(tenantId);
            return Results.Ok(items.Select(MapDto));
        });

        group.MapGet("/{productType}", async (HttpContext context, ITenantBrandingRepository repo, string productType) =>
        {
            var tenantId = context.GetTenantId();
            var item = await repo.FindByTenantAndProductAsync(tenantId, productType);
            return item != null ? Results.Ok(MapDto(item)) : Results.NotFound();
        });

        group.MapPut("/", async (HttpContext context, ITenantBrandingRepository repo, UpsertBrandingDto request) =>
        {
            var tenantId = context.GetTenantId();
            var branding = new TenantBranding
            {
                TenantId = tenantId,
                ProductType = request.ProductType,
                BrandName = request.BrandName,
                LogoUrl = request.LogoUrl,
                PrimaryColor = request.PrimaryColor,
                SecondaryColor = request.SecondaryColor,
                AccentColor = request.AccentColor,
                TextColor = request.TextColor,
                BackgroundColor = request.BackgroundColor,
                ButtonRadius = request.ButtonRadius,
                FontFamily = request.FontFamily,
                SupportEmail = request.SupportEmail,
                SupportPhone = request.SupportPhone,
                WebsiteUrl = request.WebsiteUrl,
                EmailHeaderHtml = request.EmailHeaderHtml,
                EmailFooterHtml = request.EmailFooterHtml
            };
            var result = await repo.UpsertAsync(branding);
            return Results.Ok(MapDto(result));
        });

        group.MapGet("/resolved/{productType}", async (HttpContext context, IBrandingResolutionService service, string productType) =>
        {
            var tenantId = context.GetTenantId();
            var resolved = await service.ResolveAsync(tenantId, productType);
            return Results.Ok(resolved);
        });
    }

    private static BrandingDto MapDto(TenantBranding b) => new()
    {
        Id = b.Id, TenantId = b.TenantId, ProductType = b.ProductType, BrandName = b.BrandName,
        LogoUrl = b.LogoUrl, PrimaryColor = b.PrimaryColor, SecondaryColor = b.SecondaryColor,
        AccentColor = b.AccentColor, TextColor = b.TextColor, BackgroundColor = b.BackgroundColor,
        ButtonRadius = b.ButtonRadius, FontFamily = b.FontFamily, SupportEmail = b.SupportEmail,
        SupportPhone = b.SupportPhone, WebsiteUrl = b.WebsiteUrl,
        EmailHeaderHtml = b.EmailHeaderHtml, EmailFooterHtml = b.EmailFooterHtml,
        CreatedAt = b.CreatedAt, UpdatedAt = b.UpdatedAt
    };
}
