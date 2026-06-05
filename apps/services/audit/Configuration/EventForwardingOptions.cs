namespace PlatformAuditEventService.Configuration;

/// <summary>
/// Configuration for the audit event forwarding pipeline.
/// Bound from the "EventForwarding" section in appsettings.
/// Environment variable override prefix: EventForwarding__
///
/// Event forwarding publishes a lightweight integration event to downstream
/// systems each time an audit record is successfully persisted. It is:
///   - Post-persist: forwarding only happens after the record is durably written.
///   - Best-effort: a forwarding failure is logged as a Warning but never causes
///     the ingest response to fail. Persistence is always the primary responsibility.
///   - Filtered: callers can restrict forwarding to specific categories, event type
///     prefixes, or minimum severity levels without changing application code.
///
/// BrokerType guide:
///   None         — forwarding disabled regardless of Enabled flag.
///   NoOp         — wires the pipeline; logs what would be forwarded; sends nothing.
///   InMemory     — publishes to an in-process Channel&lt;T&gt;; consumed by a BackgroundService.
///                  Useful for local fanout without an external broker.
///   RabbitMq     — publishes to a RabbitMQ exchange. Requires ConnectionString.
///   AzureServiceBus — publishes to an Azure Service Bus topic. Requires ConnectionString.
///   AwsSns       — publishes to an AWS SNS topic. Requires TopicOrExchangeName + AWS credentials.
///
/// Extension:
///   Implement <see cref="Services.Forwarding.IIntegrationEventPublisher"/> and register it
///   conditionally in Program.cs based on <see cref="BrokerType"/>.
/// </summary>
public sealed class EventForwardingOptions
{
    public const string SectionName = "EventForwarding";

    // ── Master switch ─────────────────────────────────────────────────────────

    /// <summary>
    /// When false, forwarding is completely disabled. No events are evaluated or published.
    /// Default: false (safe default — activate only when a downstream consumer is ready).
    /// </summary>
    public bool Enabled { get; set; } = false;

    // ── Broker selection ──────────────────────────────────────────────────────

    /// <summary>
    /// Which broker backend to use.
    /// Default: "NoOp" — pipeline is wired but no messages are sent.
    /// </summary>
    public string BrokerType { get; set; } = "NoOp";

    /// <summary>
    /// Broker connection string (RabbitMQ AMQP URI, Azure Service Bus connection string, etc.).
    /// Inject via environment variable — never commit to appsettings.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Target topic, exchange, or subject namespace.
    /// Example (RabbitMQ): "audit.events"
    /// Example (Azure Service Bus): "audit-records"
    /// Example (AWS SNS): "arn:aws:sns:us-east-1:123456789:audit-records"
    /// </summary>
    public string? TopicOrExchangeName { get; set; }

    /// <summary>
    /// Optional prefix prepended to the event type when routing or filtering in the broker.
    /// Example: "legalsynq.audit." → event type becomes "legalsynq.audit.record.ingested".
    /// </summary>
    public string SubjectPrefix { get; set; } = "legalsynq.audit.";

    // ── Filtering ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Restrict forwarding to these event categories.
    /// Empty list = forward all categories (default).
    /// Values must match <see cref="Enums.EventCategory"/> names (case-insensitive).
    /// Example: ["Security", "Compliance"]
    /// </summary>
    public List<string> ForwardCategories { get; set; } = new();

    /// <summary>
    /// Restrict forwarding to event types that start with one of these prefixes.
    /// Empty list = forward all event types (default).
    /// Example: ["user.", "consent.", "document."]
    /// </summary>
    public List<string> ForwardEventTypePrefixes { get; set; } = new();

    /// <summary>
    /// Minimum severity level to forward.
    /// Events below this severity are silently dropped by the forwarder.
    /// Must match a <see cref="Enums.SeverityLevel"/> name (case-insensitive).
    /// Default: "Info" (forward Info, Warning, Error, Critical).
    /// </summary>
    public string MinSeverity { get; set; } = "Info";

    // ── Payload control ───────────────────────────────────────────────────────

    /// <summary>
    /// When true, replay records (<c>AuditEventRecord.IsReplay = true</c>) are forwarded.
    /// When false, replay records are silently skipped.
    /// Default: false — downstream consumers typically should not re-process replayed events.
    /// </summary>
    public bool ForwardReplayRecords { get; set; } = false;
}
