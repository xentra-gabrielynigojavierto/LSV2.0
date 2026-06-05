using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.Authorization;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class AvailabilityTemplateEndpoints
{
    public static void MapAvailabilityTemplateEndpoints(this WebApplication app)
    {
        app.MapGet("/api/providers/{providerId:guid}/availability-templates", async (
            Guid providerId,
            IAvailabilityTemplateService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.ScheduleManage, ct);
            var templates = await service.GetByProviderAsync(tenantId, providerId, ct);
            return Results.Ok(templates);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        app.MapPost("/api/providers/{providerId:guid}/availability-templates", async (
            Guid providerId,
            [FromBody] CreateAvailabilityTemplateRequest request,
            IAvailabilityTemplateService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.ScheduleManage, ct);
            var template = await service.CreateAsync(tenantId, providerId, ctx.UserId, request, ct);
            return Results.Created($"/api/availability-templates/{template.Id}", template);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        app.MapPut("/api/availability-templates/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAvailabilityTemplateRequest request,
            IAvailabilityTemplateService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.ScheduleManage, ct);
            var template = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(template);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);
    }
}
