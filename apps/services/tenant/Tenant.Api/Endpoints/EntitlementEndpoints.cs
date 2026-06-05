using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class EntitlementEndpoints
{
    public static WebApplication MapEntitlementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tenants/{tenantId:guid}/entitlements")
            .RequireAuthorization(Policies.AdminOnly);

        // ── List ──────────────────────────────────────────────────────────────

        group.MapGet("/", async (
            Guid tenantId,
            IEntitlementService service,
            CancellationToken ct) =>
        {
            var result = await service.ListByTenantAsync(tenantId, ct);
            return Results.Ok(result);
        });

        // ── Get by id ─────────────────────────────────────────────────────────

        group.MapGet("/{id:guid}", async (
            Guid tenantId,
            Guid id,
            IEntitlementService service,
            CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(result);
        });

        // ── Create ────────────────────────────────────────────────────────────

        group.MapPost("/", async (
            Guid tenantId,
            [FromBody] CreateEntitlementRequest request,
            IEntitlementService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(tenantId, request, ct);
            return Results.Created($"/api/tenants/{tenantId}/entitlements/{result.Id}", result);
        });

        // ── Update ────────────────────────────────────────────────────────────

        group.MapPut("/{id:guid}", async (
            Guid tenantId,
            Guid id,
            [FromBody] UpdateEntitlementRequest request,
            IEntitlementService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(tenantId, id, request, ct);
            return Results.Ok(result);
        });

        // ── Set default ───────────────────────────────────────────────────────

        group.MapPut("/{id:guid}/default", async (
            Guid tenantId,
            Guid id,
            IEntitlementService service,
            CancellationToken ct) =>
        {
            var result = await service.SetDefaultAsync(tenantId, id, ct);
            return Results.Ok(result);
        });

        // ── Delete ────────────────────────────────────────────────────────────

        group.MapDelete("/{id:guid}", async (
            Guid tenantId,
            Guid id,
            IEntitlementService service,
            CancellationToken ct) =>
        {
            await service.DeleteAsync(tenantId, id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
