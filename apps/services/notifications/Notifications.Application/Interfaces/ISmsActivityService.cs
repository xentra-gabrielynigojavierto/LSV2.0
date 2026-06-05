using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-006: Service for SMS activity log and reporting.
/// Orchestrates repository queries and applies phone masking before returning results.
/// All output is safe for API consumers — no credentials, no raw phone numbers.
/// </summary>
public interface ISmsActivityService
{
    /// <summary>
    /// Returns a paginated SMS activity log matching the supplied query.
    /// Phone numbers in MaskedRecipient follow the platform masking convention:
    /// first 3 characters + "***".
    /// </summary>
    Task<SmsActivityPagedResult> GetActivityAsync(SmsActivityQuery query, CancellationToken ct = default);

    /// <summary>
    /// Returns aggregate counts for SMS activity matching the supplied query.
    /// </summary>
    Task<SmsActivitySummaryDto> GetSummaryAsync(SmsActivityQuery query, CancellationToken ct = default);
}
