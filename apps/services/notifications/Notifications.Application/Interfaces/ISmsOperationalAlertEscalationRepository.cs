using Notifications.Application.DTOs;
using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-011: Repository for SMS alert escalation attempt persistence.
///
/// All TargetMasked values are stored pre-masked by the caller.
/// This repository never holds or returns raw webhook URLs, emails, or credentials.
/// </summary>
public interface ISmsOperationalAlertEscalationRepository
{
    Task<SmsOperationalAlertEscalation> CreateAsync(
        SmsOperationalAlertEscalation escalation, CancellationToken ct = default);

    Task UpdateAsync(SmsOperationalAlertEscalation escalation, CancellationToken ct = default);

    Task<SmsOperationalAlertEscalation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<SmsAlertEscalationListResult> ListAsync(SmsAlertEscalationQuery query, CancellationToken ct = default);

    Task<SmsEscalationSummaryDto> SummarizeAsync(SmsAlertEscalationQuery query, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a recent duplicate escalation exists for the same alert+policy+payloadHash
    /// within the given cooldown window. Returns the duplicate if found, null otherwise.
    /// </summary>
    Task<SmsOperationalAlertEscalation?> FindRecentDuplicateAsync(
        Guid alertId,
        Guid? policyId,
        string? payloadHash,
        int cooldownMinutes,
        CancellationToken ct = default);

    /// <summary>
    /// Returns pending escalations with NextRetryAt &lt;= now, up to limit records.
    /// Ordered by NextRetryAt ascending (oldest due first).
    /// </summary>
    Task<List<SmsOperationalAlertEscalation>> GetPendingRetriesAsync(
        int limit, DateTime now, CancellationToken ct = default);
}
