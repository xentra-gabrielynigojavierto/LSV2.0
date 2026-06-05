using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class AvailabilityExceptionEndpoints
{
    public static void MapAvailabilityExceptionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/providers/{providerId:guid}/availability-exceptions", async (
            Guid providerId,
            bool? isActive,
            IAvailabilityExceptionService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.GetByProviderAsync(tenantId, providerId, isActive, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        app.MapPost("/api/providers/{providerId:guid}/availability-exceptions", async (
            Guid providerId,
            [FromBody] CreateAvailabilityExceptionRequest request,
            IAvailabilityExceptionService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.CreateAsync(tenantId, providerId, ctx.UserId, request, ct);
            return Results.Created($"/api/providers/{providerId}/availability-exceptions/{result.Id}", result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        app.MapPut("/api/availability-exceptions/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAvailabilityExceptionRequest request,
            IAvailabilityExceptionService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        app.MapPost("/api/providers/{providerId:guid}/slots/apply-exceptions", async (
            Guid providerId,
            IAvailabilityExceptionService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.ApplyExceptionsToSlotsAsync(tenantId, providerId, ctx.UserId, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);
    }
}
