using Identity.Application;
using Identity.Application.DTOs;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Endpoints;

public static class TenantBrandingEndpoints
{
    public static void MapTenantBrandingEndpoints(this WebApplication app)
    {
        // ── GET /api/tenants/current/branding ────────────────────────────────
        // Anonymous — must never require auth (the login page loads this before auth).
        //
        // DEPRECATED [TENANT-B09]: This endpoint is now the fallback-only path.
        //
        // The Tenant service (GET /tenant/api/v1/public/branding/by-code/{code}) is the
        // authoritative read source as of TENANT-B09. This endpoint remains active for:
        //   1. HybridFallback mode — called when the Tenant service is unavailable.
        //   2. Rollback — when TENANT_BRANDING_READ_SOURCE is reverted to Identity.
        //   3. Login page safety net — the login page must always render.
        //
        // Removal target: after ≥30 days of Tenant-primary production with zero Identity
        // fallback triggers in logs. See TENANT-B09-report.md §9.
        //
        // Tenant resolution priority:
        //   1. X-Tenant-Code header  — sent by Next.js in dev (NEXT_PUBLIC_TENANT_CODE)
        //   2. Host header           — subdomain-based, production only
        //      e.g. "firm-a.legalsynq.com" → TenantDomains lookup
        app.MapGet("/api/tenants/current/branding", async (
            HttpContext httpContext,
            ITenantRepository tenantRepository,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var log = loggerFactory.CreateLogger("Identity.Api.TenantBrandingEndpoints");
            log.LogWarning(
                "[DEPRECATED] Identity branding endpoint invoked. " +
                "This path is now fallback-only. Preferred: Tenant service /api/v1/public/branding/by-code/{code}.");

            // Emit deprecation header so gateway/BFF/clients can detect legacy path usage.
            httpContext.Response.Headers["X-Deprecated"] = "true";
            httpContext.Response.Headers["X-Deprecated-By"] = "TENANT-B09";
            httpContext.Response.Headers["X-Preferred-Endpoint"] = "/tenant/api/v1/public/branding/by-code/{code}";

            var tenant = await ResolveTenantAsync(httpContext, tenantRepository, ct);

            if (tenant is null || !tenant.IsActive)
            {
                // Return a safe default rather than 404 — the login page must always render
                return Results.Ok(new TenantBrandingResponse(
                    TenantId:       string.Empty,
                    TenantCode:     string.Empty,
                    DisplayName:    "LegalSynq",
                    LogoUrl:        null,
                    LogoDocumentId: null,
                    LogoWhiteDocumentId: null,
                    PrimaryColor:   null,
                    FaviconUrl:     null));
            }

            return Results.Ok(new TenantBrandingResponse(
                TenantId:       tenant.Id.ToString(),
                TenantCode:     tenant.Code,
                DisplayName:    tenant.Name,
                LogoUrl:        null,
                LogoDocumentId: tenant.LogoDocumentId?.ToString(),
                LogoWhiteDocumentId: tenant.LogoWhiteDocumentId?.ToString(),
                PrimaryColor:   null,
                FaviconUrl:     null));
        })
        .AllowAnonymous();
    }

    private static async Task<Identity.Domain.Tenant?> ResolveTenantAsync(
        HttpContext httpContext,
        ITenantRepository tenantRepository,
        CancellationToken ct)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Tenant-Code", out var tenantCodeHeader))
        {
            var code = tenantCodeHeader.ToString().Trim();
            if (!string.IsNullOrEmpty(code))
            {
                var tenant = await tenantRepository.GetByCodeAsync(code, ct);
                if (tenant is not null) return tenant;

                var upper = code.ToUpperInvariant();
                if (upper != code)
                {
                    tenant = await tenantRepository.GetByCodeAsync(upper, ct);
                    if (tenant is not null) return tenant;
                }

                tenant = await tenantRepository.GetBySubdomainAsync(code, ct);
                if (tenant is not null) return tenant;
            }
        }

        // Priority 2: resolve from Host header via TenantDomains table (production)
        var host = httpContext.Request.Headers["X-Forwarded-Host"].FirstOrDefault()
            ?? httpContext.Request.Host.Host;

        if (!string.IsNullOrEmpty(host))
            return await tenantRepository.GetByHostAsync(host, ct);

        return null;
    }
}
