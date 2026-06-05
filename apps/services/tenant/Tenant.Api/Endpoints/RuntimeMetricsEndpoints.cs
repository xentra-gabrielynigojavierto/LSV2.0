using BuildingBlocks.Authorization;
using Microsoft.Extensions.Options;
using Tenant.Application.Configuration;
using Tenant.Application.Metrics;

namespace Tenant.Api.Endpoints;

/// <summary>
/// TENANT-B08 — Admin diagnostics endpoint for in-process runtime metrics.
/// TENANT-STABILIZATION — Extended with identity proxy counters and cutover check.
///
/// GET /api/v1/admin/runtime-metrics
///   Returns lifetime read, sync, cache, and identity-proxy counters.
///   Counters are process-memory only and reset on service restart.
///   The cutoverCheck object summarises B13 gate prerequisites visible from
///   the Tenant service perspective.
///   Requires PlatformAdmin role.
/// </summary>
public static class RuntimeMetricsEndpoints
{
    public static void MapRuntimeMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/runtime-metrics", (
            TenantRuntimeMetrics     metrics,
            IOptions<TenantFeatures> opts) =>
        {
            var s = metrics.Snapshot();
            var f = opts.Value;

            var totalIdentityProxyCalls =
                s.IdentityProxySessionSettingsOk   + s.IdentityProxySessionSettingsFail   +
                s.IdentityProxyRetryProvisioningOk + s.IdentityProxyRetryProvisioningFail +
                s.IdentityProxyRetryVerificationOk + s.IdentityProxyRetryVerificationFail;

            var totalIdentityProxyFails =
                s.IdentityProxySessionSettingsFail  +
                s.IdentityProxyRetryProvisioningFail +
                s.IdentityProxyRetryVerificationFail;

            return Results.Ok(new
            {
                startedAtUtc  = s.StartedAtUtc,
                uptimeSeconds = s.UptimeSeconds,
                branding = new
                {
                    attempted    = s.BrandingAttempted,
                    succeeded    = s.BrandingSucceeded,
                    failed       = s.BrandingFailed,
                    cacheHits    = s.BrandingCacheHits,
                    cacheMisses  = s.BrandingCacheMisses,
                    cacheHitRate = s.BrandingCacheHits + s.BrandingCacheMisses == 0
                        ? (double?)null
                        : Math.Round((double)s.BrandingCacheHits / (s.BrandingCacheHits + s.BrandingCacheMisses) * 100, 1),
                },
                resolution = new
                {
                    attempted    = s.ResolutionAttempted,
                    succeeded    = s.ResolutionSucceeded,
                    failed       = s.ResolutionFailed,
                    cacheHits    = s.ResolutionCacheHits,
                    cacheMisses  = s.ResolutionCacheMisses,
                    cacheHitRate = s.ResolutionCacheHits + s.ResolutionCacheMisses == 0
                        ? (double?)null
                        : Math.Round((double)s.ResolutionCacheHits / (s.ResolutionCacheHits + s.ResolutionCacheMisses) * 100, 1),
                },
                sync = new
                {
                    attempted = s.SyncAttemptsReceived,
                    succeeded = s.SyncSucceeded,
                    failed    = s.SyncFailed,
                    successRate = s.SyncAttemptsReceived == 0
                        ? (double?)null
                        : Math.Round((double)s.SyncSucceeded / s.SyncAttemptsReceived * 100, 1),
                },
                identityProxy = new
                {
                    sessionSettings = new
                    {
                        ok   = s.IdentityProxySessionSettingsOk,
                        fail = s.IdentityProxySessionSettingsFail,
                    },
                    retryProvisioning = new
                    {
                        ok   = s.IdentityProxyRetryProvisioningOk,
                        fail = s.IdentityProxyRetryProvisioningFail,
                    },
                    retryVerification = new
                    {
                        ok   = s.IdentityProxyRetryVerificationOk,
                        fail = s.IdentityProxyRetryVerificationFail,
                    },
                    totalCalls = totalIdentityProxyCalls,
                    totalFails = totalIdentityProxyFails,
                    proxyFailRate = totalIdentityProxyCalls == 0
                        ? (double?)null
                        : Math.Round((double)totalIdentityProxyFails / totalIdentityProxyCalls * 100, 1),
                },
                cacheConfig = new
                {
                    enabled    = f.TenantReadCachingEnabled,
                    ttlSeconds = f.TenantReadCacheTtlSeconds,
                },
                cutoverCheck = new
                {
                    branding = new
                    {
                        readSourceCanonical  = f.TenantBrandingReadSource.ToString(),
                        brandingProxyActive  = f.TenantBrandingReadSource.ToString() != "Identity",
                        note = "Identity branding reads: check /api/admin/branding-metrics on the web BFF for HybridFallback counters.",
                    },
                    resolution = new
                    {
                        readSourceCanonical = f.TenantResolutionReadSource.ToString(),
                        resolutionActive    = f.TenantResolutionReadSource.ToString() != "Identity",
                    },
                    identityProxyRouting = new
                    {
                        sessionSettingsRouted    = true,
                        retryProvisioningRouted  = true,
                        retryVerificationRouted  = true,
                        note = "All three CC→Identity operations now route via Tenant service proxy endpoints (TENANT-STABILIZATION).",
                    },
                    note = "B13 gate: branding/resolution read sources must be Tenant, " +
                           "proxy counters must be non-zero (proving traffic flows), " +
                           "proxy fail rate must be <5% over ≥7 days.",
                },
                note = "Counters are process-memory only and reset on service restart.",
            });
        })
        .RequireAuthorization(Policies.AdminOnly);
    }
}
