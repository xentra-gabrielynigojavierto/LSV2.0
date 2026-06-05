namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-022: A single stage within a governance rollout plan.
/// Each stage defines a tenant percentage or explicit cohort scope, an observation window,
/// and transitions sequentially once the prior stage completes and health thresholds pass.
///
/// Only one stage may be active at a time per rollout plan.
/// StageNumber is unique per RolloutPlanId and defines progression order.
/// </summary>
public sealed class SmsGovernanceRolloutStage
{
    public Guid    Id              { get; set; } = Guid.NewGuid();
    public Guid    RolloutPlanId   { get; set; }

    /// <summary>Ordered execution position. Unique per rollout plan. 1-based.</summary>
    public int     StageNumber     { get; set; }

    public string? StageName       { get; set; }

    /// <summary>pending | active | completed | paused | failed | rolled_back</summary>
    public string  StageState      { get; set; } = RolloutStageStates.Pending;

    /// <summary>
    /// Percentage (0-100) of tenants/cohorts targeted in this stage.
    /// Null for explicit cohort-list strategies.
    /// </summary>
    public decimal? TenantPercentage { get; set; }

    /// <summary>
    /// Observation window in minutes. The rollout worker will not advance past this stage
    /// until DurationMinutes have elapsed after StartedAt and health is acceptable.
    /// Null = must be manually advanced.
    /// </summary>
    public int?    DurationMinutes  { get; set; }

    public DateTime? StartedAt      { get; set; }
    public DateTime? CompletedAt    { get; set; }
    public DateTime? FailedAt       { get; set; }
    public string?   FailureReason  { get; set; }

    public DateTime  CreatedAt      { get; set; }
    public DateTime  UpdatedAt      { get; set; }
}

public static class RolloutStageStates
{
    public const string Pending    = "pending";
    public const string Active     = "active";
    public const string Completed  = "completed";
    public const string Paused     = "paused";
    public const string Failed     = "failed";
    public const string RolledBack = "rolled_back";

    public static readonly IReadOnlySet<string> TerminalStates =
        new HashSet<string> { Completed, Failed, RolledBack };
}
