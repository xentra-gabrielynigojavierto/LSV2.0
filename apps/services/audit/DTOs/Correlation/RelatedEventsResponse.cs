using PlatformAuditEventService.DTOs.Query;

namespace PlatformAuditEventService.DTOs.Correlation;

/// <summary>
/// Response payload for GET /audit/events/{auditId}/related.
///
/// Returns the anchor event, the ordered list of related events (deduped, self-excluded),
/// and a label indicating which correlation strategy produced results.
/// </summary>
public sealed class RelatedEventsResponse
{
    /// <summary>The stable public identifier of the anchor event (the one that was queried).</summary>
    public required Guid AnchorId { get; init; }

    /// <summary>The EventType of the anchor event — useful for display without a separate round-trip.</summary>
    public required string AnchorEventType { get; init; }

    /// <summary>
    /// The highest-priority correlation strategy that produced at least one result.
    /// Possible values: "correlation_id" | "session_id" | "actor_entity_window" | "actor_window" | "none".
    ///
    /// When multiple tiers produced results (tiers 1-3 are additive), this field contains the
    /// strongest key among all matched tiers. The individual <see cref="Related"/> items each carry
    /// their own <see cref="RelatedAuditEventResult.MatchedBy"/> label.
    /// </summary>
    public required string StrategyUsed { get; init; }

    /// <summary>
    /// Total number of unique related events returned (length of <see cref="Related"/>).
    /// </summary>
    public required int TotalRelated { get; init; }

    /// <summary>
    /// Related events ordered by OccurredAtUtc ascending.
    /// Each item carries a <see cref="RelatedAuditEventResult.MatchedBy"/> label and
    /// a <see cref="RelatedAuditEventResult.MatchKey"/> value.
    ///
    /// Deduplication: if an event matched multiple tiers, only the highest-priority
    /// <see cref="RelatedAuditEventResult.MatchedBy"/> label is kept.
    /// </summary>
    public required IReadOnlyList<RelatedAuditEventResult> Related { get; init; }
}
