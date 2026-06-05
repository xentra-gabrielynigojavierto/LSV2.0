using PlatformAuditEventService.DTOs.Retention;
using PlatformAuditEventService.Entities;
using PlatformAuditEventService.Enums;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Evaluates the configured retention policy against persisted audit records.
///
/// v1 contract:
///   All operations are read-only. No records are modified, archived, or deleted
///   by any method on this interface. The service is safe to call from monitoring,
///   admin dashboards, and scheduled evaluation jobs without risk of data loss.
///
/// Retention resolution priority (highest → lowest):
///   1. LegalHold — if enabled and the record is on hold, it is never purged.
///   2. Per-tenant override  (<c>Retention:TenantOverrides[tenantId]</c>).
///   3. Per-category override (<c>Retention:CategoryOverrides[category]</c>).
///   4. Default (<c>Retention:DefaultRetentionDays</c>).
///   5. 0 → <see cref="StorageTier.Indefinite"/> — retain forever.
///
/// Extension points:
///   - Legal hold: add per-record hold tracking (future entity) and check it
///     before <c>ComputeExpirationDate</c>.
///   - Dynamic policies: replace config-driven resolution with a policy repository
///     that supports per-tenant agreements stored in the database.
/// </summary>
public interface IRetentionService
{
    /// <summary>
    /// Compute the UTC expiration date for a single record under the current policy.
    ///
    /// Returns null when the record has no expiry (policy = indefinite, or
    /// <c>Retention:DefaultRetentionDays = 0</c> with no overrides).
    ///
    /// Legal hold: in v1, this method does not check for legal holds.
    /// A future implementation should return null (treat as indefinite) when
    /// the record is under an active legal hold.
    /// </summary>
    DateTimeOffset? ComputeExpirationDate(AuditEventRecord record);

    /// <summary>
    /// Resolve the effective retention window in days for a single record.
    ///
    /// Returns 0 when retention is indefinite.
    /// </summary>
    int ResolveRetentionDays(AuditEventRecord record);

    /// <summary>
    /// Classify a record into a <see cref="StorageTier"/> based on its age
    /// and the resolved retention policy.
    ///
    /// Does not access the database — classification is purely a function of the
    /// record's <c>RecordedAtUtc</c> and the current configuration.
    /// </summary>
    StorageTier ClassifyTier(AuditEventRecord record);

    /// <summary>
    /// Evaluate the retention policy across the primary record store.
    ///
    /// Pulls a sample of the oldest records (governed by
    /// <see cref="RetentionEvaluationRequest.SampleLimit"/>), classifies each
    /// by tier, and returns aggregate counts.
    ///
    /// This is always a dry-run in v1 — no records are modified, archived, or deleted.
    /// The result is intended for operator dashboards, scheduled log output, and
    /// compliance reporting.
    /// </summary>
    Task<RetentionEvaluationResult> EvaluateAsync(
        RetentionEvaluationRequest request,
        CancellationToken          ct = default);

    /// <summary>
    /// Return a human-readable summary of the currently configured retention policy.
    /// Used in <see cref="RetentionEvaluationResult.PolicySummary"/> and log messages.
    /// </summary>
    string BuildPolicySummary();
}
