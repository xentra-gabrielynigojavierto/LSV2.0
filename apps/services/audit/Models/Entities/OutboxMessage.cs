namespace PlatformAuditEventService.Entities;

/// <summary>
/// Transactional outbox record for durable event forwarding.
///
/// The outbox pattern ensures that audit events and their forwarding messages are
/// written in the same database transaction. The <see cref="PlatformAuditEventService.Jobs.OutboxRelayHostedService"/>
/// reads unpublished messages and delivers them to the configured broker.
///
/// This guarantees at-least-once delivery even if the broker is temporarily unavailable
/// at the time of ingest — the relay retries failed messages up to <see cref="RetryCount"/>
/// attempts before marking them as permanently failed.
///
/// Design notes:
/// - Created in the same EF SaveChanges as the AuditEventRecord (same transaction).
/// - Never updated directly by the ingest pipeline — only by the relay worker.
/// - ProcessedAtUtc being non-null means the message was successfully published.
/// - IsPermanentlyFailed=true means retries are exhausted; operator intervention needed.
/// </summary>
public sealed class OutboxMessage
{
    // ── Primary key ───────────────────────────────────────────────────────────

    /// <summary>Auto-increment surrogate PK.</summary>
    public long Id { get; init; }

    // ── Envelope ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Stable public identifier for this outbox message.
    /// Corresponds to IntegrationEvent.EventId.
    /// </summary>
    public required Guid MessageId { get; init; }

    /// <summary>
    /// Event type identifier. Example: "legalsynq.audit.record.ingested"
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Serialized JSON payload of the integration event.
    /// </summary>
    public required string PayloadJson { get; init; }

    /// <summary>
    /// When the message was created (same moment as the ingest transaction). UTC.
    /// </summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    // ── Delivery tracking ─────────────────────────────────────────────────────

    /// <summary>
    /// When the message was successfully published to the broker. Null until then.
    /// The relay sets this field on success.
    /// </summary>
    public DateTimeOffset? ProcessedAtUtc { get; set; }

    /// <summary>
    /// How many delivery attempts have been made (including the current in-flight attempt).
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Error message from the last delivery attempt. Used for operator diagnostics.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// When true, the relay has exhausted all retry attempts and will no longer try.
    /// Operator intervention is required to re-enqueue or discard this message.
    /// </summary>
    public bool IsPermanentlyFailed { get; set; }

    /// <summary>
    /// Name of the broker the relay should deliver to.
    /// Allows mixed-broker deployments where different event types route to different brokers.
    /// Default: "default"
    /// </summary>
    public string BrokerName { get; set; } = "default";
}
