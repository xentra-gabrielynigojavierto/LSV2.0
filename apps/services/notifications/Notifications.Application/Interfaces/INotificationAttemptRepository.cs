using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface INotificationAttemptRepository
{
    Task<NotificationAttempt?> GetByIdAsync(Guid id);
    Task<NotificationAttempt?> FindByProviderMessageIdAsync(string providerMessageId);
    Task<List<NotificationAttempt>> GetByNotificationIdAsync(Guid notificationId);
    Task<NotificationAttempt> CreateAsync(NotificationAttempt attempt);
    Task UpdateAsync(NotificationAttempt attempt);
    Task UpdateStatusAsync(Guid id, string status, DateTime? completedAt = null);

    /// <summary>
    /// Find stale outbound SMS attempts for vendor reconciliation.
    /// Returns attempts where Channel=sms, ProviderMessageId is set,
    /// Status is in <paramref name="statuses"/>, and UpdatedAt is older than <paramref name="olderThan"/>.
    /// Results are ordered by UpdatedAt ascending (oldest first) and bounded by <paramref name="limit"/>.
    /// </summary>
    Task<List<NotificationAttempt>> GetStaleSmsAttemptsAsync(
        int limit,
        DateTime olderThan,
        IReadOnlyCollection<string> statuses,
        CancellationToken ct = default);

    /// <summary>
    /// LS-NOTIF-SMS-007: Persist reconciliation tracking fields after a pull-based
    /// reconciliation attempt.
    ///
    /// Increments <c>ReconciliationAttemptCount</c> and updates all <c>LastReconciliation*</c>
    /// fields. Does NOT modify <c>Status</c> or <c>CompletedAt</c> — those remain under
    /// the exclusive control of <c>DeliveryStatusService</c>.
    ///
    /// If <paramref name="attemptId"/> is not found, the call is silently ignored.
    /// All callers should wrap this in a try/catch so tracking failures never
    /// surface as reconciliation failures.
    /// </summary>
    Task UpdateReconciliationTrackingAsync(
        Guid attemptId,
        string outcome,
        string? errorCode,
        string? providerStatus,
        string? normalizedStatus,
        DateTime reconciledAt,
        CancellationToken ct = default);

    /// <summary>
    /// LS-NOTIF-SMS-013: Persist cost metadata on an SMS attempt.
    ///
    /// Best-effort — callers must wrap in try/catch so cost recording failures
    /// never affect delivery semantics. Does NOT modify Status, CompletedAt,
    /// or any reconciliation fields.
    ///
    /// No credentials, raw provider payloads, or phone numbers may be passed.
    /// </summary>
    Task UpdateCostAsync(
        Guid attemptId,
        decimal? estimatedCostAmount,
        decimal? actualCostAmount,
        string? costCurrency,
        string costSource,
        DateTime costRecordedAt,
        CancellationToken ct = default);
}
