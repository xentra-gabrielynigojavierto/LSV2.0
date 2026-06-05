namespace PlatformAuditEventService.Services.Forwarding;

/// <summary>
/// No-op implementation of <see cref="IIntegrationEventPublisher"/>.
///
/// Used when <c>EventForwarding:BrokerType = NoOp</c> (the default).
/// Logs what would be published without sending anything to an external broker.
///
/// This is the correct production-safe default for a system where the event
/// forwarding infrastructure is configured separately from the application:
/// - Safe: no outbound connections are established.
/// - Observable: log output shows exactly what the live pipeline would publish,
///   including event type, subject, schema version, and correlation ID.
/// - Configurable: upgrading to a real broker only requires registering a
///   different <see cref="IIntegrationEventPublisher"/> in Program.cs.
///
/// Replacement:
///   Register a different implementation (e.g. <c>RabbitMqIntegrationEventPublisher</c>)
///   in Program.cs when a broker is available. No forwarder changes are required.
/// </summary>
public sealed class NoOpIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ILogger<NoOpIntegrationEventPublisher> _logger;

    public NoOpIntegrationEventPublisher(ILogger<NoOpIntegrationEventPublisher> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public string BrokerName => "NoOp";

    /// <inheritdoc/>
    public ValueTask PublishAsync<TPayload>(
        IntegrationEvent<TPayload> integrationEvent,
        CancellationToken          ct = default)
    {
        _logger.LogDebug(
            "NoOpPublisher: would publish EventId={EventId} EventType={EventType} " +
            "SchemaVersion={SchemaVersion} CorrelationId={CorrelationId} " +
            "PublishedAt={PublishedAt:o}. No message sent (BrokerType=NoOp).",
            integrationEvent.EventId,
            integrationEvent.EventType,
            integrationEvent.SchemaVersion,
            integrationEvent.CorrelationId ?? "–",
            integrationEvent.PublishedAtUtc);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask PublishRawAsync(
        string            eventType,
        string            payloadJson,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "NoOpPublisher.PublishRawAsync: EventType={EventType} PayloadLength={Length}. " +
            "No message sent (BrokerType=NoOp).",
            eventType, payloadJson.Length);

        return ValueTask.CompletedTask;
    }
}
