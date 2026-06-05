namespace Notifications.Domain;

/// <summary>
/// LS-NOTIF-SMS-011: Persisted record of a single escalation delivery attempt
/// made for an SmsOperationalAlert via a configured SmsOperationalEscalationPolicy.
///
/// Security constraints:
///   - TargetMasked holds only the safe display form of the target (never the raw URL/email).
///   - No credentials, phone numbers, provider payloads, CredentialsJson, or SettingsJson.
///   - MetadataJson may contain safe, non-sensitive operational metadata only.
/// </summary>
public class SmsOperationalAlertEscalation
{
    public Guid Id { get; set; }

    // ── Links ─────────────────────────────────────────────────────────────────

    /// <summary>The alert that triggered this escalation.</summary>
    public Guid AlertId { get; set; }

    /// <summary>The policy that was applied. Null if escalated manually without a policy.</summary>
    public Guid? PolicyId { get; set; }

    // ── Channel info ──────────────────────────────────────────────────────────

    /// <summary>Channel type used for delivery (e.g. "teams_webhook", "email").</summary>
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Masked/safe representation of the delivery target.
    /// e.g. "a***@example.com", "https://outlook.office.com/***"
    /// NEVER stores the raw webhook URL or full email address.
    /// </summary>
    public string? TargetMasked { get; set; }

    // ── Classification ────────────────────────────────────────────────────────

    /// <summary>Severity copied from the alert at escalation time.</summary>
    public string Severity { get; set; } = "warning";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Delivery status:
    ///   pending   — created, delivery not yet attempted or queued for retry
    ///   sent      — successfully delivered to the channel
    ///   failed    — delivery failed (non-retryable, or retries exhausted)
    ///   suppressed — skipped due to dedup/cooldown
    ///   skipped   — skipped because no adapter found for channel type
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>Total delivery attempts made so far (incremented on each try).</summary>
    public int AttemptCount { get; set; }

    /// <summary>UTC timestamp of the most recent delivery attempt.</summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>UTC timestamp when delivery succeeded (Status = "sent").</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>Failure reason string (truncated to 1000 chars). Null on success.</summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// When to retry this escalation. Null if not scheduled for retry.
    /// Only set when Status is still "pending" after a retryable failure.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// When suppression expires. Set when Status="suppressed" due to cooldown.
    /// </summary>
    public DateTime? SuppressedUntil { get; set; }

    // ── Dedup ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// SHA-256 hash of a canonical string derived from safe alert metadata.
    /// Used to detect duplicate escalations within a cooldown window.
    /// Does not contain any raw target, credential, or phone number.
    /// </summary>
    public string? PayloadHash { get; set; }

    // ── Metadata ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Optional JSON object with safe operational metadata
    /// (e.g. externalMessageId from the channel).
    /// Must not contain credentials, URLs, or phone numbers.
    /// </summary>
    public string? MetadataJson { get; set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
