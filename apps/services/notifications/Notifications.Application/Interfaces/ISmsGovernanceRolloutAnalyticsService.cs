namespace Notifications.Application.Interfaces;

// ── Analytics DTOs ────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-022: Aggregate analytics for a rollout plan.
/// No raw phones, message bodies, or credentials.
/// </summary>
public sealed record RolloutAnalyticsDto(
    Guid     RolloutPlanId,
    string   RolloutState,
    string   RolloutStrategy,
    int      TotalStages,
    int      CompletedStages,
    int      ActiveStages,
    int      FailedStages,
    int      TotalCohortTenants,
    int      ActiveCohortTenants,
    int      RolledBackCohortTenants,
    double   BlockRate,
    double   WarnRate,
    double   ReviewRate,
    int      ActivationFailureCount,
    int      PauseEventCount,
    int      ThresholdBreachCount,
    TimeSpan? RolloutDuration,
    IReadOnlyList<RolloutStageAnalyticsDto> StageBreakdown);

/// <summary>
/// LS-NOTIF-SMS-022: Per-stage analytics summary.
/// </summary>
public sealed record RolloutStageAnalyticsDto(
    Guid     StageId,
    int      StageNumber,
    string?  StageName,
    string   StageState,
    int      CohortTenants,
    double   BlockRate,
    double   WarnRate,
    double   ReviewRate,
    int      SampleSize,
    TimeSpan? StageDuration);

/// <summary>
/// LS-NOTIF-SMS-022: Per-cohort analytics summary.
/// TenantId is returned as an opaque identifier only.
/// </summary>
public sealed record RolloutCohortAnalyticsDto(
    Guid     CohortId,
    Guid     TenantId,
    string   CohortName,
    bool     Enabled,
    bool     IsActivated,
    bool     IsRolledBack,
    double   BlockRate,
    double   WarnRate,
    double   ReviewRate,
    int      SampleSize);

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-022: Rollout analytics service.
/// Aggregates governance decision and rule match metric data across rollout cohorts.
/// All data is safe aggregate data — no raw phones, message content, or credentials.
/// Bounded queries prevent unbounded scans.
/// </summary>
public interface ISmsGovernanceRolloutAnalyticsService
{
    /// <summary>
    /// Returns aggregate analytics for the entire rollout plan.
    /// </summary>
    Task<RolloutAnalyticsDto?> GetRolloutAnalyticsAsync(Guid rolloutId, CancellationToken ct = default);

    /// <summary>
    /// Returns per-stage analytics for a rollout plan.
    /// </summary>
    Task<IReadOnlyList<RolloutStageAnalyticsDto>> GetRolloutStageAnalyticsAsync(Guid rolloutId, CancellationToken ct = default);

    /// <summary>
    /// Returns per-cohort tenant analytics for a rollout plan.
    /// TenantId is returned as opaque identifier only.
    /// </summary>
    Task<IReadOnlyList<RolloutCohortAnalyticsDto>> GetRolloutCohortAnalyticsAsync(Guid rolloutId, CancellationToken ct = default);
}
