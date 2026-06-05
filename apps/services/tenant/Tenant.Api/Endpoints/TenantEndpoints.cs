using BuildingBlocks.Authorization;
using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants");

        // ── GET /api/v1/tenants ───────────────────────────────────────────────
        group.MapGet("/", async (
            ITenantService svc,
            CancellationToken ct,
            int page     = 1,
            int pageSize = 20) =>
        {
            var (items, total) = await svc.ListAsync(page, pageSize, ct);
            return Results.Ok(new
            {
                items,
                total,
                page,
                pageSize
            });
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── GET /api/v1/tenants/{id} ──────────────────────────────────────────
        group.MapGet("/{id:guid}", async (
            Guid id,
            ITenantService svc,
            CancellationToken ct) =>
        {
            var result = await svc.GetByIdAsync(id, ct);
            if (result is null) throw new NotFoundException($"Tenant '{id}' was not found.");
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── GET /api/v1/tenants/by-code/{code} ───────────────────────────────
        group.MapGet("/by-code/{code}", async (
            string code,
            ITenantService svc,
            CancellationToken ct) =>
        {
            var result = await svc.GetByCodeAsync(code, ct);
            if (result is null) throw new NotFoundException($"Tenant with code '{code}' was not found.");
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── POST /api/v1/tenants ──────────────────────────────────────────────
        group.MapPost("/", async (
            CreateTenantRequest request,
            ITenantService svc,
            CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(request, ct);
            return Results.Created($"/api/v1/tenants/{result.Id}", result);
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── PUT /api/v1/tenants/{id} ──────────────────────────────────────────
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateTenantRequest request,
            ITenantService svc,
            CancellationToken ct) =>
        {
            var result = await svc.UpdateAsync(id, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── DELETE /api/v1/tenants/{id} — soft delete (deactivates) ──────────
        group.MapDelete("/{id:guid}", async (
            Guid id,
            ITenantService svc,
            CancellationToken ct) =>
        {
            await svc.DeactivateAsync(id, ct);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly);
    }
}
