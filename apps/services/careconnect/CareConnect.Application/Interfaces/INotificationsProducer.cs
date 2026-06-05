namespace CareConnect.Application.Interfaces;

/// <summary>
/// LS-NOTIF-CORE-023: Canonical producer interface for submitting outbound notifications
/// to the platform Notifications service.
///
/// Implementations call POST /v1/notifications using the canonical producer contract.
/// CareConnect retains responsibility for building the email content; this interface
/// is solely concerned with delivery submission.
/// </summary>
public interface INotificationsProducer
{
    /// <summary>
    /// Submits an email notification to the platform Notifications service.
    /// Throws <see cref="InvalidOperationException"/> if the submission fails so callers
    /// can catch and update their domain notification record to Failed status.
    /// </summary>
    /// <param name="tenantId">The tenant on whose behalf this notification is sent.</param>
    /// <param name="eventKey">Stable business event identifier, e.g. "referral.created".</param>
    /// <param name="toAddress">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML email body.</param>
    /// <param name="idempotencyKey">Optional deduplication key — prevents duplicate delivery on retry.</param>
    /// <param name="correlationId">Optional correlation ID (e.g. referralId) for tracing.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SubmitAsync(
        Guid              tenantId,
        string            eventKey,
        string            toAddress,
        string            subject,
        string            htmlBody,
        string?           idempotencyKey = null,
        string?           correlationId  = null,
        CancellationToken ct             = default);
}
