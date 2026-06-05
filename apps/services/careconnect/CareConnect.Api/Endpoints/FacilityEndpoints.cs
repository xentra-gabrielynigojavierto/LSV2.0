using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class FacilityEndpoints
{
    public static void MapFacilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/facilities");

        group.MapGet("/", async (
            IFacilityService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var facilities = await service.GetAllAsync(tenantId, ct);
            return Results.Ok(facilities);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        group.MapPost("/", async (
            [FromBody] CreateFacilityRequest request,
            IFacilityService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var facility = await service.CreateAsync(tenantId, ctx.UserId, request, ct);
            return Results.Created($"/api/facilities/{facility.Id}", facility);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateFacilityRequest request,
            IFacilityService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var facility = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(facility);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);
    }
}
