using PlatformAuditEventService.DTOs.Query;

namespace PlatformAuditEventService.DTOs.Correlation;

/// <summary>
/// A single audit event that was found to be related to an anchor event,
/// annotated with the correlation key that linked it.
/// </summary>
public sealed class RelatedAuditEventResult
{
    /// <summary>
    /// Identifies which correlation key caused this event to be included.
    ///
    /// Possible values:
    ///   "correlation_id"       — shares the same CorrelationId as the anchor event.
    ///   "session_id"           — shares the same SessionId as the anchor event.
    ///   "actor_entity_window"  — same ActorId + EntityId within ±4 h of the anchor.
    ///   "actor_window"         — same ActorId within ±2 h (fallback when no stronger key matched).
    /// </summary>
    public required string MatchedBy { get; init; }

    /// <summary>
    /// The actual correlation key value that was matched (e.g. the correlationId string,
    /// session GUID, or "actorId/entityId" pair). Aids UI labelling and deep-linking.
    /// </summary>
    public required string MatchKey { get; init; }

    /// <summary>The full audit event record that was found to be related.</summary>
    public required AuditEventRecordResponse Event { get; init; }
}
