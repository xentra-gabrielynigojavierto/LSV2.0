namespace PlatformAuditEventService.Services.Forwarding;

/// <summary>
/// Generic broker envelope that wraps a typed integration event payload.
///
/// Every message published by <see cref="IIntegrationEventPublisher"/> is
/// wrapped in this envelope. The envelope provides the routing metadata that
/// broker implementations need (EventType for topic/routing-key selection,
/// EventId for deduplication, CorrelationId for tracing) while keeping the
/// domain payload in a strongly-typed <typeparamref name="TPayload"/>.
///
/// Serialisation note:
///   Brokers typically serialise the full envelope to JSON. Ensure the
///   payload type is JSON-serialisable (no circular references, EF proxies, etc.).
///   <see cref="Services.Forwarding.AuditRecordIntegrationEvent"/> is designed
///   for this — it contains only primitive and value types.
///
/// Versioning:
///   <see cref="SchemaVersion"/> is a free-form string for consumer-side schema
///   negotiation. Use a monotonically increasing integer ("1", "2") or SemVer
///   ("1.0", "2.0") for compatibility declarations.
/// </summary>
/// <typeparam name="TPayload">The domain event payload type.</typeparam>
public sealed class IntegrationEvent<TPayload>
{
    /// <summary>
    /// Unique identifier for this envelope instance.
    /// Used by broker deduplication filters and idempotent consumers.
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Dot-separated event type identifier.
    /// Used for broker routing, topic selection, and consumer filtering.
    /// Example: "legalsynq.audit.record.ingested"
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>Schema version of <typeparamref name="TPayload"/>. Default: "1".</summary>
    public string SchemaVersion { get; init; } = "1";

    /// <summary>The domain-specific payload of this event.</summary>
    public required TPayload Payload { get; init; }

    /// <summary>UTC timestamp when this envelope was created (just before publishing).</summary>
    public required DateTimeOffset PublishedAtUtc { get; init; }

    /// <summary>
    /// Correlation ID for end-to-end tracing.
    /// Propagated from the originating audit record when available.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// The service that created and published this event.
    /// Always "audit" for events originating here.
    /// </summary>
    public string SourceService { get; init; } = "audit";
}
