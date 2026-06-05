# Canonical Audit Event Contract

**Service**: Platform Audit/Event Service  
**Version**: 1.0.0  
**Namespace**: `PlatformAuditEventService.DTOs.Ingest.IngestAuditEventRequest`  
**Endpoint**: `POST /api/auditevents` (single) · `POST /api/auditevents/batch` (batch)

---

## Overview

The canonical event contract defines the structure that every producer must conform to when submitting audit events to the Platform Audit/Event Service. The contract is intentionally domain-neutral — it carries structured context (who, what, where, when) without enforcing business-specific rules in the validator.

Producers are expected to:
1. Populate all **required** fields before submitting.
2. Set `IdempotencyKey` for retry-safe ingestion.
3. Use the `Scope` object to convey tenancy context.
4. Use `Before` / `After` for state snapshots in `DataChange` category events.

---

## Top-Level Fields

| Field               | Type                  | Required | Max Length | Notes |
|---------------------|-----------------------|----------|------------|-------|
| `eventId`           | `Guid?`               | No       | —          | Source-assigned identifier. Not used for deduplication. |
| `eventType`         | `string`              | **Yes**  | 200        | Convention: `{domain}.{resource}.{verb}`. Example: `user.login.succeeded`. |
| `eventCategory`     | `EventCategory` enum  | **Yes**  | —          | Drives retention policy and dashboard routing. |
| `sourceSystem`      | `string`              | **Yes**  | 200        | Logical name of the producing system. Example: `care-connect`. |
| `sourceService`     | `string?`             | **Yes**  | 200        | Sub-component or microservice. Example: `appointment-worker`. |
| `sourceEnvironment` | `string?`             | No       | 100        | Deployment environment: `production`, `staging`, `dev`. |
| `scope`             | `AuditEventScopeDto`  | **Yes**  | —          | Tenancy and organizational scope. See [Scope Object](#scope-object). |
| `actor`             | `AuditEventActorDto`  | **Yes**  | —          | Principal that performed the action. See [Actor Object](#actor-object). |
| `entity`            | `AuditEventEntityDto?`| No       | —          | Resource that was acted upon. See [Entity Object](#entity-object). |
| `action`            | `string`              | No       | 200        | PascalCase verb. Examples: `Created`, `Updated`, `LoginAttempted`. |
| `description`       | `string`              | No       | 2000       | Human-readable summary for audit log displays. |
| `before`            | `string?`             | No       | 1,048,576  | JSON snapshot of resource state **before** the action. Stored verbatim. |
| `after`             | `string?`             | No       | 1,048,576  | JSON snapshot of resource state **after** the action. Stored verbatim. |
| `metadata`          | `string?`             | No       | 1,048,576  | Arbitrary JSON object for additional context. Stored verbatim. |
| `correlationId`     | `string?`             | No       | 200        | Distributed trace ID. Typically W3C traceparent or `X-Correlation-Id`. |
| `requestId`         | `string?`             | No       | 200        | HTTP request identifier from the originating request. |
| `sessionId`         | `string?`             | No       | 200        | User session identifier when action occurred within a session. |
| `visibility`        | `VisibilityScope` enum| **Yes**  | —          | Controls who can retrieve this record through the query API. |
| `severity`          | `SeverityLevel` enum  | **Yes**  | —          | Operational severity at the time of the event. |
| `occurredAtUtc`     | `DateTimeOffset?`     | **Yes**  | —          | UTC time the event occurred in the source system. |
| `idempotencyKey`    | `string?`             | No       | 300        | Source-provided key for deduplication within the configured window. |
| `isReplay`          | `bool`                | No       | —          | Set `true` when resubmitting via upstream replay. Default: `false`. |
| `tags`              | `string[]?`           | No       | 20 items   | Ad-hoc string labels. Max 100 chars per tag. No taxonomy enforced. |

---

## Scope Object

**Type**: `AuditEventScopeDto`  
**Required**: Yes (object must be present; fields within are conditionally required)

| Field            | Type           | Required when                  | Max Length | Notes |
|------------------|----------------|--------------------------------|------------|-------|
| `scopeType`      | `ScopeType`    | Always                         | —          | The organizational level. |
| `platformId`     | `string?`      | Never (optional)               | —          | Must be a valid GUID format if provided. |
| `tenantId`       | `string?`      | Tenant / Organization / User   | 100        | Primary tenancy boundary. |
| `organizationId` | `string?`      | Organization                   | 100        | Organization within the tenant. |
| `userId`         | `string?`      | User                           | 200        | User within the tenant. |

### ScopeType Values

| Value          | Int | TenantId | OrganizationId | UserId |
|----------------|-----|----------|----------------|--------|
| `Global`       | 1   | —        | —              | —      |
| `Platform`     | 2   | —        | —              | —      |
| `Tenant`       | 3   | Required | —              | —      |
| `Organization` | 4   | Required | Required       | —      |
| `User`         | 5   | Required | —              | Required |
| `Service`      | 6   | —        | —              | —      |

---

## Actor Object

**Type**: `AuditEventActorDto`  
**Required**: Yes (object must be present; all fields within are optional)

| Field       | Type        | Max Length | Notes |
|-------------|-------------|------------|-------|
| `type`      | `ActorType` | —          | Required enum. |
| `id`        | `string?`   | 200        | Principal identifier. Omit for Anonymous actors. |
| `name`      | `string?`   | 300        | Display name. |
| `ipAddress` | `string?`   | 45         | IPv4 or IPv6 (max 45 chars covers full IPv6 with zone ID). |
| `userAgent` | `string?`   | 500        | HTTP User-Agent header. |

### ActorType Values

| Value            | Int | Use case |
|------------------|-----|----------|
| `User`           | 1   | Authenticated human user |
| `ServiceAccount` | 2   | M2M client, managed identity |
| `System`         | 3   | Background jobs, lifecycle hooks |
| `Api`            | 4   | External API key caller |
| `Scheduler`      | 5   | Scheduled task or cron job |
| `Anonymous`      | 6   | Unauthenticated caller |
| `Support`        | 7   | Platform-operator acting on behalf of a tenant |

---

## Entity Object

**Type**: `AuditEventEntityDto`  
**Required**: No — omit for events without a target resource (e.g. system startup, login attempt)

| Field  | Type      | Max Length | Notes |
|--------|-----------|------------|-------|
| `type` | `string?` | 200        | Resource type. Example: `Patient`, `Appointment`, `FundTransaction`. |
| `id`   | `string?` | 200        | Resource identifier. |

---

## Enum Reference

### EventCategory

| Value            | Int | Description |
|------------------|-----|-------------|
| `Security`       | 1   | Auth, threat, intrusion events |
| `Access`         | 2   | Read, list, search operations |
| `Business`       | 3   | Domain workflow events (default) |
| `Administrative` | 4   | Tenant/user/role management |
| `System`         | 5   | Platform mechanics, startup, errors |
| `Compliance`     | 6   | HIPAA, SOC 2, regulatory trail |
| `DataChange`     | 7   | Mutations with Before/After snapshots |
| `Integration`    | 8   | Cross-service calls, webhooks |
| `Performance`    | 9   | Latency, throughput observations |

### SeverityLevel

| Value      | Int | Use case |
|------------|-----|----------|
| `Debug`    | 1   | Verbose diagnostic — dev/trace only |
| `Info`     | 2   | Normal operational activity (default) |
| `Notice`   | 3   | Significant but expected |
| `Warn`     | 4   | Recoverable condition |
| `Error`    | 5   | Operation failed — investigation needed |
| `Critical` | 6   | Severe failure — immediate attention |
| `Alert`    | 7   | System-level failure — data loss risk |

### VisibilityScope

| Value          | Int | Who can see |
|----------------|-----|-------------|
| `Platform`     | 1   | Super-admins only |
| `Tenant`       | 2   | Tenant admins + platform admins (default) |
| `Organization` | 3   | Org-level roles within tenantId + orgId |
| `User`         | 4   | The individual actor + admins |
| `Internal`     | 5   | Not exposed via query API |

---

## Batch Submission

**Endpoint**: `POST /api/auditevents/batch`  
**Type**: `BatchIngestRequest`

| Field                | Type                           | Required | Max          | Notes |
|----------------------|--------------------------------|----------|--------------|-------|
| `events`             | `IngestAuditEventRequest[]`    | **Yes**  | 500 per batch| At least 1 item required. |
| `batchCorrelationId` | `string?`                      | No       | 200          | Propagated to items that don't supply their own `correlationId`. |
| `stopOnFirstError`   | `bool`                         | No       | —            | Default `false`. When `true`, halts after first item failure. |

**Batch processing semantics**:
- `stopOnFirstError=false` (default): all items are attempted; per-item success/failure reported.
- `stopOnFirstError=true`: halts after first failure; remaining items are returned as Skipped.
- Idempotency operates per-item via `IdempotencyKey`; no batch-level dedup key.

---

## State Snapshot Fields (Before / After)

Used in `DataChange` category events to carry resource mutation context.

- Stored verbatim as raw text — the audit service does not parse, validate, or index the JSON.
- Max 1,048,576 chars (~1 MB) per field.
- `Before` is null for creation events or events with no prior state.
- `After` is null for deletion events or events with no resulting state.
- Producers should redact PII before submitting snapshots, per HIPAA minimum necessary guidance.

---

## Timestamps

- All timestamps must be `DateTimeOffset` in UTC (`+00:00`).
- `OccurredAtUtc`: when the event happened in the source system. **Required**. Must not exceed +5 minutes from server time. Must not be older than 7 years.
- `RecordedAtUtc`: server receipt time set by the ingest pipeline — not a client field.

---

## Idempotency

- Supply `IdempotencyKey` on every event to enable retry-safe ingestion.
- The service will reject a duplicate key within the configured dedup window, returning HTTP 409.
- Keys longer than 300 characters are rejected.
- For batch submissions, idempotency operates per-item — there is no batch-level key.

---

## Tags

- Open list of string labels for ad-hoc grouping and filtering.
- No taxonomy enforced — callers may define their own labeling conventions.
- Max **20 tags** per event; max **100 characters** per tag.
- Examples: `["pii", "gdpr", "high-risk"]`, `["phi", "hipaa"]`, `["critical-path"]`

---

## Minimal Valid Payload Example

```json
{
  "eventType": "user.login.succeeded",
  "eventCategory": "Security",
  "sourceSystem": "identity-service",
  "sourceService": "auth-api",
  "visibility": "Tenant",
  "severity": "Info",
  "occurredAtUtc": "2026-03-30T14:00:00Z",
  "scope": {
    "scopeType": "Tenant",
    "tenantId": "tenant-abc-123"
  },
  "actor": {
    "type": "User",
    "id": "user-789",
    "name": "Jane Doe"
  }
}
```

## Full Payload Example (DataChange)

```json
{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventType": "patient.record.updated",
  "eventCategory": "DataChange",
  "sourceSystem": "care-connect",
  "sourceService": "patient-api",
  "sourceEnvironment": "production",
  "visibility": "Tenant",
  "severity": "Notice",
  "occurredAtUtc": "2026-03-30T14:01:00Z",
  "scope": {
    "scopeType": "Organization",
    "tenantId": "tenant-abc-123",
    "organizationId": "org-456"
  },
  "actor": {
    "type": "User",
    "id": "user-789",
    "name": "Dr. Jane Doe",
    "ipAddress": "192.168.1.10",
    "userAgent": "Mozilla/5.0 ..."
  },
  "entity": {
    "type": "Patient",
    "id": "patient-001"
  },
  "action": "Updated",
  "description": "Patient address updated by Dr. Jane Doe",
  "before": "{\"address\": \"123 Old St\"}",
  "after": "{\"address\": \"456 New Ave\"}",
  "metadata": "{\"changedFields\": [\"address\"]}",
  "correlationId": "corr-xyz-000",
  "requestId": "req-111",
  "sessionId": "sess-222",
  "idempotencyKey": "patient-001-update-20260330-001",
  "tags": ["phi", "hipaa", "address-change"]
}
```
