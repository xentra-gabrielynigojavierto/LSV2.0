using BuildingBlocks.Authorization;
using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Application.Metrics;

namespace Tenant.Api.Endpoints;

/// <summary>
/// TENANT-B11/B12/STABILIZATION — Admin-focused tenant management endpoints.
///
/// B11: GET list, GET detail, PATCH status.
/// B12: POST create (canonical Tenant-first creation),
///      POST entitlement toggle (Tenant-first, best-effort Identity sync).
/// STABILIZATION: Three Identity admin operations now proxied via Tenant service
///      so Control Center routes all tenant admin through a single owner:
///
///   PATCH  /api/v1/admin/tenants/{id}/session-settings  — proxy → Identity
///   POST   /api/v1/admin/tenants/{id}/provisioning/retry — proxy → Identity
///   POST   /api/v1/admin/tenants/{id}/verification/retry — proxy → Identity
///
/// Routes (all require AdminOnly):
///   GET    /api/v1/admin/tenants                                     — paged list
///   GET    /api/v1/admin/tenants/{id}                                — full detail
///   PATCH  /api/v1/admin/tenants/{id}/status                         — status update
///   PATCH  /api/v1/admin/tenants/{id}/session-settings               — proxy → Identity
///   POST   /api/v1/admin/tenants                                     — CANONICAL CREATE (B12)
///   POST   /api/v1/admin/tenants/{id}/entitlements/{productCode}     — entitlement toggle (B12)
///   POST   /api/v1/admin/tenants/{id}/provisioning/retry             — proxy → Identity
///   POST   /api/v1/admin/tenants/{id}/verification/retry             — proxy → Identity
/// </summary>
public static class TenantAdminEndpoints
{
    public static void MapTenantAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/tenants")
                       .RequireAuthorization(Policies.AdminOnly);

        // ── POST /api/v1/admin/tenants ─────────────────────────────────────────
        // TENANT-B12 — Canonical Tenant-first creation entry point.
        // Creates Tenant DB record first, then calls Identity for admin-user/org/provisioning.

        group.MapPost("/", async (
            AdminCreateTenantRequest body,
            ITenantAdminService      svc,
            CancellationToken        ct) =>
        {
            var result = await svc.CreateTenantAsync(body, ct);
            return Results.Created($"/api/v1/admin/tenants/{result.TenantId}", result);
        });

        // ── GET /api/v1/admin/tenants ──────────────────────────────────────────

        group.MapGet("/", async (
            ITenantAdminService svc,
            CancellationToken   ct,
            int  page     = 1,
            int  pageSize = 20) =>
        {
            var (items, total) = await svc.ListAdminAsync(page, pageSize, ct);
            return Results.Ok(new
            {
                items,
                totalCount = total,
                page,
                pageSize,
            });
        });

        // ── GET /api/v1/admin/tenants/{id} ────────────────────────────────────

        group.MapGet("/{id:guid}", async (
            Guid                id,
            ITenantAdminService svc,
            CancellationToken   ct) =>
        {
            var result = await svc.GetAdminDetailAsync(id, ct);
            if (result is null) throw new NotFoundException($"Tenant '{id}' was not found.");
            return Results.Ok(result);
        });

        // ── PATCH /api/v1/admin/tenants/{id}/status ───────────────────────────

        group.MapPatch("/{id:guid}/status", async (
            Guid                id,
            StatusUpdateRequest body,
            ITenantAdminService svc,
            CancellationToken   ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Status))
                return Results.BadRequest(new { error = "status is required." });

            var result = await svc.UpdateStatusAsync(id, body.Status, ct);
            return Results.Ok(result);
        });

        // ── PATCH /api/v1/admin/tenants/{id}/session-settings ─────────────────
        // TENANT-STABILIZATION — Proxy to Identity. Control Center routes this
        // through Tenant so Identity is not called directly by any CC client.

        group.MapPatch("/{id:guid}/session-settings", async (
            Guid                     id,
            SessionSettingsRequest   body,
            IIdentityCompatAdapter   compat,
            TenantRuntimeMetrics     metrics,
            CancellationToken        ct) =>
        {
            var ok = await compat.SetSessionTimeoutAsync(id, body.SessionTimeoutMinutes, ct);

            if (ok) metrics.IncrementIdentityProxySessionSettingsOk();
            else    metrics.IncrementIdentityProxySessionSettingsFail();

            return ok
                ? Results.Ok(new { tenantId = id, sessionTimeoutMinutes = body.SessionTimeoutMinutes })
                : Results.StatusCode(502);
        });

        // ── POST /api/v1/admin/tenants/{id}/entitlements/{productCode} ─────────
        // TENANT-B12 — Admin entitlement toggle (Tenant-first, best-effort Identity sync).
        // Response shape is compatible with control-center mapEntitlementResponse mapper.

        group.MapPost("/{id:guid}/entitlements/{productCode}", async (
            Guid                id,
            string              productCode,
            EntitlementToggleRequest body,
            ITenantAdminService svc,
            CancellationToken   ct) =>
        {
            if (string.IsNullOrWhiteSpace(productCode))
                return Results.BadRequest(new { error = "productCode is required." });

            var result = await svc.ToggleEntitlementAsync(id, productCode, body.Enabled, ct);
            return Results.Ok(result);
        });

        // ── POST /api/v1/admin/tenants/{id}/provisioning/retry ────────────────
        // TENANT-STABILIZATION — Proxy to Identity admin retry endpoint.

        group.MapPost("/{id:guid}/provisioning/retry", async (
            Guid                          id,
            IIdentityProvisioningAdapter  provisioning,
            TenantRuntimeMetrics          metrics,
            CancellationToken             ct) =>
        {
            var result = await provisioning.RetryProvisioningAsync(id, ct);

            var isTransportFailure = result.Error?.Contains("timed out") == true ||
                                     result.Error?.Contains("error:") == true;

            if (isTransportFailure)
            {
                metrics.IncrementIdentityProxyRetryProvisioningFail();
                return Results.StatusCode(502);
            }

            if (result.Success) metrics.IncrementIdentityProxyRetryProvisioningOk();
            else                metrics.IncrementIdentityProxyRetryProvisioningFail();

            return Results.Ok(new
            {
                success            = result.Success,
                provisioningStatus = result.ProvisioningStatus,
                hostname           = result.Hostname,
                error              = result.Error,
            });
        });

        // ── POST /api/v1/admin/tenants/{id}/verification/retry ────────────────
        // TENANT-STABILIZATION — Proxy to Identity admin retry endpoint.

        group.MapPost("/{id:guid}/verification/retry", async (
            Guid                          id,
            IIdentityProvisioningAdapter  provisioning,
            TenantRuntimeMetrics          metrics,
            CancellationToken             ct) =>
        {
            var result = await provisioning.RetryVerificationAsync(id, ct);

            var isTransportFailure = result.Error?.Contains("timed out") == true ||
                                     result.Error?.Contains("error:") == true;

            if (isTransportFailure)
            {
                metrics.IncrementIdentityProxyRetryVerificationFail();
                return Results.StatusCode(502);
            }

            if (result.Success) metrics.IncrementIdentityProxyRetryVerificationOk();
            else                metrics.IncrementIdentityProxyRetryVerificationFail();

            return Results.Ok(new
            {
                success            = result.Success,
                provisioningStatus = result.ProvisioningStatus,
                hostname           = result.Hostname,
                failureStage       = result.FailureStage,
                error              = result.Error,
            });
        });
    }

    private record StatusUpdateRequest(string? Status);
    private record EntitlementToggleRequest(bool Enabled);
    private record SessionSettingsRequest(int? SessionTimeoutMinutes);
}
