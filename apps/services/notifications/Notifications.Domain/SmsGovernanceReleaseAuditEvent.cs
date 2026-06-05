namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-021: Append-only audit trail entry for release package lifecycle events.
/// MetadataJson must be safe — no raw phones, credentials, message bodies, or provider payloads.
/// </summary>
public class SmsGovernanceReleaseAuditEvent
{
    public Guid    Id               { get; set; }
    public Guid    ReleasePackageId { get; set; }

    /// <summary>
    /// created | item_added | item_removed | submitted_for_review | approval_requested
    /// | approved | rejected | scheduled | activated | activation_failed
    /// | superseded | archived | rollback_linked
    /// </summary>
    public string  EventType     { get; set; } = string.Empty;
    public string? PreviousState { get; set; }
    public string? NewState      { get; set; }
    public string? Actor         { get; set; }
    public string? Reason        { get; set; }

    /// <summary>Safe JSON metadata — no secrets, phones, or credentials.</summary>
    public string? MetadataJson  { get; set; }

    public DateTime CreatedAt { get; set; }
}

public static class ReleaseAuditEventTypes
{
    public const string Created             = "created";
    public const string ItemAdded           = "item_added";
    public const string ItemRemoved         = "item_removed";
    public const string SubmittedForReview  = "submitted_for_review";
    public const string ApprovalRequested   = "approval_requested";
    public const string Approved            = "approved";
    public const string Rejected            = "rejected";
    public const string Scheduled           = "scheduled";
    public const string Activated           = "activated";
    public const string ActivationFailed    = "activation_failed";
    public const string Superseded          = "superseded";
    public const string Archived            = "archived";
    public const string RollbackLinked      = "rollback_linked";

    // ── LS-NOTIF-SMS-021-HARDENING ────────────────────────────────────────────

    /// <summary>Approval actor's declared role did not match the stage's required ApproverRole.</summary>
    public const string ApprovalRoleMismatch     = "approval_role_mismatch";

    /// <summary>Activation concurrency lock successfully acquired.</summary>
    public const string ActivationLockAcquired   = "activation_lock_acquired";

    /// <summary>Activation concurrency lock released after success or failure.</summary>
    public const string ActivationLockReleased   = "activation_lock_released";

    /// <summary>Failed to acquire activation lock — concurrent activation already running.</summary>
    public const string ActivationLockFailed     = "activation_lock_failed";

    /// <summary>Activation failed but retry is scheduled within the backoff window.</summary>
    public const string ActivationRetryScheduled = "activation_retry_scheduled";

    /// <summary>Release integrity check detected a structural violation.</summary>
    public const string IntegrityCheckFailed     = "integrity_check_failed";
}
