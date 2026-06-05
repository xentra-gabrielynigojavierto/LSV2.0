using Microsoft.EntityFrameworkCore;
using Monitoring.Application.Queries;
using Monitoring.Domain.Monitoring;
using Monitoring.Infrastructure.Persistence;

namespace Monitoring.Infrastructure.Queries;

/// <summary>
/// EF Core implementation of <see cref="IMonitoringReadService"/>.
///
/// <para><b>Join strategy</b>: EntityCurrentStatus shares its PK with
/// MonitoredEntityId (1:0..1). Rather than translating a LEFT JOIN to
/// LINQ GroupJoin (verbose and error-prone with MySQL EF provider), this
/// implementation loads both sets into memory and joins in-process.
/// The entity registry is expected to remain small (tens to hundreds of
/// rows) so two sequential queries outperform a client-side lateral join
/// on the MySQL side.</para>
///
/// <para><b>No-tracking</b>: all queries use <c>AsNoTracking()</c>
/// because this service never mutates state.</para>
/// </summary>
public sealed class EfCoreMonitoringReadService : IMonitoringReadService
{
    private readonly MonitoringDbContext _db;

    public EfCoreMonitoringReadService(MonitoringDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<MonitoringStatusResult[]> GetStatusAsync(CancellationToken ct)
    {
        var entities = await _db.MonitoredEntities
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ThenBy(e => e.CreatedAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var statuses = await _db.EntityCurrentStatuses
            .AsNoTracking()
            .ToDictionaryAsync(s => s.MonitoredEntityId, ct)
            .ConfigureAwait(false);

        return entities.Select(e =>
        {
            statuses.TryGetValue(e.Id, out var s);
            return new MonitoringStatusResult(
                EntityId:        e.Id,
                Name:            e.Name,
                Scope:           e.Scope,
                Status:          s?.CurrentStatus ?? EntityStatus.Unknown,
                LastCheckedAtUtc: s?.LastCheckedAtUtc,
                LastElapsedMs:   s?.LastElapsedMs);
        }).ToArray();
    }

    /// <inheritdoc/>
    public async Task<MonitoringAlertResult[]> GetActiveAlertsAsync(CancellationToken ct)
    {
        return await _db.MonitoringAlerts
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.TriggeredAtUtc)
            .Select(a => new MonitoringAlertResult(
                a.Id,
                a.MonitoredEntityId,
                a.EntityName,
                a.Scope,
                a.ImpactLevel,
                a.AlertType,
                a.Message,
                a.TriggeredAtUtc,
                a.ResolvedAtUtc))
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<MonitoringSummaryResult> GetSummaryAsync(CancellationToken ct)
    {
        // Must run sequentially — MonitoringDbContext is not thread-safe.
        // Task.WhenAll would start both queries on the same scoped context
        // instance concurrently, triggering InvalidOperationException.
        var statuses = await GetStatusAsync(ct).ConfigureAwait(false);
        var alerts   = await GetActiveAlertsAsync(ct).ConfigureAwait(false);

        return new MonitoringSummaryResult(
            Statuses:     statuses,
            ActiveAlerts: alerts);
    }
}
