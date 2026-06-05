using CareConnect.Application.DTOs;
using CareConnect.Domain;

namespace CareConnect.Application.Interfaces;

/// <summary>
/// LSCC-005 / LSCC-005-01 / LSCC-01-002 / CC2-INT-B03: Handles secure token generation and email
/// notification dispatch for the referral flow.
///
/// Token strategy (LSCC-005-01): HMAC-SHA256 signed token encoding referralId + tokenVersion + expiry (30 days).
/// Token format: {referralId}:{tokenVersion}:{expiryUnixSeconds}:{hmacHex}, Base64url-encoded.
/// Incrementing a referral's TokenVersion invalidates all previously issued tokens.
///
/// Email strategy: notification records are always created in the DB (Pending state).
/// An SMTP send is then attempted immediately. On success → Sent; on failure → Failed.
/// AttemptCount and LastAttemptAtUtc are updated on each attempt. Never a silent failure.
///
/// Secret hardening (CC2-INT-B03): ReferralToken:Secret must be explicitly configured in all
/// non-Development environments. Missing secret in Production/Staging throws on startup.
/// </summary>
public interface IReferralEmailService
{
    /// <summary>
    /// LSCC-005-01: Generates a time-bound HMAC-signed view token for the given referral.
    /// The token embeds referralId, tokenVersion, and an expiry timestamp (30 days).
    /// Use the referral's current TokenVersion so token revocation works correctly.
    /// </summary>
    string GenerateViewToken(Guid referralId, int tokenVersion);

    /// <summary>
    /// LSCC-005-01: Validates a view token. Returns a ViewTokenValidationResult containing
    /// both the ReferralId and the TokenVersion embedded in the token on success.
    /// Returns null if the token is invalid, tampered with, or expired.
    ///
    /// IMPORTANT: The caller must also verify that result.TokenVersion matches the referral's
    /// current TokenVersion to detect revoked tokens.
    /// </summary>
    ViewTokenValidationResult? ValidateViewToken(string token);

    /// <summary>
    /// Queues a "new referral received" notification to the provider's email address
    /// and attempts an immediate SMTP send.
    /// Called after referral creation (fire-and-observe pattern — never gates creation).
    /// Also covers TOKEN_GENERATED event since a signed token is included in the email.
    /// </summary>
    Task SendNewReferralNotificationAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default);

    /// <summary>
    /// CC2-INT-B03: Sends a PROVIDER_ASSIGNED notification when a provider is associated
    /// with a referral after initial creation (e.g. re-assignment or delayed assignment).
    /// Routes through the platform Notifications service like all other events.
    /// <para>
    /// <paramref name="dedupeKeySuffix"/> is appended to the base dedupe key
    /// (<c>referral:{id}:provider_assigned:{providerId}</c>) to differentiate re-assignment
    /// events from the initial assignment and from each other. Pass an empty string (default)
    /// for the initial assignment, or a unique suffix (e.g. <c>:reassigned:{ticks}</c>) for
    /// each manual re-assignment so the notification is always delivered regardless of how
    /// many times the same provider is assigned.
    /// </para>
    /// </summary>
    Task SendProviderAssignedNotificationAsync(
        Referral referral,
        Provider provider,
        Guid?    actingUserId,
        string   dedupeKeySuffix   = "",
        CancellationToken ct = default);

    /// <summary>
    /// LSCC-005-01: Queues a resend of the "new referral received" notification.
    /// Creates a new notification record (type: ReferralEmailResent) and attempts SMTP send.
    /// The new token uses the referral's current TokenVersion, so revoked tokens stay revoked.
    /// </summary>
    Task ResendNewReferralNotificationAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default);

    /// <summary>
    /// Queues acceptance confirmation notifications for the provider, the referrer, and the client
    /// (LSCC-01-002), and attempts immediate SMTP sends for each.
    /// Called after the referral is accepted (fire-and-observe — never gates acceptance).
    /// Client email is skipped gracefully when ClientEmail is not stored; acceptance is never blocked.
    /// </summary>
    Task SendAcceptanceConfirmationsAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default);

    Task SendRejectionNotificationsAsync(
        Referral referral,
        Provider provider,
        Guid? actingUserId,
        CancellationToken ct = default);

    Task SendCancellationNotificationsAsync(
        Referral referral,
        Provider provider,
        Guid? actingUserId,
        CancellationToken ct = default);

    /// <summary>
    /// LSCC-005-02: Re-attempts an email send for an existing failed notification record.
    /// Called exclusively by <c>ReferralEmailRetryWorker</c> — not for manual operator resend.
    /// Rebuilds the email body from current referral/provider state. Updates the same
    /// notification record in-place; no new record is created.
    /// </summary>
    Task RetryNotificationAsync(
        CareConnectNotification notification,
        Referral                referral,
        Provider                provider,
        CancellationToken       ct = default);

    /// <summary>
    /// Sends an email notification to the "other party" when a new comment is posted
    /// on the public referral thread. If the comment is from the referrer, the provider
    /// is notified, and vice versa.
    /// </summary>
    Task SendCommentNotificationAsync(
        Referral        referral,
        ReferralComment comment,
        CancellationToken ct = default);
}
