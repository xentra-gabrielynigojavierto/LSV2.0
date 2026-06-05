using BuildingBlocks.Authorization;
using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class DomainEndpoints
{
    public static void MapDomainEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants/{tenantId:guid}/domains");

        // ── GET /api/v1/tenants/{tenantId}/domains ────────────────────────────
        group.MapGet("/", async (
            Guid            tenantId,
            IDomainService  svc,
            CancellationToken ct) =>
        {
            var items = await svc.ListByTenantAsync(tenantId, ct);
            return Results.Ok(new { items, total = items.Count });
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── POST /api/v1/tenants/{tenantId}/domains ───────────────────────────
        group.MapPost("/", async (
            Guid                 tenantId,
            CreateDomainRequest  request,
            IDomainService       svc,
            CancellationToken    ct) =>
        {
            var result = await svc.CreateAsync(tenantId, request, ct);
            return Results.Created($"/api/v1/tenants/{tenantId}/domains/{result.Id}", result);
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── PUT /api/v1/tenants/{tenantId}/domains/{domainId} ─────────────────
        group.MapPut("/{domainId:guid}", async (
            Guid                 tenantId,
            Guid                 domainId,
            UpdateDomainRequest  request,
            IDomainService       svc,
            CancellationToken    ct) =>
        {
            var result = await svc.UpdateAsync(tenantId, domainId, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── DELETE /api/v1/tenants/{tenantId}/domains/{domainId} ─────────────
        // Soft deactivation — sets Status = Inactive.
        group.MapDelete("/{domainId:guid}", async (
            Guid             tenantId,
            Guid             domainId,
            IDomainService   svc,
            CancellationToken ct) =>
        {
            await svc.DeactivateAsync(tenantId, domainId, ct);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly);
    }
}
