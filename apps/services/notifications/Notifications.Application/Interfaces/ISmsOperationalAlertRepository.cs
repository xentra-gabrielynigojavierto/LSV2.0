using Notifications.Application.DTOs;
using Notifications.Domain;

namespace Notifications.Application.Interfaces;

/// <summary>
/// LS-NOTIF-SMS-010: Repository for SMS operational alert persistence.
///
/// All queries:
///  - Never return CredentialsJson, SettingsJson, RecipientJson, or phone numbers.
///  - Never trigger SMS sends, retries, reconciliation, or provider calls.
///  - Support deduplication: active alerts with the same (AlertType, TenantId,
///    Provider, ProviderConfigId) are updated rather than duplicated.
/// </summary>
public interface ISmsOperationalAlertRepository
{
    /// <summary>
    /// Returns a paginated list of alerts matching the query filters.
    /// Results are ordered by Status (active first) then LastObservedAt DESC.
    /// </summary>
    Task<SmsAlertListResult> ListAsync(SmsAlertQuery query, CancellationToken ct = default);

    /// <summary>
    /// Returns aggregate counts for the alert summary panel.
    /// Counts are across all alerts (no date filter applied).
    /// </summary>
    Task<SmsAlertSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a single alert by ID, or null if not found.
    /// </summary>
    Task<SmsOperationalAlert?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Finds an existing active alert with the same deduplication key:
    ///   (AlertType, TenantId, Provider, ProviderConfigId)
    /// Returns null if no active alert exists for that combination.
    /// </summary>
    Task<SmsOperationalAlert?> FindActiveAlertAsync(
        string alertType,
        Guid? tenantId,
        string? provider,
        Guid? providerConfigId,
        CancellationToken ct = default);

    /// <summary>
    /// Finds the most-recently-resolved alert for the given dedup key
    /// that was resolved within the last <paramref name="cooldownMinutes"/> minutes.
    /// Returns null if no such alert exists (i.e. cooldown has expired or no prior alert).
    /// </summary>
    Task<SmsOperationalAlert?> FindRecentlyResolvedAlertAsync(
        string alertType,
        Guid? tenantId,
        string? provider,
        Guid? providerConfigId,
        int cooldownMinutes,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a new alert record. Sets CreatedAt, UpdatedAt, FirstObservedAt, LastObservedAt
    /// to the current UTC time if not already populated.
    /// </summary>
    Task<SmsOperationalAlert> CreateAsync(SmsOperationalAlert alert, CancellationToken ct = default);

    /// <summary>
    /// Persists changes to an existing alert entity.
    /// Caller is responsible for modifying entity fields before calling this.
    /// </summary>
    Task UpdateAsync(SmsOperationalAlert alert, CancellationToken ct = default);

    /// <summary>
    /// Sets Status="resolved", ResolvedAt=now, ResolvedBy, ResolutionNote on an active alert.
    /// Returns false if the alert was not found or was already resolved.
    /// </summary>
    Task<bool> ResolveAsync(
        Guid id,
        string? resolvedBy,
        string? resolutionNote,
        CancellationToken ct = default);

    /// <summary>
    /// Sets SuppressedUntil on an alert to suppress re-alerting for the given duration.
    /// Also sets Status="suppressed" if the alert is currently active.
    /// Returns false if the alert was not found.
    /// </summary>
    Task<bool> SuppressAsync(
        Guid id,
        DateTime suppressedUntil,
        CancellationToken ct = default);
}
