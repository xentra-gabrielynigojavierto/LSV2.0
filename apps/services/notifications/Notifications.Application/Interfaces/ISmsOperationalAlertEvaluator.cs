using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-010: Evaluates all SMS operational alert threshold rules
/// against recent attempt data and creates/updates persisted alerts as needed.
///
/// The evaluator:
///  - Fetches a minimal, safe projection of ntf_NotificationAttempts (no credentials).
///  - Applies all 8 threshold rules in a single evaluation cycle.
///  - Deduplicates against existing active alerts via ISmsOperationalAlertRepository.
///  - Never sends SMS, triggers retries, or calls providers.
/// </summary>
public interface ISmsOperationalAlertEvaluator
{
    /// <summary>
    /// Runs a complete evaluation cycle over the specified time window.
    /// Returns a summary of alerts created, updated, and suppressed.
    /// </summary>
    Task<SmsAlertEvaluationResult> EvaluateAsync(
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken ct = default);
}
