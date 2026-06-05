using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class ContactEndpoints
{
    public static void MapContactEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/contacts")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/", ListContacts)
            .RequirePermission(LiensPermissions.LienService);

        group.MapGet("/{id:guid}", GetContactById)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPost("/", CreateContact)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPut("/{id:guid}", UpdateContact)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPut("/{id:guid}/deactivate", DeactivateContact)
            .RequirePermission(LiensPermissions.LienService);

        group.MapPut("/{id:guid}/reactivate", ReactivateContact)
            .RequirePermission(LiensPermissions.LienService);
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

    private static async Task<IResult> ListContacts(
        IContactService contactService,
        ICurrentRequestContext ctx,
        string? search = null,
        string? contactType = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await contactService.SearchAsync(
            tenantId, search, contactType, isActive, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetContactById(
        Guid id,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await contactService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Contact '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateContact(
        CreateContactRequest request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await contactService.CreateAsync(tenantId, orgId, userId, request, ct);
        return Results.Created($"/api/liens/contacts/{result.Id}", result);
    }

    private static async Task<IResult> UpdateContact(
        Guid id,
        UpdateContactRequest request,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await contactService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeactivateContact(
        Guid id,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await contactService.DeactivateAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ReactivateContact(
        Guid id,
        IContactService contactService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await contactService.ReactivateAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }
}
