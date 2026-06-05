using Monitoring.Api.Contracts;
using Monitoring.Application.Queries;

namespace Monitoring.Api.Endpoints;

/// <summary>
/// Read-only endpoints for uptime rollup data.
///
/// <para>All endpoints are anonymous — same policy as the existing
/// <see cref="MonitoringReadEndpoints"/> group. They are called from within
/// the trust boundary (Control Center backend, public status page server)
/// and do not require authentication.</para>
///
/// <para>Data is derived exclusively from the <c>uptime_hourly_rollups</c>
/// table, which is populated by <c>UptimeAggregationHostedService</c> from
/// raw <c>check_results</c> history. Alert state (including manual resolves)
/// is never consulted.</para>
/// </summary>
public static class UptimeReadEndpoints
{
    private static readonly HashSet<string> ValidWindows =
        new(StringComparer.OrdinalIgnoreCase) { "24h", "7d", "30d", "90d" };

    public static IEndpointRouteBuilder MapUptimeReadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/monitoring/uptime").AllowAnonymous();

        // GET /monitoring/uptime/rollups?window=24h|7d|30d|90d
        group.MapGet("/rollups", GetRollupsAsync);

        // GET /monitoring/uptime/history?entityId={guid}&window=24h|7d|30d|90d
        group.MapGet("/history", GetHistoryAsync);

        return app;
    }

    // ── GET /monitoring/uptime/rollups ─────────────────────────────────────────

    private static async Task<IResult> GetRollupsAsync(
        IUptimeReadService svc,
        string?  window,
        CancellationToken ct)
    {
        var resolvedWindow = ResolveWindow(window);
        var result = await svc.GetRollupsAsync(resolvedWindow, ct).ConfigureAwait(false);

        var response = new UptimeRollupsResponse(
            Window:              result.Window,
            WindowStartUtc:      result.WindowStartUtc,
            WindowEndUtc:        result.WindowEndUtc,
            OverallUptimePercent: result.OverallUptimePercent,
            ComponentCount:      result.ComponentCount,
            InsufficientData:    result.InsufficientData,
            Components:          result.Components.Select(MapComponent).ToList());

        return Results.Ok(response);
    }

    // ── GET /monitoring/uptime/history ────────────────────────────────────────

    private static async Task<IResult> GetHistoryAsync(
        IUptimeReadService svc,
        string?  entityId,
        string?  window,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entityId) || !Guid.TryParse(entityId, out var entityGuid))
        {
            return Results.BadRequest(new
            {
                error = "entityId query parameter is required and must be a valid GUID."
            });
        }

        var resolvedWindow = ResolveWindow(window);
        var result = await svc.GetHistoryAsync(entityGuid, resolvedWindow, ct).ConfigureAwait(false);

        if (result is null)
        {
            return Results.NotFound(new { error = $"Monitored entity '{entityGuid}' not found." });
        }

        var response = new UptimeHistoryResponse(
            EntityId:       result.EntityId,
            EntityName:     result.EntityName,
            Window:         result.Window,
            WindowStartUtc: result.WindowStartUtc,
            WindowEndUtc:   result.WindowEndUtc,
            Buckets:        result.Buckets.Select(MapBucket).ToList());

        return Results.Ok(response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ResolveWindow(string? raw) =>
        raw is not null && ValidWindows.Contains(raw) ? raw.ToLowerInvariant() : "24h";

    private static UptimeComponentResponse MapComponent(UptimeComponentSummary c) =>
        new(
            EntityId:                    c.EntityId,
            EntityName:                  c.EntityName,
            UptimePercent:               c.UptimePercent,
            WeightedAvailabilityPercent: c.WeightedAvailabilityPercent,
            UpCount:                     c.UpCount,
            DegradedCount:               c.DegradedCount,
            DownCount:                   c.DownCount,
            UnknownCount:                c.UnknownCount,
            TotalCountable:              c.TotalCountable,
            AvgLatencyMs:                c.AvgLatencyMs,
            MaxLatencyMs:                c.MaxLatencyMs,
            InsufficientData:            c.InsufficientData);

    private static UptimeHistoryBucketResponse MapBucket(UptimeHistoryBucket b) =>
        new(
            BucketStartUtc:   b.BucketStartUtc,
            UptimePercent:    b.UptimePercent,
            DominantStatus:   b.DominantStatus,
            UpCount:          b.UpCount,
            DegradedCount:    b.DegradedCount,
            DownCount:        b.DownCount,
            UnknownCount:     b.UnknownCount,
            AvgLatencyMs:     b.AvgLatencyMs,
            MaxLatencyMs:     b.MaxLatencyMs,
            InsufficientData: b.InsufficientData);
}
