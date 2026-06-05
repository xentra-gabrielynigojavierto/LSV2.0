using PlatformAuditEventService.DTOs.Alerts;
using PlatformAuditEventService.DTOs.Analytics;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Alert lifecycle engine that converts anomaly detection results into durable,
/// deduplicated alert records.
///
/// Implementations must:
/// - Use deterministic fingerprinting to prevent duplicate open alerts for the same condition.
/// - Update existing active alerts (increment DetectionCount, refresh LastDetectedAtUtc)
///   rather than creating duplicates.
/// - Enforce tenant isolation — callers without platform admin scope see only their tenant's data.
/// - Never block the caller if a non-critical side effect (e.g. notification dispatch) fails.
/// </summary>
public interface IAuditAlertService
{
    /// <summary>
    /// Runs the anomaly detection engine and upserts alert records for all firing rules.
    ///
    /// Deduplication rules:
    ///   • A new alert is created when no active (Open/Acknowledged) alert with the same
    ///     fingerprint exists.
    ///   • When an active alert with the same fingerprint already exists, it is refreshed
    ///     (LastDetectedAtUtc = now, DetectionCount++, Title/Description/ContextJson updated).
    ///   • When a Resolved alert with the same fingerprint exists and the resolution was within
    ///     the cooldown window, the anomaly is suppressed (no new record).
    ///   • When a Resolved alert exists outside the cooldown window, a new Open alert is created.
    ///
    /// Cooldown: 1 hour after resolution.
    /// </summary>
    Task<AuditEvaluateAlertsResponse> EvaluateAsync(
        AuditAnomalyRequest request,
        string?             callerTenantId,
        bool                isPlatformAdmin,
        CancellationToken   ct = default);

    /// <summary>
    /// Returns alert records matching the given filter parameters.
    /// Tenant isolation is enforced: non-platform-admin callers see only their tenant's alerts.
    /// </summary>
    Task<AuditAlertListResponse> ListAsync(
        AuditAlertQueryRequest request,
        string?                callerTenantId,
        bool                   isPlatformAdmin,
        CancellationToken      ct = default);

    /// <summary>
    /// Returns a single alert by its public <paramref name="alertId"/>.
    /// Returns null if not found or if the caller is not permitted to see the alert.
    /// </summary>
    Task<AuditAlertItem?> GetByIdAsync(
        Guid              alertId,
        string?           callerTenantId,
        bool              isPlatformAdmin,
        CancellationToken ct = default);

    /// <summary>
    /// Transitions an Open or Resolved alert to the Acknowledged state.
    /// Sets AcknowledgedAtUtc and AcknowledgedBy.
    /// Returns false if the alert was not found or the caller is not permitted to act on it.
    /// </summary>
    Task<bool> AcknowledgeAsync(
        Guid              alertId,
        string            acknowledgedBy,
        string?           callerTenantId,
        bool              isPlatformAdmin,
        CancellationToken ct = default);

    /// <summary>
    /// Transitions an Open or Acknowledged alert to the Resolved state.
    /// Sets ResolvedAtUtc and ResolvedBy.
    /// Returns false if the alert was not found or the caller is not permitted to act on it.
    /// </summary>
    Task<bool> ResolveAsync(
        Guid              alertId,
        string            resolvedBy,
        string?           callerTenantId,
        bool              isPlatformAdmin,
        CancellationToken ct = default);
}
