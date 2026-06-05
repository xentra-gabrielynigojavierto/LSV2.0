using PlatformAuditEventService.Entities;

namespace PlatformAuditEventService.Services.Forwarding;

/// <summary>
/// Ingest-pipeline abstraction for forwarding a persisted audit record to downstream systems.
///
/// The forwarder is called by <see cref="Services.AuditEventIngestionService"/> immediately
/// after a record is successfully appended to the audit store. It bridges the audit domain
/// (AuditEventRecord) and the integration event contract (AuditRecordIntegrationEvent).
///
/// Responsibilities:
///   1. Check whether forwarding is enabled (<c>EventForwarding:Enabled</c>).
///   2. Apply configured filters (category, event type prefix, min severity, replay flag).
///   3. Map the persisted <see cref="AuditEventRecord"/> to an <see cref="AuditRecordIntegrationEvent"/>.
///   4. Wrap in a typed <see cref="IntegrationEvent{TPayload}"/> envelope.
///   5. Delegate to <see cref="IIntegrationEventPublisher"/> for broker delivery.
///
/// Contract guarantees:
///   - <b>Post-persist only.</b>
///     <see cref="ForwardAsync"/> is called only after the record is durably written.
///     The append-only guarantee of the audit store is never compromised.
///   - <b>Best-effort, non-blocking.</b>
///     The caller wraps the call in a try-catch. A forwarding failure must not cause
///     the ingest to fail or return a non-2xx response.
///   - <b>Read-only.</b>
///     The forwarder must not modify the <see cref="AuditEventRecord"/> or call
///     any repository write methods.
///   - <b>No hashes in the published payload.</b>
///     <c>Hash</c> and <c>PreviousHash</c> are internal integrity fields and must
///     not appear in the integration event sent to downstream consumers.
///
/// Integration point in the ingest pipeline:
/// <code>
///   // AuditEventIngestionService.IngestOneAsync — after AppendAsync:
///   var persisted = await _records.AppendAsync(entity, ct);
///
///   // Step 7: Event forwarding (post-persist, best-effort)
///   try   { await _forwarder.ForwardAsync(persisted, ct); }
///   catch { _logger.LogWarning(...); }   // never re-throws
///
///   return new IngestItemResult { Accepted = true, AuditId = persisted.AuditId };
/// </code>
///
/// Future extension points:
///   - <b>Content-based routing:</b> inject a routing strategy into the forwarder
///     to publish different event types to different topics/exchanges.
///   - <b>Outbox pattern:</b> replace the direct call with a write to a transactional
///     outbox table to achieve at-least-once delivery guarantees.
///   - <b>Schema versioning:</b> bump <see cref="IntegrationEvent{TPayload}.SchemaVersion"/>
///     when the <see cref="AuditRecordIntegrationEvent"/> contract changes.
/// </summary>
public interface IAuditEventForwarder
{
    /// <summary>
    /// Conditionally forward a persisted audit record as an integration event.
    ///
    /// Returns immediately (ValueTask.CompletedTask equivalent) when:
    ///   - <c>EventForwarding:Enabled = false</c>, or
    ///   - the record is filtered out by category, event type, severity, or replay rules.
    ///
    /// When forwarding is enabled and the record passes all filters, maps it to
    /// <see cref="AuditRecordIntegrationEvent"/> and publishes via
    /// <see cref="IIntegrationEventPublisher"/>.
    ///
    /// This method must not throw. If forwarding fails, it must log and return.
    /// The caller also wraps this call in a try-catch as a secondary safety net.
    /// </summary>
    ValueTask ForwardAsync(AuditEventRecord record, CancellationToken ct = default);
}
