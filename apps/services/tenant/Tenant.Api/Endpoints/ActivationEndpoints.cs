using BuildingBlocks.Authorization;
using BuildingBlocks.Exceptions;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Api.Endpoints;

/// <summary>
/// BLK-TS-02 — Tenant Infrastructure &amp; Activation endpoints.
///
/// GET  /api/v1/tenants/{id}/provisioning-status        — provisioning state (admin)
/// GET  /api/v1/tenants/{id}/products                   — product entitlement list (admin)
/// POST /api/v1/tenants/{id}/products/{productCode}/activate — idempotent product activation (admin)
/// </summary>
public static class ActivationEndpoints
{
    public static void MapActivationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants/{id:guid}")
                       .RequireAuthorization(Policies.AdminOnly);

        // ── GET /api/v1/tenants/{id}/provisioning-status ──────────────────────
        //
        // Returns the Tenant-owned provisioning state (Unknown/InProgress/Provisioned/Failed)
        // plus provisionedAtUtc and lastProvisioningError when applicable.

        group.MapGet("/provisioning-status", async (
            Guid                id,
            ITenantService      svc,
            CancellationToken   ct) =>
        {
            var tenant = await svc.GetByIdAsync(id, ct);
            if (tenant is null)
                throw new NotFoundException($"Tenant '{id}' was not found.");

            return Results.Ok(new
            {
                tenantId             = tenant.Id,
                tenantCode           = tenant.Code,
                provisioningStatus   = tenant.ProvisioningStatus,
                provisionedAtUtc     = tenant.ProvisionedAtUtc,
                lastProvisioningError = tenant.LastProvisioningError,
            });
        });

        // ── GET /api/v1/tenants/{id}/products ────────────────────────────────
        //
        // Lists all product entitlements for the tenant.
        // Includes CareConnect and any other product keys.

        group.MapGet("/products", async (
            Guid                  id,
            IEntitlementService   svc,
            CancellationToken     ct) =>
        {
            var items = await svc.ListByTenantAsync(id, ct);
            var products = items.Select(e => new
            {
                productKey         = e.ProductKey,
                productDisplayName = e.ProductDisplayName,
                isActive           = e.IsEnabled,
                isDefault          = e.IsDefault,
                planCode           = e.PlanCode,
                effectiveFromUtc   = e.EffectiveFromUtc,
                effectiveToUtc     = e.EffectiveToUtc,
            });

            return Results.Ok(new { tenantId = id, products });
        });

        // ── POST /api/v1/tenants/{id}/products/{productCode}/activate ─────────
        //
        // Idempotent product activation.
        // - Creates the entitlement if it does not exist (IsEnabled = true).
        // - Enables it if it exists but is disabled.
        // - Returns current state without error if already active.
        //
        // Canonical product codes (normalized to lowercase):
        //   synq_careconnect, liens, task, …

        group.MapPost("/products/{productCode}/activate", async (
            Guid                  id,
            string                productCode,
            IEntitlementService   svc,
            CancellationToken     ct) =>
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return Results.BadRequest(new { error = "productCode is required." });

            var result = await svc.ActivateProductAsync(id, productCode, null, ct);

            return Results.Ok(new
            {
                tenantId           = id,
                productKey         = result.ProductKey,
                productDisplayName = result.ProductDisplayName,
                isActive           = result.IsEnabled,
                effectiveFromUtc   = result.EffectiveFromUtc,
                activatedAtUtc     = result.EffectiveFromUtc,
            });
        });
    }
}
