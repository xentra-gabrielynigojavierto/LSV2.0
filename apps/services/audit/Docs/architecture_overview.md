# Platform Audit/Event Service — Architecture Overview

## Design Principles

1. **Domain-agnostic** — no dependency on any specific product, tenant model, UI, or identity provider.
2. **Append-only writes** — audit records are immutable. The repository interface exposes no update or delete operations.
3. **Tamper-evident** — every record carries an HMAC-SHA256 integrity hash over its canonical fields.
4. **Portable** — single .NET 8 project with no monorepo build coupling. Independently deployable.
5. **Extensible** — clean interface layer between service, repository, and persistence adapter.

## Layered Structure

```
HTTP Request
    │
    ▼
ExceptionMiddleware ──► catches all unhandled exceptions → structured JSON error
CorrelationIdMiddleware ──► reads/generates X-Correlation-ID header
    │
    ▼
Controller (AuditEventsController / HealthController)
    │
    ▼
FluentValidation (IngestAuditEventRequestValidator)
    │
    ▼
AuditEventService (IAuditEventService)
    │  ├─ AuditEventMapper.ToModel() — normalizes fields, computes integrity hash
    │  └─ IntegrityHasher.Compute() — HMAC-SHA256
    ▼
IAuditEventRepository
    │
    ▼
InMemoryAuditEventRepository (dev) → swap for DB-backed adapter (prod)
```

## Extension Points

- **New persistence adapter** — implement `IAuditEventRepository` and register in `Program.cs`.
- **New event fields** — add to `AuditEvent` + `IngestAuditEventRequest` + update mapper + validator.
- **Batch ingestion** — add `POST /api/auditevents/batch` endpoint accepting `IList<IngestAuditEventRequest>`.
- **Event streaming** — add a publisher job that pushes new events to a Redis Stream or message bus.
- **Downstream consumers** — subscribe to published events for dashboards, alerting, or compliance exports.
