using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Domain;

namespace Comms.Api.Endpoints;

public static class SenderConfigEndpoints
{
    public static void MapSenderConfigEndpoints(this WebApplication app)
    {
        app.MapPost("/api/comms/email/sender-configs", CreateSenderConfig)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.EmailConfigManage);

        app.MapGet("/api/comms/email/sender-configs", ListSenderConfigs)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.EmailConfigManage);

        app.MapGet("/api/comms/email/sender-configs/{id:guid}", GetSenderConfig)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.EmailConfigManage);

        app.MapPatch("/api/comms/email/sender-configs/{id:guid}", UpdateSenderConfig)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.EmailConfigManage);
    }

    private static async Task<IResult> CreateSenderConfig(
        CreateTenantEmailSenderConfigRequest request,
        ISenderConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.CreateAsync(request, tenantId, userId, ct);
        return Results.Created($"/api/comms/email/sender-configs/{result.Id}", result);
    }

    private static async Task<IResult> ListSenderConfigs(
        ISenderConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var result = await service.ListAsync(tenantId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSenderConfig(
        Guid id,
        ISenderConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var result = await service.GetByIdAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> UpdateSenderConfig(
        Guid id,
        UpdateTenantEmailSenderConfigRequest request,
        ISenderConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.UpdateAsync(id, request, tenantId, userId, ct);
        return Results.Ok(result);
    }
}
