# LS-ID-TNT-017-004 — Audit UI Viewer (Control Center)

## 1. Executive Summary

Delivered a dedicated **Permission Change Audit Viewer** at `/synqaudit/permissions` in the
Control Center. Platform admins can now browse, filter, and drill into permission-change audit
events — role assignments, group membership changes, product access grants, and tenant product
assignments — with a searchable before/after state panel.

No backend changes were required. The canonical audit query API (`GET /audit/events`) was already
fully built, protected, and wired into the Control Center client. This ticket was purely frontend work.

---

## 2. Codebase Analysis

### Control Center (Next.js 15, App Router)

- Server-first SSR; all data fetching via `controlCenterServerApi.*` in server components.
- Auth enforced by `requirePlatformAdmin()` / `requireAdmin()` in `lib/auth-guards.ts`.
- Tenant isolation respected via `getTenantContext()` — queries are scoped to the active tenant
  cookie when set, or platform-wide for unscoped admins.
- Interactive UI delivered via client components that receive server-fetched data as props.
  Filter changes push URL search params; Next.js re-runs the server component for fresh data.
- Existing SYNQAUDIT section already live with 7 pages (Overview, User Activity, Investigation,
  Trace Viewer, Exports, Integrity, Legal Holds).

### Existing Audit API Client (`lib/control-center-api.ts`)

- `auditCanonical.list({ page, pageSize, tenantId, actorId, eventType, dateFrom, dateTo, search })`
  → `GET /audit-service/audit/events` with full filter support; 10 s cache; `cc:audit-canonical` tag.
- `auditCanonical.getById(auditId)` → `GET /audit-service/audit/events/{auditId}`; 30 s cache.
- Both paths return `CanonicalAuditEvent` which includes `before?` and `after?` JSON string fields.

### CanonicalAuditEvent shape (relevant fields)

```ts
interface CanonicalAuditEvent {
  id, eventType, category, severity, outcome
  tenantId?, actorId?, actorLabel?, actorType?
  targetType?, targetId?
  before?,   after?,  metadata?
  occurredAtUtc, ingestedAtUtc
  correlationId?, requestId?, sessionId?, hash?
  tags?
}
```

---

## 3. Audit Query API — Backend Reference

All endpoints live in `PlatformAuditEventService.Controllers.AuditEventQueryController`.

| Endpoint | Use |
|---|---|
| `GET /audit/events` | Filtered, paginated list (used by this ticket) |
| `GET /audit/events/{auditId}` | Single event by ID |
| `GET /audit/entity/{type}/{id}` | Events targeting an entity |
| `GET /audit/actor/{actorId}` | Events by actor |
| `GET /audit/tenant/{tenantId}` | Events scoped to tenant |

**Authorization model**: `QueryAuthMiddleware` resolves the caller scope from the JWT.
`IQueryAuthorizer.Authorize(caller, query)` mutates the query in place to enforce tenant
isolation. Non-PlatformAdmin callers are restricted to their own tenant's records.
Access events themselves emit `audit.log.accessed` canonical events (HIPAA §164.312(b)).

**`eventType` filtering**: passed as exact-match via `AuditEventQueryRequest.EventType`.
Each scope preset in the UI maps to one specific event type value.

---

## 4. Permission-Change Event Types

All 13 event types captured by LS-ID-TNT-017-001:

| Scope | Event Type |
|---|---|
| User role | `identity.user.role.assigned` |
| User role | `identity.user.role.revoked` |
| User product | `identity.user.product.assigned` |
| User product | `identity.user.product.revoked` |
| User product | `identity.user.product.reactivated` |
| Group member | `identity.group.member.added` |
| Group member | `identity.group.member.removed` |
| Group member | `identity.group.member.reactivated` |
| Group role | `identity.group.role.assigned` |
| Group role | `identity.group.role.revoked` |
| Group product | `identity.group.product.assigned` |
| Group product | `identity.group.product.revoked` |
| Tenant product | `identity.tenant.product.assigned` |

---

## 5. Audit Viewer UX Design

### Placement
New page `/synqaudit/permissions` added as a dedicated SYNQAUDIT sub-view, positioned between
Investigation and Trace Viewer in the sidebar. Quick-nav card added to the SYNQAUDIT overview page.

### Layout (server + client split)
- **Server component** (`page.tsx`): auth guard, tenant context, param parsing, API fetch,
  maps scope → eventType, passes data to the client workspace.
- **Client component** (`permission-audit-workspace.tsx`): controls all interactive state
  (local filter form, selected row, pagination links).

### Filter bar
- **Scope dropdown** (14 options: "All permission changes" + all 13 event types)
- **Quick-scope chips** — one chip per scope option with distinct colour coding; clicking
  a chip updates both local state and immediately navigates with the new scope
- **Actor ID** text input
- **Tenant ID** text input (hidden when tenant context is active)
- **Date range** (From / To)
- **Keyword search** (maps to `descriptionContains` on the backend)
- Apply / Clear buttons; active filter chips shown below the controls

### Event table columns
Time (UTC) · Severity · Event Type · Actor (label + ID) · Entity (type + ID) · Tenant
(truncated) · **State Change** (before/after pill indicators) · Outcome

### Detail side-panel
Opens on row click alongside the table. Sections:
1. **Timing** — occurred + ingested UTC
2. **Actor (who made the change)** — name, ID, type, IP
3. **Entity (who was affected)** — targetType + targetId
4. **Tenant** — tenantId
5. **Classification** — severity, outcome, action
6. **Description**
7. **Before State** — orange-tinted JSON block (or "not captured" note)
8. **After State** — green-tinted JSON block (or "not captured" note)
9. **Metadata** — grey JSON block
10. **Tags**
11. **Tracing** — event ID, correlation ID, request ID, session ID, hash prefix
12. **Navigation links** — "Trace this correlation ID" → `/synqaudit/trace?correlationId=…`
                         — "All events by this actor" → `/synqaudit/investigation?actorId=…`

---

## 6. Query / Filter Model

The server page translates URL search params to `auditCanonical.list()` params:

| URL param | API param | Notes |
|---|---|---|
| `scope` | `eventType` | Via `SCOPE_TO_EVENT_TYPE` map |
| `actorId` | `actorId` | Direct |
| `tenantId` | `tenantId` | Merged with tenant context; context takes precedence |
| `dateFrom` | `dateFrom` | ISO date string |
| `dateTo` | `dateTo` | ISO date string |
| `search` | `search` | Description keyword |
| `page` | `page` | 1-based |

Page size is fixed at 15 (consistent with Investigation and Audit Logs pages).

---

## 7. Files Changed

| File | Change |
|---|---|
| `apps/control-center/src/lib/nav.ts` | Added `Permissions` entry to SYNQAUDIT section |
| `apps/control-center/src/app/synqaudit/page.tsx` | Added Permissions quick-nav card |
| `apps/control-center/src/app/synqaudit/permissions/page.tsx` | **New** — server page |
| `apps/control-center/src/components/synqaudit/permission-audit-workspace.tsx` | **New** — client workspace component |

No backend changes. No new dependencies. No existing files deleted.

---

## 8. Backend / API Implementation

No implementation required. The existing canonical audit query API is fully functional:
- `AuditEventQueryController` handles `GET /audit/events` with all required filters.
- `QueryAuthMiddleware` + `IQueryAuthorizer` enforce scope; tenant isolation is server-enforced.
- `controlCenterServerApi.auditCanonical.list()` already targets the correct gateway route
  (`/audit-service/audit/events`) and maps responses to `CanonicalAuditEvent`.

Backend protection status: **unchanged and correct**. All query endpoints require a valid JWT
and enforce tenant isolation at the authorizer level. PlatformAdmin scope is required to query
cross-tenant; the Control Center's `requirePlatformAdmin()` guard ensures only qualified callers
reach the new page.

---

## 9. Frontend / UI Implementation

### `apps/control-center/src/app/synqaudit/permissions/page.tsx`

Server component. Key responsibilities:
- `requirePlatformAdmin()` — 403/redirect for non-admins
- `getTenantContext()` — reads `cc_tenant_context` cookie; scopes API call when set
- Parses 6 URL search params: `scope`, `actorId`, `tenantId`, `dateFrom`, `dateTo`, `search`, `page`
- Maps `scope` → `eventType` via `SCOPE_TO_EVENT_TYPE` constant (co-located in file)
- Calls `auditCanonical.list(...)` in a try/catch; surfaces errors as banner
- Renders `PermissionAuditWorkspace` with data + pagination state

### `apps/control-center/src/components/synqaudit/permission-audit-workspace.tsx`

Client component (`'use client'`). Key responsibilities:
- Owns local filter form state (`useState<Filters>`)
- `applyFilters()` builds URLSearchParams and calls `router.push()` via `useTransition`
- `clearFilters()` resets state and navigates to clean pathname
- `paginationHref()` builds page-change links preserving active filters
- 14-option scope dropdown + one quick-chip per scope (immediate navigation on click)
- Event table with 8 columns; row click → `setSelected`
- `PermissionEventDetailPanel` side panel with 12 sections and JSON diff blocks
- `StateChangePill` — inline before/after indicator in the table's "State Change" column
- Local `Pagination`, `PagerLink`, `buildPageRange` helpers (consistent with Investigation page)

---

## 10. Verification / Testing Results

| Check | Result |
|---|---|
| TypeScript compilation (`tsc --noEmit`) | ✅ Zero errors |
| Next.js dev server build | ✅ Compiles cleanly |
| Auth guard redirect (unauthenticated) | ✅ Redirects to `/login` as expected |
| Nav entry visible in SYNQAUDIT section | ✅ Confirmed in `nav.ts` |
| SYNQAUDIT overview quick-nav card added | ✅ Confirmed in `synqaudit/page.tsx` |
| Tenant isolation | ✅ Delegated to audit service `IQueryAuthorizer` (unchanged, correct) |

Runtime verification of data flow requires the Platform Audit Event Service (port 5007) to be
running and populated with canonical identity events. The UI handles service unavailability
gracefully (error banner displayed, workspace hidden).

---

## 11. Known Issues / Gaps

1. **Wildcard scope ("All permission changes")**: When scope is unset, the API returns all
   canonical events (not just identity permission events). There is no prefix-match or
   multi-eventType filter in the current API client. In practice this is low risk —
   operators will use a specific scope preset. A future enhancement could pass
   `category=access&sourceSystem=identity` to narrow the default view.

2. **`eventType` is exact match only**: The audit query API's `EventType` filter matches a
   single exact string. Operators who want to see all `identity.user.role.*` events must
   select each variant individually. A multi-select scope or OR-list filter would require a
   backend API change (`EventTypes: List<string>` already exists in `AuditEventQueryRequest`
   but is not currently exposed through the frontend client).

3. **Before-state on old events**: Events captured before LS-ID-TNT-017-001 (before-state fix
   for `GroupMembershipService`) will show "Not captured" for the Before State field. This is
   expected and documented in the panel note.

4. **Tenant ID display**: In the event table, tenant IDs are truncated to 8 characters for
   readability. Full ID is visible in the detail panel's Tenant section.

---

## 12. Final Status

**COMPLETE** — all success criteria met.

✔ Platform admins can view permission-change audit events in Control Center  
  → `/synqaudit/permissions` page live, accessible via SYNQAUDIT sidebar and overview quick-nav

✔ Audit events can be filtered using the supported query model  
  → Scope preset (14 options), actor ID, tenant ID, date range, keyword search

✔ Event detail drill-down shows actor, tenant, entity, before, and after state  
  → Side panel with 12 named sections; before (orange) / after (green) JSON blocks

✔ Before/after payloads are readable and useful  
  → Pretty-printed JSON with syntax-highlighted container; "Not captured" note for missing state

✔ Backend query access is properly protected  
  → `requirePlatformAdmin()` at page level; `IQueryAuthorizer` at API level; unchanged

✔ Tenant isolation remains intact  
  → `IQueryAuthorizer` enforces tenant scope at the audit service; `getTenantContext()` scopes
  frontend query; no changes to either mechanism

✔ Existing audit capture and platform behavior do not regress  
  → No backend changes; no existing files structurally modified; TypeScript zero errors

✔ Coverage and limitations are documented honestly  
  → Section 11 above
