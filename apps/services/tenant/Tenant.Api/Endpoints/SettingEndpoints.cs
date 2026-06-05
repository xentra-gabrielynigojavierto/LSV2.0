using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class SettingEndpoints
{
    public static WebApplication MapSettingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tenants/{tenantId:guid}/settings")
            .RequireAuthorization(Policies.AdminOnly);

        // ── List ──────────────────────────────────────────────────────────────

        group.MapGet("/", async (
            Guid tenantId,
            ISettingService service,
            CancellationToken ct) =>
        {
            var result = await service.ListByTenantAsync(tenantId, ct);
            return Results.Ok(result);
        });

        // ── Get by id ─────────────────────────────────────────────────────────

        group.MapGet("/{id:guid}", async (
            Guid tenantId,
            Guid id,
            ISettingService service,
            CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(result);
        });

        // ── Upsert ────────────────────────────────────────────────────────────

        group.MapPut("/", async (
            Guid tenantId,
            [FromBody] UpsertSettingRequest request,
            ISettingService service,
            CancellationToken ct) =>
        {
            var result = await service.UpsertAsync(tenantId, request, ct);
            return Results.Ok(result);
        });

        // ── Delete ────────────────────────────────────────────────────────────

        group.MapDelete("/{id:guid}", async (
            Guid tenantId,
            Guid id,
            ISettingService service,
            CancellationToken ct) =>
        {
            await service.DeleteAsync(tenantId, id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
