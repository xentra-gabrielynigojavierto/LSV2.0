namespace Flow.Application.Adapters.AuditAdapter;

/// <summary>
/// E13.1 — read-only audit query seam on the Flow side, parallel to the
/// write seam <see cref="IAuditAdapter"/>. Used by the read-only Control
/// Center timeline endpoint on <c>AdminWorkflowInstancesController</c>.
///
/// Implementations MUST be safe to call when the audit service is
/// unreachable or unconfigured: callers expect a graceful empty list
/// rather than an exception, because a triage view should never break
/// because the audit pipeline is degraded.
/// </summary>
public interface IAuditQueryAdapter
{
    /// <summary>
    /// Fetch every audit event recorded against
    /// (<paramref name="entityType"/>, <paramref name="entityId"/>),
    /// optionally constrained to <paramref name="tenantId"/>. The
    /// returned list is unsorted at this layer — callers (the
    /// timeline normalizer) apply deterministic ordering.
    ///
    /// The returned <see cref="AuditEventFetchResult.Truncated"/> flag
    /// is set when the implementation hit its internal page ceiling
    /// and there are more events upstream that the caller did not
    /// receive. Callers are expected to surface this verbatim so the
    /// UI can prompt the operator to drill into the audit service.
    /// </summary>
    Task<AuditEventFetchResult> GetEventsForEntityAsync(
        string entityType,
        string entityId,
        string? tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result envelope for <see cref="IAuditQueryAdapter.GetEventsForEntityAsync"/>.
/// Truncated is reported by the adapter (not inferred from list size)
/// so a result of exactly N=ceiling events is not misclassified as
/// truncated when the upstream had no further pages.
/// </summary>
public sealed record AuditEventFetchResult(
    IReadOnlyList<AuditEventRecord> Events,
    bool Truncated);

/// <summary>
/// E13.1 — minimal read model for an audit record consumed by the Flow
/// timeline normalizer. Only the fields the normalizer touches are
/// modelled here; any additional fields the audit service may return
/// are intentionally not part of this contract so the seam stays
/// stable as the audit schema evolves.
///
/// Optional fields use null defaults so a record with only the
/// required fields (event id, action, occurred-at) is still valid.
/// </summary>
public sealed record AuditEventRecord(
    string EventId,
    string Action,
    DateTimeOffset OccurredAtUtc,
    Guid? AuditId = null,
    string? EventCategory = null,
    string? SourceSystem = null,
    string? SourceService = null,
    string? TenantId = null,
    string? ActorId = null,
    string? ActorName = null,
    string? ActorType = null,
    string? EntityType = null,
    string? EntityId = null,
    string? Description = null,
    string? CorrelationId = null,
    string? RequestId = null,
    string? SessionId = null,
    string? Severity = null,
    string? Visibility = null,
    string? MetadataJson = null);
