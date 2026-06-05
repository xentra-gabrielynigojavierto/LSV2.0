using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class ServiceOfferingEndpoints
{
    public static void MapServiceOfferingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/service-offerings");

        group.MapGet("/", async (
            IServiceOfferingService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var offerings = await service.GetAllAsync(tenantId, ct);
            return Results.Ok(offerings);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        group.MapPost("/", async (
            [FromBody] CreateServiceOfferingRequest request,
            IServiceOfferingService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var offering = await service.CreateAsync(tenantId, ctx.UserId, request, ct);
            return Results.Created($"/api/service-offerings/{offering.Id}", offering);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateServiceOfferingRequest request,
            IServiceOfferingService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var offering = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(offering);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);
    }
}
