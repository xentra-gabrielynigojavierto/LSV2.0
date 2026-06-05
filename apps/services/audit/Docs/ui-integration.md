# UI Integration Guide

**Service**: Platform Audit Event Service  
**Audience**: Frontend and BFF (Backend-for-Frontend) developers  
**Version**: 1.0.0  
**Base path**: `/audit`

---

## Overview

The Platform Audit Event Service exposes a set of read-only query endpoints that power audit log interfaces at every level of the organizational hierarchy. This guide describes how to build UI views for each interface tier, which endpoints to call, which filters to apply, how visibility and authorization interact with what the caller can see, and how to structure timeline and table views.

There is no frontend-framework-specific code in this guide. All patterns are expressed in terms of HTTP calls, response shapes, and UI behaviour that translate to any stack.

---

## Authorization Background

Every query is authorized server-side. The service resolves the caller's scope from their JWT claims and silently enforces constraints on the query before it executes. UI developers do not need to implement scope enforcement — but they do need to understand what the server will and will not return for each caller tier.

| Caller scope | How it is granted | What the server enforces automatically |
|---|---|---|
| `PlatformAdmin` | Role: `platform-audit-admin` | No tenant restriction; sees all visibility levels including `Platform` |
| `TenantAdmin` | Role: `tenant-admin` or `compliance-officer` | Own tenant only; sees `Tenant` and below |
| `OrganizationAdmin` | Role: `org-admin` or `department-admin` | Own org within own tenant; sees `Organization` and below |
| `Restricted` | Role: `compliance-reader` or `auditor` | Own tenant, read-only; sees `Tenant` and below |
| `TenantUser` | Role: `tenant-user` or `user` | Own tenant; sees `User`-scope records only |
| `UserSelf` | Role: `self-reader` | Own records only (`actorId` locked to caller's user ID); sees `User`-scope records only |

**Key implication for UI**: never filter out records based on tenant or visibility in the client. The server always returns only what the caller is permitted to see. Attempts to pass a different `tenantId` than the caller's own are silently corrected by the server; they do not produce an error and do not expose other tenants' data.

`Internal`-scope records are never returned regardless of caller scope. They exist only for service-internal diagnostics.

---

## Endpoint Reference

All endpoints are GET requests under the `/audit` prefix and return a paginated `AuditEventQueryResponse` (except the single-record endpoint, which returns `AuditEventRecordResponse`).

| Endpoint | Path | Primary use |
|---|---|---|
| Filtered list | `GET /audit/events` | General-purpose filtered, paginated list. Used by all tiers. |
| Single record | `GET /audit/events/{auditId}` | Detail view for a specific record by its stable `AuditId` UUID. |
| By entity | `GET /audit/entity/{entityType}/{entityId}` | History of all events that targeted a specific resource (record timeline). |
| By actor | `GET /audit/actor/{actorId}` | All events attributed to a specific actor (any type). |
| By user | `GET /audit/user/{userId}` | All events performed by a specific human user (enforces `actorType=User`). |
| By tenant | `GET /audit/tenant/{tenantId}` | All events scoped to a tenant. Non-`PlatformAdmin` callers are constrained to their own tenant. |
| By organization | `GET /audit/organization/{organizationId}` | All events scoped to a specific organization within the caller's tenant. |
| Export (submit) | `POST /audit/exports` | Submit an async export job. Returns an `ExportId`. |
| Export (status) | `GET /audit/exports/{exportId}` | Poll the status of a previously submitted export job. |

---

## Filter Parameters

All list endpoints accept these query string filters. Filters are AND-ed together; omitting a filter removes that constraint.

### Scope Filters

| Parameter | Type | Description |
|---|---|---|
| `tenantId` | `string` | Restrict to a specific tenant. Ignored (overridden) for non-`PlatformAdmin` callers. |
| `organizationId` | `string` | Restrict to a specific organization within the tenant. Overridden for `OrganizationAdmin` callers. |

### Classification Filters

| Parameter | Type | Description |
|---|---|---|
| `category` | `EventCategory` enum | Filter by event category. See [Categories](#event-categories). |
| `minSeverity` | `SeverityLevel` enum | Return only events at or above this severity. |
| `maxSeverity` | `SeverityLevel` enum | Return only events at or below this severity. |
| `eventTypes` | `string[]` | One or more exact `eventType` dot-codes to match (OR logic). Supply as repeated query string parameters or comma-separated. |
| `sourceSystem` | `string` | Exact match on the producing service system name. |
| `sourceService` | `string` | Exact match on the producing service sub-component name. |

### Actor Filters

| Parameter | Type | Description |
|---|---|---|
| `actorId` | `string` | Events attributed to this actor. Overridden for `UserSelf` callers. |
| `actorType` | `ActorType` enum | Filter by actor type (e.g. `User`, `ServiceAccount`, `System`). |

### Entity Filters

| Parameter | Type | Description |
|---|---|---|
| `entityType` | `string` | Exact resource type (e.g. `User`, `Document`, `Appointment`). |
| `entityId` | `string` | Exact resource identifier. Combine with `entityType` for a precise resource lookup. |

### Correlation / Trace Filters

| Parameter | Type | Description |
|---|---|---|
| `correlationId` | `string` | Return all events that share a distributed trace correlation ID. |
| `sessionId` | `string` | Return all events that share a session identifier. |
| `requestId` | `string` | Return all events from a specific originating HTTP request. |

### Time Range

| Parameter | Type | Description |
|---|---|---|
| `from` | `DateTimeOffset` (ISO 8601 UTC) | Inclusive lower bound. |
| `to` | `DateTimeOffset` (ISO 8601 UTC) | Exclusive upper bound. |

### Text Search

| Parameter | Type | Description |
|---|---|---|
| `descriptionContains` | `string` | Case-insensitive substring search within the `description` field. Expensive on large datasets — always combine with a `from`/`to` time range. |

### Visibility

| Parameter | Type | Description |
|---|---|---|
| `maxVisibility` | `VisibilityScope` enum | The server uses this to restrict results further. Normally set by the authorization layer automatically. Do not override unless you have a specific UI reason to show a subset. |
| `visibility` | `VisibilityScope` enum | Exact visibility match. Takes precedence over `maxVisibility` when both are set. |

### Pagination and Sorting

| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | `int` | `1` | 1-based page number. |
| `pageSize` | `int` | `50` | Records per page. Server-enforced maximum is 500. |
| `sortBy` | `string` | `occurredAtUtc` | Sort field. Accepted values: `occurredAtUtc`, `recordedAtUtc`, `severity`. |
| `sortDescending` | `bool` | `true` | Newest-first when `true`. |

---

## Response Shape

### Paginated List Response

```json
{
  "data": {
    "items": [ ...AuditEventRecord... ],
    "totalCount": 2847,
    "page": 1,
    "pageSize": 50,
    "totalPages": 57,
    "hasNext": true,
    "hasPrev": false,
    "earliestOccurredAtUtc": "2026-01-01T00:00:00Z",
    "latestOccurredAtUtc":   "2026-03-30T15:00:00Z"
  },
  "traceId": "00-abc..."
}
```

`earliestOccurredAtUtc` and `latestOccurredAtUtc` reflect the full matching result set, not just the current page. Use these to render a time-range bar or timeline scrubber alongside a table view.

### Single Record Response

Each record in `items` (and the single-record endpoint) has this shape:

```json
{
  "auditId":          "550e8400-e29b-41d4-a716-446655440000",
  "eventId":          null,
  "eventType":        "user.login.succeeded",
  "eventCategory":    "Security",
  "sourceSystem":     "identity-service",
  "sourceService":    "auth-api",
  "sourceEnvironment": "production",
  "scope": {
    "scopeType":      "Tenant",
    "tenantId":       "tenant-abc-123",
    "organizationId": null,
    "userId":         null
  },
  "actor": {
    "type":      "User",
    "id":        "user-789",
    "name":      "Jane Doe",
    "ipAddress": "203.0.113.1",
    "userAgent": "Mozilla/5.0 ..."
  },
  "entity": null,
  "action":        "LoginSucceeded",
  "description":   "User Jane Doe authenticated successfully.",
  "before":        null,
  "after":         null,
  "metadata":      null,
  "correlationId": "trace-abc-001",
  "requestId":     "req-001",
  "sessionId":     "sess-456",
  "visibility":    "Tenant",
  "severity":      "Info",
  "occurredAtUtc": "2026-03-30T14:00:00Z",
  "recordedAtUtc": "2026-03-30T14:00:00.412Z",
  "hash":          null,
  "isReplay":      false,
  "tags":          ["auth", "login"]
}
```

**Field notes for UI**:

| Field | UI usage |
|---|---|
| `auditId` | Stable deep-link ID. Use this to construct permalink URLs to a record detail view. |
| `eventType` | Primary display identifier. Show in a monospaced or badge style. |
| `eventCategory` | Category badge / filter chip. Use to colorize rows. |
| `severity` | Severity badge. Use color coding (see [Severity Display](#severity-display-guidance)). |
| `actor.name` | Show as the human-readable "who". Fall back to `actor.id` when `name` is null (machine actors). |
| `actor.ipAddress` | Redacted for callers below `TenantAdmin` scope. Show only when present. |
| `entity` | Show as a resource link when present. Null for non-resource events (login, startup). |
| `description` | Human-readable summary. Primary text for a timeline card or table cell. |
| `occurredAtUtc` | Display as the event timestamp. Show in the user's local timezone. |
| `recordedAtUtc` | The time the service persisted the record. Show in a detail panel; generally not in list rows. |
| `before` / `after` | Raw JSON strings. Show as formatted JSON diff in a detail panel for `DataChange` events. |
| `metadata` | Raw JSON. Show as formatted JSON in a collapsible "Additional context" section. |
| `correlationId` | Show in detail view as a trace link. Clicking it can filter the list to the same `correlationId`. |
| `hash` | Only non-null when `ExposeIntegrityHash=true` and caller is `PlatformAdmin`. Show in detail view for integrity verification tools. |
| `isReplay` | Show a "Replay" badge when `true`. Indicates a historically submitted record. |
| `tags` | Show as a tag list. Useful as clickable filter chips. |

---

## Interface Tier Guidance

### Platform Admin Interface

**Scope**: `PlatformAdmin` — cross-tenant, all visibility levels including `Platform`.

**Use cases**: cross-tenant security investigation, platform health monitoring, global compliance views, per-tenant drill-down.

**Primary endpoint**: `GET /audit/events`

**Recommended starting filters**:

```
GET /audit/events
  ?minSeverity=Warn
  &from=<24h ago>
  &sortBy=occurredAtUtc
  &sortDescending=true
  &pageSize=50
```

**Key filter scenarios**:

| View | Filters to apply |
|---|---|
| Cross-tenant security overview | `category=Security`, `minSeverity=Warn`, time range |
| All activity for a specific tenant | `tenantId={id}`, time range |
| Platform-level system events | `category=System`, `visibility=Platform` |
| Failed logins across platform | `eventTypes=user.login.failed`, time range |
| Activity by a specific source service | `sourceSystem=identity-service`, `sourceService=auth-api` |
| Follow a distributed trace | `correlationId={traceId}` |
| Look up a specific record | `GET /audit/events/{auditId}` |

**Recommended table columns**: Timestamp · Category · Severity · Event Type · Source System · Tenant · Actor · Description

**Notes**:
- The `tenantId` dropdown in a platform admin view is the primary cross-tenant selector. Populate it from the identity service's tenant list, then pass it as a query filter.
- `Platform`-visibility records are only visible to `PlatformAdmin` callers. Do not surface a visibility filter to lower-privilege users — it will have no effect (the server enforces it).
- The integrity `hash` field is only populated when `ExposeIntegrityHash=true` in configuration. Do not show a "Verify integrity" action unless this flag is enabled.

---

### Tenant Admin Interface

**Scope**: `TenantAdmin` or `Restricted` — own tenant only; sees `Tenant` and below visibility.

**Use cases**: compliance officer review, tenant-wide security event monitoring, user activity investigation, export for compliance reports.

**Primary endpoints**: `GET /audit/tenant/{tenantId}` or `GET /audit/events` (server constrains to own tenant automatically)

The two approaches are equivalent from an authorization standpoint. `GET /audit/tenant/{tenantId}` is semantically clearer. `GET /audit/events` is more flexible when additional filters are applied.

**Recommended starting filters**:

```
GET /audit/tenant/{tenantId}
  ?minSeverity=Notice
  &from=<7 days ago>
  &sortDescending=true
  &pageSize=50
```

**Key filter scenarios**:

| View | Filters to apply |
|---|---|
| Security events (auth, access) | `category=Security`, time range |
| Compliance events | `category=Compliance`, time range |
| DataChange events for a specific period | `category=DataChange`, `from`, `to` |
| All activity for a specific user | `GET /audit/user/{userId}` or `actorId={userId}` |
| All events targeting a specific resource | `GET /audit/entity/{entityType}/{entityId}` |
| Events from a specific service | `sourceSystem=care-connect`, time range |
| High-severity events | `minSeverity=Error`, time range |
| Search by description | `descriptionContains=suspended`, `from`, `to` |

**Recommended table columns**: Timestamp · Category · Severity · Event Type · Actor · Organization · Description

**Notes**:
- Do not show a `tenantId` selector on this interface — the server always constrains to the caller's own tenant. A tenantId input is ignored and may confuse users.
- The `Restricted` scope (compliance readers, auditors) has the same access range as `TenantAdmin` but is intended for read-only reviewers. Restrict export actions to users with `TenantAdmin` scope if your RBAC allows you to distinguish between them in the UI.
- `Platform`-visibility records are not returned for this scope. Do not show a visibility filter that includes `Platform`.

---

### Organization Admin Interface

**Scope**: `OrganizationAdmin` — own organization within own tenant; sees `Organization` and below visibility.

**Use cases**: org-level clinical manager reviewing staff activity, department admin reviewing resource access, org-scoped compliance views.

**Primary endpoint**: `GET /audit/organization/{organizationId}`

The server automatically constrains results to the caller's own organization. Passing a different `organizationId` in the path or query string has no effect for this scope.

**Recommended starting filters**:

```
GET /audit/organization/{organizationId}
  ?category=DataChange
  &from=<30 days ago>
  &sortDescending=true
  &pageSize=50
```

**Key filter scenarios**:

| View | Filters to apply |
|---|---|
| Staff activity overview | time range, `actorType=User` |
| PHI access log | `category=Access`, `entityType=PatientRecord`, time range |
| Record changes by a specific clinician | `actorId={clinicianId}`, `category=DataChange` |
| Referral and appointment events | `eventTypes=referral.created,appointment.scheduled` |
| Workflow approvals | `eventTypes=workflow.approved`, time range |
| Resource history for a specific patient record | `GET /audit/entity/PatientRecord/{patientId}` |

**Recommended table columns**: Timestamp · Category · Severity · Event Type · Actor · Resource · Description

**Notes**:
- `Tenant`-visibility records are not returned for `OrganizationAdmin` scope (the visibility floor is `Organization`). Security events like login attempts are not visible at this level — they are scoped to the tenant level by the producing service.
- The entity history view (`GET /audit/entity/{entityType}/{entityId}`) is especially useful here for showing a complete tamper-evident history of a patient record, appointment, or referral.
- For organization-level compliance exports, scope the export to `Organization` with the org's identifier and apply a date range.

---

### Individual User Activity / History Interface

**Scope**: `UserSelf` or `TenantUser` — own records only (`actorId` locked to caller's user ID); sees `User`-scope visibility.

**Use cases**: "My activity" or "My history" page in the patient portal or user settings; GDPR/HIPAA subject access request support; personal account activity view.

**Primary endpoint**: `GET /audit/user/{userId}`

The server enforces `actorType=User` and overrides `actorId` to the caller's own user ID regardless of the path segment value. A user cannot retrieve another user's history through this endpoint.

**Recommended starting filters**:

```
GET /audit/user/{userId}
  ?from=<90 days ago>
  &sortDescending=true
  &pageSize=25
```

**Key filter scenarios**:

| View | Filters to apply |
|---|---|
| Personal login history | `eventTypes=user.login.succeeded,user.login.failed` |
| Personal document access history | `category=Access`, `entityType=Document` |
| Recent activity (last 30 days) | `from=<30 days ago>` |
| Activity from a specific session | `sessionId={sessionId}` |

**Recommended table columns**: Timestamp · Activity · Resource · IP Address

**Notes**:
- `User`-scope records only. Logins, document views, personal account changes. Security events at the tenant scope (e.g. failed login from a different IP) are visible here because `user.login.failed` is produced with `Visibility=Tenant`, but a `UserSelf` caller will not see those — only events the user directly triggered with `actorId` = their own ID.
- `actor.ipAddress` and `actor.userAgent` are redacted at `TenantUser` scope. Only show these columns when the caller holds `TenantAdmin` or above, or in the user's own `UserSelf` view if the platform policy permits it.
- Keep this view simple: a clean chronological list with a description, timestamp, and optionally resource name. Users do not need to see `correlationId`, `requestId`, or raw JSON payloads.
- For a GDPR/HIPAA subject access request export, use `POST /audit/exports` with `scopeType=User`, `scopeId={userId}`, and a broad time range in `csv` format.

---

## Visibility Scope and What Each Interface Can See

This table summarises which visibility levels each interface tier can retrieve. The server enforces this — you do not need to apply client-side filtering.

| `VisibilityScope` | Platform Admin | Tenant Admin / Restricted | Org Admin | Tenant User / UserSelf |
|---|---|---|---|---|
| `Platform` | Yes | No | No | No |
| `Tenant` | Yes | Yes | No | No |
| `Organization` | Yes | Yes | Yes | No |
| `User` | Yes | Yes | Yes | Yes |
| `Internal` | Never (any scope) | — | — | — |

---

## Table View Guidance

### Recommended Default Column Set

A sensible default for most interfaces. Adjust columns per tier as noted.

| Column | Field | Notes |
|---|---|---|
| Timestamp | `occurredAtUtc` | Display in user's local timezone. Show date + time. |
| Category | `eventCategory` | Badge with category-specific color. |
| Severity | `severity` | Colored badge (see [Severity Display](#severity-display-guidance)). |
| Event | `eventType` | Monospace or code-style. Optionally show `action` as a human label beside it. |
| Actor | `actor.name` or `actor.id` | Fall back to type label (e.g. "System") for non-user actors. |
| Resource | `entity.type` + `entity.id` | Show only when non-null. Link to the resource's own detail page when possible. |
| Description | `description` | Primary readable summary. Truncate at ~120 characters in the table; show full text in the detail panel. |

### Sortable Columns

Expose sort controls for:
- **Timestamp** → `sortBy=occurredAtUtc` (default)
- **Severity** → `sortBy=severity`
- **Recorded At** → `sortBy=recordedAtUtc` (useful for identifying processing lag)

Always default to `sortDescending=true` (newest first). Users expecting an audit log almost always want the most recent events at the top.

### Pagination

Use the response fields to build a pagination control:

```
totalCount:  2847
page:        1
pageSize:    50
totalPages:  57
hasNext:     true
hasPrev:     false
```

Recommended page sizes to offer: 25, 50, 100, 250. The server cap is 500. Larger pages increase query latency — prefer smaller pages for live views and larger pages for export-preview or compliance reviewers.

### Row Detail Expansion

Clicking a row should open a detail panel (side sheet, modal, or expanded row). The detail panel should show:

- Full `description`
- All core fields: `auditId`, `eventType`, `eventCategory`, `sourceSystem`, `sourceService`, `sourceEnvironment`
- Actor block: type, id, name, ip address (when present), user agent (when present)
- Scope block: `scopeType`, `tenantId`, `organizationId`
- Entity block (when non-null): type, id
- Correlation links: `correlationId` (clickable to filter by correlation), `requestId`, `sessionId`
- Timestamps: `occurredAtUtc` (local), `recordedAtUtc` (UTC)
- State snapshots: formatted JSON for `before`, `after`, `metadata` — shown in collapsible sections for `DataChange` events
- Tags (when non-empty)
- `isReplay` flag if `true`
- `hash` (integrity) — shown only when the field is non-null; wrap in a "Verify" action if the platform exposes an integrity check UI

A permalink to `GET /audit/events/{auditId}` can be placed in the detail header for shareable deep links.

---

## Timeline View Guidance

A timeline view renders events as chronological cards or entries along a time axis, rather than rows in a table.

### When to use timeline vs table

| Scenario | Recommended view |
|---|---|
| Resource history (`/audit/entity/{type}/{id}`) | Timeline — shows life of a specific record |
| User activity history | Timeline — personal history reads naturally as a feed |
| Cross-resource compliance investigation | Table — needs sortable columns and dense data |
| Cross-tenant platform monitoring | Table — volume is too high for a card layout |
| Correlation trace view | Timeline — shows a single request's journey across services |

### Using `earliestOccurredAtUtc` and `latestOccurredAtUtc`

The paginated response includes `earliestOccurredAtUtc` and `latestOccurredAtUtc` across the full matching result set (not just the current page). Use these to:

- Render a time-range scrubber or brush control above the timeline
- Display "Showing events from X to Y" as a context label
- Pre-populate `from` and `to` inputs in the filter bar when no date range is currently set

### Timeline Card Fields

For a timeline card, show at minimum:

```
[Category badge] [Severity badge]   [occurredAtUtc — local time]
[actor.name or actor.id]  →  [action or eventType]
[description (truncated)]
[entity.type: entity.id]  (when present)
[tags]                     (when present)
```

Color the left border or card background using `eventCategory` or `severity` for quick visual scanning.

### Grouping by Date

For personal history and resource history views, group timeline cards by local date:

```
March 30, 2026
─ 15:00  Workflow Approved       wf-prior-auth-20260330-001
─ 14:30  Document Viewed         BAA Agreement 2026
─ 14:00  Login Succeeded         —

March 29, 2026
─ 11:00  Role Assigned           user-789 → ClinicalManager
```

### Correlation Trace View

When a user clicks a `correlationId` value anywhere in the UI, navigate to a timeline filtered by that correlation ID:

```
GET /audit/events?correlationId={traceId}&sortBy=occurredAtUtc&sortDescending=false
```

This reconstructs the end-to-end journey of a single user action across all services that recorded audit events for it. Sort ascending (`sortDescending=false`) so the trace reads in chronological order from the originating event.

---

## Severity Display Guidance

Use consistent color conventions across all interfaces. Suggested mapping:

| Severity | Badge color | Usage |
|---|---|---|
| `Debug` | Grey | Not returned in production; dev/trace only |
| `Info` | Blue | Normal operations — default color |
| `Notice` | Teal / Cyan | Significant events — slightly elevated |
| `Warn` | Amber / Orange | Recoverable failures — attention warranted |
| `Error` | Red | Operation failed — immediate review |
| `Critical` | Dark red | Severe failure — urgent |
| `Alert` | Red-purple | System failure — critical escalation |

In list views with many rows, apply the severity color only to the badge, not to the entire row background — full-row coloring makes dense tables visually overwhelming.

Use `minSeverity=Warn` as the default filter for security and operations dashboards. This shows events worth reviewing without flooding the view with `Info` entries.

---

## Event Category Display Guidance

Suggested badge colors for categories:

| Category | Suggested color |
|---|---|
| `Security` | Red-orange |
| `Access` | Blue |
| `Business` | Green |
| `Administrative` | Purple |
| `System` | Grey |
| `Compliance` | Teal |
| `DataChange` | Amber |
| `Integration` | Indigo |
| `Performance` | Orange |

In filtered views, show the active category as a highlighted filter chip that can be removed to return to the full view.

---

## Export Usage Recommendations

Exports are submitted as background jobs and polled for completion. They are intended for compliance reporting, data extraction for legal holds, and integration with downstream analytics systems — not for large in-UI paginated reads.

### Submit an Export

```
POST /audit/exports
Content-Type: application/json

{
  "scopeType":             "Tenant",
  "scopeId":               "{tenantId}",
  "category":              "Security",
  "minSeverity":           "Warn",
  "from":                  "2026-01-01T00:00:00Z",
  "to":                    "2026-04-01T00:00:00Z",
  "format":                "Csv",
  "includeStateSnapshots": false,
  "includeTags":           true,
  "includeHashes":         false
}
```

Returns `202 Accepted` with an `ExportStatusResponse` containing the `exportId`.

### Poll for Completion

```
GET /audit/exports/{exportId}
```

Poll until `status` is a terminal value: `Completed`, `Failed`, `Cancelled`, or `Expired`. Recommended polling interval: 2 seconds, back off to 10 seconds after 30 seconds.

When `status = Completed`, the response includes a file path or download URL. Stream this to the user.

### Export Format Recommendations

| Format | When to use |
|---|---|
| `Csv` | Compliance reports, spreadsheet review, sharing with legal teams |
| `Json` | Integration with downstream systems; preserves all nested fields (scope, actor, entity) |
| `Ndjson` | Data pipeline ingestion (one record per line; stream-parseable) |

### Export Scope Mapping

| Interface tier | `scopeType` | `scopeId` |
|---|---|---|
| Platform Admin (all tenants) | `Platform` | null |
| Platform Admin (one tenant) | `Tenant` | `{tenantId}` |
| Tenant Admin | `Tenant` | `{tenantId}` |
| Organization Admin | `Organization` | `{organizationId}` |
| User (subject access) | `User` | `{userId}` |

### UI Export Recommendations

- **Always require a date range** before allowing export submission. Unbounded exports on large datasets are slow and produce unexpectedly large files.
- **Show a progress indicator** during polling. Exports are typically fast for narrow date ranges; wider ranges can take longer.
- **Disable `includeStateSnapshots`** for security/access category exports where `before`/`after` fields are null — this keeps file sizes small.
- **Enable `includeStateSnapshots`** for `DataChange` category exports where users specifically need to see what changed.
- **`includeHashes`** should only be surfaced to `PlatformAdmin` users in an integrity verification workflow. Do not show this option to tenant or org admins.
- After submitting an export, show the `exportId` as a reference number the user can use to retrieve the export later.

---

## Recommended Filter Bar Components

For most interfaces, a filter bar with the following controls provides a good balance of usability and power:

| Control | Binds to | Notes |
|---|---|---|
| Date range picker | `from`, `to` | Default to last 7 days. Required before export. |
| Category selector | `category` | Single-select dropdown. "All categories" = omit parameter. |
| Min severity selector | `minSeverity` | Dropdown: Info, Notice, Warn, Error, Critical, Alert. Default: Info or None. |
| Event type search | `eventTypes` | Typeahead or multi-select chip input. |
| Actor search | `actorId` | User search — bind to actor's user ID, not display name. |
| Resource type + ID | `entityType`, `entityId` | Two-field input; entityId disabled until entityType is set. |
| Text search | `descriptionContains` | Debounce at 300ms; require at least 3 characters; show a warning that it requires a date range for performance. |
| Source system | `sourceSystem` | Dropdown populated from known services (identity-service, care-connect, etc.). |

---

## Pagination vs. Infinite Scroll

Use **standard pagination** (page number, previous/next controls) for:
- Compliance review tables where auditors need to navigate to a specific page
- Exported-view equivalents in the UI
- Dense table views where users need to jump to specific records

Use **load-more / infinite scroll** for:
- Timeline views (personal history, resource history)
- Correlation trace views

Do not use cursor-based infinite scroll that fetches forward while the user scrolls — the API is page-based (`page`, `pageSize`). Implement load-more by incrementing `page` and appending to the existing items list.

---

## Deep Linking

Construct deep links using `auditId` — the stable public UUID assigned to every record at ingest time.

```
/audit-log/events/{auditId}
```

The detail view fetches:
```
GET /audit/events/{auditId}
```

Returns `404` when the record does not exist or the caller's scope does not permit access. Render a "Record not found or not accessible" message rather than distinguishing between the two (to avoid information disclosure).

---

## Error Handling

| HTTP Code | Meaning | UI action |
|---|---|---|
| `400 Bad Request` | Invalid filter parameters | Show validation message near the offending filter control |
| `401 Unauthorized` | Session expired or no credentials | Redirect to login |
| `403 Forbidden` | Caller lacks scope for the requested query | Show "You do not have permission to view this audit data" message |
| `404 Not Found` | Record or export job not found | Show "Record not found" message |
| `503 Service Unavailable` | Export subsystem not configured | Show "Export is not available on this instance" |

---

## See Also

- [Query Authorization Model](query-authorization-model.md) — full scope, role, and enforcement reference
- [Canonical Event Contract](canonical-event-contract.md) — all field definitions
- [Producer Integration Guide](producer-integration.md) — how upstream services emit events
- [Event Forwarding Model](event-forwarding-model.md) — downstream forwarding architecture
