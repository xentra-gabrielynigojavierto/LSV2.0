namespace Notifications.Application.Options;

/// <summary>
/// LS-NOTIF-SMS-022: Configuration for governance rollout orchestration.
/// Bound from appsettings.json section "SmsGovernanceRollouts".
/// </summary>
public sealed class SmsGovernanceRolloutsOptions
{
    public const string SectionName = "SmsGovernanceRollouts";

    /// <summary>Master switch. When false, rollout APIs return 503.</summary>
    public bool    Enabled                              { get; set; } = true;

    /// <summary>
    /// When true, SmsGovernanceRolloutWorker runs and processes active rollouts.
    /// Disabled by default — enable explicitly in production.
    /// </summary>
    public bool    RolloutWorkerEnabled                 { get; set; } = false;

    /// <summary>How often the rollout worker polls for stage advancement, in minutes.</summary>
    public int     RolloutPollMinutes                   { get; set; } = 5;

    /// <summary>Maximum number of rollouts the worker processes per cycle.</summary>
    public int     MaxRolloutsPerCycle                  { get; set; } = 10;

    /// <summary>Default TenantPercentage for the first canary stage when not explicitly set.</summary>
    public decimal DefaultCanaryPercentage              { get; set; } = 5m;

    /// <summary>Default DurationMinutes for rollout stages when not explicitly set.</summary>
    public int     DefaultStageDurationMinutes          { get; set; } = 60;

    /// <summary>
    /// When true and a threshold breach is detected, the rollout is automatically paused.
    /// The operator must manually resume or rollback.
    /// </summary>
    public bool    AutoPauseOnThresholdBreach           { get; set; } = true;

    /// <summary>
    /// When true and a CRITICAL threshold breach is detected (action = "rollback" in threshold JSON),
    /// the rollout is automatically rolled back.
    /// When false, the rollout is paused and the operator decides.
    /// </summary>
    public bool    AutoRollbackOnCriticalThresholdBreach { get; set; } = false;

    /// <summary>
    /// When true, evaluation errors (e.g. DB query failures) treat the rollout as healthy
    /// and do not pause/rollback. When false, evaluation errors pause the rollout.
    /// Default true (fail open) to prevent spurious production pauses.
    /// </summary>
    public bool    FailOpenOnRolloutEvaluationError      { get; set; } = true;
}
