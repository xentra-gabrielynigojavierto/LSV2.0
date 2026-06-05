using Tenant.Application.Metrics;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

/// <summary>
/// TENANT-B07 — Internal sync endpoint.
///
/// Receives dual-write events from the Identity service and upserts them
/// into the Tenant service's own store.
///
/// Route:  POST /api/internal/tenant-sync/upsert
/// Auth:   X-Sync-Token header must match TenantService:SyncSecret config value.
///         When SyncSecret is empty/unset, the token check is skipped (dev mode).
///
/// This route is NOT exposed via the YARP gateway — no gateway route exists for
/// /api/internal/* paths, so it is reachable only by services on the internal
/// network (i.e. Identity calling directly on the Tenant service port 5005).
///
/// TENANT-B08: Emits TenantRuntimeMetrics on each attempt/success/failure and
/// evicts public read cache entries for the synced tenant.
/// </summary>
public static class SyncEndpoints
{
    public static void MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/internal/tenant-sync/upsert", async (
            HttpContext           httpContext,
            SyncUpsertBody        body,
            ITenantService        tenantSvc,
            IBrandingService      brandingSvc,
            IResolutionService    resolutionSvc,
            TenantRuntimeMetrics  metrics,
            IConfiguration        configuration,
            ILoggerFactory        loggerFactory,
            CancellationToken     ct) =>
        {
            var log = loggerFactory.CreateLogger("Tenant.Api.SyncEndpoints");

            // ── Token guard ────────────────────────────────────────────────────
            var syncSecret    = configuration.GetValue<string>("TenantService:SyncSecret");
            var incomingToken = httpContext.Request.Headers["X-Sync-Token"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(syncSecret) &&
                !string.Equals(incomingToken, syncSecret, StringComparison.Ordinal))
            {
                log.LogWarning(
                    "[TenantSync] Rejected — invalid or missing X-Sync-Token from {RemoteIp}",
                    httpContext.Connection.RemoteIpAddress);
                return Results.Unauthorized();
            }

            // ── Validate payload ───────────────────────────────────────────────
            if (body.TenantId == Guid.Empty)
                return Results.BadRequest(new { error = "tenantId is required." });

            if (string.IsNullOrWhiteSpace(body.Code))
                return Results.BadRequest(new { error = "code is required." });

            if (string.IsNullOrWhiteSpace(body.DisplayName))
                return Results.BadRequest(new { error = "displayName is required." });

            log.LogInformation(
                "[TenantSync] Received {EventType} for TenantId={TenantId} Code={Code}",
                body.EventType ?? "Update",
                body.TenantId,
                body.Code);

            metrics.IncrementSyncAttempted();

            // ── Upsert via Tenant service ──────────────────────────────────────
            try
            {
                await tenantSvc.UpsertFromSyncAsync(new TenantSyncRequest(
                    TenantId:            body.TenantId,
                    Code:                body.Code,
                    DisplayName:         body.DisplayName,
                    Status:              body.Status ?? "Active",
                    Subdomain:           body.Subdomain,
                    LogoDocumentId:      body.LogoDocumentId,
                    LogoWhiteDocumentId: body.LogoWhiteDocumentId,
                    SourceCreatedAtUtc:  body.SourceCreatedAtUtc,
                    SourceUpdatedAtUtc:  body.SourceUpdatedAtUtc,
                    EventType:           body.EventType ?? "Update"), ct);

                metrics.IncrementSyncSucceeded();

                // ── Evict stale cache entries for this tenant ──────────────────
                // TENANT-B08: Ensure subsequent reads immediately see the updated data.
                brandingSvc.EvictPublicCache(body.Code, body.Subdomain);
                resolutionSvc.EvictCache(body.Code, body.Subdomain);

                log.LogInformation(
                    "[TenantSync] Upserted TenantId={TenantId} Code={Code}",
                    body.TenantId,
                    body.Code);

                return Results.Ok(new
                {
                    tenantId    = body.TenantId,
                    code        = body.Code,
                    eventType   = body.EventType ?? "Update",
                    syncedAtUtc = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                metrics.IncrementSyncFailed();

                log.LogError(
                    ex,
                    "[TenantSync] Upsert failed for TenantId={TenantId} Code={Code}",
                    body.TenantId,
                    body.Code);
                return Results.Problem(
                    detail:     "Tenant sync upsert failed. See service logs.",
                    statusCode: 500);
            }
        })
        .AllowAnonymous();
    }

    private record SyncUpsertBody(
        Guid      TenantId,
        string    Code,
        string    DisplayName,
        string?   Status,
        string?   Subdomain,
        Guid?     LogoDocumentId,
        Guid?     LogoWhiteDocumentId,
        DateTime? SourceCreatedAtUtc,
        DateTime? SourceUpdatedAtUtc,
        string?   EventType);
}
