using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class CareConnectNotification : AuditableEntity
{
    public Guid    Id                  { get; private set; }
    public Guid    TenantId            { get; private set; }
    public string  NotificationType    { get; private set; } = string.Empty;
    public string  RelatedEntityType   { get; private set; } = string.Empty;
    public Guid    RelatedEntityId     { get; private set; }
    public string  RecipientType       { get; private set; } = string.Empty;
    public string? RecipientAddress    { get; private set; }
    public string? Subject             { get; private set; }
    public string? Message             { get; private set; }
    public string  Status              { get; private set; } = NotificationStatus.Pending;
    public DateTime? ScheduledForUtc  { get; private set; }
    public DateTime? SentAtUtc        { get; private set; }
    public DateTime? FailedAtUtc      { get; private set; }
    public string? FailureReason      { get; private set; }

    // LSCC-005-01: delivery tracking fields
    public int       AttemptCount      { get; private set; }
    public DateTime? LastAttemptAtUtc  { get; private set; }

    // LSCC-005-02: retry scheduling and source tracking
    /// <summary>
    /// When the next automatic retry should occur (null if not scheduled or already sent/exhausted).
    /// Cleared when the notification is sent or retries are exhausted.
    /// </summary>
    public DateTime? NextRetryAfterUtc { get; private set; }

    /// <summary>
    /// Records how this notification was triggered.
    /// Use <see cref="NotificationSource"/> constants.
    /// </summary>
    public string TriggerSource { get; private set; } = NotificationSource.Initial;

    public string? DedupeKey { get; private set; }

    private CareConnectNotification() { }

    /// <summary>
    /// Marks the notification as successfully sent.
    /// Increments AttemptCount, records LastAttemptAtUtc, and clears the retry schedule.
    /// </summary>
    public void MarkSent()
    {
        AttemptCount      += 1;
        LastAttemptAtUtc   = DateTime.UtcNow;
        Status             = NotificationStatus.Sent;
        SentAtUtc          = DateTime.UtcNow;
        NextRetryAfterUtc  = null;
        UpdatedAtUtc       = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the notification as failed, storing the failure reason.
    /// Increments AttemptCount and records LastAttemptAtUtc.
    /// Optionally schedules the next automatic retry via <paramref name="nextRetryAfterUtc"/>.
    /// </summary>
    public void MarkFailed(string reason, DateTime? nextRetryAfterUtc = null)
    {
        AttemptCount      += 1;
        LastAttemptAtUtc   = DateTime.UtcNow;
        Status             = NotificationStatus.Failed;
        FailedAtUtc        = DateTime.UtcNow;
        FailureReason      = reason?.Length > 2000 ? reason[..2000] : reason;
        NextRetryAfterUtc  = nextRetryAfterUtc;
        UpdatedAtUtc       = DateTime.UtcNow;
    }

    /// <summary>
    /// LSCC-005-02: Clears any scheduled retry (e.g., after a successful manual resend).
    /// This prevents the retry worker from picking up an old failed notification
    /// whose lifecycle has effectively been superseded by the manual resend.
    /// </summary>
    public void ClearRetrySchedule()
    {
        NextRetryAfterUtc = null;
        UpdatedAtUtc      = DateTime.UtcNow;
    }

    public static CareConnectNotification Create(
        Guid    tenantId,
        string  notificationType,
        string  relatedEntityType,
        Guid    relatedEntityId,
        string  recipientType,
        string? recipientAddress,
        string? subject,
        string? message,
        DateTime? scheduledForUtc,
        Guid?   createdByUserId,
        string  triggerSource = NotificationSource.Initial,
        string? dedupeKey = null)
    {
        return new CareConnectNotification
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            NotificationType  = notificationType,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId   = relatedEntityId,
            RecipientType     = recipientType,
            RecipientAddress  = recipientAddress,
            Subject           = subject,
            Message           = message,
            Status            = NotificationStatus.Pending,
            ScheduledForUtc   = scheduledForUtc,
            AttemptCount      = 0,
            LastAttemptAtUtc  = null,
            NextRetryAfterUtc = null,
            TriggerSource     = triggerSource,
            DedupeKey         = dedupeKey,
            CreatedByUserId   = createdByUserId,
            UpdatedByUserId   = createdByUserId,
            CreatedAtUtc      = DateTime.UtcNow,
            UpdatedAtUtc      = DateTime.UtcNow
        };
    }
}
