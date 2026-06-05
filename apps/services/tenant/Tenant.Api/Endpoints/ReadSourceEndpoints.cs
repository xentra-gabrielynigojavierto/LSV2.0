using BuildingBlocks.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tenant.Application.Configuration;
using Tenant.Application.Metrics;
using Tenant.Infrastructure.Data;

namespace Tenant.Api.Endpoints;

public static class ReadSourceEndpoints
{
    public static void MapReadSourceEndpoints(this IEndpointRouteBuilder app)
    {
        // ── GET /api/v1/admin/read-source ──────────────────────────────────────
        // Returns current read-source feature flag config for operators.
        // TENANT-B09: Emits a deprecation warning log if any path is still in Identity mode.
        app.MapGet("/api/v1/admin/read-source", (
            IOptions<TenantFeatures> opts,
            ILoggerFactory loggerFactory) =>
        {
            var f = opts.Value;
            var log = loggerFactory.CreateLogger("Tenant.Api.ReadSourceEndpoints");

            var effectiveBranding   = f.TenantBrandingReadSource.ToString();
            var effectiveResolution = f.TenantResolutionReadSource.ToString();

            // TENANT-B09: warn if any path still routes reads through Identity.
            if (f.TenantReadSource == TenantReadSource.Identity
                || f.TenantBrandingReadSource == TenantReadSource.Identity
                || f.TenantResolutionReadSource == TenantReadSource.Identity)
            {
                log.LogWarning(
                    "[DEPRECATION] Identity read-source mode is active. " +
                    "TenantReadSource={TenantReadSource} TenantBrandingReadSource={BrandingSource} " +
                    "TenantResolutionReadSource={ResolutionSource}. " +
                    "Switch to Tenant or HybridFallback. See TENANT-B09-report.md §4.",
                    f.TenantReadSource, f.TenantBrandingReadSource, f.TenantResolutionReadSource);
            }

            return Results.Ok(new
            {
                tenantReadSource            = f.TenantReadSource.ToString(),
                tenantBrandingReadSource    = f.TenantBrandingReadSource.ToString(),
                tenantResolutionReadSource  = f.TenantResolutionReadSource.ToString(),
                tenantDualWriteEnabled      = f.TenantDualWriteEnabled,
                tenantDualWriteStrictMode   = f.TenantDualWriteStrictMode,
                effectiveBrandingSource     = effectiveBranding,
                effectiveResolutionSource   = effectiveResolution,
                caching = new
                {
                    enabled    = f.TenantReadCachingEnabled,
                    ttlSeconds = f.TenantReadCacheTtlSeconds,
                },
            });
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── GET /api/v1/admin/cutover-check ────────────────────────────────────
        // TENANT-B07 / TENANT-B08 — Operator-facing cutover validation endpoint.
        // Returns current read-source config, latest migration run summary, tenant
        // counts, runtime metrics summary, and a readiness assessment.
        app.MapGet("/api/v1/admin/cutover-check", async (
            IOptions<TenantFeatures> opts,
            TenantDbContext          db,
            TenantRuntimeMetrics     metrics,
            CancellationToken        ct) =>
        {
            var f = opts.Value;

            var effectiveBranding   = f.TenantBrandingReadSource    != TenantReadSource.Identity
                ? f.TenantBrandingReadSource.ToString()
                : f.TenantReadSource.ToString();

            var effectiveResolution = f.TenantResolutionReadSource  != TenantReadSource.Identity
                ? f.TenantResolutionReadSource.ToString()
                : f.TenantReadSource.ToString();

            // ── Latest migration run ───────────────────────────────────────────
            var latestRun = await db.MigrationRuns
                .OrderByDescending(r => r.StartedAtUtc)
                .FirstOrDefaultAsync(ct);

            object? migrationSummary = null;
            if (latestRun is not null)
            {
                migrationSummary = new
                {
                    runId            = latestRun.Id,
                    mode             = latestRun.Mode,
                    startedAtUtc     = latestRun.StartedAtUtc,
                    completedAtUtc   = latestRun.CompletedAtUtc,
                    totalScanned     = latestRun.TotalScanned,
                    tenantsCreated   = latestRun.TenantsCreated,
                    tenantsUpdated   = latestRun.TenantsUpdated,
                    tenantsSkipped   = latestRun.TenantsSkipped,
                    errors           = latestRun.Errors,
                    durationMs       = latestRun.DurationMs,
                    errorMessage     = latestRun.ErrorMessage,
                };
            }

            // ── Tenant DB counts ───────────────────────────────────────────────
            var tenantCount          = await db.Tenants.CountAsync(ct);
            var activeTenantCount    = await db.Tenants.CountAsync(t => t.Status == Domain.TenantStatus.Active, ct);

            // ── Runtime metrics snapshot ───────────────────────────────────────
            var snap = metrics.Snapshot();
            var runtimeMetrics = new
            {
                uptimeSeconds  = snap.UptimeSeconds,
                sync = new
                {
                    attempted  = snap.SyncAttemptsReceived,
                    succeeded  = snap.SyncSucceeded,
                    failed     = snap.SyncFailed,
                },
                branding = new
                {
                    attempted    = snap.BrandingAttempted,
                    cacheHitRate = snap.BrandingCacheHits + snap.BrandingCacheMisses == 0
                        ? (double?)null
                        : Math.Round((double)snap.BrandingCacheHits / (snap.BrandingCacheHits + snap.BrandingCacheMisses) * 100, 1),
                },
                resolution = new
                {
                    attempted    = snap.ResolutionAttempted,
                    cacheHitRate = snap.ResolutionCacheHits + snap.ResolutionCacheMisses == 0
                        ? (double?)null
                        : Math.Round((double)snap.ResolutionCacheHits / (snap.ResolutionCacheHits + snap.ResolutionCacheMisses) * 100, 1),
                },
            };

            // ── Readiness assessment ───────────────────────────────────────────
            var migrationOk    = latestRun is not null
                                 && latestRun.Mode == "Execute"
                                 && latestRun.Errors == 0;
            var brandingReady  = effectiveBranding   == "Tenant";
            var resolveReady   = effectiveResolution  == "Tenant";
            var dualWriteReady = f.TenantDualWriteEnabled;
            var syncHealthy    = snap.SyncAttemptsReceived == 0 || snap.SyncFailed == 0;

            var readiness = (brandingReady && resolveReady && migrationOk && dualWriteReady && syncHealthy)
                            ? "ready"
                          : (migrationOk || brandingReady || resolveReady || dualWriteReady)
                            ? "partial"
                          : "not_ready";

            var notes = new List<string?>();
            if (!brandingReady)  notes.Add("Set TenantBrandingReadSource=Tenant (or use HybridFallback first) to enable Tenant-first branding.");
            if (!resolveReady)   notes.Add("Set TenantResolutionReadSource=Tenant (or use HybridFallback first) to enable Tenant-first resolution.");
            if (!migrationOk)    notes.Add("Run POST /api/admin/migration/execute to migrate all tenant records into the Tenant service.");
            if (!dualWriteReady) notes.Add("Enable TenantDualWriteEnabled=true to keep Tenant data in sync with Identity writes.");
            if (!syncHealthy)    notes.Add($"Sync failures detected this process lifetime: {snap.SyncFailed} of {snap.SyncAttemptsReceived} attempts failed.");

            return Results.Ok(new
            {
                readiness,
                config = new
                {
                    tenantReadSource           = f.TenantReadSource.ToString(),
                    effectiveBrandingSource    = effectiveBranding,
                    effectiveResolutionSource  = effectiveResolution,
                    tenantDualWriteEnabled     = f.TenantDualWriteEnabled,
                    tenantDualWriteStrictMode  = f.TenantDualWriteStrictMode,
                    caching = new
                    {
                        enabled    = f.TenantReadCachingEnabled,
                        ttlSeconds = f.TenantReadCacheTtlSeconds,
                    },
                },
                tenantDb = new
                {
                    totalTenants  = tenantCount,
                    activeTenants = activeTenantCount,
                    note          = "Identity tenant count not queryable from this service; compare manually if needed.",
                },
                runtimeMetrics,
                migrationSummary,
                notes = notes.Where(n => n is not null).ToArray(),
            });
        })
        .RequireAuthorization(Policies.AdminOnly);
    }
}
