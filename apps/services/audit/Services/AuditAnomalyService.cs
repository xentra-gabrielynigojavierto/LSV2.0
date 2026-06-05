using Microsoft.EntityFrameworkCore;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs.Analytics;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Deterministic, rule-based anomaly detection over the canonical audit event store.
///
/// Evaluation windows (always fixed relative to request time):
///   Recent window:   [now - 24h, now)
///   Baseline window: [now - 8d,  now - 1d)  → 7 calendar days for daily average
///
/// All queries:
///   - Are bounded by the above windows (OccurredAtUtc index).
///   - Use AsNoTracking() for read performance.
///   - Apply tenant scope before any aggregation.
///   - Hit at least one indexed column per query.
///
/// Rules only fire when BOTH an absolute minimum AND a ratio/percentage threshold are met
/// to reduce false positives in low-volume environments.
/// </summary>
public sealed class AuditAnomalyService : IAuditAnomalyService
{
    // ── Thresholds (deterministic, inline constants) ───────────────────────────

    private const double SpikeRatioThreshold          = 3.0;  // 3× daily average
    private const int    DenialMinAbsolute             = 5;
    private const double ActorConcentrationPct         = 30.0;
    private const int    ActorConcentrationMin         = 20;
    private const double TenantConcentrationPct        = 40.0;
    private const int    TenantConcentrationMin        = 50;
    private const int    GovernanceBurstMin            = 3;
    private const int    ExportSpikeMin                = 5;
    private const int    SeverityEscalationAbsoluteMin = 10;
    private const double SeverityEscalationPct         = 10.0;
    private const double EventTypeConcentrationPct     = 50.0;
    private const int    EventTypeConcentrationMin     = 30;

    private const int BaselineDays = 7; // baseline window spans 7 days

    private readonly IDbContextFactory<AuditEventDbContext> _factory;
    private readonly ILogger<AuditAnomalyService>           _log;

    public AuditAnomalyService(
        IDbContextFactory<AuditEventDbContext> factory,
        ILogger<AuditAnomalyService>           log)
    {
        _factory = factory;
        _log     = log;
    }

    // ─────────────────────────────────────────────────────────────────────────

    public async Task<AuditAnomalyResponse> DetectAsync(
        AuditAnomalyRequest request,
        string?             callerTenantId,
        bool                isPlatformAdmin,
        CancellationToken   ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // ── 1. Resolve windows ────────────────────────────────────────────────

        var recentFrom    = now.AddHours(-24);
        var recentTo      = now;
        var baselineFrom  = now.AddDays(-8);
        var baselineTo    = now.AddDays(-1);

        // ── 2. Resolve tenant scope ───────────────────────────────────────────

        string? effectiveTenantId = callerTenantId
            ?? (isPlatformAdmin ? request.TenantId : null);

        _log.LogDebug(
            "AuditAnomaly detect: tenant={Tenant} pa={PA} recentFrom={From:u}",
            effectiveTenantId ?? "(all)", isPlatformAdmin, recentFrom);

        // ── 3. Evaluate rules ─────────────────────────────────────────────────

        await using var db = await _factory.CreateDbContextAsync(ct);

        // Base IQueryable helpers (tenant-scoped, no-tracking, no date filter yet)
        IQueryable<Entities.AuditEventRecord> ScopedBase() =>
            effectiveTenantId is not null
                ? db.AuditEventRecords.AsNoTracking().Where(r => r.TenantId == effectiveTenantId)
                : db.AuditEventRecords.AsNoTracking();

        var recentBase   = ScopedBase().Where(r => r.OccurredAtUtc >= recentFrom   && r.OccurredAtUtc < recentTo);
        var baselineBase = ScopedBase().Where(r => r.OccurredAtUtc >= baselineFrom && r.OccurredAtUtc < baselineTo);

        // Common scalars used by multiple rules
        var recentTotal   = await recentBase.LongCountAsync(ct);
        var baselineTotal = await baselineBase.LongCountAsync(ct);
        var baselineDailyAvg = baselineTotal / (double)BaselineDays;

        var anomalies = new List<AuditAnomalyItem>();

        // ── Rule 1: DENIAL_SPIKE ──────────────────────────────────────────────

        var recentDenials = await recentBase
            .Where(r => r.EventType.Contains(".denied") || r.EventType.Contains(".deny"))
            .LongCountAsync(ct);

        var baselineDenials = await baselineBase
            .Where(r => r.EventType.Contains(".denied") || r.EventType.Contains(".deny"))
            .LongCountAsync(ct);

        var denialDailyAvg = baselineDenials / (double)BaselineDays;

        if (recentDenials >= DenialMinAbsolute && denialDailyAvg > 0)
        {
            var ratio = recentDenials / denialDailyAvg;
            if (ratio >= SpikeRatioThreshold)
            {
                anomalies.Add(new AuditAnomalyItem
                {
                    RuleKey       = "DENIAL_SPIKE",
                    Title         = "Unusual spike in access denials",
                    Description   = $"Denial events in the last 24h ({recentDenials:N0}) are " +
                                    $"{ratio:F1}× the 7-day daily average " +
                                    $"({denialDailyAvg:F1} events/day). Threshold: {SpikeRatioThreshold}×.",
                    Severity      = "High",
                    RecentValue   = recentDenials,
                    BaselineValue = denialDailyAvg,
                    Threshold     = SpikeRatioThreshold,
                    ActualValue   = Math.Round(ratio, 2),
                    DrillDownPath = "/synqaudit/investigation?category=Security",
                });
            }
        }
        else if (recentDenials >= DenialMinAbsolute && denialDailyAvg == 0)
        {
            // No baseline but non-trivial recent count — new anomaly with no history
            anomalies.Add(new AuditAnomalyItem
            {
                RuleKey       = "DENIAL_SPIKE",
                Title         = "Access denials with no prior baseline",
                Description   = $"{recentDenials:N0} denial events occurred in the last 24h " +
                                $"with no activity in the prior 7-day baseline. This may indicate a new attack pattern.",
                Severity      = "High",
                RecentValue   = recentDenials,
                BaselineValue = 0,
                Threshold     = SpikeRatioThreshold,
                ActualValue   = recentDenials,
                DrillDownPath = "/synqaudit/investigation?category=Security",
            });
        }

        // ── Rule 2: ACTOR_CONCENTRATION ───────────────────────────────────────

        if (recentTotal > 0)
        {
            var topActorGroup = await recentBase
                .Where(r => r.ActorId != null)
                .GroupBy(r => r.ActorId!)
                .Select(g => new { ActorId = g.Key, Count = (long)g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(1)
                .ToListAsync(ct);

            if (topActorGroup.Count > 0)
            {
                var top        = topActorGroup[0];
                var pct        = top.Count / (double)recentTotal * 100.0;

                if (top.Count >= ActorConcentrationMin && pct >= ActorConcentrationPct)
                {
                    // Look up actor name (best-effort)
                    var actorName = await recentBase
                        .Where(r => r.ActorId == top.ActorId && r.ActorName != null)
                        .OrderByDescending(r => r.OccurredAtUtc)
                        .Select(r => r.ActorName)
                        .FirstOrDefaultAsync(ct);

                    anomalies.Add(new AuditAnomalyItem
                    {
                        RuleKey          = "ACTOR_CONCENTRATION",
                        Title            = "Unusual actor activity concentration",
                        Description      = $"Actor '{actorName ?? top.ActorId}' generated " +
                                           $"{pct:F1}% of all events ({top.Count:N0} of {recentTotal:N0}) " +
                                           $"in the last 24h. Threshold: {ActorConcentrationPct}% and ≥{ActorConcentrationMin} events.",
                        Severity         = "Medium",
                        RecentValue      = top.Count,
                        BaselineValue    = recentTotal,
                        Threshold        = ActorConcentrationPct,
                        ActualValue      = Math.Round(pct, 2),
                        AffectedActorId  = top.ActorId,
                        AffectedActorName = actorName,
                        DrillDownPath    = $"/synqaudit/investigation?actorId={Uri.EscapeDataString(top.ActorId)}",
                    });
                }
            }
        }

        // ── Rule 3: TENANT_CONCENTRATION (platform admin, cross-tenant only) ──

        if (isPlatformAdmin && effectiveTenantId is null && recentTotal > 0)
        {
            var topTenantGroup = await db.AuditEventRecords
                .AsNoTracking()
                .Where(r => r.OccurredAtUtc >= recentFrom && r.OccurredAtUtc < recentTo && r.TenantId != null)
                .GroupBy(r => r.TenantId!)
                .Select(g => new { TenantId = g.Key, Count = (long)g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(1)
                .ToListAsync(ct);

            if (topTenantGroup.Count > 0)
            {
                var top = topTenantGroup[0];
                var pct = top.Count / (double)recentTotal * 100.0;

                if (top.Count >= TenantConcentrationMin && pct >= TenantConcentrationPct)
                {
                    anomalies.Add(new AuditAnomalyItem
                    {
                        RuleKey           = "TENANT_CONCENTRATION",
                        Title             = "Unusual tenant activity concentration",
                        Description       = $"Tenant '{top.TenantId}' generated " +
                                            $"{pct:F1}% of all platform events ({top.Count:N0} of {recentTotal:N0}) " +
                                            $"in the last 24h. Threshold: {TenantConcentrationPct}% and ≥{TenantConcentrationMin} events.",
                        Severity          = "Medium",
                        RecentValue       = top.Count,
                        BaselineValue     = recentTotal,
                        Threshold         = TenantConcentrationPct,
                        ActualValue       = Math.Round(pct, 2),
                        AffectedTenantId  = top.TenantId,
                        DrillDownPath     = "/synqaudit/investigation",
                    });
                }
            }
        }

        // ── Rule 4: GOVERNANCE_BURST ──────────────────────────────────────────

        var recentGov = await recentBase
            .Where(r =>
                r.EventType.StartsWith("audit.legal_hold")  ||
                r.EventType.StartsWith("audit.integrity")   ||
                r.EventType.StartsWith("audit.log.exported"))
            .LongCountAsync(ct);

        var baselineGov = await baselineBase
            .Where(r =>
                r.EventType.StartsWith("audit.legal_hold")  ||
                r.EventType.StartsWith("audit.integrity")   ||
                r.EventType.StartsWith("audit.log.exported"))
            .LongCountAsync(ct);

        var govDailyAvg = baselineGov / (double)BaselineDays;

        if (recentGov >= GovernanceBurstMin)
        {
            if (govDailyAvg > 0 && recentGov / govDailyAvg >= SpikeRatioThreshold)
            {
                var ratio = recentGov / govDailyAvg;
                anomalies.Add(new AuditAnomalyItem
                {
                    RuleKey       = "GOVERNANCE_BURST",
                    Title         = "Unusual governance activity burst",
                    Description   = $"Governance events (legal holds, integrity checks, exports) " +
                                    $"in the last 24h ({recentGov:N0}) are {ratio:F1}× the 7-day daily average " +
                                    $"({govDailyAvg:F1} events/day). Threshold: {SpikeRatioThreshold}×.",
                    Severity      = "Medium",
                    RecentValue   = recentGov,
                    BaselineValue = govDailyAvg,
                    Threshold     = SpikeRatioThreshold,
                    ActualValue   = Math.Round(ratio, 2),
                    DrillDownPath = "/synqaudit/legal-holds",
                });
            }
            else if (govDailyAvg == 0)
            {
                anomalies.Add(new AuditAnomalyItem
                {
                    RuleKey       = "GOVERNANCE_BURST",
                    Title         = "Governance activity with no prior baseline",
                    Description   = $"{recentGov:N0} governance events occurred in the last 24h " +
                                    $"with no activity in the prior 7-day baseline.",
                    Severity      = "Medium",
                    RecentValue   = recentGov,
                    BaselineValue = 0,
                    Threshold     = SpikeRatioThreshold,
                    ActualValue   = recentGov,
                    DrillDownPath = "/synqaudit/legal-holds",
                });
            }
        }

        // ── Rule 5: EXPORT_SPIKE ──────────────────────────────────────────────

        var recentExport = await recentBase
            .Where(r =>
                r.EventType.StartsWith("audit.log.accessed")  ||
                r.EventType.StartsWith("audit.log.exported"))
            .LongCountAsync(ct);

        var baselineExport = await baselineBase
            .Where(r =>
                r.EventType.StartsWith("audit.log.accessed")  ||
                r.EventType.StartsWith("audit.log.exported"))
            .LongCountAsync(ct);

        var exportDailyAvg = baselineExport / (double)BaselineDays;

        if (recentExport >= ExportSpikeMin && exportDailyAvg > 0)
        {
            var ratio = recentExport / exportDailyAvg;
            if (ratio >= SpikeRatioThreshold)
            {
                anomalies.Add(new AuditAnomalyItem
                {
                    RuleKey       = "EXPORT_SPIKE",
                    Title         = "Unusual audit access/export volume",
                    Description   = $"Audit access and export events in the last 24h ({recentExport:N0}) " +
                                    $"are {ratio:F1}× the 7-day daily average ({exportDailyAvg:F1} events/day). " +
                                    $"Threshold: {SpikeRatioThreshold}×.",
                    Severity      = "Medium",
                    RecentValue   = recentExport,
                    BaselineValue = exportDailyAvg,
                    Threshold     = SpikeRatioThreshold,
                    ActualValue   = Math.Round(ratio, 2),
                    DrillDownPath = "/synqaudit/exports",
                });
            }
        }

        // ── Rule 6: SEVERITY_ESCALATION ───────────────────────────────────────

        var recentCritical = await recentBase
            .Where(r => r.Severity == SeverityLevel.Critical || r.Severity == SeverityLevel.Alert)
            .LongCountAsync(ct);

        if (recentCritical > 0)
        {
            var critPct   = recentTotal > 0 ? recentCritical / (double)recentTotal * 100.0 : 0.0;
            var absFired  = recentCritical > SeverityEscalationAbsoluteMin;
            var pctFired  = critPct >= SeverityEscalationPct;

            if (absFired || pctFired)
            {
                anomalies.Add(new AuditAnomalyItem
                {
                    RuleKey       = "SEVERITY_ESCALATION",
                    Title         = "Critical/Alert severity surge",
                    Description   = $"Critical or Alert severity events account for {critPct:F1}% " +
                                    $"of activity in the last 24h ({recentCritical:N0} of {recentTotal:N0} events). " +
                                    $"Threshold: >{SeverityEscalationAbsoluteMin} absolute or ≥{SeverityEscalationPct}% of total.",
                    Severity      = "High",
                    RecentValue   = recentCritical,
                    BaselineValue = recentTotal,
                    Threshold     = SeverityEscalationPct,
                    ActualValue   = Math.Round(critPct, 2),
                    DrillDownPath = "/synqaudit/investigation?severity=Critical",
                });
            }
        }

        // ── Rule 7: EVENTTYPE_CONCENTRATION ──────────────────────────────────

        if (recentTotal > 0)
        {
            var topTypeGroup = await recentBase
                .GroupBy(r => r.EventType)
                .Select(g => new { EventType = g.Key, Count = (long)g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(1)
                .ToListAsync(ct);

            if (topTypeGroup.Count > 0)
            {
                var top = topTypeGroup[0];
                var pct = top.Count / (double)recentTotal * 100.0;

                if (top.Count >= EventTypeConcentrationMin && pct >= EventTypeConcentrationPct)
                {
                    anomalies.Add(new AuditAnomalyItem
                    {
                        RuleKey            = "EVENTTYPE_CONCENTRATION",
                        Title              = "Event type dominance detected",
                        Description        = $"Event type '{top.EventType}' accounts for {pct:F1}% " +
                                             $"of all activity ({top.Count:N0} of {recentTotal:N0} events) " +
                                             $"in the last 24h. Threshold: {EventTypeConcentrationPct}% and ≥{EventTypeConcentrationMin} events.",
                        Severity           = "Low",
                        RecentValue        = top.Count,
                        BaselineValue      = recentTotal,
                        Threshold          = EventTypeConcentrationPct,
                        ActualValue        = Math.Round(pct, 2),
                        AffectedEventType  = top.EventType,
                        DrillDownPath      = $"/synqaudit/investigation?eventType={Uri.EscapeDataString(top.EventType)}",
                    });
                }
            }
        }

        // ── 4. Sort and return ────────────────────────────────────────────────

        var severityOrder = new Dictionary<string, int>
        {
            ["High"]   = 0,
            ["Medium"] = 1,
            ["Low"]    = 2,
        };

        anomalies.Sort((a, b) =>
        {
            var sA = severityOrder.GetValueOrDefault(a.Severity, 9);
            var sB = severityOrder.GetValueOrDefault(b.Severity, 9);
            return sA != sB ? sA.CompareTo(sB) : string.Compare(a.RuleKey, b.RuleKey, StringComparison.Ordinal);
        });

        return new AuditAnomalyResponse
        {
            EvaluatedAt       = now,
            RecentWindowFrom  = recentFrom,
            RecentWindowTo    = recentTo,
            BaselineWindowFrom = baselineFrom,
            BaselineWindowTo  = baselineTo,
            EffectiveTenantId = effectiveTenantId,
            TotalAnomalies    = anomalies.Count,
            Anomalies         = anomalies,
        };
    }
}
