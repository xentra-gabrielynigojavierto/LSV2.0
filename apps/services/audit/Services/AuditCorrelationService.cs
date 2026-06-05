using PlatformAuditEventService.DTOs.Correlation;
using PlatformAuditEventService.DTOs.Query;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Canonical implementation of <see cref="IAuditCorrelationService"/>.
///
/// Uses <see cref="IAuditEventQueryService"/> for all sub-queries so that
/// visibility scoping and response shaping rules are applied uniformly.
/// </summary>
public sealed class AuditCorrelationService : IAuditCorrelationService
{
    // Maximum events returned per tier query (guards against runaway fan-out).
    private const int TierMaxResults = 200;

    // Time window radii used by the actor-based tiers.
    private static readonly TimeSpan ActorEntityWindow = TimeSpan.FromHours(4);
    private static readonly TimeSpan ActorOnlyWindow   = TimeSpan.FromHours(2);

    private readonly IAuditEventQueryService         _queryService;
    private readonly ILogger<AuditCorrelationService> _logger;

    public AuditCorrelationService(
        IAuditEventQueryService          queryService,
        ILogger<AuditCorrelationService> logger)
    {
        _queryService = queryService;
        _logger       = logger;
    }

    /// <inheritdoc />
    public async Task<RelatedEventsResponse?> GetRelatedAsync(
        Guid              anchorAuditId,
        string?           callerTenantId,
        CancellationToken ct = default)
    {
        // ── 1. Fetch anchor event ────────────────────────────────────────────
        // Build a minimal scope filter so the anchor lookup respects the caller's
        // tenant boundary (same isolation guarantee as a filtered list query).
        var anchorScopeFilter = callerTenantId is not null
            ? new AuditEventQueryRequest { TenantId = callerTenantId }
            : null;
        var anchor = await _queryService.GetByAuditIdAsync(anchorAuditId, anchorScopeFilter, ct);
        if (anchor is null)
        {
            _logger.LogDebug(
                "Correlation: anchor AuditId={AuditId} not found.", anchorAuditId);
            return null;
        }

        // Effective tenantId: caller's enforced tenant, falling back to the anchor's own tenant.
        var effectiveTenant = callerTenantId ?? anchor.Scope?.TenantId;

        // Collect results per tier — dedup map: AuditId → RelatedAuditEventResult.
        // Insertion order determines display order within a tier; earlier insertions win dedup.
        var seen   = new Dictionary<Guid, RelatedAuditEventResult>();
        var bucket = new List<RelatedAuditEventResult>();

        // ── Tier 1 — CorrelationId ───────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(anchor.CorrelationId))
        {
            var tier1 = await RunQueryAsync(new AuditEventQueryRequest
            {
                TenantId      = effectiveTenant,
                CorrelationId = anchor.CorrelationId,
                PageSize      = TierMaxResults,
                SortBy        = "occurredAtUtc",
                SortDescending = false,
            }, ct);

            Merge(tier1.Items, "correlation_id", anchor.CorrelationId!, anchorAuditId, seen, bucket);
        }

        // ── Tier 2 — SessionId ───────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(anchor.SessionId))
        {
            var tier2 = await RunQueryAsync(new AuditEventQueryRequest
            {
                TenantId  = effectiveTenant,
                SessionId = anchor.SessionId,
                PageSize  = TierMaxResults,
                SortBy    = "occurredAtUtc",
                SortDescending = false,
            }, ct);

            Merge(tier2.Items, "session_id", anchor.SessionId!, anchorAuditId, seen, bucket);
        }

        // ── Tier 3 — ActorId + EntityId + ±4h window ────────────────────────
        var anchorActorId = anchor.Actor.Id;
        var anchorEntityId = anchor.Entity?.Id;

        if (!string.IsNullOrWhiteSpace(anchorActorId) &&
            !string.IsNullOrWhiteSpace(anchorEntityId))
        {
            var from = anchor.OccurredAtUtc - ActorEntityWindow;
            var to   = anchor.OccurredAtUtc + ActorEntityWindow;

            var tier3 = await RunQueryAsync(new AuditEventQueryRequest
            {
                TenantId       = effectiveTenant,
                ActorId        = anchorActorId,
                EntityId       = anchorEntityId,
                From           = from,
                To             = to,
                PageSize       = TierMaxResults,
                SortBy         = "occurredAtUtc",
                SortDescending = false,
            }, ct);

            var matchKey = $"{anchorActorId}/{anchorEntityId}";
            Merge(tier3.Items, "actor_entity_window", matchKey, anchorAuditId, seen, bucket);
        }

        // ── Tier 4 — ActorId + ±2h window (fallback only) ───────────────────
        if (seen.Count == 0 && !string.IsNullOrWhiteSpace(anchorActorId))
        {
            var from = anchor.OccurredAtUtc - ActorOnlyWindow;
            var to   = anchor.OccurredAtUtc + ActorOnlyWindow;

            var tier4 = await RunQueryAsync(new AuditEventQueryRequest
            {
                TenantId       = effectiveTenant,
                ActorId        = anchorActorId,
                From           = from,
                To             = to,
                PageSize       = 20,      // Intentionally capped — weaker signal.
                SortBy         = "occurredAtUtc",
                SortDescending = false,
            }, ct);

            Merge(tier4.Items, "actor_window", anchorActorId!, anchorAuditId, seen, bucket);
        }

        // ── Build response ────────────────────────────────────────────────────

        // Determine the highest-priority strategy that produced results.
        var strategyUsed = DetermineStrategy(bucket);

        // Final ordering: chronological ascending.
        var ordered = bucket
            .OrderBy(r => r.Event.OccurredAtUtc)
            .ToList();

        _logger.LogDebug(
            "Correlation complete. AnchorId={AuditId} TotalRelated={Count} Strategy={Strategy}",
            anchorAuditId, ordered.Count, strategyUsed);

        return new RelatedEventsResponse
        {
            AnchorId       = anchorAuditId,
            AnchorEventType = anchor.EventType,
            StrategyUsed   = strategyUsed,
            TotalRelated   = ordered.Count,
            Related        = ordered,
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Task<AuditEventQueryResponse> RunQueryAsync(
        AuditEventQueryRequest request,
        CancellationToken      ct)
        => _queryService.QueryAsync(request, ct);

    /// <summary>
    /// Merges query results into the dedup map and bucket.
    /// The first occurrence for a given AuditId wins (higher-priority tier ran first).
    /// The anchor event itself is excluded.
    /// </summary>
    private static void Merge(
        IReadOnlyList<AuditEventRecordResponse> items,
        string                                  matchedBy,
        string                                  matchKey,
        Guid                                    anchorId,
        Dictionary<Guid, RelatedAuditEventResult> seen,
        List<RelatedAuditEventResult>              bucket)
    {
        foreach (var item in items)
        {
            // Skip if this is the anchor event.
            if (item.AuditId == anchorId) continue;

            // First insertion wins (highest-priority tier).
            if (seen.ContainsKey(item.AuditId)) continue;

            var entry = new RelatedAuditEventResult
            {
                MatchedBy = matchedBy,
                MatchKey  = matchKey,
                Event     = item,
            };
            seen[item.AuditId] = entry;
            bucket.Add(entry);
        }
    }

    private static string DetermineStrategy(List<RelatedAuditEventResult> bucket)
    {
        if (bucket.Count == 0) return "none";

        // Priority order mirrors the cascade tier order.
        var priorities = new[] { "correlation_id", "session_id", "actor_entity_window", "actor_window" };
        var matchedBySet = bucket.Select(r => r.MatchedBy).ToHashSet();

        foreach (var p in priorities)
        {
            if (matchedBySet.Contains(p)) return p;
        }
        return "none";
    }
}
