using CareConnect.Domain;

namespace CareConnect.Application.Services;

/// <summary>
/// LSCC-005-02: Defines and evaluates the automatic retry policy for failed referral-related emails.
///
/// Retry schedule:
///   Attempt 1 → immediate (the initial send in SendNewReferralNotificationAsync / SendAcceptanceConfirmationsAsync)
///   Attempt 2 → 5 minutes after the first failure
///   Attempt 3 → 30 minutes after the second failure
///   After attempt 3: RetryExhausted — no further automatic retries
///
/// Notification types that are eligible for automatic retry:
///   - ReferralCreated (provider new-referral notification)
///   - ReferralAcceptedProvider (provider acceptance confirmation)
///   - ReferralAcceptedReferrer (referrer acceptance confirmation)
///   - ReferralEmailAutoRetry (auto-retry re-attempt markers — retried inline, not nested)
///
/// Manual resend (ManualResend trigger source) is always available via the API regardless
/// of retry state and produces a new notification record. Manual resend does NOT reset or
/// restart the automatic retry counter on the original record.
/// </summary>
public static class ReferralRetryPolicy
{
    /// <summary>Maximum total send attempts (including the initial one).</summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Retry delays indexed by attempt number that just failed.
    /// Index 1 → delay before attempt 2.
    /// Index 2 → delay before attempt 3.
    /// </summary>
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.Zero,          // index 0 — unused
        TimeSpan.FromMinutes(5),  // attempt 1 failed → wait 5 min
        TimeSpan.FromMinutes(30), // attempt 2 failed → wait 30 min
    };

    /// <summary>
    /// Notification types that are eligible for automatic retry.
    /// Only system-generated referral email notifications are retried.
    /// Manual resend records are NOT retried automatically.
    /// </summary>
    private static readonly IReadOnlySet<string> RetryEligibleTypes = new HashSet<string>
    {
        NotificationType.ReferralCreated,
        NotificationType.ReferralAcceptedProvider,
        NotificationType.ReferralAcceptedReferrer,
        NotificationType.ReferralEmailAutoRetry,
    };

    /// <summary>
    /// Returns true if the notification is currently eligible for automatic retry:
    /// - Status is Failed
    /// - NotificationType is in the retry-eligible set
    /// - TriggerSource is not ManualResend (manual resends have their own lifecycle)
    /// - AttemptCount is below MaxAttempts
    /// </summary>
    public static bool IsEligibleForRetry(CareConnectNotification n)
        => n.Status == NotificationStatus.Failed
        && RetryEligibleTypes.Contains(n.NotificationType)
        && n.TriggerSource != NotificationSource.ManualResend
        && n.AttemptCount < MaxAttempts;

    /// <summary>
    /// Returns true if all automatic retries have been exhausted:
    /// - Status is Failed
    /// - AttemptCount has reached or exceeded MaxAttempts
    /// </summary>
    public static bool IsExhausted(CareConnectNotification n)
        => n.Status == NotificationStatus.Failed && n.AttemptCount >= MaxAttempts;

    /// <summary>
    /// Returns the derived display state for a notification, extending the persisted
    /// Status with retry-aware states (Retrying, RetryExhausted).
    /// These derived states are computed from AttemptCount and NextRetryAfterUtc.
    /// </summary>
    public static string GetDerivedStatus(CareConnectNotification n)
    {
        if (n.Status == NotificationStatus.Sent)     return "Sent";
        if (n.Status == NotificationStatus.Pending)  return "Pending";
        if (n.Status != NotificationStatus.Failed)   return n.Status;

        if (IsExhausted(n))                          return "RetryExhausted";
        if (n.NextRetryAfterUtc.HasValue)            return "Retrying";
        return "Failed";
    }

    /// <summary>
    /// Calculates when the next retry should be scheduled, based on the number of
    /// attempts already made. Returns null if retries are exhausted.
    /// </summary>
    public static DateTime? GetNextRetryAfter(int attemptCountAfterFailure)
    {
        if (attemptCountAfterFailure >= MaxAttempts) return null;
        if (attemptCountAfterFailure < 1 || attemptCountAfterFailure >= RetryDelays.Length)
            return null;
        return DateTime.UtcNow.Add(RetryDelays[attemptCountAfterFailure]);
    }
}
