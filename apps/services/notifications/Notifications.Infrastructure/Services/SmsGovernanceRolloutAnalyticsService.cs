using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-022: Rollout analytics service.
/// Queries SmsGovernanceRuleMatchMetric and rollout audit events.
/// Returns safe aggregate data only — no raw phones, message content, or credentials.
/// All queries are bounded to the last 7 days of metrics data.
/// </summary>
public sealed class SmsGovernanceRolloutAnalyticsService : ISmsGovernanceRolloutAnalyticsService
{
    private readonly NotificationsDbContext                       _db;
    private readonly ILogger<SmsGovernanceRolloutAnalyticsService> _logger;

    private static readonly TimeSpan MetricWindow = TimeSpan.FromDays(7);

    public SmsGovernanceRolloutAnalyticsService(
        NotificationsDbContext                         db,
        ILogger<SmsGovernanceRolloutAnalyticsService>  logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<RolloutAnalyticsDto?> GetRolloutAnalyticsAsync(
        Guid rolloutId, CancellationToken ct = default)
    {
        var plan = await _db.SmsGovernanceRolloutPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == rolloutId, ct);

        if (plan is null) return null;

        var stages = await _db.SmsGovernanceRolloutStages
            .AsNoTracking()
            .Where(s => s.RolloutPlanId == rolloutId)
            .ToListAsync(ct);

        var cohorts = await _db.SmsGovernanceTenantCohorts
            .AsNoTracking()
            .Where(c => c.RolloutPlanId == rolloutId)
            .ToListAsync(ct);

        var auditEvents = await _db.SmsGovernanceRolloutAuditEvents
            .AsNoTracking()
            .Where(e => e.RolloutPlanId == rolloutId)
            .ToListAsync(ct);

        var tenantIds   = cohorts.Where(c => c.Enabled).Select(c => c.TenantId).Distinct().ToList();
        var windowStart = DateTime.UtcNow - MetricWindow;

        var metrics = tenantIds.Count > 0
            ? await _db.SmsGovernanceRuleMatchMetrics
                .AsNoTracking()
                .Where(m => tenantIds.Contains(m.TenantId!.Value) && m.WindowStart >= windowStart)
                .ToListAsync(ct)
            : new List<SmsGovernanceRuleMatchMetric>();

        var totalMatch  = metrics.Sum(m => m.MatchCount);
        var totalBlock  = metrics.Sum(m => m.BlockCount);
        var totalWarn   = metrics.Sum(m => m.WarnCount);
        var totalReview = metrics.Sum(m => m.ReviewCount);

        var blockRate  = totalMatch > 0 ? (double)totalBlock  / totalMatch : 0;
        var warnRate   = totalMatch > 0 ? (double)totalWarn   / totalMatch : 0;
        var reviewRate = totalMatch > 0 ? (double)totalReview / totalMatch : 0;

        var pauseCount     = auditEvents.Count(e => e.EventType == RolloutAuditEventTypes.RolloutPaused);
        var thresholdCount = auditEvents.Count(e => e.EventType == RolloutAuditEventTypes.ThresholdExceeded);

        TimeSpan? duration = plan.StartedAt.HasValue
            ? (plan.CompletedAt ?? plan.FailedAt ?? plan.RolledBackAt ?? DateTime.UtcNow) - plan.StartedAt.Value
            : null;

        var stageBreakdown = await GetRolloutStageAnalyticsAsync(rolloutId, ct);

        return new RolloutAnalyticsDto(
            RolloutPlanId:           rolloutId,
            RolloutState:            plan.RolloutState,
            RolloutStrategy:         plan.RolloutStrategy,
            TotalStages:             stages.Count,
            CompletedStages:         stages.Count(s => s.StageState == RolloutStageStates.Completed),
            ActiveStages:            stages.Count(s => s.StageState == RolloutStageStates.Active),
            FailedStages:            stages.Count(s => s.StageState == RolloutStageStates.Failed),
            TotalCohortTenants:      cohorts.Count,
            ActiveCohortTenants:     cohorts.Count(c => c.ActivatedAt.HasValue && !c.RolledBackAt.HasValue),
            RolledBackCohortTenants: cohorts.Count(c => c.RolledBackAt.HasValue),
            BlockRate:               blockRate,
            WarnRate:                warnRate,
            ReviewRate:              reviewRate,
            ActivationFailureCount:  auditEvents.Count(e => e.EventType == RolloutAuditEventTypes.StageFailed),
            PauseEventCount:         pauseCount,
            ThresholdBreachCount:    thresholdCount,
            RolloutDuration:         duration,
            StageBreakdown:          stageBreakdown);
    }

    public async Task<IReadOnlyList<RolloutStageAnalyticsDto>> GetRolloutStageAnalyticsAsync(
        Guid rolloutId, CancellationToken ct = default)
    {
        var stages = await _db.SmsGovernanceRolloutStages
            .AsNoTracking()
            .Where(s => s.RolloutPlanId == rolloutId)
            .OrderBy(s => s.StageNumber)
            .ToListAsync(ct);

        var cohorts = await _db.SmsGovernanceTenantCohorts
            .AsNoTracking()
            .Where(c => c.RolloutPlanId == rolloutId)
            .ToListAsync(ct);

        var windowStart = DateTime.UtcNow - MetricWindow;
        var result      = new List<RolloutStageAnalyticsDto>(stages.Count);

        foreach (var stage in stages)
        {
            var stageTenantIds = cohorts
                .Where(c => c.StageId == stage.Id && c.Enabled)
                .Select(c => c.TenantId)
                .Distinct()
                .ToList();

            if (!stageTenantIds.Any())
            {
                stageTenantIds = cohorts
                    .Where(c => c.StageId == null && c.Enabled)
                    .Select(c => c.TenantId)
                    .Distinct()
                    .ToList();
            }

            var metrics = stageTenantIds.Count > 0
                ? await _db.SmsGovernanceRuleMatchMetrics
                    .AsNoTracking()
                    .Where(m => stageTenantIds.Contains(m.TenantId!.Value) && m.WindowStart >= windowStart)
                    .ToListAsync(ct)
                : new List<SmsGovernanceRuleMatchMetric>();

            var totalMatch  = metrics.Sum(m => m.MatchCount);
            var blockRate  = totalMatch > 0 ? (double)metrics.Sum(m => m.BlockCount)  / totalMatch : 0;
            var warnRate   = totalMatch > 0 ? (double)metrics.Sum(m => m.WarnCount)   / totalMatch : 0;
            var reviewRate = totalMatch > 0 ? (double)metrics.Sum(m => m.ReviewCount) / totalMatch : 0;

            TimeSpan? stageDuration = stage.StartedAt.HasValue
                ? (stage.CompletedAt ?? stage.FailedAt ?? DateTime.UtcNow) - stage.StartedAt.Value
                : null;

            result.Add(new RolloutStageAnalyticsDto(
                StageId:       stage.Id,
                StageNumber:   stage.StageNumber,
                StageName:     stage.StageName,
                StageState:    stage.StageState,
                CohortTenants: stageTenantIds.Count,
                BlockRate:     blockRate,
                WarnRate:      warnRate,
                ReviewRate:    reviewRate,
                SampleSize:    totalMatch,
                StageDuration: stageDuration));
        }

        return result;
    }

    public async Task<IReadOnlyList<RolloutCohortAnalyticsDto>> GetRolloutCohortAnalyticsAsync(
        Guid rolloutId, CancellationToken ct = default)
    {
        var cohorts = await _db.SmsGovernanceTenantCohorts
            .AsNoTracking()
            .Where(c => c.RolloutPlanId == rolloutId)
            .ToListAsync(ct);

        var windowStart = DateTime.UtcNow - MetricWindow;
        var result      = new List<RolloutCohortAnalyticsDto>(cohorts.Count);

        foreach (var cohort in cohorts)
        {
            var metrics = await _db.SmsGovernanceRuleMatchMetrics
                .AsNoTracking()
                .Where(m => m.TenantId == cohort.TenantId && m.WindowStart >= windowStart)
                .ToListAsync(ct);

            var totalMatch  = metrics.Sum(m => m.MatchCount);
            var blockRate  = totalMatch > 0 ? (double)metrics.Sum(m => m.BlockCount)  / totalMatch : 0;
            var warnRate   = totalMatch > 0 ? (double)metrics.Sum(m => m.WarnCount)   / totalMatch : 0;
            var reviewRate = totalMatch > 0 ? (double)metrics.Sum(m => m.ReviewCount) / totalMatch : 0;

            result.Add(new RolloutCohortAnalyticsDto(
                CohortId:    cohort.Id,
                TenantId:    cohort.TenantId,
                CohortName:  cohort.CohortName,
                Enabled:     cohort.Enabled,
                IsActivated: cohort.ActivatedAt.HasValue,
                IsRolledBack: cohort.RolledBackAt.HasValue,
                BlockRate:   blockRate,
                WarnRate:    warnRate,
                ReviewRate:  reviewRate,
                SampleSize:  totalMatch));
        }

        return result;
    }
}
