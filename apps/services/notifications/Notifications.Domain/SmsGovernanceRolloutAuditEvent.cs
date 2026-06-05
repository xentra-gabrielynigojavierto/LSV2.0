namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-022: Append-only audit event for governance rollout lifecycle changes.
/// Supplements the existing SmsGovernanceReleaseAuditEvent (which records release-level
/// activation events) with rollout-plan and stage-level audit events.
///
/// No raw phone numbers, credentials, message bodies, or provider payloads are stored here.
/// MetadataJson contains safe operational aggregate fields only.
/// </summary>
public sealed class SmsGovernanceRolloutAuditEvent
{
    public Guid    Id              { get; set; } = Guid.NewGuid();
    public Guid    RolloutPlanId   { get; set; }

    /// <summary>Null for plan-level events; non-null for stage-specific events.</summary>
    public Guid?   StageId         { get; set; }

    /// <summary>Null for platform-wide rollouts; non-null when targeting a specific tenant.</summary>
    public Guid?   TenantId        { get; set; }

    /// <summary>See RolloutAuditEventTypes constants.</summary>
    public string  EventType        { get; set; } = string.Empty;

    public string? PreviousState    { get; set; }
    public string? NewState         { get; set; }

    /// <summary>The user or system actor that triggered this event.</summary>
    public string? Actor            { get; set; }

    public string? Reason           { get; set; }

    /// <summary>Safe aggregate metadata JSON. No phones, credentials, or message content.</summary>
    public string? MetadataJson     { get; set; }

    public DateTime CreatedAt       { get; set; }
}

/// <summary>
/// LS-NOTIF-SMS-022: Rollout audit event type constants.
/// </summary>
public static class RolloutAuditEventTypes
{
    public const string RolloutCreated        = "rollout_created";
    public const string StageAdded            = "stage_added";
    public const string CohortAdded           = "cohort_added";
    public const string RolloutStarted        = "rollout_started";
    public const string StageStarted          = "stage_started";
    public const string StageCompleted        = "stage_completed";
    public const string StageFailed           = "stage_failed";
    public const string RolloutPaused         = "rollout_paused";
    public const string RolloutResumed        = "rollout_resumed";
    public const string RolloutFailed         = "rollout_failed";
    public const string RolloutRolledBack     = "rollout_rolled_back";
    public const string RolloutCompleted      = "rollout_completed";
    public const string ThresholdExceeded     = "threshold_exceeded";
    public const string StageAdvanced         = "stage_advanced";
    public const string CohortActivated       = "cohort_activated";
    public const string CohortRolledBack      = "cohort_rolled_back";
}
