namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-021: Governance release package.
/// Groups governance changes (rule packs, rules, profiles) into an auditable,
/// approval-gated, optionally scheduled deployment unit.
///
/// State machine:
///   draft → pending_review → approved → active | scheduled → active
///   pending_review → rejected → archived | draft
///   active → superseded | archived
///   activation_failed → archived | draft
/// </summary>
public class SmsGovernanceReleasePackage
{
    public Guid    Id                    { get; set; }
    public Guid?   TenantId              { get; set; }   // null = platform/global release
    public string  Name                  { get; set; } = string.Empty;
    public string? Description           { get; set; }

    /// <summary>draft | pending_review | approved | scheduled | active | superseded | rejected | archived | activation_failed</summary>
    public string  ReleaseState          { get; set; } = ReleaseStates.Draft;

    /// <summary>rule_pack | rule_set | compliance_profile | mixed_governance</summary>
    public string  ReleaseType           { get; set; } = ReleaseTypes.MixedGovernance;

    public DateTime? ScheduledActivationAt { get; set; }
    public DateTime? ActivatedAt           { get; set; }
    public DateTime? SupersededAt          { get; set; }
    public Guid?     SupersededByReleaseId { get; set; }
    public DateTime? RejectedAt            { get; set; }
    public DateTime? ArchivedAt            { get; set; }

    public DateTime  CreatedAt   { get; set; }
    public DateTime  UpdatedAt   { get; set; }
    public string?   CreatedBy   { get; set; }
    public string?   UpdatedBy   { get; set; }

    // ── LS-NOTIF-SMS-021-HARDENING: Activation concurrency locking ────────────

    /// <summary>
    /// Identifies the active activation lock. Null means unlocked.
    /// Set atomically to prevent concurrent activations of the same release.
    /// </summary>
    public Guid?     ActivationLockId         { get; set; }

    /// <summary>When the current activation lock was acquired.</summary>
    public DateTime? ActivationLockAcquiredAt { get; set; }

    /// <summary>
    /// When the activation lock expires. Stale locks past this point
    /// are forcibly expired by the next caller to prevent permanent deadlock.
    /// </summary>
    public DateTime? ActivationLockExpiresAt  { get; set; }

    /// <summary>The actor (user or worker) that holds the lock.</summary>
    public string?   ActivationLockedBy       { get; set; }

    // ── LS-NOTIF-SMS-021-HARDENING: Retry tracking ────────────────────────────

    /// <summary>Cumulative count of activation attempts (success resets this).</summary>
    public int       ActivationAttemptCount      { get; set; }

    /// <summary>Timestamp of the most recent activation attempt.</summary>
    public DateTime? LastActivationAttemptAt     { get; set; }

    /// <summary>
    /// Backoff gate — the worker skips this release until now >= NextActivationRetryAt.
    /// Null = no backoff, eligible immediately.
    /// </summary>
    public DateTime? NextActivationRetryAt       { get; set; }

    /// <summary>
    /// Truncated failure reason from the last failed activation attempt (max 500 chars).
    /// Cleared on successful activation.
    /// </summary>
    public string?   LastActivationFailureReason { get; set; }
}

public static class ReleaseStates
{
    public const string Draft             = "draft";
    public const string PendingReview     = "pending_review";
    public const string Approved          = "approved";
    public const string Scheduled         = "scheduled";
    public const string Active            = "active";
    public const string Superseded        = "superseded";
    public const string Rejected          = "rejected";
    public const string Archived          = "archived";
    public const string ActivationFailed  = "activation_failed";

    public static readonly IReadOnlySet<string> EditableStates =
        new HashSet<string> { Draft };

    public static readonly IReadOnlySet<string> TerminalStates =
        new HashSet<string> { Archived, Superseded };

    public static bool IsEditable(string state) => EditableStates.Contains(state);
    public static bool IsTerminal(string state)  => TerminalStates.Contains(state);
}

public static class ReleaseTypes
{
    public const string RulePack          = "rule_pack";
    public const string RuleSet           = "rule_set";
    public const string ComplianceProfile = "compliance_profile";
    public const string MixedGovernance   = "mixed_governance";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { RulePack, RuleSet, ComplianceProfile, MixedGovernance };
}
