// BLK-TS-01 — Tenant Core Foundation endpoints.
// BLK-CC-01 — Provision endpoint now accepts X-Provisioning-Token for service-to-service calls
//             (e.g. CareConnect → Tenant service during provider onboarding).
//
// GET  /api/v1/tenants/check-code  — validate and check uniqueness of a tenant code (anonymous)
// POST /api/v1/tenants/provision   — minimal tenant creation (admin JWT OR X-Provisioning-Token)
using BuildingBlocks.Authorization;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class ProvisionEndpoints
{
    public static void MapProvisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants");

        // ── GET /api/v1/tenants/check-code?code=acme ─────────────────────────
        //
        // Public: no auth required — reveals only availability, no sensitive data.
        // Normalizes input, validates format, checks uniqueness.
        //
        // 200 { available: true,  normalizedCode: "acme" }
        // 200 { available: false, normalizedCode: "acme", error: "..." }
        group.MapGet("/check-code", async (
            string?          code,
            ITenantService   svc,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new
                {
                    error = new { code = "invalid_input", message = "The 'code' query parameter is required." }
                });

            var result = await svc.CheckCodeAsync(code, ct);
            return Results.Ok(result);
        })
        .AllowAnonymous();

        // ── POST /api/v1/tenants/provision ────────────────────────────────────
        //
        // Auth: accepts either:
        //   a) JWT with PlatformAdmin role (admin portal), OR
        //   b) X-Provisioning-Token header matching TenantService:ProvisioningSecret (service-to-service)
        //
        // In dev mode (ProvisioningSecret empty), the token check is skipped.
        //
        // Minimal provision: tenantName + tenantCode → canonical tenant record.
        // Subdomain defaults to the normalized code.
        // Does NOT create users, Identity memberships, DNS, or product entitlements.
        //
        // 201 { tenantId, tenantCode, subdomain }
        // 401 missing/invalid auth
        // 409 duplicate code / subdomain
        // 422 invalid code format
        group.MapPost("/provision", async (
            HttpContext       httpContext,
            ProvisionRequest  request,
            ITenantService    svc,
            IConfiguration    configuration,
            ILoggerFactory    loggerFactory,
            CancellationToken ct) =>
        {
            var log = loggerFactory.CreateLogger("Tenant.Api.ProvisionEndpoints");

            // Accept admin JWT (already authenticated via RequireAuthorization below)
            // OR a valid X-Provisioning-Token for service-to-service calls.
            if (!IsAuthorized(httpContext, configuration, log))
                return Results.Unauthorized();

            var result = await svc.ProvisionAsync(request, ct);
            return Results.Created($"/api/v1/tenants/{result.TenantId}", result);
        })
        .AllowAnonymous();  // token check is manual above; AdminOnly JWT is checked inside IsAuthorized

        // ── GET /api/v1/tenants/{id}/subdomain ────────────────────────────────
        //
        // Service-to-service: returns the subdomain slug for a given tenant.
        // Used by CareConnect to build tenant-branded email links.
        //
        // Auth: X-Provisioning-Token (same secret as /provision) OR admin JWT.
        // Dev mode (ProvisioningSecret empty): allowed without token.
        //
        // 200 { subdomain: "acme" }
        // 401 missing/invalid auth
        // 404 tenant not found
        group.MapGet("/{id:guid}/subdomain", async (
            Guid            id,
            HttpContext      httpContext,
            ITenantService   svc,
            IConfiguration   configuration,
            ILoggerFactory   loggerFactory,
            CancellationToken ct) =>
        {
            var log = loggerFactory.CreateLogger("Tenant.Api.ProvisionEndpoints");
            if (!IsAuthorized(httpContext, configuration, log))
                return Results.Unauthorized();

            var result = await svc.GetByIdAsync(id, ct);
            if (result is null) return Results.NotFound();
            return Results.Ok(new { subdomain = result.Subdomain });
        })
        .AllowAnonymous();
    }

    // ── Auth helper ───────────────────────────────────────────────────────────
    //
    // Accepts either:
    //  1. A valid admin JWT (user is authenticated + has PlatformAdmin role)
    //  2. A valid X-Provisioning-Token header (service-to-service)
    //  3. Dev mode: ProvisioningSecret is empty → skip token check
    //
    // If neither condition is met, returns false (→ 401).
    private static bool IsAuthorized(
        HttpContext    httpContext,
        IConfiguration configuration,
        ILogger        log)
    {
        // Path 1: Valid admin JWT
        if (httpContext.User.Identity?.IsAuthenticated == true &&
            httpContext.User.IsInRole(Roles.PlatformAdmin))
        {
            return true;
        }

        // Path 2: Provisioning token (service-to-service)
        var secret        = configuration["TenantService:ProvisioningSecret"];
        var incomingToken = httpContext.Request.Headers["X-Provisioning-Token"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(secret))
        {
            // Dev mode — skip token check, allow the call
            log.LogDebug("[ProvisionEndpoints] ProvisioningSecret not configured — allowing call in dev mode.");
            return true;
        }

        if (!string.Equals(incomingToken, secret, StringComparison.Ordinal))
        {
            log.LogWarning(
                "[ProvisionEndpoints] /provision rejected — invalid or missing X-Provisioning-Token " +
                "from {RemoteIp}.", httpContext.Connection.RemoteIpAddress);
            return false;
        }

        return true;
    }
}
