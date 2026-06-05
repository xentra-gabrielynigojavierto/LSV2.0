using BuildingBlocks.Authorization;
using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class BrandingEndpoints
{
    public static void MapBrandingEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Authenticated branding management ─────────────────────────────────

        var admin = app.MapGroup("/api/v1/tenants/{tenantId:guid}/branding");

        // GET /api/v1/tenants/{tenantId}/branding
        admin.MapGet("/", async (
            Guid             tenantId,
            IBrandingService svc,
            CancellationToken ct) =>
        {
            var result = await svc.GetByTenantIdAsync(tenantId, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly);

        // PUT /api/v1/tenants/{tenantId}/branding
        admin.MapPut("/", async (
            Guid                   tenantId,
            UpdateBrandingRequest  request,
            IBrandingService       svc,
            CancellationToken      ct) =>
        {
            var result = await svc.UpsertAsync(tenantId, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── Public branding endpoints (unauthenticated) ───────────────────────

        var pub = app.MapGroup("/api/v1/public/branding");

        // GET /api/v1/public/branding/by-code/{code}
        pub.MapGet("/by-code/{code}", async (
            string           code,
            IBrandingService svc,
            CancellationToken ct) =>
        {
            var result = await svc.GetPublicByCodeAsync(code, ct);
            if (result is null) return Results.NotFound(new { error = new { code = "not_found", message = $"No active tenant with code '{code}' was found." } });
            return Results.Ok(result);
        })
        .AllowAnonymous();

        // GET /api/v1/public/branding/by-subdomain/{subdomain}
        pub.MapGet("/by-subdomain/{subdomain}", async (
            string           subdomain,
            IBrandingService svc,
            CancellationToken ct) =>
        {
            var result = await svc.GetPublicBySubdomainAsync(subdomain, ct);
            if (result is null) return Results.NotFound(new { error = new { code = "not_found", message = $"No active tenant with subdomain '{subdomain}' was found." } });
            return Results.Ok(result);
        })
        .AllowAnonymous();
    }
}
