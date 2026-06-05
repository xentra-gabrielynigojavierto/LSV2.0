// LSCC-009: Admin activation queue endpoints.
// All endpoints require PlatformOrTenantAdmin authorization.
//
// GET  /api/admin/activations          — list pending activation requests
// GET  /api/admin/activations/{id}     — detail for one activation request
// POST /api/admin/activations/{id}/approve — approve + link provider to org
//
// BLK-GOV-02: Uses AdminTenantScope helpers — replaces inline manual tenant checks.
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class ActivationAdminEndpoints
{
    public static IEndpointRouteBuilder MapActivationAdminEndpoints(
        this IEndpointRouteBuilder routes)
    {
        // GET /api/admin/activations
        routes
            .MapGet("/api/admin/activations", GetPendingAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        // GET /api/admin/activations/{id}
        routes
            .MapGet("/api/admin/activations/{id:guid}", GetByIdAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        // POST /api/admin/activations/{id}/approve
        routes
            .MapPost("/api/admin/activations/{id:guid}/approve", ApproveAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        return routes;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    // BLK-GOV-02: AdminTenantScope.PlatformWide — PlatformAdmin sees all; TenantAdmin filtered.
    private static async Task<IResult> GetPendingAsync(
        IActivationRequestService service,
        ICurrentRequestContext    ctx,
        HttpContext               http,
        CancellationToken         ct)
    {
        var scope = AdminTenantScope.PlatformWide(ctx, http);
        if (scope.IsError) return scope.Error!;

        var items = await service.GetPendingAsync(ct);

        // Non-PlatformAdmin: filter to caller's tenant only.
        if (!scope.IsPlatformWide)
            items = items.Where(i => i.TenantId == scope.TenantId!.Value).ToList();

        return Results.Ok(new { items, count = items.Count });
    }

    // BLK-GOV-02: AdminTenantScope.CheckOwnership — TenantAdmin may only retrieve their own tenant's requests.
    private static async Task<IResult> GetByIdAsync(
        Guid                      id,
        IActivationRequestService service,
        ICurrentRequestContext    ctx,
        HttpContext               http,
        CancellationToken         ct)
    {
        var detail = await service.GetByIdAsync(id, ct);
        if (detail is null)
            return Results.NotFound(new { error = $"ActivationRequest '{id}' was not found." });

        var deny = AdminTenantScope.CheckOwnership(ctx, detail.TenantId, http);
        if (deny is not null) return deny;

        return Results.Ok(detail);
    }

    // BLK-GOV-02: AdminTenantScope.CheckOwnership — TenantAdmin may only approve their own tenant's requests.
    private static async Task<IResult> ApproveAsync(
        Guid                      id,
        [FromBody] ApproveActivationRequest request,
        IActivationRequestService service,
        ICurrentRequestContext    ctx,
        HttpContext               http,
        CancellationToken         ct)
    {
        if (request.OrganizationId == Guid.Empty)
            return Results.BadRequest(new { error = "organizationId is required and must be a valid GUID." });

        // For non-PlatformAdmin: verify the activation request belongs to the caller's tenant
        // before executing the approval.
        if (!ctx.IsPlatformAdmin)
        {
            var detail = await service.GetByIdAsync(id, ct);
            if (detail is null)
                return Results.NotFound(new { error = $"ActivationRequest '{id}' was not found." });

            var deny = AdminTenantScope.CheckOwnership(ctx, detail.TenantId, http);
            if (deny is not null) return deny;
        }

        var result = await service.ApproveAsync(id, request.OrganizationId, ctx.UserId, ct);
        return Results.Ok(result);
    }
}
