namespace Notifications.Application.Options;

/// <summary>
/// LS-NOTIF-SMS-021: Configuration for governance release management.
/// Bound from appsettings.json section "SmsGovernanceReleaseManagement".
/// </summary>
public sealed class SmsGovernanceReleaseManagementOptions
{
    public const string SectionName = "SmsGovernanceReleaseManagement";

    /// <summary>Master switch. When false, release management APIs return 503.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>When true, releases must pass approval workflow before activation.</summary>
    public bool RequireApprovalForActivation { get; set; } = true;

    /// <summary>
    /// JSON array of default approval stages when RequireApprovalForActivation = true.
    /// Each element: { "stage": int, "approverRole": string, "requiredApprovals": int }
    /// </summary>
    public string? DefaultApprovalStagesJson { get; set; } =
        """[{"stage":1,"approverRole":"PlatformAdmin","requiredApprovals":1}]""";

    /// <summary>When true, approved releases can be activated immediately without scheduling.</summary>
    public bool AllowImmediateActivation { get; set; } = true;

    /// <summary>When true, approved releases can be scheduled for future activation.</summary>
    public bool AllowScheduledActivation { get; set; } = true;

    /// <summary>
    /// When true, the SmsGovernanceReleaseActivationWorker runs and processes
    /// scheduled releases automatically. Disabled by default — enable explicitly.
    /// </summary>
    public bool ScheduledActivationWorkerEnabled { get; set; } = false;

    /// <summary>How often the scheduled-activation worker polls, in minutes.</summary>
    public int ScheduledActivationPollMinutes { get; set; } = 5;

    /// <summary>
    /// When true, errors during release evaluation at governance-check time
    /// fail open (allow) rather than blocking delivery.
    /// Does NOT affect activation — activation failures always set activation_failed.
    /// </summary>
    public bool FailOpenOnReleaseEvaluationError { get; set; } = true;

    /// <summary>Maximum number of items allowed in a single release package.</summary>
    public int MaxReleaseItems { get; set; } = 500;

    // ── LS-NOTIF-SMS-021-HARDENING ────────────────────────────────────────────

    /// <summary>
    /// When true, the approver's declared role must match the stage's ApproverRole.
    /// When false, any authenticated PlatformAdmin can approve any stage.
    /// </summary>
    public bool EnforceApprovalRoles { get; set; } = true;

    /// <summary>
    /// When EnforceApprovalRoles = true, a user declaring role "PlatformAdmin"
    /// is always allowed regardless of the stage's ApproverRole.
    /// Set to false to enforce exact role matching with no fallback.
    /// </summary>
    public bool AllowPlatformAdminApprovalFallback { get; set; } = true;

    /// <summary>
    /// Maximum number of activation attempts before the release is permanently
    /// marked activation_failed with no further retries.
    /// </summary>
    public int ActivationRetryLimit { get; set; } = 3;

    /// <summary>
    /// Linear backoff per retry attempt (minutes). NextActivationRetryAt is set to
    /// now + (ActivationRetryBackoffMinutes × attemptCount) after each failure.
    /// </summary>
    public int ActivationRetryBackoffMinutes { get; set; } = 10;

    /// <summary>
    /// How long an activation lock remains valid before it is considered stale
    /// and may be forcibly expired by the next caller.
    /// </summary>
    public int ActivationLockTimeoutMinutes { get; set; } = 10;

    /// <summary>Maximum number of scheduled releases the worker processes per cycle.</summary>
    public int MaxScheduledReleasesPerCycle { get; set; } = 10;
}
