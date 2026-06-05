using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-020: Governance rule effectiveness analytics service.
///
/// Queries aggregate metrics from SmsGovernanceRuleMatchMetric (daily buckets).
/// Also implements ISmsGovernanceMatchRecorder — match recording is
/// fire-and-forget (exceptions are swallowed to protect the delivery pipeline).
///
/// No message content, raw phone numbers, or credentials are stored or returned.
/// </summary>
public sealed class SmsGovernanceAnalyticsService
    : ISmsGovernanceAnalyticsService, ISmsGovernanceMatchRecorder
{
    private readonly NotificationsDbContext                   _db;
    private readonly SmsGovernanceAnalyticsOptions           _opts;
    private readonly ILogger<SmsGovernanceAnalyticsService>  _logger;

    public SmsGovernanceAnalyticsService(
        NotificationsDbContext                          db,
        IOptions<SmsGovernanceAnalyticsOptions>        opts,
        ILogger<SmsGovernanceAnalyticsService>         logger)
    {
        _db     = db;
        _opts   = opts.Value;
        _logger = logger;
    }

    // ─── ISmsGovernanceMatchRecorder ─────────────────────────────────────────

    /// <summary>
    /// Fire-and-forget match recording.
    /// Called from SmsGovernanceRuleEngine after each evaluation.
    /// Exceptions are swallowed — delivery pipeline must not be affected.
    /// </summary>
    public void RecordMatches(
        SmsGovernanceRuleEvaluationResult result,
        Guid? tenantId,
        bool isDryRun)
    {
        if (!_opts.Enabled) return;
        if (result.MatchedRules.Count == 0) return;

        // Fire and forget — no await, no throwing
        _ = Task.Run(async () =>
        {
            try
            {
                await RecordMatchesInternalAsync(result, tenantId, isDryRun);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RecordMatches: non-blocking metric recording failed — ignored");
            }
        });
    }

    private async Task RecordMatchesInternalAsync(
        SmsGovernanceRuleEvaluationResult result,
        Guid? tenantId,
        bool isDryRun)
    {
        var windowStart = DateTime.UtcNow.Date;                         // midnight UTC
        var windowEnd   = windowStart.AddDays(1).AddTicks(-1);         // 23:59:59.9999999
        var now         = DateTime.UtcNow;

        foreach (var match in result.MatchedRules)
        {
            // Upsert via raw SQL for atomic increment — avoids read-modify-write races
            await _db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO `ntf_SmsGovernanceRuleMatchMetrics`
                    (`Id`, `RuleId`, `RulePackId`, `TenantId`, `RuleType`, `Severity`,
                     `DecisionType`, `ReasonCode`,
                     `MatchCount`, `BlockCount`, `WarnCount`, `ReviewCount`, `AllowCount`,
                     `SimulationCount`, `LiveCount`,
                     `WindowStart`, `WindowEnd`, `LastMatchedAt`, `CreatedAt`, `UpdatedAt`)
                VALUES
                    (UUID(), {0}, {1}, {2}, {3}, {4},
                     {5}, {6},
                     1,
                     CASE WHEN {5} = 'block'            THEN 1 ELSE 0 END,
                     CASE WHEN {5} = 'warn'             THEN 1 ELSE 0 END,
                     CASE WHEN {5} = 'review_required'  THEN 1 ELSE 0 END,
                     CASE WHEN {5} = 'allow'            THEN 1 ELSE 0 END,
                     CASE WHEN {7} = 1                  THEN 1 ELSE 0 END,
                     CASE WHEN {7} = 0                  THEN 1 ELSE 0 END,
                     {8}, {9}, {10}, {10}, {10})
                ON DUPLICATE KEY UPDATE
                    `MatchCount`      = `MatchCount`      + 1,
                    `BlockCount`      = `BlockCount`      + CASE WHEN {5} = 'block'           THEN 1 ELSE 0 END,
                    `WarnCount`       = `WarnCount`       + CASE WHEN {5} = 'warn'            THEN 1 ELSE 0 END,
                    `ReviewCount`     = `ReviewCount`     + CASE WHEN {5} = 'review_required' THEN 1 ELSE 0 END,
                    `AllowCount`      = `AllowCount`      + CASE WHEN {5} = 'allow'           THEN 1 ELSE 0 END,
                    `SimulationCount` = `SimulationCount` + CASE WHEN {7} = 1                 THEN 1 ELSE 0 END,
                    `LiveCount`       = `LiveCount`       + CASE WHEN {7} = 0                 THEN 1 ELSE 0 END,
                    `LastMatchedAt`   = {10},
                    `UpdatedAt`       = {10}
                """,
                match.RuleId,
                match.RulePackId,
                (object?)tenantId ?? DBNull.Value,
                match.RuleType,
                match.Severity,
                result.DecisionType,
                result.ReasonCode,
                isDryRun ? 1 : 0,
                windowStart,
                windowEnd,
                now);
        }
    }

    // ─── ISmsGovernanceAnalyticsService ──────────────────────────────────────

    public async Task<(IReadOnlyList<RuleEffectivenessRow> Rows, int Total)> GetRuleEffectivenessAsync(
        GovernanceAnalyticsQuery query,
        CancellationToken ct = default)
    {
        var (from, to) = ResolveDateWindow(query);

        var q = _db.SmsGovernanceRuleMatchMetrics
            .AsNoTracking()
            .Where(m => m.WindowStart >= from && m.WindowStart <= to);

        q = ApplyCommonFilters(q, query);

        var grouped = await q
            .GroupBy(m => new { m.RuleId, m.RulePackId, m.RuleType, m.Severity })
            .Select(g => new
            {
                g.Key.RuleId,
                g.Key.RulePackId,
                g.Key.RuleType,
                g.Key.Severity,
                MatchCount      = g.Sum(m => m.MatchCount),
                BlockCount      = g.Sum(m => m.BlockCount),
                WarnCount       = g.Sum(m => m.WarnCount),
                ReviewCount     = g.Sum(m => m.ReviewCount),
                AllowCount      = g.Sum(m => m.AllowCount),
                SimulationCount = g.Sum(m => m.SimulationCount),
                LiveCount       = g.Sum(m => m.LiveCount),
                LastMatchedAt   = g.Max(m => m.LastMatchedAt),
            })
            .OrderByDescending(g => g.MatchCount)
            .ToListAsync(ct);

        // Enrich with rule names from the rules table
        var ruleIds = grouped
            .Where(g => g.RuleId.HasValue)
            .Select(g => g.RuleId!.Value)
            .Distinct()
            .ToList();

        var ruleNames = ruleIds.Count > 0
            ? await _db.SmsGovernanceRules
                .AsNoTracking()
                .Where(r => ruleIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Name })
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct)
            : new Dictionary<Guid, string>();

        var total = grouped.Count;
        var rows  = grouped
            .Skip((query.Page - 1) * query.PageSize)
            .Take(Math.Min(query.PageSize, _opts.MaxResultRows))
            .Select(g => new RuleEffectivenessRow
            {
                RuleId          = g.RuleId,
                RulePackId      = g.RulePackId,
                RuleName        = g.RuleId.HasValue && ruleNames.TryGetValue(g.RuleId.Value, out var n) ? n : null,
                RuleType        = g.RuleType,
                Severity        = g.Severity,
                TotalMatches    = g.MatchCount,
                BlockCount      = g.BlockCount,
                WarnCount       = g.WarnCount,
                ReviewCount     = g.ReviewCount,
                AllowCount      = g.AllowCount,
                SimulationCount = g.SimulationCount,
                LiveCount       = g.LiveCount,
                BlockRate       = g.MatchCount > 0 ? (double)g.BlockCount  / g.MatchCount : 0,
                ReviewRate      = g.MatchCount > 0 ? (double)g.ReviewCount / g.MatchCount : 0,
                LastMatchedAt   = g.LastMatchedAt,
            })
            .ToList();

        return (rows, total);
    }

    public async Task<IReadOnlyList<MatchAnalyticsRow>> GetRuleMatchAnalyticsAsync(
        GovernanceAnalyticsQuery query,
        CancellationToken ct = default)
    {
        var (from, to) = ResolveDateWindow(query);

        var q = _db.SmsGovernanceRuleMatchMetrics
            .AsNoTracking()
            .Where(m => m.WindowStart >= from && m.WindowStart <= to);

        q = ApplyCommonFilters(q, query);

        return await q
            .OrderBy(m => m.WindowStart)
            .Take(_opts.MaxResultRows)
            .Select(m => new MatchAnalyticsRow
            {
                WindowStart     = m.WindowStart,
                RuleId          = m.RuleId,
                RulePackId      = m.RulePackId,
                RuleType        = m.RuleType,
                Severity        = m.Severity,
                DecisionType    = m.DecisionType,
                MatchCount      = m.MatchCount,
                SimulationCount = m.SimulationCount,
                LiveCount       = m.LiveCount,
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FalsePositiveCandidateRow>> GetFalsePositiveCandidatesAsync(
        GovernanceAnalyticsQuery query,
        CancellationToken ct = default)
    {
        var (from, to) = ResolveDateWindow(query);

        var grouped = await _db.SmsGovernanceRuleMatchMetrics
            .AsNoTracking()
            .Where(m => m.WindowStart >= from && m.WindowStart <= to)
            .GroupBy(m => new { m.RuleId, m.RulePackId, m.RuleType, m.Severity })
            .Select(g => new
            {
                g.Key.RuleId,
                g.Key.RulePackId,
                g.Key.RuleType,
                g.Key.Severity,
                MatchCount      = g.Sum(m => m.MatchCount),
                WarnCount       = g.Sum(m => m.WarnCount),
                SimulationCount = g.Sum(m => m.SimulationCount),
                LiveCount       = g.Sum(m => m.LiveCount),
            })
            .ToListAsync(ct);

        var ruleIds = grouped
            .Where(g => g.RuleId.HasValue)
            .Select(g => g.RuleId!.Value)
            .Distinct()
            .ToList();

        var ruleNames = ruleIds.Count > 0
            ? await _db.SmsGovernanceRules
                .AsNoTracking()
                .Where(r => ruleIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Name })
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct)
            : new Dictionary<Guid, string>();

        var candidates = new List<FalsePositiveCandidateRow>();

        foreach (var g in grouped)
        {
            var heuristic = DetectFalsePositiveHeuristic(g.WarnCount, g.MatchCount, g.SimulationCount, g.LiveCount);
            if (heuristic == null) continue;

            var fpScore = ComputeFpScore(g.WarnCount, g.MatchCount, g.SimulationCount, g.LiveCount);

            candidates.Add(new FalsePositiveCandidateRow
            {
                RuleId          = g.RuleId,
                RulePackId      = g.RulePackId,
                RuleName        = g.RuleId.HasValue && ruleNames.TryGetValue(g.RuleId.Value, out var n) ? n : null,
                RuleType        = g.RuleType,
                Severity        = g.Severity,
                TotalMatches    = g.MatchCount,
                WarnCount       = g.WarnCount,
                SimulationCount = g.SimulationCount,
                LiveCount       = g.LiveCount,
                Heuristic       = heuristic,
                FpScore         = fpScore,
            });
        }

        return candidates
            .OrderByDescending(c => c.FpScore)
            .Take(_opts.MaxResultRows)
            .ToList();
    }

    public async Task<(IReadOnlyList<PackEffectivenessRow> Rows, int Total)> GetPackEffectivenessAsync(
        GovernanceAnalyticsQuery query,
        CancellationToken ct = default)
    {
        var (from, to) = ResolveDateWindow(query);

        var q = _db.SmsGovernanceRuleMatchMetrics
            .AsNoTracking()
            .Where(m => m.WindowStart >= from && m.WindowStart <= to
                        && m.RulePackId != null);

        if (query.TenantId.HasValue) q = q.Where(m => m.TenantId == query.TenantId);

        var grouped = await q
            .GroupBy(m => m.RulePackId)
            .Select(g => new
            {
                RulePackId   = g.Key,
                MatchCount   = g.Sum(m => m.MatchCount),
                BlockCount   = g.Sum(m => m.BlockCount),
                WarnCount    = g.Sum(m => m.WarnCount),
                ReviewCount  = g.Sum(m => m.ReviewCount),
                AllowCount   = g.Sum(m => m.AllowCount),
                LastMatchedAt = g.Max(m => m.LastMatchedAt),
            })
            .OrderByDescending(g => g.MatchCount)
            .ToListAsync(ct);

        // Enrich with pack names and active rule counts
        var packIds = grouped.Where(g => g.RulePackId.HasValue).Select(g => g.RulePackId!.Value).ToList();

        var packMeta = packIds.Count > 0
            ? await _db.SmsGovernanceRulePacks
                .AsNoTracking()
                .Where(p => packIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct)
            : new Dictionary<Guid, string>();

        var activeRuleCounts = packIds.Count > 0
            ? await _db.SmsGovernanceRules
                .AsNoTracking()
                .Where(r => packIds.Contains(r.RulePackId) && r.Enabled)
                .GroupBy(r => r.RulePackId)
                .Select(g => new { PackId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.PackId, g => g.Count, ct)
            : new Dictionary<Guid, int>();

        var total = grouped.Count;
        var rows  = grouped
            .Skip((query.Page - 1) * query.PageSize)
            .Take(Math.Min(query.PageSize, _opts.MaxResultRows))
            .Select(g => new PackEffectivenessRow
            {
                RulePackId    = g.RulePackId,
                PackName      = g.RulePackId.HasValue && packMeta.TryGetValue(g.RulePackId.Value, out var n) ? n : null,
                ActiveRules   = g.RulePackId.HasValue && activeRuleCounts.TryGetValue(g.RulePackId.Value, out var c) ? c : 0,
                TotalMatches  = g.MatchCount,
                BlockCount    = g.BlockCount,
                WarnCount     = g.WarnCount,
                ReviewCount   = g.ReviewCount,
                AllowCount    = g.AllowCount,
                BlockRate     = g.MatchCount > 0 ? (double)g.BlockCount / g.MatchCount : 0,
                LastMatchedAt = g.LastMatchedAt,
            })
            .ToList();

        return (rows, total);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private (DateTime From, DateTime To) ResolveDateWindow(GovernanceAnalyticsQuery query)
    {
        var to   = query.To   ?? DateTime.UtcNow;
        var from = query.From ?? to.AddDays(-_opts.WindowDays);
        return (from, to);
    }

    private static IQueryable<SmsGovernanceRuleMatchMetric> ApplyCommonFilters(
        IQueryable<SmsGovernanceRuleMatchMetric> q,
        GovernanceAnalyticsQuery query)
    {
        if (query.RuleId.HasValue)              q = q.Where(m => m.RuleId     == query.RuleId);
        if (query.RulePackId.HasValue)          q = q.Where(m => m.RulePackId == query.RulePackId);
        if (query.TenantId.HasValue)            q = q.Where(m => m.TenantId   == query.TenantId);
        if (!string.IsNullOrEmpty(query.RuleType))  q = q.Where(m => m.RuleType == query.RuleType);
        if (!string.IsNullOrEmpty(query.Severity))  q = q.Where(m => m.Severity == query.Severity);
        if (!query.IncludeSimulation) q = q.Where(m => m.SimulationCount == 0);
        if (!query.IncludeLive)       q = q.Where(m => m.LiveCount       == 0);
        return q;
    }

    private string? DetectFalsePositiveHeuristic(int warn, int total, int sim, int live)
    {
        if (total == 0) return null;

        // Heuristic 1: high warn count with very low block count → likely too broad
        if (warn >= _opts.FalsePositiveWarnThreshold && total > 0
            && (double)warn / total > 0.8)
            return "High warn rate (>80% of matches are warns — rule may be too broad)";

        // Heuristic 2: predominantly simulation matches, rarely live
        if (sim > 0 && live == 0 && sim >= 5)
            return "Simulation-only matches (never triggered in live delivery pipeline)";

        if (total >= 10 && live > 0 && (double)live / (sim + live) < _opts.FalsePositiveLiveToSimRatio)
            return $"Low live-to-simulation ratio ({live}/{sim + live}) — rule may not be relevant for live traffic";

        return null;
    }

    private static double ComputeFpScore(int warn, int total, int sim, int live)
    {
        if (total == 0) return 0;
        var warnRatio = (double)warn / total;
        var simRatio  = total > 0 ? (double)sim / (sim + live + 1) : 0;
        return Math.Round((warnRatio * 0.6) + (simRatio * 0.4), 4);
    }
}
