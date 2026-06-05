# Event Forwarding Model

## Overview

The Platform Audit Event Service ships with a lightweight, two-layer event forwarding abstraction that enables downstream systems to consume audit events without polling the audit store. Forwarding is fully opt-in: persistence is always the primary responsibility, and forwarding failure never causes an ingest request to fail.

---

## Design Principles

| Principle | Detail |
|---|---|
| **Post-persist only** | Forwarding is triggered after `AppendAsync` succeeds. The audit record is durable before any forwarding attempt. |
| **Best-effort** | A forwarding failure is logged as a `Warning` and suppressed. The ingest response is always `201 Accepted` regardless. |
| **Read-only** | The forwarder never modifies the `AuditEventRecord` or calls any repository write methods. |
| **Append-only uncompromised** | No forwarding path can bypass or alter the integrity chain. |
| **No hashes in payload** | `Hash` and `PreviousHash` are internal audit-store fields. They are never included in integration events sent downstream. |
| **Stateless, Singleton-safe** | Both forwarder and publisher are registered as Singletons with no mutable state after construction. |

---

## Layer Diagram

```
HTTP Ingest Request
        │
        ▼
AuditEventIngestController
        │  validates
        ▼
AuditEventIngestionService.IngestOneAsync()
   │
   ├─ Step 1: Idempotency check
   ├─ Step 2: AuditId + RecordedAtUtc
   ├─ Step 3: Chain lookup (PreviousHash)
   ├─ Step 4: Hash computation
   ├─ Step 5: Entity construction
   ├─ Step 6: AppendAsync() ← primary responsibility
   │
   └─ Step 7: IAuditEventForwarder.ForwardAsync(persisted, ct)   ← post-persist
              (wrapped in try-catch — failure = Warning log only)
                    │
                    ▼
              NoOpAuditEventForwarder  (v1 default)
               │
               ├─ Filter: Enabled check
               ├─ Filter: IsReplay guard
               ├─ Filter: Category allowlist
               ├─ Filter: EventType prefix allowlist
               ├─ Filter: MinSeverity threshold
               │
               ├─ Map: AuditEventRecord → AuditRecordIntegrationEvent
               │        (no Hash, PreviousHash, BeforeJson, AfterJson, Tags)
               │
               ├─ Wrap: IntegrationEvent<AuditRecordIntegrationEvent> envelope
               │
               └─ IIntegrationEventPublisher.PublishAsync(envelope, ct)
                        │
                        ▼
                  NoOpIntegrationEventPublisher  (v1 default)
                  (logs "would publish…", sends nothing)
```

---

## Interfaces

### `IAuditEventForwarder`
**Namespace**: `PlatformAuditEventService.Services.Forwarding`

The ingest-pipeline-facing abstraction. The ingestion service depends only on this interface.

```csharp
public interface IAuditEventForwarder
{
    ValueTask ForwardAsync(AuditEventRecord record, CancellationToken ct = default);
}
```

- Called once per successfully persisted record.
- Returns `ValueTask` (zero-allocation for the common no-op path).
- Must not throw — callers also wrap in try-catch as a secondary safety net.

### `IIntegrationEventPublisher`
**Namespace**: `PlatformAuditEventService.Services.Forwarding`

The broker-facing abstraction. Knows nothing about the audit domain.

```csharp
public interface IIntegrationEventPublisher
{
    string BrokerName { get; }
    ValueTask PublishAsync<TPayload>(
        IntegrationEvent<TPayload> integrationEvent,
        CancellationToken ct = default);
}
```

- Swap the registered implementation to change brokers without touching the forwarder.
- Must be thread-safe (Singleton lifetime).

---

## Integration Event Contract

### `IntegrationEvent<TPayload>` — Envelope

| Field | Type | Description |
|---|---|---|
| `EventId` | `string` | Unique envelope ID (new Guid per publish). For broker deduplication. |
| `EventType` | `string` | Dot-separated routing key, e.g. `legalsynq.audit.record.ingested`. |
| `SchemaVersion` | `string` | Payload contract version ("1" for v1). Increment on breaking changes. |
| `Payload` | `TPayload` | Domain payload (see below). |
| `PublishedAtUtc` | `DateTimeOffset` | Envelope creation time (UTC). |
| `CorrelationId` | `string?` | Propagated from the audit record for distributed tracing. |
| `SourceService` | `string` | Always `"platform-audit-event-service"`. |

### `AuditRecordIntegrationEvent` — Payload

| Field | Type | Notes |
|---|---|---|
| `AuditId` | `Guid` | Stable identifier for the audit record. |
| `EventType` | `string` | e.g. `"user.login.succeeded"` |
| `EventCategory` | `string` | Category name, e.g. `"Security"` |
| `Severity` | `string` | Severity name, e.g. `"Warning"` |
| `SourceSystem` | `string` | e.g. `"identity-service"` |
| `TenantId` | `string?` | Null for platform-level events. |
| `OrganizationId` | `string?` | Null when not applicable. |
| `ActorId` | `string?` | Null for system actors. |
| `ActorType` | `string` | e.g. `"User"`, `"Service"` |
| `EntityType` | `string?` | e.g. `"Document"` |
| `EntityId` | `string?` | |
| `Action` | `string` | e.g. `"DocumentSigned"` |
| `OccurredAtUtc` | `DateTimeOffset` | When the event occurred in the source system. |
| `RecordedAtUtc` | `DateTimeOffset` | When the record was written to the audit store. |
| `CorrelationId` | `string?` | |
| `IsReplay` | `bool` | Consumers should apply their own idempotency for replays. |

**Deliberately excluded**: `Hash`, `PreviousHash`, `BeforeJson`, `AfterJson`, `Tags`.  
Consumers needing full record detail should query `GET /audit/events/{auditId}` directly.

---

## Configuration

Section: `EventForwarding` in appsettings.json.

```json
{
  "EventForwarding": {
    "Enabled": false,
    "BrokerType": "NoOp",
    "ConnectionString": null,
    "TopicOrExchangeName": null,
    "SubjectPrefix": "legalsynq.audit.",
    "ForwardCategories": [],
    "ForwardEventTypePrefixes": [],
    "MinSeverity": "Info",
    "ForwardReplayRecords": false
  }
}
```

| Key | Default | Description |
|---|---|---|
| `Enabled` | `false` | Master switch. Safe default — activate only when a consumer is ready. |
| `BrokerType` | `"NoOp"` | Selects the publisher implementation. |
| `ConnectionString` | `null` | Broker connection string. Inject via environment variable. |
| `TopicOrExchangeName` | `null` | Target topic/exchange. |
| `SubjectPrefix` | `"legalsynq.audit."` | Prepended to event types for routing. |
| `ForwardCategories` | `[]` | Empty = forward all categories. |
| `ForwardEventTypePrefixes` | `[]` | Empty = forward all event types. |
| `MinSeverity` | `"Info"` | Skip events below this severity. |
| `ForwardReplayRecords` | `false` | When false, replay records are silently dropped. |

---

## Filter Evaluation Order

When `Enabled = true`, the `NoOpAuditEventForwarder` evaluates each record through these gates in order. The first failing gate skips the record (logged at `Debug`):

1. **Master switch** — `Enabled = false` → skip all.
2. **Replay guard** — `IsReplay = true` and `ForwardReplayRecords = false` → skip.
3. **Category allowlist** — `ForwardCategories` non-empty and record category not in list → skip.
4. **EventType prefix allowlist** — `ForwardEventTypePrefixes` non-empty and no prefix matches → skip.
5. **MinSeverity threshold** — record severity < `MinSeverity` → skip.

Only records passing all gates are mapped and forwarded.

---

## v1 Implementations

### `NoOpAuditEventForwarder`
- Wires the complete pipeline (filters → mapping → envelope → publisher).
- Validates forwarding configuration in any environment without broker connectivity.
- When `Enabled = false`: returns `ValueTask.CompletedTask` with zero overhead.
- When `Enabled = true`: exercises all filters, logs at `Debug` for skipped and forwarded records.

### `NoOpIntegrationEventPublisher`
- Logs "would publish…" at `Debug` level with envelope metadata.
- Sends nothing. Establishes no outbound connections.

---

## Adding a Real Broker

All changes are localised to Program.cs. No domain code changes are required.

### Step 1 — Implement the publisher

```csharp
// Services/Forwarding/RabbitMqIntegrationEventPublisher.cs
public sealed class RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher
{
    public string BrokerName => "RabbitMq";
    
    public async ValueTask PublishAsync<TPayload>(
        IntegrationEvent<TPayload> integrationEvent, CancellationToken ct)
    {
        // Serialise to JSON, publish to exchange via IConnection.
        // Route by integrationEvent.EventType.
        // Handle transient failures internally — log and swallow, don't re-throw.
    }
}
```

### Step 2 — Register conditionally in Program.cs

```csharp
var brokerType = fwdOpts.BrokerType;

builder.Services.AddSingleton<IIntegrationEventPublisher>(brokerType switch
{
    "RabbitMq"         => sp => new RabbitMqIntegrationEventPublisher(fwdOpts, sp.GetRequiredService<...>()),
    "AzureServiceBus"  => sp => new AzureServiceBusIntegrationEventPublisher(fwdOpts, ...),
    "AwsSns"           => sp => new AwsSnsIntegrationEventPublisher(fwdOpts, ...),
    _                  => sp => new NoOpIntegrationEventPublisher(sp.GetRequiredService<ILogger<NoOpIntegrationEventPublisher>>()),
});
```

`IAuditEventForwarder` registration stays unchanged. The forwarder is decoupled from the broker choice.

---

## At-Least-Once Delivery (Future)

The v1 design is at-most-once: if the broker publish fails, the event is not retried. For guaranteed delivery, implement the transactional outbox pattern:

1. **Ingest step 7**: instead of calling the publisher directly, write the serialised `IntegrationEvent` to an `OutboxEvents` table inside the same database transaction as the audit record.
2. **Relay background service**: reads un-published outbox rows, publishes to the broker, marks rows as published.
3. **Cleanup**: a second job deletes acknowledged rows after a retention window.

This approach preserves the append-only audit chain while providing at-least-once delivery semantics without distributed transactions.
