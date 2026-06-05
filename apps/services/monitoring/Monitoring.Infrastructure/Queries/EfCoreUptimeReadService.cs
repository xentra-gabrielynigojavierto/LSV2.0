using Microsoft.EntityFrameworkCore;
using Monitoring.Application.Queries;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Infrastructure.Queries;

/// <summary>
/// EF Core implementation of <see cref="IUptimeReadService"/>.
///
/// <para>Reads from <c>uptime_hourly_rollups</c> only. Check history and alert
/// tables are never consulted — uptime percentages are fully rollup-driven.</para>
///
/// <para>Window resolution:
/// <list type="bullet">
///   <item>24h → last 24 hours</item>
///   <item>7d  → last 7 days</item>
///   <item>30d → last 30 days</item>
///   <item>90d → last 90 days</item>
/// </list>
/// Window end is always "now" (current UTC); start = end - duration.</para>
/// </summary>
public sealed class EfCoreUptimeReadService : IUptimeReadService
{
    private readonly MonitoringDbContext _db;

    public EfCoreUptimeReadService(MonitoringDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<UptimeRollupsResult> GetRollupsAsync(string window, CancellationToken ct)
    {
        var (start, end) = ResolveWindow(window);

        var rollups = await _db.UptimeHourlyRollups
            .AsNoTracking()
            .Where(r => r.BucketHourUtc >= start && r.BucketHourUtc < end)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Aggregate per entity across all hours in the window.
        var byEntity = rollups
            .GroupBy(r => (r.MonitoredEntityId, r.EntityName))
            .Select(g =>
            {
                var entityId   = g.Key.MonitoredEntityId;
                var entityName = g.Key.EntityName;

                var up      = g.Sum(r => r.UpCount);
                var deg     = g.Sum(r => r.DegradedCount);
                var down    = g.Sum(r => r.DownCount);
                var unknown = g.Sum(r => r.UnknownCount);
                var total   = up + deg + down;

                double? uptimePct   = total > 0 ? (double)up / total * 100.0 : null;
                double? weightedPct = total > 0 ? (up + deg * 0.5) / total * 100.0 : null;

                var totalForLatency = g.Sum(r => r.TotalCount);
                var sumMs           = g.Sum(r => r.SumElapsedMs);
                var maxMs           = g.Max(r => r.MaxElapsedMs);
                double? avgMs       = totalForLatency > 0 ? (double)sumMs / totalForLatency : null;

                return new UptimeComponentSummary(
                    EntityId:                    entityId,
                    EntityName:                  entityName,
                    UpCount:                     up,
                    DegradedCount:               deg,
                    DownCount:                   down,
                    UnknownCount:                unknown,
                    TotalCountable:              total,
                    UptimePercent:               uptimePct.HasValue ? Math.Round(uptimePct.Value, 4) : null,
                    WeightedAvailabilityPercent: weightedPct.HasValue ? Math.Round(weightedPct.Value, 4) : null,
                    AvgLatencyMs:                avgMs.HasValue ? Math.Round(avgMs.Value, 2) : null,
                    MaxLatencyMs:                maxMs,
                    InsufficientData:            total == 0);
            })
            .OrderBy(c => c.EntityName)
            .ToList();

        // Overall uptime = up / (up + deg + down) across all entities.
        var totalUp   = byEntity.Sum(c => c.UpCount);
        var totalDen  = byEntity.Sum(c => c.TotalCountable);
        double? overallPct = totalDen > 0 ? Math.Round((double)totalUp / totalDen * 100.0, 4) : null;

        return new UptimeRollupsResult(
            Window:              window,
            WindowStartUtc:      start,
            WindowEndUtc:        end,
            OverallUptimePercent: overallPct,
            ComponentCount:      byEntity.Count,
            InsufficientData:    overallPct is null,
            Components:          byEntity);
    }

    /// <inheritdoc/>
    public async Task<UptimeHistoryResult?> GetHistoryAsync(
        Guid   entityId,
        string window,
        CancellationToken ct)
    {
        var (start, end) = ResolveWindow(window);

        var rollups = await _db.UptimeHourlyRollups
            .AsNoTracking()
            .Where(r => r.MonitoredEntityId == entityId
                     && r.BucketHourUtc >= start
                     && r.BucketHourUtc < end)
            .OrderBy(r => r.BucketHourUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Determine the entity name from the rollup rows (or fall back to entity lookup).
        string? entityName = rollups.FirstOrDefault()?.EntityName;
        if (entityName is null)
        {
            // Entity exists but has no rollup data yet — check if the entity exists.
            var entity = await _db.MonitoredEntities
                .AsNoTracking()
                .Where(e => e.Id == entityId)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (entity is null) return null;  // Entity not found at all.
            entityName = entity;
        }

        var buckets = rollups.Select(r =>
        {
            var total = r.UpCount + r.DegradedCount + r.DownCount;
            double? uptimePct = total > 0
                ? Math.Round((double)r.UpCount / total * 100.0, 4)
                : null;
            double? avgMs = r.TotalCount > 0
                ? Math.Round((double)r.SumElapsedMs / r.TotalCount, 2)
                : null;

            return new UptimeHistoryBucket(
                BucketStartUtc:   r.BucketHourUtc,
                UpCount:          r.UpCount,
                DegradedCount:    r.DegradedCount,
                DownCount:        r.DownCount,
                UnknownCount:     r.UnknownCount,
                UptimePercent:    uptimePct,
                DominantStatus:   DeriveStatus(r.UpCount, r.DegradedCount, r.DownCount, r.UnknownCount),
                AvgLatencyMs:     avgMs,
                MaxLatencyMs:     r.MaxElapsedMs,
                InsufficientData: r.InsufficientData);
        }).ToList();

        return new UptimeHistoryResult(
            EntityId:       entityId,
            EntityName:     entityName,
            Window:         window,
            WindowStartUtc: start,
            WindowEndUtc:   end,
            Buckets:        buckets);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (DateTime Start, DateTime End) ResolveWindow(string window)
    {
        var end = DateTime.UtcNow;
        var start = window switch
        {
            "24h" => end.AddHours(-24),
            "7d"  => end.AddDays(-7),
            "30d" => end.AddDays(-30),
            "90d" => end.AddDays(-90),
            _     => end.AddHours(-24),   // safe default
        };
        return (start, end);
    }

    private static string DeriveStatus(int up, int degraded, int down, int unknown)
    {
        var total = up + degraded + down;
        if (total == 0) return unknown > 0 ? "Unknown" : "Unknown";
        if (down > up + degraded) return "Down";
        if (degraded > up)        return "Degraded";
        return "Healthy";
    }
}
