# Producer Integration Guide

**Service**: Platform Audit Event Service  
**Audience**: Upstream service teams (identity-service, fund-service, care-connect, gateway, etc.)  
**Version**: 1.0.0  
**Ingest endpoints**: `POST /internal/audit/events` · `POST /internal/audit/events/batch`

---

## Overview

Every microservice in the LegalSynq platform is a **producer** of audit events. Producers submit structured event records to the Platform Audit Event Service, which is the single source of truth for all tamper-evident audit trails.

This guide covers:
- Canonical event contract (field-by-field reference)
- Required vs optional fields
- Idempotency, replay, and correlation guidance
- Visibility and severity selection
- Practical examples for the most common event types
- C# client integration pattern

---

## Ingest Endpoints

| Endpoint | Method | Description |
|---|---|---|
| `/internal/audit/events` | POST | Ingest a single event. Returns `201 Created` with `AuditId`. |
| `/internal/audit/events/batch` | POST | Ingest up to 500 events in one request. Returns per-item results. |

Both endpoints are **internal only** — not exposed through the public API gateway. Callers must be trusted internal services operating within the private network.

---

## Authentication

### Development (Mode=None)
No credentials required. Never deploy this to production.

### Production (Mode=ServiceToken)
Include the `x-service-token` header on every request:

```http
POST /internal/audit/events
x-service-token: <your-service-token>
x-source-system: identity-service
x-source-service: auth-api
Content-Type: application/json
```

Tokens are provisioned per service and injected via environment variables:
```
IngestAuth__ServiceTokens__0__Token       = <token>
IngestAuth__ServiceTokens__0__ServiceName = identity-service
IngestAuth__ServiceTokens__0__Enabled     = true
```

Generate a token: `openssl rand -base64 32`

---

## Canonical Event Contract

### Required Fields

Every ingest request **must** include these fields:

| Field | Type | Max | Description |
|---|---|---|---|
| `eventType` | `string` | 200 | Dot-notation event code. Convention: `{domain}.{resource}.{verb}`. Example: `user.login.succeeded`. |
| `eventCategory` | `EventCategory` | — | Broad domain classification. Drives retention and dashboard routing. |
| `sourceSystem` | `string` | 200 | Logical name of the producing system. Example: `identity-service`. |
| `sourceService` | `string` | 200 | Sub-component or microservice within `sourceSystem`. Example: `auth-api`. |
| `visibility` | `VisibilityScope` | — | Controls who can retrieve this record via the query API. |
| `severity` | `SeverityLevel` | — | Operational severity at the time the event occurred. |
| `occurredAtUtc` | `DateTimeOffset` | — | UTC time the event occurred in the source system. Must not be more than 5 minutes in the future or older than 7 years. |
| `scope` | `ScopeObject` | — | Tenancy and organizational scope. See [Scope Object](#scope-object). |
| `actor` | `ActorObject` | — | The principal that performed the action. See [Actor Object](#actor-object). |

### Optional Fields

These fields are validated only when provided:

| Field | Type | Max | Description |
|---|---|---|---|
| `eventId` | `Guid?` | — | Source-assigned domain event identifier. Not used for deduplication. |
| `sourceEnvironment` | `string?` | 100 | Deployment environment: `production`, `staging`, `dev`. |
| `entity` | `EntityObject?` | — | The resource acted upon. Omit for non-resource events (login, startup, etc.). |
| `action` | `string?` | 200 | PascalCase verb. Examples: `Created`, `Updated`, `Approved`. |
| `description` | `string?` | 2000 | Human-readable summary for audit log displays. |
| `before` | `string?` | 1,048,576 | JSON snapshot of resource state **before** the action. |
| `after` | `string?` | 1,048,576 | JSON snapshot of resource state **after** the action. |
| `metadata` | `string?` | 1,048,576 | Arbitrary JSON for additional context that doesn't fit canonical fields. |
| `correlationId` | `string?` | 200 | Distributed trace correlation ID (W3C traceparent or `X-Correlation-Id`). |
| `requestId` | `string?` | 200 | HTTP request identifier from the originating request. |
| `sessionId` | `string?` | 200 | User session identifier when the action occurred within a session context. |
| `idempotencyKey` | `string?` | 300 | Source-provided key for deduplication. Strongly recommended for retry safety. |
| `isReplay` | `bool` | — | Set `true` when re-submitting an event through a replay mechanism. Default: `false`. |
| `tags` | `string[]?` | 20 items | Ad-hoc string labels. Max 100 chars per tag. No taxonomy enforced. |

---

## Scope Object

Scopes the event to the correct organizational boundary. The `scopeType` determines which ID fields are required.

| `scopeType` | `tenantId` | `organizationId` | `userId` | Use case |
|---|---|---|---|---|
| `Global` | — | — | — | Platform-wide events (e.g. infrastructure alerts) |
| `Platform` | — | — | — | Platform-level administrative events |
| `Tenant` | **Required** | — | — | Tenant-level events (most common) |
| `Organization` | **Required** | **Required** | — | Organization-scoped events |
| `User` | **Required** | — | **Required** | User-specific events |
| `Service` | — | — | — | Service-to-service integration events |

```json
"scope": {
  "scopeType": "Tenant",
  "tenantId": "tenant-abc-123"
}
```

---

## Actor Object

Identifies the principal that performed the action. All fields within the actor object are optional, but at minimum set `type`.

| `type` | When to use |
|---|---|
| `User` | Authenticated human user |
| `ServiceAccount` | Machine-to-machine client, managed identity |
| `System` | Background jobs, lifecycle hooks, automated processes |
| `Api` | External API key caller |
| `Scheduler` | Scheduled task or cron job |
| `Anonymous` | Unauthenticated caller (e.g. failed login attempt) |
| `Support` | Platform operator acting on behalf of a tenant |

```json
"actor": {
  "type": "User",
  "id": "user-789",
  "name": "Jane Doe",
  "ipAddress": "192.168.1.10",
  "userAgent": "Mozilla/5.0 ..."
}
```

---

## Entity Object

The resource acted upon. Omit when the event is not targeted at a specific resource (login attempt, system startup, session ending).

```json
"entity": {
  "type": "Appointment",
  "id": "appt-456"
}
```

---

## Enum Reference

### EventCategory

| Value | Description | Typical events |
|---|---|---|
| `Security` | Auth, threat, intrusion events | Login, authorization denial, MFA, password change |
| `Access` | Read, list, search operations | Document viewed, record read, export downloaded |
| `Business` | Domain workflow events | Referral created, appointment scheduled, plan activated |
| `Administrative` | Tenant/user/role management | Tenant created, role assigned, user invited |
| `System` | Platform mechanics, startup, errors | Service startup, config reload, health check |
| `Compliance` | HIPAA, SOC 2, regulatory trail | Consent signed, BAA executed, data retention enforced |
| `DataChange` | Mutations with Before/After snapshots | Record updated, field changed (use `before`/`after`) |
| `Integration` | Cross-service calls, webhooks | Outbound webhook sent, external API called |
| `Performance` | Latency, throughput observations | Slow query alert, throughput degradation |

### SeverityLevel

| Value | When to use |
|---|---|
| `Debug` | Verbose diagnostic — dev/trace only. Not forwarded to downstream consumers. |
| `Info` | Normal operational activity. Most successful operations. |
| `Notice` | Significant but expected event (state change, tenant creation). |
| `Warn` | Recoverable condition — failed login, rate limit hit, retry needed. |
| `Error` | Operation failed, investigation needed. |
| `Critical` | Severe failure — immediate attention required. |
| `Alert` | System-level failure — potential data loss risk. |

### VisibilityScope

| Value | Who can query this record |
|---|---|
| `Platform` | Platform super-admins only |
| `Tenant` | Tenant admins + platform admins (default for most events) |
| `Organization` | Org-level roles within the `tenantId` + `organizationId` |
| `User` | The individual actor and admins above |
| `Internal` | Not exposed via the query API (service internals, debug traces) |

---

## Idempotency Guidance

**Always supply `idempotencyKey` when possible.** It is the only mechanism that makes retries safe.

### Why it matters

Network failures, pod restarts, and deployment rollovers all cause at-least-once delivery scenarios. Without an idempotency key, a retry creates a duplicate audit record. With one, the second submission returns `409 Conflict` without persisting a duplicate.

### Key construction guidelines

A good idempotency key is:
- **Unique per logical event** — not per HTTP request
- **Deterministic** — reconstructable from the event's natural identifiers
- **Stable across retries** — the same retry with the same key must produce the same key

```csharp
// Good: deterministic from stable event fields
$"{sourceSystem}:{eventType}:{entityId}:{occurredAtUtc:yyyyMMddHHmmss}"

// Good: use the source system's own event/transaction ID
$"identity-service:session-started:{sessionId}"

// Avoid: random GUIDs per request (defeats the purpose)
Guid.NewGuid().ToString()
```

### Key length
Max 300 characters. Keys longer than this are rejected with `400 Bad Request`.

### Dedup window
Configured per deployment. The default window is sufficient to cover typical retry windows (minutes to hours). Check with platform ops for the configured value.

### Response codes

| Code | Meaning |
|---|---|
| `201 Created` | Event accepted and persisted. Body contains `AuditId`. |
| `409 Conflict` | Duplicate `IdempotencyKey` — event already ingested. Treat as success. |
| `503 Service Unavailable` | Transient persistence failure. Retry with exponential backoff. |

---

## Replay Guidance

Set `isReplay: true` when re-submitting an event that originally occurred in the past, through a migration or re-processing pipeline.

### Replay behaviour
- A new `AuditId` and `RecordedAtUtc` are assigned by the server.
- The `OccurredAtUtc` from the original event is preserved.
- The `IsReplay` flag is set on the persisted record as a semantic marker.
- The record participates in the integrity chain like any other record.

### When to use replay
- Data migration from a legacy audit store
- Re-processing events after a data recovery operation
- Backfilling events that were dropped during an outage

### Caution
- Supply `idempotencyKey` on replay events to prevent double-submission if the replay pipeline retries.
- Downstream event forwarding skips replay records by default (`ForwardReplayRecords=false`). Confirm with platform ops before enabling forwarding for replays.

---

## Correlation, Request, and Session Guidance

Use these fields to connect an audit event to the originating user action for distributed tracing.

| Field | Source | Example |
|---|---|---|
| `correlationId` | W3C `traceparent` header or `X-Correlation-Id` header propagated across services | `00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01` |
| `requestId` | HTTP request identifier from the originating gateway or load balancer request | `req-abc-123` |
| `sessionId` | User session identifier (from session store, JWT `jti`, or cookie) | `sess-789xyz` |

**Best practice**: populate all three whenever the event originates from a user-initiated HTTP request. This enables complete end-to-end tracing from the user action through every microservice that touched the request.

For background jobs and scheduled tasks, `correlationId` can be set to the job run ID or batch ID. `requestId` and `sessionId` are typically null in this case.

---

## Visibility Scope Guidance

Choose the most restrictive visibility that still allows legitimate query access.

| Scenario | Recommended visibility |
|---|---|
| Security events (login, auth denial) | `Tenant` — tenant admins need to see these |
| Organization-level administrative events | `Organization` — org admins only |
| User's own activity | `User` — only the user and admins above them |
| Platform-level system events | `Platform` — super-admins only |
| Internal service diagnostics | `Internal` — never visible via query API |
| Most business events (appointments, referrals) | `Tenant` (default) |
| PHI-adjacent operations | `Organization` or `Tenant` with appropriate tagging |

---

## Severity Guidance

| Scenario | Severity |
|---|---|
| Successful login, normal operation | `Info` |
| Record viewed, document downloaded | `Info` |
| Referral created, appointment scheduled | `Info` |
| Tenant created, role assigned | `Notice` |
| Patient record updated, data change | `Notice` |
| Failed login attempt | `Warn` |
| Authorization denied | `Warn` |
| Payment declined | `Warn` |
| API call failed, integration error | `Error` |
| Data loss potential detected | `Critical` |
| System-wide failure | `Alert` |

Default: `Info`. When in doubt, use `Info` for successful operations and `Warn` for recoverable failures.

---

## Before / After State Snapshots

Use `before` and `after` with `eventCategory: DataChange` when you need a tamper-evident record of what changed.

```json
{
  "eventCategory": "DataChange",
  "before": "{\"status\": \"active\", \"email\": \"old@example.com\"}",
  "after":  "{\"status\": \"suspended\", \"email\": \"old@example.com\"}",
  "metadata": "{\"changedFields\": [\"status\"], \"changedBy\": \"admin-op\"}"
}
```

**HIPAA compliance**: redact PHI from snapshots before submitting. The audit service stores `before`/`after` verbatim and does not parse or index them. The responsibility for minimising PHI exposure rests with the producer.

---

## Batch Submission

For high-throughput producers (background workers, migration jobs), batch up to 500 events per request:

```json
POST /internal/audit/events/batch
{
  "batchCorrelationId": "migration-run-20260330-001",
  "stopOnFirstError": false,
  "events": [
    { ... event 1 ... },
    { ... event 2 ... }
  ]
}
```

- `batchCorrelationId` is propagated to any event that doesn't supply its own `correlationId`.
- `stopOnFirstError=false` (default): all events are attempted; partial acceptance is possible.
- `stopOnFirstError=true`: halts after the first failure; remaining events are marked `Skipped`.
- Idempotency operates per-item — there is no batch-level dedup key.
- Response status: `200 OK` (all accepted), `207 Multi-Status` (partial), `422 Unprocessable Entity` (all rejected).

---

## Event Examples

### 1. Login Success

```json
{
  "eventType": "user.login.succeeded",
  "eventCategory": "Security",
  "sourceSystem": "identity-service",
  "sourceService": "auth-api",
  "visibility": "Tenant",
  "severity": "Info",
  "occurredAtUtc": "2026-03-30T14:00:00Z",
  "scope": { "scopeType": "Tenant", "tenantId": "tenant-abc-123" },
  "actor": {
    "type": "User",
    "id": "user-789",
    "name": "Jane Doe",
    "ipAddress": "203.0.113.1",
    "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
  },
  "action": "LoginSucceeded",
  "description": "User Jane Doe authenticated successfully.",
  "correlationId": "trace-abc-001",
  "requestId": "req-001",
  "sessionId": "sess-new-456",
  "idempotencyKey": "identity-service:user.login.succeeded:user-789:20260330T140000Z",
  "tags": ["auth", "login"]
}
```

### 2. Login Failure

```json
{
  "eventType": "user.login.failed",
  "eventCategory": "Security",
  "sourceSystem": "identity-service",
  "sourceService": "auth-api",
  "visibility": "Tenant",
  "severity": "Warn",
  "occurredAtUtc": "2026-03-30T14:01:00Z",
  "scope": { "scopeType": "Tenant", "tenantId": "tenant-abc-123" },
  "actor": {
    "type": "Anonymous",
    "ipAddress": "203.0.113.99",
    "userAgent": "curl/8.0"
  },
  "action": "LoginFailed",
  "description": "Login attempt failed: invalid credentials for email user@example.com.",
  "metadata": "{\"reason\": \"InvalidCredentials\", \"attemptCount\": 3}",
  "correlationId": "trace-abc-002",
  "requestId": "req-002",
  "idempotencyKey": "identity-service:user.login.failed:user@example.com:20260330T140100Z",
  "tags": ["auth", "login-failure"]
}
```

### 3. Authorization Denied

```json
{
  "eventType": "user.authorization.denied",
  "eventCategory": "Security",
  "sourceSystem": "care-connect",
  "sourceService": "patient-api",
  "visibility": "Tenant",
  "severity": "Warn",
  "occurredAtUtc": "2026-03-30T14:02:00Z",
  "scope": { "scopeType": "Organization", "tenantId": "tenant-abc-123", "organizationId": "org-456" },
  "actor": { "type": "User", "id": "user-789", "name": "Jane Doe" },
  "entity": { "type": "PatientRecord", "id": "patient-001" },
  "action": "AuthorizationDenied",
  "description": "User Jane Doe was denied access to PatientRecord patient-001: insufficient permissions.",
  "metadata": "{\"requiredPermission\": \"patient:read:phi\", \"userRole\": \"receptionist\"}",
  "correlationId": "trace-abc-003",
  "requestId": "req-003",
  "sessionId": "sess-456",
  "idempotencyKey": "care-connect:user.authorization.denied:user-789:patient-001:20260330T140200Z",
  "tags": ["authz", "denied", "phi"]
}
```

### 4. Tenant Created

```json
{
  "eventType": "tenant.created",
  "eventCategory": "Administrative",
  "sourceSystem": "identity-service",
  "sourceService": "tenant-provisioning-api",
  "visibility": "Platform",
  "severity": "Notice",
  "occurredAtUtc": "2026-03-30T09:00:00Z",
  "scope": { "scopeType": "Tenant", "tenantId": "tenant-new-999" },
  "actor": { "type": "ServiceAccount", "id": "provisioning-service", "name": "Tenant Provisioning Service" },
  "entity": { "type": "Tenant", "id": "tenant-new-999" },
  "action": "Created",
  "description": "New tenant 'Acme Health Partners' provisioned.",
  "after": "{\"tenantId\": \"tenant-new-999\", \"name\": \"Acme Health Partners\", \"plan\": \"enterprise\"}",
  "metadata": "{\"provisionedBy\": \"onboarding-workflow-run-20260330\"}",
  "correlationId": "trace-provision-001",
  "idempotencyKey": "identity-service:tenant.created:tenant-new-999:20260330T090000Z",
  "tags": ["tenant-lifecycle", "provisioning"]
}
```

### 5. Organization Relationship Created

```json
{
  "eventType": "organization.relationship.created",
  "eventCategory": "Administrative",
  "sourceSystem": "care-connect",
  "sourceService": "organization-api",
  "visibility": "Tenant",
  "severity": "Notice",
  "occurredAtUtc": "2026-03-30T10:00:00Z",
  "scope": { "scopeType": "Organization", "tenantId": "tenant-abc-123", "organizationId": "org-456" },
  "actor": { "type": "User", "id": "admin-001", "name": "Admin User" },
  "entity": { "type": "OrganizationRelationship", "id": "rel-org-456-org-789" },
  "action": "Created",
  "description": "Referral relationship established between Org-456 (Primary Care) and Org-789 (Specialist Group).",
  "after": "{\"fromOrgId\": \"org-456\", \"toOrgId\": \"org-789\", \"relationshipType\": \"ReferralPartner\"}",
  "correlationId": "trace-rel-001",
  "idempotencyKey": "care-connect:organization.relationship.created:org-456:org-789:20260330T100000Z",
  "tags": ["org-relationship", "referral-network"]
}
```

### 6. Role Assignment Changed

```json
{
  "eventType": "user.role.assigned",
  "eventCategory": "Administrative",
  "sourceSystem": "identity-service",
  "sourceService": "rbac-api",
  "visibility": "Tenant",
  "severity": "Notice",
  "occurredAtUtc": "2026-03-30T11:00:00Z",
  "scope": { "scopeType": "Organization", "tenantId": "tenant-abc-123", "organizationId": "org-456" },
  "actor": { "type": "User", "id": "admin-001", "name": "Admin User" },
  "entity": { "type": "User", "id": "user-789" },
  "action": "RoleAssigned",
  "description": "Role 'ClinicalManager' assigned to user Jane Doe in Org-456.",
  "before": "{\"roles\": [\"Clinician\"]}",
  "after":  "{\"roles\": [\"Clinician\", \"ClinicalManager\"]}",
  "metadata": "{\"assignedRole\": \"ClinicalManager\", \"scope\": \"org-456\"}",
  "correlationId": "trace-rbac-001",
  "requestId": "req-rbac-001",
  "sessionId": "sess-admin-001",
  "idempotencyKey": "identity-service:user.role.assigned:user-789:ClinicalManager:org-456:20260330T110000Z",
  "tags": ["rbac", "role-change"]
}
```

### 7. Record Updated (DataChange with Before/After)

```json
{
  "eventType": "patient.record.updated",
  "eventCategory": "DataChange",
  "sourceSystem": "care-connect",
  "sourceService": "patient-api",
  "visibility": "Organization",
  "severity": "Notice",
  "occurredAtUtc": "2026-03-30T12:00:00Z",
  "scope": { "scopeType": "Organization", "tenantId": "tenant-abc-123", "organizationId": "org-456" },
  "actor": { "type": "User", "id": "clinician-001", "name": "Dr. Jane Doe" },
  "entity": { "type": "PatientRecord", "id": "patient-001" },
  "action": "Updated",
  "description": "Patient contact information updated by Dr. Jane Doe.",
  "before": "{\"phone\": \"555-0100\", \"email\": \"old@example.com\"}",
  "after":  "{\"phone\": \"555-0199\", \"email\": \"new@example.com\"}",
  "metadata": "{\"changedFields\": [\"phone\", \"email\"], \"changeReason\": \"PatientRequest\"}",
  "correlationId": "trace-update-001",
  "requestId": "req-update-001",
  "sessionId": "sess-clinician-001",
  "idempotencyKey": "care-connect:patient.record.updated:patient-001:20260330T120000Z",
  "tags": ["phi", "hipaa", "contact-info-change"]
}
```

### 8. Referral Created

```json
{
  "eventType": "referral.created",
  "eventCategory": "Business",
  "sourceSystem": "care-connect",
  "sourceService": "referral-api",
  "visibility": "Organization",
  "severity": "Info",
  "occurredAtUtc": "2026-03-30T13:00:00Z",
  "scope": { "scopeType": "Organization", "tenantId": "tenant-abc-123", "organizationId": "org-456" },
  "actor": { "type": "User", "id": "clinician-001", "name": "Dr. Jane Doe" },
  "entity": { "type": "Referral", "id": "ref-20260330-001" },
  "action": "Created",
  "description": "Referral to Specialist Group created for patient patient-001.",
  "after": "{\"referralId\": \"ref-20260330-001\", \"toOrgId\": \"org-789\", \"urgency\": \"Routine\"}",
  "metadata": "{\"patientId\": \"patient-001\", \"specialty\": \"Cardiology\"}",
  "correlationId": "trace-referral-001",
  "requestId": "req-referral-001",
  "sessionId": "sess-clinician-001",
  "idempotencyKey": "care-connect:referral.created:ref-20260330-001",
  "tags": ["referral", "workflow"]
}
```

### 9. Appointment Scheduled

```json
{
  "eventType": "appointment.scheduled",
  "eventCategory": "Business",
  "sourceSystem": "care-connect",
  "sourceService": "scheduling-api",
  "visibility": "Organization",
  "severity": "Info",
  "occurredAtUtc": "2026-03-30T14:00:00Z",
  "scope": { "scopeType": "Organization", "tenantId": "tenant-abc-123", "organizationId": "org-456" },
  "actor": { "type": "User", "id": "scheduler-001", "name": "Front Desk Staff" },
  "entity": { "type": "Appointment", "id": "appt-20260330-001" },
  "action": "Scheduled",
  "description": "Appointment scheduled with Dr. Smith for patient patient-001 on 2026-04-15 at 10:00 AM.",
  "after": "{\"appointmentId\": \"appt-20260330-001\", \"providerId\": \"prov-smith\", \"scheduledFor\": \"2026-04-15T10:00:00Z\"}",
  "metadata": "{\"patientId\": \"patient-001\", \"appointmentType\": \"FollowUp\"}",
  "correlationId": "trace-appt-001",
  "requestId": "req-appt-001",
  "sessionId": "sess-scheduler-001",
  "idempotencyKey": "care-connect:appointment.scheduled:appt-20260330-001",
  "tags": ["scheduling", "appointment"]
}
```

### 10. Document Viewed

```json
{
  "eventType": "document.viewed",
  "eventCategory": "Access",
  "sourceSystem": "care-connect",
  "sourceService": "document-api",
  "visibility": "Organization",
  "severity": "Info",
  "occurredAtUtc": "2026-03-30T14:30:00Z",
  "scope": { "scopeType": "Organization", "tenantId": "tenant-abc-123", "organizationId": "org-456" },
  "actor": {
    "type": "User",
    "id": "clinician-001",
    "name": "Dr. Jane Doe",
    "ipAddress": "10.0.0.50",
    "userAgent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)"
  },
  "entity": { "type": "Document", "id": "doc-hipaa-baa-2026" },
  "action": "Viewed",
  "description": "Document 'BAA Agreement 2026' viewed by Dr. Jane Doe.",
  "metadata": "{\"documentName\": \"BAA Agreement 2026\", \"documentType\": \"LegalAgreement\"}",
  "correlationId": "trace-doc-001",
  "requestId": "req-doc-001",
  "sessionId": "sess-clinician-001",
  "idempotencyKey": "care-connect:document.viewed:doc-hipaa-baa-2026:clinician-001:20260330T143000Z",
  "tags": ["document-access", "hipaa"]
}
```

### 11. Workflow Approved

```json
{
  "eventType": "workflow.approved",
  "eventCategory": "Compliance",
  "sourceSystem": "care-connect",
  "sourceService": "workflow-engine",
  "visibility": "Tenant",
  "severity": "Notice",
  "occurredAtUtc": "2026-03-30T15:00:00Z",
  "scope": { "scopeType": "Organization", "tenantId": "tenant-abc-123", "organizationId": "org-456" },
  "actor": { "type": "User", "id": "approver-001", "name": "Medical Director" },
  "entity": { "type": "WorkflowInstance", "id": "wf-prior-auth-20260330-001" },
  "action": "Approved",
  "description": "Prior authorization workflow 'wf-prior-auth-20260330-001' approved by Medical Director.",
  "after": "{\"workflowId\": \"wf-prior-auth-20260330-001\", \"outcome\": \"Approved\", \"approvedAt\": \"2026-03-30T15:00:00Z\"}",
  "metadata": "{\"workflowType\": \"PriorAuthorization\", \"patientId\": \"patient-001\", \"procedureCode\": \"CPT-99213\"}",
  "correlationId": "trace-wf-001",
  "requestId": "req-wf-001",
  "sessionId": "sess-approver-001",
  "idempotencyKey": "care-connect:workflow.approved:wf-prior-auth-20260330-001",
  "tags": ["workflow", "compliance", "prior-auth"]
}
```

---

## EventType Naming Conventions

Follow the `{domain}.{resource}.{verb}` pattern. Use lowercase dot-separated tokens.

| Domain | Examples |
|---|---|
| `user` | `user.login.succeeded`, `user.login.failed`, `user.password.changed`, `user.mfa.enrolled` |
| `tenant` | `tenant.created`, `tenant.suspended`, `tenant.deleted` |
| `organization` | `organization.created`, `organization.relationship.created`, `organization.deactivated` |
| `patient` | `patient.record.created`, `patient.record.updated`, `patient.record.deleted` |
| `appointment` | `appointment.scheduled`, `appointment.cancelled`, `appointment.completed` |
| `referral` | `referral.created`, `referral.accepted`, `referral.declined` |
| `document` | `document.uploaded`, `document.viewed`, `document.signed`, `document.deleted` |
| `workflow` | `workflow.started`, `workflow.approved`, `workflow.rejected`, `workflow.completed` |
| `role` | `user.role.assigned`, `user.role.revoked` |
| `consent` | `consent.granted`, `consent.revoked`, `consent.updated` |
| `fund` | `fund.account.created`, `fund.transaction.posted`, `fund.transfer.initiated` |
| `system` | `system.startup`, `system.config.changed`, `system.health.degraded` |

---

## HTTP Response Reference

### Single Ingest

| Code | Meaning | Action |
|---|---|---|
| `201 Created` | Event accepted and persisted | Extract `AuditId` from response body |
| `400 Bad Request` | Structural validation failure | Fix field errors in response body |
| `401 Unauthorized` | Missing or invalid `x-service-token` | Check token and configuration |
| `403 Forbidden` | Source system not in allowlist | Contact platform ops to add source |
| `409 Conflict` | Duplicate `idempotencyKey` | Treat as success — event already recorded |
| `503 Service Unavailable` | Transient persistence failure | Retry with exponential backoff (suggested: 100ms, 500ms, 2s, 10s) |

### Batch Ingest

| Code | Meaning |
|---|---|
| `200 OK` | All events accepted |
| `207 Multi-Status` | Partial — inspect per-item `Results` array |
| `400 Bad Request` | Batch-level structural validation failure |
| `422 Unprocessable Entity` | All events rejected |

---

## C# Client

See [`Examples/AuditEventClientExample.cs`](../Examples/AuditEventClientExample.cs) for a complete, ready-to-adapt .NET 8 client implementation showing all 11 event patterns plus single and batch ingest.

---

## See Also

- [Canonical Event Contract](canonical-event-contract.md) — authoritative field reference
- [Ingest Auth Guide](ingest-auth.md) — service token setup and rotation
- [Event Forwarding Model](event-forwarding-model.md) — how events are forwarded to downstream systems
- [Architecture Overview](architecture_overview.md) — service topology and data flow
