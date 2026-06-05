using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs.Analytics;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditAnalyticsService"/>.
///
/// All queries:
/// - Are bounded by a mandatory date range (caller-supplied or service-defaulted).
/// - Use AsNoTracking() for read performance.
/// - Apply tenant scope before any aggregation to preserve isolation.
/// - Hit at least one indexed column (OccurredAtUtc, EventCategory, EventType,
///   TenantId, ActorId) — see <see cref="AuditEventRecordConfiguration"/>.
///
/// Performance guardrails:
/// - Default window: last 30 days. Maximum window: 90 days (enforced here).
/// - TopActors / TopEventTypes: capped at 10/15 rows via Take().
/// - TopTenants: capped at 10 rows, only populated for platform-admin callers.
/// - Denial/governance counts: bounded by the same date+tenant predicates;
///   the string predicate runs on the server-filtered subset.
/// </summary>
public sealed class AuditAnalyticsService : IAuditAnalyticsService
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaxWindow     = TimeSpan.FromDays(90);

    private readonly IDbContextFactory<AuditEventDbContext> _factory;
    private readonly ILogger<AuditAnalyticsService>         _log;

    public AuditAnalyticsService(
        IDbContextFactory<AuditEventDbContext> factory,
        ILogger<AuditAnalyticsService>         log)
    {
        _factory = factory;
        _log     = log;
    }

    // ─────────────────────────────────────────────────────────────────────────

    public async Task<AuditAnalyticsSummaryResponse> GetSummaryAsync(
        AuditAnalyticsSummaryRequest request,
        string?                      callerTenantId,
        bool                         isPlatformAdmin,
        CancellationToken            ct = default)
    {
        // ── 1. Resolve effective date window ──────────────────────────────────

        var to   = request.To   ?? DateTimeOffset.UtcNow;
        var from = request.From ?? to - DefaultWindow;

        // Clamp the window to MaxWindow
        if ((to - from) > MaxWindow)
            from = to - MaxWindow;

        // ── 2. Resolve effective tenant scope ──────────────────────────────────
        // Tenant-scoped callers always see only their own tenant.
        // Platform admins use request.TenantId if provided (or no tenant filter).

        string? effectiveTenantId = callerTenantId
            ?? (isPlatformAdmin ? request.TenantId : null);

        _log.LogDebug(
            "AuditAnalytics summary: from={From:u} to={To:u} tenant={Tenant} pa={Pa}",
            from, to, effectiveTenantId ?? "(all)", isPlatformAdmin);

        // ── 3. Run all analytics queries ──────────────────────────────────────

        await using var db = await _factory.CreateDbContextAsync(ct);

        var base_ = db.AuditEventRecords
            .AsNoTracking()
            .Where(r => r.OccurredAtUtc >= from && r.OccurredAtUtc < to);

        if (effectiveTenantId is not null)
            base_ = base_.Where(r => r.TenantId == effectiveTenantId);

        if (request.Category.HasValue)
            base_ = base_.Where(r => r.EventCategory == request.Category.Value);

        // Run all count/aggregation tasks in sequence (single DbContext, no parallelism)
        // to avoid multi-threaded EF context issues.

        var totalEvents = await base_.LongCountAsync(ct);

        var securityCount = await base_
            .Where(r => r.EventCategory == EventCategory.Security)
            .LongCountAsync(ct);

        var denialCount = await base_
            .Where(r => r.EventType.Contains(".denied") || r.EventType.Contains(".deny"))
            .LongCountAsync(ct);

        var governanceCount = await base_
            .Where(r =>
                r.EventType.StartsWith("audit.legal_hold")  ||
                r.EventType.StartsWith("audit.integrity")   ||
                r.EventType.StartsWith("audit.log.exported"))
            .LongCountAsync(ct);

        // Volume by day ───────────────────────────────────────────────────────

        var rawVolumeByDay = await base_
            .GroupBy(r => new
            {
                Year  = r.OccurredAtUtc.Year,
                Month = r.OccurredAtUtc.Month,
                Day   = r.OccurredAtUtc.Day,
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Count = (long)g.Count(),
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day)
            .ToListAsync(ct);

        var volumeByDay = rawVolumeByDay
            .Select(x => new AuditVolumeByDayItem
            {
                Date  = $"{x.Year:D4}-{x.Month:D2}-{x.Day:D2}",
                Count = x.Count,
            })
            .ToList();

        // Category breakdown ──────────────────────────────────────────────────

        var rawByCategory = await base_
            .GroupBy(r => r.EventCategory)
            .Select(g => new { Category = g.Key, Count = (long)g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        var byCategory = rawByCategory
            .Select(x => new AuditCategoryBreakdownItem
            {
                Category      = x.Category.ToString(),
                CategoryValue = (int)x.Category,
                Count         = x.Count,
            })
            .ToList();

        // Severity breakdown ──────────────────────────────────────────────────

        var rawBySeverity = await base_
            .GroupBy(r => r.Severity)
            .Select(g => new { Severity = g.Key, Count = (long)g.Count() })
            .OrderBy(x => x.Severity)
            .ToListAsync(ct);

        var bySeverity = rawBySeverity
            .Select(x => new AuditSeverityBreakdownItem
            {
                Severity      = x.Severity.ToString(),
                SeverityValue = (int)x.Severity,
                Count         = x.Count,
            })
            .ToList();

        // Top event types ─────────────────────────────────────────────────────

        var topEventTypes = await base_
            .GroupBy(r => r.EventType)
            .Select(g => new AuditTopEventTypeItem
            {
                EventType = g.Key,
                Count     = (long)g.Count(),
            })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToListAsync(ct);

        // Top actors ──────────────────────────────────────────────────────────
        // Actors without an ActorId are excluded (anonymous/system events).

        var rawTopActors = await base_
            .Where(r => r.ActorId != null)
            .GroupBy(r => r.ActorId!)
            .Select(g => new
            {
                ActorId = g.Key,
                Count   = (long)g.Count(),
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        // Fetch a recent display name for each top actor (latest in window)
        var actorIds = rawTopActors.Select(a => a.ActorId).ToList();
        var actorNames = await base_
            .Where(r => r.ActorId != null && actorIds.Contains(r.ActorId!) && r.ActorName != null)
            .GroupBy(r => r.ActorId!)
            .Select(g => new
            {
                ActorId   = g.Key,
                ActorName = g.OrderByDescending(r => r.OccurredAtUtc).Select(r => r.ActorName).First(),
            })
            .ToDictionaryAsync(x => x.ActorId, x => x.ActorName, ct);

        var topActors = rawTopActors
            .Select(a => new AuditTopActorItem
            {
                ActorId   = a.ActorId,
                ActorName = actorNames.TryGetValue(a.ActorId, out var name) ? name : null,
                Count     = a.Count,
            })
            .ToList();

        // Top tenants (platform admin only) ───────────────────────────────────

        IReadOnlyList<AuditTopTenantItem>? topTenants = null;

        if (isPlatformAdmin && effectiveTenantId is null)
        {
            topTenants = await base_
                .Where(r => r.TenantId != null)
                .GroupBy(r => r.TenantId!)
                .Select(g => new AuditTopTenantItem
                {
                    TenantId = g.Key,
                    Count    = (long)g.Count(),
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync(ct);
        }

        // ── 4. Assemble response ──────────────────────────────────────────────

        return new AuditAnalyticsSummaryResponse
        {
            From                = from,
            To                  = to,
            EffectiveTenantId   = effectiveTenantId,
            TotalEvents         = totalEvents,
            SecurityEventCount  = securityCount,
            DenialEventCount    = denialCount,
            GovernanceEventCount = governanceCount,
            VolumeByDay         = volumeByDay,
            ByCategory          = byCategory,
            BySeverity          = bySeverity,
            TopEventTypes       = topEventTypes,
            TopActors           = topActors,
            TopTenants          = topTenants,
        };
    }
}
