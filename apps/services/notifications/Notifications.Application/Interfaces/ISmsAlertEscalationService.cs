using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-011: Orchestrates escalation delivery for SMS operational alerts.
///
/// Constraints:
///   - Must NEVER trigger SMS sends.
///   - External delivery failures must not throw — all errors are caught and persisted.
///   - Respects global SMS_ALERT_ESCALATION_ENABLED flag.
///   - Applies policy cooldown/dedup before every delivery attempt.
/// </summary>
public interface ISmsAlertEscalationService
{
    /// <summary>
    /// Loads the alert by ID and escalates via all matching enabled policies.
    /// </summary>
    Task EscalateAlertAsync(Guid alertId, CancellationToken ct = default);

    /// <summary>
    /// Escalates an already-loaded alert via all matching enabled policies.
    /// Preferred overload when the caller already holds the alert entity.
    /// </summary>
    Task EscalateAlertAsync(SmsOperationalAlert alert, CancellationToken ct = default);

    /// <summary>
    /// Retries a specific failed/pending escalation record.
    /// No-ops if the escalation is already sent, suppressed, or skipped.
    /// </summary>
    Task<bool> RetryEscalationAsync(Guid escalationId, string? requestedBy, CancellationToken ct = default);

    /// <summary>
    /// Processes all due retry-eligible escalations (Status=pending with NextRetryAt &lt;= now).
    /// Called by SmsAlertEscalationRetryWorker.
    /// </summary>
    Task<int> ProcessPendingRetriesAsync(int limit, CancellationToken ct = default);
}
