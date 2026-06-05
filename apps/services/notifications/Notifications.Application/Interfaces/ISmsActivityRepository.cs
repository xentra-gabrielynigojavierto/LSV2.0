using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-006: Read-only repository for SMS activity log queries.
/// Performs bounded queries — all queries filter by Channel = "sms" and
/// apply at least one indexed predicate. Results are ordered by CreatedAt DESC.
/// Never returns CredentialsJson, SettingsJson, or any provider secret.
/// </summary>
public interface ISmsActivityRepository
{
    /// <summary>
    /// Returns a bounded page of raw SMS activity records matching the query.
    /// The Total count reflects all matching rows (not just the page).
    /// </summary>
    Task<(List<SmsActivityRawRecord> Items, int Total)> QueryAsync(
        SmsActivityQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Returns aggregate status counts for all SMS attempts matching the query.
    /// Ignores Limit/Offset.
    /// </summary>
    Task<SmsActivitySummaryDto> SummarizeAsync(
        SmsActivityQuery query,
        CancellationToken ct = default);
}
