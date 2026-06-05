using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class CapabilityEndpoints
{
    public static WebApplication MapCapabilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tenants/{tenantId:guid}/capabilities")
            .RequireAuthorization(Policies.AdminOnly);

        // ── List ──────────────────────────────────────────────────────────────

        group.MapGet("/", async (
            Guid tenantId,
            ICapabilityService service,
            CancellationToken ct) =>
        {
            var result = await service.ListByTenantAsync(tenantId, ct);
            return Results.Ok(result);
        });

        // ── Get by id ─────────────────────────────────────────────────────────

        group.MapGet("/{id:guid}", async (
            Guid tenantId,
            Guid id,
            ICapabilityService service,
            CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(result);
        });

        // ── Create ────────────────────────────────────────────────────────────

        group.MapPost("/", async (
            Guid tenantId,
            [FromBody] CreateCapabilityRequest request,
            ICapabilityService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(tenantId, request, ct);
            return Results.Created($"/api/tenants/{tenantId}/capabilities/{result.Id}", result);
        });

        // ── Update ────────────────────────────────────────────────────────────

        group.MapPut("/{id:guid}", async (
            Guid tenantId,
            Guid id,
            [FromBody] UpdateCapabilityRequest request,
            ICapabilityService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(tenantId, id, request, ct);
            return Results.Ok(result);
        });

        // ── Delete ────────────────────────────────────────────────────────────

        group.MapDelete("/{id:guid}", async (
            Guid tenantId,
            Guid id,
            ICapabilityService service,
            CancellationToken ct) =>
        {
            await service.DeleteAsync(tenantId, id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
