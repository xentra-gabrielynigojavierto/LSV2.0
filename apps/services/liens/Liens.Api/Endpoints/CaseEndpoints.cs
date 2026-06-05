using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class CaseEndpoints
{
    public static void MapCaseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/cases")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/", ListCases)
            .RequirePermission(LiensPermissions.CaseRead);

        group.MapGet("/{id:guid}", GetCaseById)
            .RequirePermission(LiensPermissions.CaseRead);

        group.MapGet("/by-number/{caseNumber}", GetCaseByCaseNumber)
            .RequirePermission(LiensPermissions.CaseRead);

        group.MapPost("/", CreateCase)
            .RequirePermission(LiensPermissions.CaseCreate);

        group.MapPut("/{id:guid}", UpdateCase)
            .RequirePermission(LiensPermissions.CaseUpdate);
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

    private static async Task<IResult> ListCases(
        ICaseService caseService,
        ICurrentRequestContext ctx,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await caseService.SearchAsync(tenantId, search, status, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCaseById(
        Guid id,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await caseService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Case '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetCaseByCaseNumber(
        string caseNumber,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await caseService.GetByCaseNumberAsync(tenantId, caseNumber, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Case with number '{caseNumber}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateCase(
        CreateCaseRequest request,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await caseService.CreateAsync(tenantId, orgId, userId, request, ct);
        return Results.Created($"/api/liens/cases/{result.Id}", result);
    }

    private static async Task<IResult> UpdateCase(
        Guid id,
        UpdateCaseRequest request,
        ICaseService caseService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await caseService.UpdateAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }
}
