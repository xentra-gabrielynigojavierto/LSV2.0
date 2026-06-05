using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class ReferralNoteEndpoints
{
    public static void MapReferralNoteEndpoints(this WebApplication app)
    {
        app.MapGet("/api/referrals/{referralId:guid}/notes", async (
            Guid referralId,
            IReferralNoteService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var isAdmin  = ctx.IsPlatformAdmin || ctx.Roles.Contains(Roles.TenantAdmin, StringComparer.OrdinalIgnoreCase);

            var result = await service.GetByReferralAsync(tenantId, referralId, ctx.OrgId, isAdmin, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        app.MapPost("/api/referrals/{referralId:guid}/notes", async (
            Guid referralId,
            [FromBody] CreateReferralNoteRequest request,
            IReferralNoteService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.CreateAsync(tenantId, referralId, ctx.UserId, ctx.OrgId, request, ct);
            return Results.Created($"/api/referrals/{referralId}/notes/{result.Id}", result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        app.MapPut("/api/referral-notes/{id:guid}", async (
            Guid id,
            [FromBody] UpdateReferralNoteRequest request,
            IReferralNoteService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin)
        .RequireProductAccess(ProductCodes.SynqCareConnect);
    }
}
