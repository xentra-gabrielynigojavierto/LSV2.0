namespace Notifications.Application.Interfaces;

// ── Result types ──────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-022: Rollout health evaluation result.
/// No raw phones, message bodies, credentials, or provider payloads are returned.
/// </summary>
public sealed record RolloutHealthResult(
    bool    IsHealthy,
    bool    InsufficientData,
    string  RecommendedAction,   // "continue" | "pause" | "rollback" | "monitor"
    string? BreachReason,
    double  BlockRate,
    double  WarnRate,
    double  ReviewRate,
    double  ActivationFailureRate,
    int     SampleSize,
    int     AffectedCohortCount);

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-022: Threshold evaluator for rollout health.
/// Uses local governance decision and rule match metric data only.
/// Never calls external APIs.
/// Evaluation failures fail open (healthy) when FailOpenOnRolloutEvaluationError = true.
/// </summary>
public interface ISmsGovernanceRolloutEvaluator
{
    /// <summary>
    /// Evaluates the overall health of a rollout plan by aggregating across all active cohorts.
    /// Uses SmsGovernanceRuleMatchMetric data for the last 24-hour window.
    /// Returns InsufficientData = true when SampleSize &lt; configured minimum.
    /// </summary>
    Task<RolloutHealthResult> EvaluateRolloutHealthAsync(Guid rolloutId, CancellationToken ct = default);

    /// <summary>
    /// Evaluates the health of a specific rollout stage by filtering metrics to cohort tenants
    /// assigned to this stage.
    /// </summary>
    Task<RolloutHealthResult> EvaluateStageHealthAsync(Guid rolloutId, Guid stageId, CancellationToken ct = default);
}
