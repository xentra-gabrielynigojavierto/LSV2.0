using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Comms.Application.DTOs;
using Comms.Application.Interfaces;
using Comms.Domain;

namespace Comms.Api.Endpoints;

public static class EmailTemplateEndpoints
{
    public static void MapEmailTemplateEndpoints(this WebApplication app)
    {
        app.MapPost("/api/comms/email/templates", CreateTemplate)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.EmailConfigManage);

        app.MapGet("/api/comms/email/templates", ListTemplates)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.EmailConfigManage);

        app.MapGet("/api/comms/email/templates/{id:guid}", GetTemplate)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.EmailConfigManage);

        app.MapPatch("/api/comms/email/templates/{id:guid}", UpdateTemplate)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(CommsPermissions.ProductCode)
            .RequirePermission(CommsPermissions.EmailConfigManage);
    }

    private static async Task<IResult> CreateTemplate(
        CreateEmailTemplateConfigRequest request,
        IEmailTemplateService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.CreateAsync(request, tenantId, userId, ct);
        return Results.Created($"/api/comms/email/templates/{result.Id}", result);
    }

    private static async Task<IResult> ListTemplates(
        IEmailTemplateService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var result = await service.ListAsync(tenantId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTemplate(
        Guid id,
        IEmailTemplateService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var result = await service.GetByIdAsync(id, tenantId, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> UpdateTemplate(
        Guid id,
        UpdateEmailTemplateConfigRequest request,
        IEmailTemplateService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.UpdateAsync(id, request, tenantId, userId, ct);
        return Results.Ok(result);
    }
}
