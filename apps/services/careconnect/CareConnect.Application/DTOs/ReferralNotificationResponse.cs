namespace CareConnect.Application.DTOs;

/// <summary>
/// LSCC-005-01 / LSCC-005-02: Lightweight DTO representing a referral-related notification record.
/// Used to surface email delivery status, retry lifecycle, and history on the referral detail view.
/// </summary>
public class ReferralNotificationResponse
{
    public Guid     Id               { get; set; }
    public string   NotificationType { get; set; } = string.Empty;
    public string   RecipientType    { get; set; } = string.Empty;
    public string?  RecipientAddress { get; set; }
    public string   Status           { get; set; } = string.Empty;
    public int      AttemptCount     { get; set; }
    public string?  FailureReason    { get; set; }
    public DateTime? SentAtUtc       { get; set; }
    public DateTime? FailedAtUtc     { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime  CreatedAtUtc    { get; set; }

    // LSCC-005-02: retry lifecycle fields
    /// <summary>How this notification was triggered: Initial | AutoRetry | ManualResend.</summary>
    public string    TriggerSource      { get; set; } = "Initial";

    /// <summary>When the next automatic retry is scheduled. Null if sent, not failed, or exhausted.</summary>
    public DateTime? NextRetryAfterUtc  { get; set; }

    /// <summary>
    /// Derived UI-friendly status extending the persisted Status with retry states.
    /// Values: Pending | Sent | Failed | Retrying | RetryExhausted
    /// </summary>
    public string    DerivedStatus      { get; set; } = string.Empty;
}
