using PlatformAuditEventService.DTOs.Correlation;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Correlation engine: given an anchor audit event, discovers related events across
/// a deterministic cascade of correlation keys.
///
/// Cascade order (additive for tiers 1–3, fallback-only for tier 4):
///   Tier 1 — CorrelationId exact match       → matchedBy: "correlation_id"
///   Tier 2 — SessionId exact match           → matchedBy: "session_id"
///   Tier 3 — ActorId + EntityId + ±4h window → matchedBy: "actor_entity_window"
///   Tier 4 — ActorId + ±2h window (fallback) → matchedBy: "actor_window"
///
/// Tenant isolation is enforced: all queries are scoped to the anchor event's TenantId.
/// The anchor event itself is excluded from the result set.
/// Results are deduplicated by AuditId with the highest-priority matchedBy label retained.
/// </summary>
public interface IAuditCorrelationService
{
    /// <summary>
    /// Find all audit events related to <paramref name="anchorAuditId"/> using the
    /// correlation cascade strategy.
    ///
    /// Returns null when no anchor event exists with the given id.
    /// </summary>
    /// <param name="anchorAuditId">The stable public AuditId of the anchor event.</param>
    /// <param name="callerTenantId">
    /// The tenant scope enforced for the caller. When non-null, all sub-queries are
    /// restricted to this tenant. Null means the caller has platform-wide scope.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<RelatedEventsResponse?> GetRelatedAsync(
        Guid              anchorAuditId,
        string?           callerTenantId,
        CancellationToken ct = default);
}
