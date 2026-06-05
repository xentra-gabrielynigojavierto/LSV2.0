using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Interfaces;
using Notifications.Application.Options;
using Notifications.Domain;
using Notifications.Infrastructure.Data;
using System.Text.Json;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-SMS-022: Rollout threshold evaluator.
/// Uses SmsGovernanceRuleMatchMetric and SmsGovernanceRolloutAuditEvent data only.
/// Never calls external APIs.
/// Fail-open behavior controlled by FailOpenOnRolloutEvaluationError.
/// </summary>
public sealed class SmsGovernanceRolloutEvaluator : ISmsGovernanceRolloutEvaluator
{
    private readonly NotificationsDbContext           _db;
    private readonly SmsGovernanceRolloutsOptions     _opts;
    private readonly ILogger<SmsGovernanceRolloutEvaluator> _logger;

    private const int DefaultMinimumSampleSize = 50;
    private static readonly TimeSpan MetricWindow = TimeSpan.FromHours(24);

    public SmsGovernanceRolloutEvaluator(
        NotificationsDbContext                   db,
        IOptions<SmsGovernanceRolloutsOptions>   opts,
        ILogger<SmsGovernanceRolloutEvaluator>   logger)
    {
        _db     = db;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task<RolloutHealthResult> EvaluateRolloutHealthAsync(
        Guid rolloutId, CancellationToken ct = default)
    {
        try
        {
            var plan = await _db.SmsGovernanceRolloutPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == rolloutId, ct);

            if (plan is null)
                return HealthyResult("continue", 0, 0, 0, 0, 0);

            var cohortTenantIds = await _db.SmsGovernanceTenantCohorts
                .AsNoTracking()
                .Where(c => c.RolloutPlanId == rolloutId && c.Enabled)
                .Select(c => c.TenantId)
                .ToListAsync(ct);

            return await EvaluateForTenantsAsync(cohortTenantIds, plan.RollbackThresholdJson, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SmsGovernanceRolloutEvaluator: error evaluating rollout {RolloutId} health", rolloutId);
            return _opts.FailOpenOnRolloutEvaluationError
                ? HealthyResult("continue", 0, 0, 0, 0, 0)
                : new RolloutHealthResult(false, false, "pause", "evaluation_error",
                    0, 0, 0, 0, 0, 0);
        }
    }

    public async Task<RolloutHealthResult> EvaluateStageHealthAsync(
        Guid rolloutId, Guid stageId, CancellationToken ct = default)
    {
        try
        {
            var plan = await _db.SmsGovernanceRolloutPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == rolloutId, ct);

            if (plan is null)
                return HealthyResult("continue", 0, 0, 0, 0, 0);

            var stageTenantIds = await _db.SmsGovernanceTenantCohorts
                .AsNoTracking()
                .Where(c => c.RolloutPlanId == rolloutId
                         && c.StageId == stageId
                         && c.Enabled)
                .Select(c => c.TenantId)
                .ToListAsync(ct);

            if (!stageTenantIds.Any())
            {
                // Fall back to plan-level cohorts if no stage-specific cohorts
                stageTenantIds = await _db.SmsGovernanceTenantCohorts
                    .AsNoTracking()
                    .Where(c => c.RolloutPlanId == rolloutId && c.StageId == null && c.Enabled)
                    .Select(c => c.TenantId)
                    .ToListAsync(ct);
            }

            return await EvaluateForTenantsAsync(stageTenantIds, plan.RollbackThresholdJson, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SmsGovernanceRolloutEvaluator: error evaluating stage {StageId} health", stageId);
            return _opts.FailOpenOnRolloutEvaluationError
                ? HealthyResult("continue", 0, 0, 0, 0, 0)
                : new RolloutHealthResult(false, false, "pause", "evaluation_error",
                    0, 0, 0, 0, 0, 0);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<RolloutHealthResult> EvaluateForTenantsAsync(
        IReadOnlyList<Guid> tenantIds,
        string?             thresholdJson,
        CancellationToken   ct)
    {
        if (tenantIds.Count == 0)
            return HealthyResult("continue", 0, 0, 0, 0, 0);

        var threshold = ParseThreshold(thresholdJson);
        var windowStart = DateTime.UtcNow - MetricWindow;

        var metrics = await _db.SmsGovernanceRuleMatchMetrics
            .AsNoTracking()
            .Where(m => tenantIds.Contains(m.TenantId!.Value)
                     && m.WindowStart >= windowStart)
            .ToListAsync(ct);

        if (metrics.Count == 0)
            return HealthyResult("continue", 0, 0, 0, 0, tenantIds.Count);

        var totalMatch  = metrics.Sum(m => m.MatchCount);
        var totalBlock  = metrics.Sum(m => m.BlockCount);
        var totalWarn   = metrics.Sum(m => m.WarnCount);
        var totalReview = metrics.Sum(m => m.ReviewCount);

        if (totalMatch < threshold.MinimumSampleSize)
        {
            return new RolloutHealthResult(
                IsHealthy:             true,
                InsufficientData:      true,
                RecommendedAction:     "monitor",
                BreachReason:          null,
                BlockRate:             0,
                WarnRate:              0,
                ReviewRate:            0,
                ActivationFailureRate: 0,
                SampleSize:            totalMatch,
                AffectedCohortCount:   tenantIds.Count);
        }

        var blockRate  = totalMatch > 0 ? (double)totalBlock  / totalMatch : 0;
        var warnRate   = totalMatch > 0 ? (double)totalWarn   / totalMatch : 0;
        var reviewRate = totalMatch > 0 ? (double)totalReview / totalMatch : 0;

        string? breachReason = null;
        var     action       = "continue";

        if (blockRate > threshold.MaxBlockRate)
        {
            breachReason = $"block_rate {blockRate:P1} exceeds threshold {threshold.MaxBlockRate:P1}";
            action       = threshold.Action;
        }
        else if (warnRate > threshold.MaxWarnRate)
        {
            breachReason = $"warn_rate {warnRate:P1} exceeds threshold {threshold.MaxWarnRate:P1}";
            action       = threshold.Action;
        }
        else if (reviewRate > threshold.MaxReviewRequiredRate)
        {
            breachReason = $"review_rate {reviewRate:P1} exceeds threshold {threshold.MaxReviewRequiredRate:P1}";
            action       = threshold.Action;
        }

        var isHealthy = breachReason is null;

        return new RolloutHealthResult(
            IsHealthy:             isHealthy,
            InsufficientData:      false,
            RecommendedAction:     action,
            BreachReason:          breachReason,
            BlockRate:             blockRate,
            WarnRate:              warnRate,
            ReviewRate:            reviewRate,
            ActivationFailureRate: 0,
            SampleSize:            totalMatch,
            AffectedCohortCount:   tenantIds.Count);
    }

    private static RolloutHealthResult HealthyResult(
        string action, double br, double wr, double rr, double afr, int cohorts) =>
        new(true, false, action, null, br, wr, rr, afr, 0, cohorts);

    private static ThresholdConfig ParseThreshold(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ThresholdConfig.Default;
        try
        {
            return JsonSerializer.Deserialize<ThresholdConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? ThresholdConfig.Default;
        }
        catch
        {
            return ThresholdConfig.Default;
        }
    }

    private sealed class ThresholdConfig
    {
        public double MaxBlockRate           { get; set; } = 0.15;
        public double MaxWarnRate            { get; set; } = 0.30;
        public double MaxReviewRequiredRate  { get; set; } = 0.10;
        public double MaxActivationFailureRate { get; set; } = 0.05;
        public int    MinimumSampleSize      { get; set; } = DefaultMinimumSampleSize;
        public string Action                 { get; set; } = "pause";

        public static readonly ThresholdConfig Default = new();
    }
}
