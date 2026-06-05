# Audit Service API Documentation

## Table of Contents

- [Overview](#overview)
- [Authentication & Authorization](#authentication--authorization)
- [Authorization Scopes](#authorization-scopes)
- [Common Models](#common-models)
- [Enum Reference](#enum-reference)
- [Error Responses](#error-responses)
- [Event Ingestion](#event-ingestion-endpoints)
- [Event Query](#event-query-endpoints)
- [Export](#export-endpoints)
- [Integrity Checkpoints](#integrity-checkpoint-endpoints)
- [Legal Holds](#legal-hold-endpoints)
- [Legacy / Deprecated](#legacy--deprecated-endpoints)

---

## Overview

The Audit service provides tamper-evident event ingestion, querying, export, integrity verification, and legal hold capabilities. It exposes two route prefixes:

| Prefix | Purpose |
|---|---|
| `/internal/audit` | Machine-to-machine ingestion from trusted internal systems |
| `/audit` | Query, export, integrity, and legal hold endpoints |

All endpoints produce `application/json` responses. Request bodies use `application/json` content type unless otherwise noted.

---

## Authentication & Authorization

The Audit service uses two distinct authentication models depending on the route prefix:

### Ingestion endpoints (`/internal/audit`)

Controlled by `IngestAuth` configuration:

- **Production** — callers must present a pre-shared API key or mTLS certificate.
- **Development** (`IngestAuth:Mode=None`) — requests are accepted unauthenticated.

### Query endpoints (`/audit`)

Resolved per-request by `QueryAuthMiddleware`:

1. The middleware extracts the caller's identity and maps it to a `CallerScope`.
2. Each endpoint (or the `IQueryAuthorizer`) checks the caller's scope against the minimum required scope.
3. The query is mutated in-place to enforce scope constraints (tenant isolation, visibility ceiling, actor restriction).

Authentication modes:

| Mode | Description |
|---|---|
| `Bearer` | JWT-based. Scope resolved from token claims. |
| `None` | Development mode. All callers receive PlatformAdmin scope. |

---

## Authorization Scopes

Scopes are ordered from least to most privileged. Each scope determines what records the caller can access.

| Scope | Value | Description |
|---|---|---|
| `Unknown` | 0 | Cannot be mapped to a known role. All access denied. |
| `UserSelf` | 1 | Self-service only. Restricted to own records (`ActorId == caller.UserId`) with User-level visibility. |
| `TenantUser` | 2 | Standard tenant user. Own tenant only, User-visibility records only. |
| `Restricted` | 3 | Compliance read-only. Own tenant, Tenant-visibility and below. Cannot trigger exports. |
| `OrganizationAdmin` | 4 | Organization admin. Same tenant + organization, Organization-visibility and below. |
| `TenantAdmin` | 5 | Tenant admin. Unrestricted within own tenant. No cross-tenant or Platform-scoped access. |
| `PlatformAdmin` | 6 | Platform super-admin. Unrestricted cross-tenant read access including Platform-scope events. |

---

## Common Models

### ApiResponse\<T\>

All endpoints return responses wrapped in this standardized envelope.

| Field | Type | Description |
|---|---|---|
| `success` | `boolean` | `true` for successful operations, `false` for errors |
| `data` | `T` or `null` | Response payload on success; `null` on failure |
| `message` | `string` or `null` | Human-readable status or error message |
| `traceId` | `string` or `null` | Distributed trace identifier for correlation |
| `errors` | `string[]` | List of field-level validation errors (empty when `success` is `true`) |

**Success example:**

```json
{
  "success": true,
  "data": { ... },
  "message": "Event accepted.",
  "traceId": "abc123",
  "errors": []
}
```

**Validation failure example:**

```json
{
  "success": false,
  "data": null,
  "message": "Validation failed.",
  "traceId": "abc123",
  "errors": [
    "EventType must not be empty.",
    "SourceSystem must not be empty."
  ]
}
```

### PagedResult\<T\>

Used by integrity checkpoint listing and legacy endpoints.

| Field | Type | Description |
|---|---|---|
| `items` | `T[]` | Array of result items for the current page |
| `totalCount` | `integer` | Total number of matching items across all pages |
| `page` | `integer` | Current 1-based page number |
| `pageSize` | `integer` | Number of items per page |
| `totalPages` | `integer` | Computed total page count |
| `hasNext` | `boolean` | `true` when a next page exists |
| `hasPrev` | `boolean` | `true` when a previous page exists |

### AuditEventQueryResponse

Used by query endpoints. Extends the standard page envelope with time-range metadata.

| Field | Type | Description |
|---|---|---|
| `items` | `AuditEventRecordResponse[]` | Records matching the query on this page |
| `totalCount` | `long` | Total matching records across all pages |
| `page` | `integer` | Current 1-based page number |
| `pageSize` | `integer` | Records per page as applied by the service |
| `totalPages` | `integer` | Computed total page count |
| `hasNext` | `boolean` | `true` when a next page exists |
| `hasPrev` | `boolean` | `true` when a previous page exists |
| `earliestOccurredAtUtc` | `datetime` or `null` | Earliest event timestamp in the full result set |
| `latestOccurredAtUtc` | `datetime` or `null` | Latest event timestamp in the full result set |

---

## Enum Reference

### EventCategory

| Value | Name | Description |
|---|---|---|
| 1 | `Security` | Authentication, authorization, threat, and intrusion events |
| 2 | `Access` | Resource and API access — read, list, search operations |
| 3 | `Business` | Domain workflow events — creates, updates, transitions |
| 4 | `Administrative` | Platform/tenant administration — settings, role, user management |
| 5 | `System` | Internal platform mechanics — startup, shutdown, job execution, errors |
| 6 | `Compliance` | Events required for regulatory compliance — HIPAA, SOC 2, audit trail |
| 7 | `DataChange` | Explicit before/after record mutations — carries Before/After JSON payloads |
| 8 | `Integration` | Cross-service integration calls, webhook deliveries, external API interactions |
| 9 | `Performance` | Latency, throughput, and resource utilization observations |

### ScopeType

| Value | Name | Description |
|---|---|---|
| 1 | `Global` | Global / cross-platform scope. No tenant or org constraint |
| 2 | `Platform` | Scoped to the platform layer (infrastructure, billing, licensing) |
| 3 | `Tenant` | Scoped to a specific tenant identified by TenantId |
| 4 | `Organization` | Scoped to an organization within a tenant (TenantId + OrganizationId) |
| 5 | `User` | Scoped to a single user (TenantId + UserId/ActorId) |
| 6 | `Service` | Scoped to a specific service or integration, regardless of tenant |

### ActorType

| Value | Name | Description |
|---|---|---|
| 1 | `User` | A human user authenticated through the identity system |
| 2 | `ServiceAccount` | A non-human service principal, M2M client, or managed identity |
| 3 | `System` | The platform itself — background jobs, automated processes |
| 4 | `Api` | External API caller authenticated via API key, not a user session |
| 5 | `Scheduler` | Scheduled task or cron job |
| 6 | `Anonymous` | Unauthenticated caller — no identity could be established |
| 7 | `Support` | Internal support or platform-operator action on behalf of a tenant |

### VisibilityScope

| Value | Name | Description |
|---|---|---|
| 1 | `Platform` | Visible only to platform super-admins |
| 2 | `Tenant` | Visible to tenant admins and compliance officers (default) |
| 3 | `Organization` | Visible to organization-level roles within same tenant + org |
| 4 | `User` | Visible to the individual user and admins |
| 5 | `Internal` | Not exposed through the query API regardless of caller role |

### SeverityLevel

| Value | Name | Description |
|---|---|---|
| 1 | `Debug` | Verbose diagnostic data — development/trace contexts only |
| 2 | `Info` | Normal, expected operational activity (default) |
| 3 | `Notice` | Significant but normal event |
| 4 | `Warn` | Recoverable condition that may indicate a problem |
| 5 | `Error` | Operation failed — requires investigation |
| 6 | `Critical` | Severe failure — service degradation likely |
| 7 | `Alert` | System-level failure — data loss, security breach |

### ExportStatus

| Value | Name | Description |
|---|---|---|
| 1 | `Pending` | Job created, not yet picked up by the export worker |
| 2 | `Processing` | Export worker is actively building the output file |
| 3 | `Completed` | Output file produced and available for download |
| 4 | `Failed` | Export failed — see `errorMessage` for details |
| 5 | `Cancelled` | Cancelled before or during processing |
| 6 | `Expired` | Completed file exceeded its retention window and was purged |

---

## Error Responses

### 400 Bad Request

Returned when request validation fails. The `errors` array contains field-level details.

```json
{
  "success": false,
  "data": null,
  "message": "Validation failed.",
  "traceId": "abc123",
  "errors": [
    "EventType must not be empty.",
    "Action must not be empty."
  ]
}
```

### 401 Unauthorized

Returned when no valid credentials are presented.

```json
{
  "success": false,
  "data": null,
  "message": "Authentication is required to access this resource.",
  "traceId": "abc123",
  "errors": []
}
```

### 403 Forbidden

Returned when the caller is authenticated but lacks the required scope.

```json
{
  "success": false,
  "data": null,
  "message": "This endpoint requires TenantAdmin scope or higher. Your current scope is TenantUser.",
  "traceId": "abc123",
  "errors": []
}
```

### 404 Not Found

Returned when a requested resource does not exist.

### 409 Conflict

Returned when a duplicate idempotency key is detected (ingestion) or a legal hold is already released.

### 503 Service Unavailable

Returned when a subsystem is not configured (e.g., export provider is `None`) or a transient infrastructure error occurs during ingestion.

### Common Status Codes

| Endpoint Type | Success | Possible Errors |
|---|---|---|
| Ingest single (`POST`) | `201 Created` | `400`, `409`, `422`, `503` |
| Ingest batch (`POST`) | `200 OK` / `207 Multi-Status` | `400`, `422` |
| List / Query (`GET`) | `200 OK` | `400`, `401`, `403` |
| Get by ID (`GET`) | `200 OK` | `401`, `403`, `404` |
| Submit export (`POST`) | `202 Accepted` | `400`, `401`, `403`, `503` |
| Poll export status (`GET`) | `200 OK` | `404`, `503` |
| Generate checkpoint (`POST`) | `201 Created` | `400`, `401`, `403` |
| Create legal hold (`POST`) | `201 Created` | `400`, `404` |
| Release legal hold (`POST`) | `200 OK` | `404`, `409` |

---

## Event Ingestion Endpoints

Base path: `/internal/audit`

These endpoints are for machine-to-machine communication from trusted internal source systems only. They are NOT part of the public query/read surface.

---

### POST `/internal/audit/events`

Ingest a single audit event from an internal source system.

The pipeline enforces idempotency, computes the integrity hash, and appends the record to the tamper-evident audit chain.

**Request Body: `IngestAuditEventRequest`**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `eventId` | `guid` | No | `null` | Source-assigned domain event identifier. Not used for deduplication. |
| `eventType` | `string` | Yes | — | Dot-notation event code (max 200 chars). E.g. `"user.login.succeeded"` |
| `eventCategory` | `EventCategory` | No | `Business` | Broad domain classification. Drives retention policy and dashboard routing. |
| `sourceSystem` | `string` | Yes | — | Logical name of the producing system (max 200 chars). |
| `sourceService` | `string` | No | `null` | Sub-component within the source system. |
| `sourceEnvironment` | `string` | No | `null` | Deployment environment (e.g. `"production"`, `"staging"`). |
| `scope` | `AuditEventScopeDto` | Yes | — | Tenancy and organizational scope (see below). |
| `actor` | `AuditEventActorDto` | Yes | — | Principal that performed the action (see below). |
| `entity` | `AuditEventEntityDto` | No | `null` | Resource acted upon (see below). |
| `action` | `string` | Yes | — | PascalCase verb (max 200 chars). E.g. `"Created"`, `"LoginAttempted"` |
| `description` | `string` | Yes | — | Human-readable summary (max 2000 chars). |
| `before` | `string` (JSON) | No | `null` | JSON snapshot of resource state before the action. Stored verbatim. |
| `after` | `string` (JSON) | No | `null` | JSON snapshot of resource state after the action. Stored verbatim. |
| `metadata` | `string` (JSON) | No | `null` | Arbitrary JSON context. Stored verbatim. |
| `correlationId` | `string` | No | `null` | Distributed trace correlation ID. |
| `requestId` | `string` | No | `null` | HTTP request identifier from the originating request. |
| `sessionId` | `string` | No | `null` | User session identifier. |
| `visibility` | `VisibilityScope` | No | `Tenant` | Controls who can retrieve this record via the query API. |
| `severity` | `SeverityLevel` | No | `Info` | Operational severity at the time of the event. |
| `occurredAtUtc` | `datetime` | No | Server receipt time | UTC time the event occurred in the source system. |
| `idempotencyKey` | `string` | No | `null` | Source-provided key for deduplication. Strongly recommended. |
| `isReplay` | `boolean` | No | `false` | Set `true` when re-submitting through a replay mechanism. |
| `tags` | `string[]` | No | `null` | String labels for ad-hoc grouping (max 20 tags, max 100 chars each). |

#### AuditEventScopeDto

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `scopeType` | `ScopeType` | No | `Tenant` | Organizational level. Determines which ID fields are required. |
| `platformId` | `string` | No | `null` | Platform partition identifier. |
| `tenantId` | `string` | No | `null` | Tenant boundary. Required when ScopeType is Tenant, Organization, or User. |
| `organizationId` | `string` | No | `null` | Organization within a tenant. Required when ScopeType is Organization. |
| `userId` | `string` | No | `null` | User-level scope ID. Required when ScopeType is User. |

#### AuditEventActorDto

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `id` | `string` | No | `null` | Stable identifier of the actor. |
| `type` | `ActorType` | No | `User` | Kind of principal. |
| `name` | `string` | No | `null` | Display name at the time of the event (snapshot). |
| `ipAddress` | `string` | No | `null` | Client IP address (IPv4 or IPv6). |
| `userAgent` | `string` | No | `null` | HTTP User-Agent string. |

#### AuditEventEntityDto

| Field | Type | Required | Description |
|---|---|---|---|
| `type` | `string` | No | Resource type (PascalCase). E.g. `"User"`, `"Document"`. |
| `id` | `string` | No | Resource identifier within the source system. |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `201 Created` | Event accepted and persisted | `ApiResponse<IngestItemResult>` |
| `400 Bad Request` | Structural validation failed | `ApiResponse<object>` with `errors` |
| `409 Conflict` | Duplicate `IdempotencyKey` | `ApiResponse<object>` |
| `422 Unprocessable Entity` | Event rejected for a non-validation reason | `ApiResponse<object>` |
| `503 Service Unavailable` | Transient persistence failure | `ApiResponse<object>` |

**201 Created** also returns a `Location` header. Note: the Location URL is informational; a dedicated `GET /internal/audit/events/{auditId}` route is not exposed. Use `GET /audit/events/{auditId}` on the query API to retrieve persisted records.

#### IngestItemResult

| Field | Type | Nullable | Description |
|---|---|---|---|
| `index` | `integer` | No | Zero-based position in the batch (always `0` for single ingest) |
| `eventType` | `string` | Yes | Echo of the submitted EventType |
| `idempotencyKey` | `string` | Yes | Echo of the submitted IdempotencyKey |
| `accepted` | `boolean` | No | `true` if the item was persisted |
| `auditId` | `guid` | Yes | Platform-assigned AuditId. Null when `accepted` is `false`. |
| `rejectionReason` | `string` | Yes | Machine-readable code: `"ValidationFailed"`, `"DuplicateIdempotencyKey"`, `"PersistenceError"`, `"Skipped"` |
| `validationErrors` | `string[]` | No | Field-level errors when `rejectionReason` is `"ValidationFailed"` |

---

### POST `/internal/audit/events/batch`

Ingest a batch of audit events in a single request.

**Batch processing semantics:**

- **Default** (`stopOnFirstError = false`): all events are validated and attempted independently. Partial acceptance is possible.
- **`stopOnFirstError = true`**: processing halts after the first rejected item. Remaining items are marked `Skipped`.

**Batch size:** 1–500 events. Recommended ≤ 100 per batch for optimal throughput.

**Request Body: `BatchIngestRequest`**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `events` | `IngestAuditEventRequest[]` | Yes | — | Events to ingest (1–500 items). See single ingest DTO above. |
| `batchCorrelationId` | `string` | No | `null` | Correlation ID for the entire batch. Used as fallback for items without their own. |
| `stopOnFirstError` | `boolean` | No | `false` | When `true`, halt after the first item failure. |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `200 OK` | All events accepted | `ApiResponse<BatchIngestResponse>` |
| `207 Multi-Status` | Partial success — some accepted, some rejected | `ApiResponse<BatchIngestResponse>` |
| `400 Bad Request` | Batch-level structural validation failed | `ApiResponse<object>` with `errors` |
| `422 Unprocessable Entity` | All events rejected | `ApiResponse<BatchIngestResponse>` |

#### BatchIngestResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `submitted` | `integer` | No | Total number of events submitted |
| `accepted` | `integer` | No | Number of events accepted and persisted |
| `rejected` | `integer` | No | Number of events rejected or skipped |
| `hasErrors` | `boolean` | No | `true` when any item was rejected or skipped |
| `results` | `IngestItemResult[]` | No | Per-item results in submission order |
| `batchCorrelationId` | `string` | Yes | Echo of `batchCorrelationId` from the request |

---

## Event Query Endpoints

Base path: `/audit`

All query endpoints are subject to `QueryAuthMiddleware` scope resolution. The caller's scope constrains the result set — callers without PlatformAdmin scope are restricted to their own tenant's records. Multiple filters are AND-ed together.

All query endpoints accept the same filter parameters via query string and return `ApiResponse<AuditEventQueryResponse>`.

### Query Parameters: `AuditEventQueryRequest`

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `tenantId` | `string` | No | `null` | Restrict to a specific tenant. May be overridden by middleware. |
| `organizationId` | `string` | No | `null` | Restrict to a specific organization within the tenant. |
| `category` | `EventCategory` | No | `null` | Filter by event category. |
| `minSeverity` | `SeverityLevel` | No | `null` | Return events at or above this severity. |
| `maxSeverity` | `SeverityLevel` | No | `null` | Return events at or below this severity. |
| `eventTypes` | `string[]` | No | `null` | Filter to specific event type codes (OR-ed). |
| `sourceSystem` | `string` | No | `null` | Filter by source system name (exact match). |
| `sourceService` | `string` | No | `null` | Filter by source service name (exact match). |
| `actorId` | `string` | No | `null` | Filter to events by a specific actor. |
| `actorType` | `ActorType` | No | `null` | Filter to a specific actor type. |
| `entityType` | `string` | No | `null` | Filter to events targeting a specific resource type. |
| `entityId` | `string` | No | `null` | Filter to events targeting a specific resource ID. |
| `correlationId` | `string` | No | `null` | Filter by distributed trace correlation ID. |
| `sessionId` | `string` | No | `null` | Filter by session ID. |
| `requestId` | `string` | No | `null` | Filter by HTTP request ID. |
| `from` | `datetime` | No | `null` | Events that occurred at or after this UTC timestamp (inclusive). |
| `to` | `datetime` | No | `null` | Events that occurred before this UTC timestamp (exclusive). |
| `sourceEnvironment` | `string` | No | `null` | Filter by environment label (e.g. `"production"`). |
| `maxVisibility` | `VisibilityScope` | No | `null` | Restrict to records at or below this visibility level. |
| `visibility` | `VisibilityScope` | No | `null` | Exact visibility scope filter. Takes precedence over `maxVisibility`. |
| `descriptionContains` | `string` | No | `null` | Case-insensitive substring search in Description. |
| `page` | `integer` | No | `1` | 1-based page number. |
| `pageSize` | `integer` | No | `50` | Records per page (capped at 500). |
| `sortBy` | `string` | No | `"occurredAtUtc"` | Sort field: `"occurredAtUtc"`, `"recordedAtUtc"`, or `"severity"`. |
| `sortDescending` | `boolean` | No | `true` | `true` for newest-first ordering. |

---

### GET `/audit/events`

Execute a filtered, paginated query over all accessible audit event records.

**Responses:**

| Status | Condition |
|---|---|
| `200 OK` | Query succeeded. Body: `ApiResponse<AuditEventQueryResponse>` |
| `400 Bad Request` | Invalid query parameters |
| `401 Unauthorized` | No valid credentials presented |
| `403 Forbidden` | Caller's scope is insufficient |

---

### GET `/audit/events/{auditId}`

Retrieve a single audit event record by its stable public identifier.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `auditId` | `guid` | Platform-assigned AuditId (UUID) |

**Responses:**

| Status | Condition |
|---|---|
| `200 OK` | Record found. Body: `ApiResponse<AuditEventRecordResponse>` |
| `401 Unauthorized` | No valid credentials presented |
| `403 Forbidden` | Caller's scope is insufficient |
| `404 Not Found` | No record exists with the given AuditId |

---

### GET `/audit/entity/{entityType}/{entityId}`

Retrieve all audit events that targeted a specific resource. Path segments are applied as exact-match filters and override any corresponding query-string values.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `entityType` | `string` | Resource type (e.g. `"User"`, `"Document"`) |
| `entityId` | `string` | Resource identifier |

**Query Parameters:** All standard query parameters (see above).

**Responses:**

| Status | Condition |
|---|---|
| `200 OK` | Query succeeded. Body: `ApiResponse<AuditEventQueryResponse>` |
| `400 Bad Request` | Invalid query parameters |
| `401 Unauthorized` | No valid credentials presented |
| `403 Forbidden` | Caller's scope is insufficient |

---

### GET `/audit/actor/{actorId}`

Retrieve all audit events performed by a specific actor.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `actorId` | `string` | Stable actor identifier |

**Query Parameters:** All standard query parameters (see above). The `actorId` query parameter is overridden by the path segment.

**Responses:**

| Status | Condition |
|---|---|
| `200 OK` | Query succeeded. Body: `ApiResponse<AuditEventQueryResponse>` |
| `400 Bad Request` | Invalid query parameters |
| `401 Unauthorized` | No valid credentials presented |
| `403 Forbidden` | Caller's scope is insufficient |

---

### GET `/audit/user/{userId}`

Retrieve all audit events associated with a specific user. `actorType = User` is enforced server-side.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `userId` | `string` | User's stable identifier |

**Query Parameters:** All standard query parameters (see above). `actorId` and `actorType` are set from the path.

**Responses:**

| Status | Condition |
|---|---|
| `200 OK` | Query succeeded. Body: `ApiResponse<AuditEventQueryResponse>` |
| `400 Bad Request` | Invalid query parameters |
| `401 Unauthorized` | No valid credentials presented |
| `403 Forbidden` | Caller's scope is insufficient |

---

### GET `/audit/tenant/{tenantId}`

Retrieve all audit events scoped to a specific tenant. For non-PlatformAdmin callers, the authorizer overrides this to the caller's own tenant.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `tenantId` | `string` | Tenant identifier |

**Query Parameters:** All standard query parameters (see above). `tenantId` is set from the path.

**Responses:**

| Status | Condition |
|---|---|
| `200 OK` | Query succeeded. Body: `ApiResponse<AuditEventQueryResponse>` |
| `400 Bad Request` | Invalid query parameters |
| `401 Unauthorized` | No valid credentials presented |
| `403 Forbidden` | Caller's scope is insufficient or tenant mismatch |

---

### GET `/audit/organization/{organizationId}`

Retrieve all audit events scoped to a specific organization.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `organizationId` | `string` | Organization identifier |

**Query Parameters:** All standard query parameters (see above). `organizationId` is set from the path.

**Responses:**

| Status | Condition |
|---|---|
| `200 OK` | Query succeeded. Body: `ApiResponse<AuditEventQueryResponse>` |
| `400 Bad Request` | Invalid query parameters |
| `401 Unauthorized` | No valid credentials presented |
| `403 Forbidden` | Caller's scope is insufficient |

---

### AuditEventRecordResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `auditId` | `guid` | No | Platform-assigned stable public identifier |
| `eventId` | `guid` | Yes | Source-assigned domain event ID, if provided at ingest |
| `eventType` | `string` | No | Dot-notation event code |
| `eventCategory` | `EventCategory` | No | Broad domain classification |
| `sourceSystem` | `string` | No | Logical name of the producing system |
| `sourceService` | `string` | Yes | Sub-component within the source system |
| `sourceEnvironment` | `string` | Yes | Deployment environment label |
| `scope` | `AuditEventScopeResponseDto` | No | Tenancy and organizational scope context (see below) |
| `actor` | `AuditEventActorResponseDto` | No | Actor identity context (see below) |
| `entity` | `AuditEventEntityResponseDto` | Yes | Target resource, if applicable (see below) |
| `action` | `string` | No | Verb describing what was done |
| `description` | `string` | No | Human-readable summary |
| `before` | `string` (JSON) | Yes | Resource state before the action |
| `after` | `string` (JSON) | Yes | Resource state after the action |
| `metadata` | `string` (JSON) | Yes | Additional event context |
| `correlationId` | `string` | Yes | Distributed trace correlation ID |
| `requestId` | `string` | Yes | HTTP request identifier |
| `sessionId` | `string` | Yes | User session identifier |
| `visibility` | `VisibilityScope` | No | Visibility scope of this record |
| `severity` | `SeverityLevel` | No | Operational severity |
| `occurredAtUtc` | `datetime` | No | UTC time the event occurred |
| `recordedAtUtc` | `datetime` | No | UTC time the event was persisted |
| `hash` | `string` | Yes | HMAC-SHA256 integrity hash. Only populated when `ExposeIntegrityHash=true`. |
| `isReplay` | `boolean` | No | `true` if created by a replay mechanism |
| `tags` | `string[]` | No | Tag labels (empty list when none) |

#### AuditEventScopeResponseDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `scopeType` | `ScopeType` | No | Organizational level |
| `platformId` | `string` | Yes | Platform partition identifier |
| `tenantId` | `string` | Yes | Tenant boundary |
| `organizationId` | `string` | Yes | Organization within tenant |
| `userScopeId` | `string` | Yes | User-level scope ID. May differ from Actor.Id in impersonation scenarios. |

#### AuditEventActorResponseDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `string` | Yes | Stable actor identifier |
| `type` | `ActorType` | No | Kind of principal |
| `name` | `string` | Yes | Display name at the time of event |
| `ipAddress` | `string` | Yes | Client IP. Redacted to `null` for User-scoped callers. |
| `userAgent` | `string` | Yes | HTTP User-Agent. Redacted for insufficient caller roles. |

#### AuditEventEntityResponseDto

| Field | Type | Nullable | Description |
|---|---|---|---|
| `type` | `string` | Yes | Resource type |
| `id` | `string` | Yes | Resource identifier |

---

## Export Endpoints

Base path: `/audit`

Export endpoints create and monitor asynchronous audit data export jobs. Subject to QueryAuthMiddleware scope resolution.

**Configuration gate:** When `Export:Provider = "None"` (the default), both endpoints return `503 Service Unavailable`. Set `Export:Provider` to `"Local"` (dev), `"S3"`, or `"AzureBlob"` (production) to enable.

---

### POST `/audit/exports`

Submit a new audit data export job.

The caller's authorization scope determines which records are accessible. Cross-tenant access is denied for non-PlatformAdmin callers.

**Request Body: `ExportRequest`**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `scopeType` | `ScopeType` | No | `Tenant` | Organizational level the export is bounded to |
| `scopeId` | `string` | No | `null` | Concrete scope ID matching ScopeType. Null for Global/Platform. |
| `category` | `EventCategory` | No | `null` | Filter by event category |
| `minSeverity` | `SeverityLevel` | No | `null` | Only export events at or above this severity |
| `eventTypes` | `string[]` | No | `null` | Specific event type codes (OR-ed) |
| `actorId` | `string` | No | `null` | Export events by a specific actor |
| `entityType` | `string` | No | `null` | Export events targeting a specific resource type |
| `entityId` | `string` | No | `null` | Export events targeting a specific resource ID |
| `from` | `datetime` | No | `null` | Events that occurred at or after this UTC timestamp (inclusive) |
| `to` | `datetime` | No | `null` | Events that occurred before this UTC timestamp (exclusive) |
| `correlationId` | `string` | No | `null` | Export events by correlation ID |
| `format` | `string` | No | `"Json"` | Output format: `"Json"`, `"Csv"`, or `"Ndjson"` |
| `includeStateSnapshots` | `boolean` | No | `true` | Include Before/After JSON snapshots |
| `includeTags` | `boolean` | No | `true` | Include Tags list per record |
| `includeHashes` | `boolean` | No | `false` | Include integrity hashes (requires `ExposeIntegrityHash=true` role) |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `202 Accepted` | Job created and processing | `ApiResponse<ExportStatusResponse>` |
| `400 Bad Request` | Validation failure or unsupported format | `ApiResponse<object>` |
| `401 Unauthorized` | No valid credentials presented | `ApiResponse<object>` |
| `403 Forbidden` | Caller's scope is insufficient | `ApiResponse<object>` |
| `503 Service Unavailable` | Export not configured on this instance | `ApiResponse<object>` |

---

### GET `/audit/exports/{exportId}`

Poll the status of an existing export job. Polling is idempotent — repeated calls return the same data once the job reaches a terminal state.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `exportId` | `guid` | Platform-assigned export job identifier |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `200 OK` | Job found | `ApiResponse<ExportStatusResponse>` |
| `404 Not Found` | No job with the given exportId | `ApiResponse<object>` |
| `503 Service Unavailable` | Export not configured on this instance | `ApiResponse<object>` |

#### ExportStatusResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `exportId` | `guid` | No | Platform-assigned export job identifier |
| `scopeType` | `ScopeType` | No | Echo of the scope type from the request |
| `scopeId` | `string` | Yes | Echo of the scope ID from the request |
| `format` | `string` | No | Echo of the requested output format |
| `status` | `ExportStatus` | No | Current lifecycle state |
| `statusLabel` | `string` | No | Human-readable status label |
| `downloadUrl` | `string` | Yes | Download URL or file path when `status = Completed`. Format depends on provider. |
| `recordCount` | `long` | Yes | Number of records in the export file. Null while pending/processing. |
| `errorMessage` | `string` | Yes | Error description when `status = Failed` |
| `createdAtUtc` | `datetime` | No | When the job was submitted |
| `completedAtUtc` | `datetime` | Yes | When the job reached a terminal state |
| `isTerminal` | `boolean` | No | `true` when the job is in a terminal state |
| `isAvailable` | `boolean` | No | `true` when the output file is available for download |

---

## Integrity Checkpoint Endpoints

Base path: `/audit/integrity`

Integrity checkpoints provide tamper-evidence verification for the audit record store. Checkpoints are append-only — existing records are never updated or deleted.

---

### GET `/audit/integrity/checkpoints`

Retrieve a paginated list of persisted integrity checkpoints, newest-first.

**Required scope:** `TenantAdmin` or higher.

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `type` | `string` | No | `null` | Filter by checkpoint type label (e.g. `"hourly"`, `"daily"`, `"manual"`) |
| `from` | `datetime` | No | `null` | Checkpoints created at or after this timestamp (inclusive) |
| `to` | `datetime` | No | `null` | Checkpoints created at or before this timestamp (inclusive) |
| `page` | `integer` | No | `1` | 1-based page number |
| `pageSize` | `integer` | No | `20` | Records per page (capped at 200) |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `200 OK` | List returned successfully | `ApiResponse<PagedResult<IntegrityCheckpointResponse>>` |
| `401 Unauthorized` | No valid credentials presented | `ApiResponse<object>` |
| `403 Forbidden` | Caller scope is below TenantAdmin | `ApiResponse<object>` |

---

### POST `/audit/integrity/checkpoints/generate`

Generate a new integrity checkpoint on demand over a specified time window.

The service streams all audit event record hashes whose `RecordedAtUtc` falls within `[fromRecordedAtUtc, toRecordedAtUtc)`, concatenates them in ascending insertion-order, and computes an aggregate HMAC-SHA256 hash. The result is persisted as a new checkpoint record.

**Required scope:** `PlatformAdmin` only.

**Request Body: `GenerateCheckpointRequest`**

| Field | Type | Required | Description |
|---|---|---|---|
| `checkpointType` | `string` | Yes | Cadence/trigger label (1–64 chars). E.g. `"hourly"`, `"daily"`, `"manual"`, `"pre-audit-2026-Q1"` |
| `fromRecordedAtUtc` | `datetime` | Yes | Inclusive start of the time window |
| `toRecordedAtUtc` | `datetime` | Yes | Exclusive end of the time window. Must be strictly after `fromRecordedAtUtc`. |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `201 Created` | Checkpoint generated and persisted | `ApiResponse<IntegrityCheckpointResponse>` |
| `400 Bad Request` | Invalid request (e.g. inverted time window) | `ApiResponse<object>` |
| `401 Unauthorized` | No valid credentials presented | `ApiResponse<object>` |
| `403 Forbidden` | Caller scope is below PlatformAdmin | `ApiResponse<object>` |

#### IntegrityCheckpointResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `long` | No | Internal checkpoint identifier |
| `checkpointType` | `string` | No | Cadence or trigger label |
| `fromRecordedAtUtc` | `datetime` | No | Start of the covered time window (inclusive) |
| `toRecordedAtUtc` | `datetime` | No | End of the covered time window (exclusive) |
| `aggregateHash` | `string` | No | HMAC-SHA256 over the ordered concatenation of record hashes |
| `recordCount` | `long` | No | Number of records included in the hash computation |
| `isValid` | `boolean` | Yes | Most recent verification result. `null` if never verified. `true` = match, `false` = tamper evidence. |
| `lastVerifiedAtUtc` | `datetime` | Yes | Timestamp of the most recent verification run |
| `createdAtUtc` | `datetime` | No | When this checkpoint was created |

---

## Legal Hold Endpoints

Base path: `/audit/legal-holds`

Legal holds prevent the retention pipeline from archiving or deleting held audit event records. Intended for use by compliance officers and legal staff.

**Authorization:** Intended for PlatformAdmin and TenantAdmin-level callers (the `compliance-officer` role maps to TenantAdmin scope via `QueryAuthOptions.TenantAdminRoles`). In `Mode=None` (dev), all callers have PlatformAdmin scope by default.

---

### POST `/audit/legal-holds/{auditId}`

Place a legal hold on an audit event record.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `auditId` | `guid` | AuditId of the record to hold |

**Request Body: `CreateLegalHoldRequest`**

| Field | Type | Required | Max Length | Description |
|---|---|---|---|---|
| `legalAuthority` | `string` | Yes | 512 | Canonical legal authority reference. E.g. `"litigation-hold-2026-001"`, `"HIPAA-audit-2026"` |
| `notes` | `string` | No | 2000 | Free-text notes about the hold |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `201 Created` | Hold created successfully | `ApiResponse<LegalHoldResponse>` |
| `400 Bad Request` | Invalid request | `ApiResponse<object>` |
| `401 Unauthorized` | No valid credentials presented | `ApiResponse<object>` |
| `403 Forbidden` | Caller lacks sufficient scope | `ApiResponse<object>` |
| `404 Not Found` | Audit record not found | `ApiResponse<object>` |

---

### POST `/audit/legal-holds/{holdId}/release`

Release an active legal hold. After release, the record becomes eligible for the normal retention lifecycle.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `holdId` | `guid` | HoldId of the hold to release |

**Request Body: `ReleaseLegalHoldRequest`**

| Field | Type | Required | Max Length | Description |
|---|---|---|---|---|
| `releaseNotes` | `string` | No | 2000 | Notes explaining the reason for release |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `200 OK` | Hold released successfully | `ApiResponse<LegalHoldResponse>` |
| `401 Unauthorized` | No valid credentials presented | `ApiResponse<object>` |
| `403 Forbidden` | Caller lacks sufficient scope | `ApiResponse<object>` |
| `404 Not Found` | Hold not found | `ApiResponse<object>` |
| `409 Conflict` | Hold is already released | `ApiResponse<object>` |

---

### GET `/audit/legal-holds/record/{auditId}`

List all holds (active and released) for an audit event record.

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `auditId` | `guid` | AuditId of the record |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `200 OK` | List of holds returned (may be empty) | `ApiResponse<LegalHoldResponse[]>` |
| `401 Unauthorized` | No valid credentials presented | `ApiResponse<object>` |
| `403 Forbidden` | Caller lacks sufficient scope | `ApiResponse<object>` |

#### LegalHoldResponse

| Field | Type | Nullable | Description |
|---|---|---|---|
| `holdId` | `guid` | No | Unique hold identifier |
| `auditId` | `guid` | No | AuditId of the held record |
| `heldByUserId` | `string` | No | Identity of the user who created the hold |
| `heldAtUtc` | `datetime` | No | When the hold was created |
| `releasedAtUtc` | `datetime` | Yes | When the hold was released. Null while active. |
| `releasedByUserId` | `string` | Yes | Identity of the user who released the hold |
| `legalAuthority` | `string` | No | Legal authority reference |
| `notes` | `string` | Yes | Hold notes |
| `isActive` | `boolean` | No | `true` when `releasedAtUtc` is null |

---

## Legacy / Deprecated Endpoints

> **⚠ DEPRECATED — Sunset date: 2026-06-30**
>
> This controller writes to the legacy `AuditEvents` flat table.
> All new integrations must use the canonical endpoints:
> - Ingest: `POST /internal/audit/events`
> - Query: `GET /audit/events`
>
> These endpoints will be **removed** in the next major release.
> All responses include `Sunset: 2026-06-30` and `Link: </internal/audit/events>` deprecation headers.

Base path: `/api/AuditEvents`

---

### POST `/api/AuditEvents`

Ingest a single audit event record (legacy).

**Request Body: `IngestAuditEventRequest` (legacy)**

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `source` | `string` | Yes | — | Source system name |
| `eventType` | `string` | Yes | — | Event type code |
| `category` | `string` | Yes | — | Event category (string) |
| `severity` | `string` | No | `"INFO"` | Severity level (string) |
| `tenantId` | `string` | No | `null` | Tenant identifier |
| `actorId` | `string` | No | `null` | Actor identifier |
| `actorLabel` | `string` | No | `null` | Actor display name |
| `targetType` | `string` | No | `null` | Target resource type |
| `targetId` | `string` | No | `null` | Target resource identifier |
| `description` | `string` | Yes | — | Human-readable event description |
| `outcome` | `string` | No | `"SUCCESS"` | Operation outcome |
| `ipAddress` | `string` | No | `null` | Client IP address |
| `userAgent` | `string` | No | `null` | HTTP User-Agent |
| `correlationId` | `string` | No | `null` | Correlation ID |
| `metadata` | `string` (JSON) | No | `null` | Arbitrary JSON context |
| `occurredAtUtc` | `datetime` | No | Server receipt time | UTC time the event occurred |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `201 Created` | Event persisted | `ApiResponse<AuditEventResponse>` (legacy) |
| `400 Bad Request` | Validation failed | `ApiResponse<object>` with `errors` |

---

### GET `/api/AuditEvents/{id}`

Retrieve a single audit event by its unique identifier (legacy).

**Path Parameters:**

| Parameter | Type | Description |
|---|---|---|
| `id` | `guid` | Unique identifier |

**Responses:**

| Status | Condition | Body |
|---|---|---|
| `200 OK` | Event found | `ApiResponse<AuditEventResponse>` (legacy) |
| `404 Not Found` | Event not found | `ApiResponse<object>` |

---

### GET `/api/AuditEvents`

Query audit events with optional filters (legacy).

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `source` | `string` | No | `null` | Filter by source system |
| `eventType` | `string` | No | `null` | Filter by event type |
| `category` | `string` | No | `null` | Filter by category |
| `severity` | `string` | No | `null` | Filter by severity |
| `tenantId` | `string` | No | `null` | Filter by tenant |
| `actorId` | `string` | No | `null` | Filter by actor |
| `targetType` | `string` | No | `null` | Filter by target resource type |
| `targetId` | `string` | No | `null` | Filter by target resource ID |
| `outcome` | `string` | No | `null` | Filter by outcome |
| `from` | `datetime` | No | `null` | Events at or after this UTC timestamp |
| `to` | `datetime` | No | `null` | Events before this UTC timestamp |
| `page` | `integer` | No | `1` | Page number |
| `pageSize` | `integer` | No | `50` | Items per page |

**Response:** `200 OK` — `ApiResponse<PagedResult<AuditEventResponse>>`

#### AuditEventResponse (legacy)

| Field | Type | Nullable | Description |
|---|---|---|---|
| `id` | `guid` | No | Unique identifier |
| `source` | `string` | No | Source system name |
| `eventType` | `string` | No | Event type code |
| `category` | `string` | No | Event category |
| `severity` | `string` | No | Severity level |
| `tenantId` | `string` | Yes | Tenant identifier |
| `actorId` | `string` | Yes | Actor identifier |
| `actorLabel` | `string` | Yes | Actor display name |
| `targetType` | `string` | Yes | Target resource type |
| `targetId` | `string` | Yes | Target resource ID |
| `description` | `string` | No | Event description |
| `outcome` | `string` | No | Operation outcome |
| `ipAddress` | `string` | Yes | Client IP address |
| `correlationId` | `string` | Yes | Correlation ID |
| `metadata` | `string` (JSON) | Yes | Additional context |
| `occurredAtUtc` | `datetime` | No | When the event occurred |
| `ingestedAtUtc` | `datetime` | No | When the event was ingested |
| `integrityHash` | `string` | Yes | Integrity hash |
