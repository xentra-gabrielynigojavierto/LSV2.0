namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-022: Governance rollout plan.
/// Orchestrates canary or staged deployment of a governance release package
/// across tenant cohorts with progressive stage advancement and threshold-based safeguards.
///
/// State machine:
///   draft → pending_rollout → canary_active | staged_rollout → rollout_completed
///   canary_active | staged_rollout → rollout_paused → canary_active | staged_rollout
///   canary_active | staged_rollout | rollout_paused → rollout_rolled_back
///   canary_active | staged_rollout | rollout_paused → rollout_failed
///   rollout_completed | rollout_rolled_back | rollout_failed → archived
///
/// Raw phone numbers are NEVER stored here.
/// Credentials, SettingsJson, CredentialsJson, and webhook URLs are NEVER stored here.
/// </summary>
public sealed class SmsGovernanceRolloutPlan
{
    public Guid    Id                  { get; set; } = Guid.NewGuid();

    /// <summary>The release package this rollout deploys.</summary>
    public Guid    ReleasePackageId    { get; set; }

    /// <summary>null = platform-wide; non-null = tenant-scoped rollout plan.</summary>
    public Guid?   TenantId            { get; set; }

    public string  Name                { get; set; } = string.Empty;
    public string? Description         { get; set; }

    /// <summary>draft | pending_rollout | canary_active | staged_rollout | rollout_paused | rollout_failed | rollout_completed | rollout_rolled_back | archived</summary>
    public string  RolloutState        { get; set; } = RolloutStates.Draft;

    /// <summary>canary | staged_percentage | staged_cohort | full_activation | manual_progression</summary>
    public string  RolloutStrategy     { get; set; } = RolloutStrategies.Canary;

    /// <summary>The stage number currently active (null = not started or completed).</summary>
    public int?    CurrentStageNumber  { get; set; }

    /// <summary>
    /// JSON-serialized threshold configuration. Structure:
    /// { maxBlockRate, maxWarnRate, maxReviewRequiredRate, maxActivationFailureRate, minimumSampleSize, action }
    /// Null = use global defaults from SmsGovernanceRolloutsOptions.
    /// </summary>
    public string? RollbackThresholdJson { get; set; }

    public DateTime? StartedAt          { get; set; }
    public DateTime? PausedAt           { get; set; }
    public DateTime? ResumedAt          { get; set; }
    public DateTime? CompletedAt        { get; set; }
    public DateTime? RolledBackAt       { get; set; }
    public DateTime? FailedAt           { get; set; }
    public string?   FailureReason      { get; set; }

    public DateTime  CreatedAt          { get; set; }
    public DateTime  UpdatedAt          { get; set; }
    public string?   CreatedBy          { get; set; }
    public string?   UpdatedBy          { get; set; }
}

public static class RolloutStates
{
    public const string Draft              = "draft";
    public const string PendingRollout     = "pending_rollout";
    public const string CanaryActive       = "canary_active";
    public const string StagedRollout      = "staged_rollout";
    public const string RolloutPaused      = "rollout_paused";
    public const string RolloutFailed      = "rollout_failed";
    public const string RolloutCompleted   = "rollout_completed";
    public const string RolloutRolledBack  = "rollout_rolled_back";
    public const string Archived           = "archived";

    public static readonly IReadOnlySet<string> ActiveStates =
        new HashSet<string> { CanaryActive, StagedRollout };

    public static readonly IReadOnlySet<string> TerminalStates =
        new HashSet<string> { RolloutCompleted, RolloutRolledBack, RolloutFailed, Archived };

    public static readonly IReadOnlySet<string> EditableStates =
        new HashSet<string> { Draft };

    public static bool IsActive(string state)   => ActiveStates.Contains(state);
    public static bool IsTerminal(string state) => TerminalStates.Contains(state);
    public static bool IsEditable(string state) => EditableStates.Contains(state);
}

public static class RolloutStrategies
{
    public const string Canary              = "canary";
    public const string StagedPercentage    = "staged_percentage";
    public const string StagedCohort        = "staged_cohort";
    public const string FullActivation      = "full_activation";
    public const string ManualProgression   = "manual_progression";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { Canary, StagedPercentage, StagedCohort, FullActivation, ManualProgression };
}
