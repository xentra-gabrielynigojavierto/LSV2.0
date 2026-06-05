namespace PlatformAuditEventService.Services.Forwarding;

/// <summary>
/// Broker-level abstraction for publishing integration events to downstream systems.
///
/// This interface is concerned only with the mechanics of message delivery —
/// serialisation, routing, and broker connectivity. It has no knowledge of the
/// audit domain; domain-level filtering and payload mapping are the responsibility
/// of <see cref="IAuditEventForwarder"/>.
///
/// v1 ships with <see cref="NoOpIntegrationEventPublisher"/> (BrokerType=NoOp), which
/// logs what would be published without sending any messages.
///
/// Future broker implementations follow the same pattern:
///
///   BrokerType=InMemory      → <c>InMemoryIntegrationEventPublisher</c>
///     Publishes to a <c>Channel&lt;IntegrationEvent&lt;T&gt;&gt;</c>; consumed by a
///     <c>BackgroundService</c>. Useful for in-process fanout without an external broker.
///
///   BrokerType=RabbitMq      → <c>RabbitMqIntegrationEventPublisher</c>
///     Publishes to a RabbitMQ exchange. Requires connection string + exchange name.
///
///   BrokerType=AzureServiceBus → <c>AzureServiceBusIntegrationEventPublisher</c>
///     Publishes to an Azure Service Bus topic.
///
///   BrokerType=AwsSns         → <c>AwsSnsIntegrationEventPublisher</c>
///     Publishes to an AWS SNS topic. Requires topic ARN + AWS credentials.
///
/// Registration:
///   Register exactly one <c>IIntegrationEventPublisher</c> in Program.cs, selected by
///   <c>EventForwarding:BrokerType</c>. <see cref="IAuditEventForwarder"/> implementations
///   depend only on this interface — no broker changes require modifying the forwarder.
///
/// Reliability contract:
///   This interface makes no guarantee about delivery (at-most-once, at-least-once,
///   exactly-once). The audit service treats forwarding as best-effort — persistence
///   is the primary responsibility. Consumers must be idempotent.
///
///   For at-least-once delivery, future implementations should pair this interface
///   with an outbox pattern: write the integration event to a transactional outbox
///   table alongside the audit record, and relay from the outbox to the broker in
///   a background service.
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Short label identifying the backing broker.
    /// Used in log messages and startup diagnostics.
    /// Example: "NoOp", "InMemory", "RabbitMq", "AzureServiceBus".
    /// </summary>
    string BrokerName { get; }

    /// <summary>
    /// Publish an integration event to the configured broker.
    ///
    /// Implementations must:
    ///   1. Serialise <paramref name="integrationEvent"/> to the broker's wire format.
    ///   2. Route to the appropriate topic/exchange using <c>integrationEvent.EventType</c>.
    ///   3. Return without throwing for non-fatal delivery failures (log and swallow).
    ///
    /// Callers (i.e. <see cref="IAuditEventForwarder"/> implementations) already wrap
    /// calls to this method in a try-catch that logs and suppresses exceptions. However,
    /// implementations should prefer internal error handling over letting exceptions escape
    /// for transient connectivity issues.
    ///
    /// Thread safety: implementations must be thread-safe. The audit ingest pipeline
    /// may call this method concurrently from multiple request threads.
    /// </summary>
    ValueTask PublishAsync<TPayload>(
        IntegrationEvent<TPayload> integrationEvent,
        CancellationToken          ct = default);

    /// <summary>
    /// Publish a raw pre-serialized JSON payload without a typed envelope.
    ///
    /// Used by <see cref="Jobs.OutboxRelayHostedService"/> to re-deliver outbox messages
    /// whose payload was serialised at ingest time. The eventType is used for broker routing.
    ///
    /// Implementations may forward the raw JSON directly to the broker or wrap it in
    /// a broker-specific envelope. Consumers must be able to deserialise the raw payload.
    ///
    /// This method is separate from <see cref="PublishAsync{TPayload}"/> to avoid
    /// re-deserialising the payload from the outbox just to re-serialise it.
    /// </summary>
    ValueTask PublishRawAsync(
        string            eventType,
        string            payloadJson,
        CancellationToken ct = default);
}
