using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoring.Domain.Monitoring;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Infrastructure.UptimeAggregation;

/// <summary>
/// Background service that periodically derives hourly uptime rollups from the
/// canonical <c>check_results</c> table and persists them to
/// <c>uptime_hourly_rollups</c>.
///
/// <para><b>Isolation from alerts</b>: this engine reads only
/// <c>check_results</c>. Alert lifecycle (including manual resolve) has zero
/// effect on uptime metrics.</para>
///
/// <para><b>Idempotency</b>: on each run, it queries all check results in the
/// configured lookback window, groups them by (entity_id, hour bucket), computes
/// per-bucket statistics, then upserts the rollup rows. Running it twice over
/// the same data produces identical output.</para>
///
/// <para><b>Cadence</b>: configurable via
/// <c>Monitoring:UptimeAggregation:IntervalSeconds</c> (default 300 s / 5 min).
/// </para>
///
/// <para><b>State classification</b>:
/// <list type="bullet">
///   <item>Success → Up</item>
///   <item>NonSuccessStatusCode → Degraded (reachable but unhealthy HTTP response)</item>
///   <item>Timeout | NetworkFailure | InvalidTarget | UnexpectedFailure → Down</item>
///   <item>Skipped → Unknown (excluded from uptime denominator)</item>
/// </list>
/// </para>
/// </summary>
public sealed class UptimeAggregationHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<UptimeAggregationHostedService> _logger;
    private readonly UptimeAggregationOptions _options;

    public UptimeAggregationHostedService(
        IServiceProvider services,
        ILogger<UptimeAggregationHostedService> logger,
        IOptions<UptimeAggregationOptions> options)
    {
        _services = services;
        _logger   = logger;
        _options  = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("UptimeAggregation: disabled via config. Engine will not run.");
            return;
        }

        _logger.LogInformation(
            "UptimeAggregation: engine started. IntervalSeconds={Interval}, LookbackDays={Lookback}.",
            _options.IntervalSeconds, _options.LookbackDays);

        // Run immediately on startup so rollups are populated before the first request.
        await RunCycleAsync(stoppingToken).ConfigureAwait(false);

        var interval = TimeSpan.FromSeconds(_options.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunCycleAsync(stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("UptimeAggregation: engine stopped.");
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();

            var cutoff = DateTime.UtcNow.AddDays(-_options.LookbackDays);

            _logger.LogDebug("UptimeAggregation: reading check_results since {Cutoff:O}.", cutoff);

            // Load raw results from the canonical check_results table.
            // We project only the columns we need to keep the payload small.
            var rawResults = await db.CheckResults
                .AsNoTracking()
                .Where(r => r.CheckedAtUtc >= cutoff)
                .Select(r => new RawCheckRow(
                    r.MonitoredEntityId,
                    r.EntityName,
                    r.CheckedAtUtc,
                    r.Outcome,
                    r.ElapsedMs))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (rawResults.Count == 0)
            {
                _logger.LogDebug("UptimeAggregation: no check results in lookback window. Skipping.");
                return;
            }

            // Group by (entity_id, hour bucket).
            var buckets = rawResults
                .GroupBy(r => new BucketKey(
                    r.MonitoredEntityId,
                    r.EntityName,
                    TruncateToHour(r.CheckedAtUtc)))
                .ToList();

            _logger.LogDebug(
                "UptimeAggregation: processing {BucketCount} (entity, hour) buckets from {RowCount} results.",
                buckets.Count, rawResults.Count);

            // Load existing rollups for the same window to decide insert vs update.
            var existingRollups = await db.UptimeHourlyRollups
                .Where(r => r.BucketHourUtc >= cutoff)
                .ToDictionaryAsync(
                    r => (r.MonitoredEntityId, r.BucketHourUtc),
                    ct)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var inserted = 0;
            var updated  = 0;

            foreach (var bucket in buckets)
            {
                var (entityId, entityName, hourBucket) = bucket.Key;
                var (up, deg, down, unknown, sumMs, maxMs) = ComputeStats(bucket);

                var lookupKey = (entityId, hourBucket);

                if (existingRollups.TryGetValue(lookupKey, out var existing))
                {
                    existing.Update(entityName, up, deg, down, unknown, sumMs, maxMs, now);
                    updated++;
                }
                else
                {
                    var rollup = new UptimeHourlyRollup(
                        id:                Guid.NewGuid(),
                        monitoredEntityId: entityId,
                        entityName:        entityName,
                        bucketHourUtc:     hourBucket,
                        upCount:           up,
                        degradedCount:     deg,
                        downCount:         down,
                        unknownCount:      unknown,
                        sumElapsedMs:      sumMs,
                        maxElapsedMs:      maxMs,
                        computedAtUtc:     now,
                        createdAtUtc:      now);

                    db.UptimeHourlyRollups.Add(rollup);
                    inserted++;
                }
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "UptimeAggregation: cycle complete. Inserted={Ins}, Updated={Upd}.",
                inserted, updated);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host shutting down — not an error.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UptimeAggregation: cycle failed. Will retry on next interval.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DateTime TruncateToHour(DateTime dt) =>
        new(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc);

    private static (int Up, int Deg, int Down, int Unknown, long SumMs, long MaxMs)
        ComputeStats(IEnumerable<RawCheckRow> rows)
    {
        int  up = 0, deg = 0, down = 0, unknown = 0;
        long sumMs = 0, maxMs = 0;

        foreach (var r in rows)
        {
            switch (r.Outcome)
            {
                case CheckOutcome.Success:
                    up++;
                    break;
                case CheckOutcome.NonSuccessStatusCode:
                    // Reachable but returned a non-2xx response → Degraded.
                    deg++;
                    break;
                case CheckOutcome.Skipped:
                    unknown++;
                    break;
                default:
                    // Timeout, NetworkFailure, InvalidTarget, UnexpectedFailure → Down.
                    down++;
                    break;
            }

            sumMs += r.ElapsedMs;
            if (r.ElapsedMs > maxMs) maxMs = r.ElapsedMs;
        }

        return (up, deg, down, unknown, sumMs, maxMs);
    }

    // ── Private value types ───────────────────────────────────────────────────

    private sealed record RawCheckRow(
        Guid          MonitoredEntityId,
        string        EntityName,
        DateTime      CheckedAtUtc,
        CheckOutcome  Outcome,
        long          ElapsedMs);

    private sealed record BucketKey(
        Guid     MonitoredEntityId,
        string   EntityName,
        DateTime HourBucket);
}
