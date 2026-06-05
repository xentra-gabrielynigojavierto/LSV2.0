# E17 Report

## Scope Executed
E17 — Outbox Ops Visibility, Dead-Letter, and Manual Retry.

Added a read-only inspection surface and governed manual retry for the Flow
async outbox so operators can observe and remediate async processing failures
from the Control Center without touching the database directly.

## Assumptions

1. **Outbox entity** (`OutboxMessage : AuditableEntity`) has no per-tenant EF
   query filter by design (the `OutboxProcessor` runs in a null-tenant scope).
   Admin endpoints use `IgnoreQueryFilters()` + explicit tenant predicate.
2. **Statuses**: `Pending`, `Processing`, `Succeeded`, `Failed`, `DeadLettered`
   (string constants in `OutboxStatus`).
3. **Retry eligibility**: only `Failed` and `DeadLettered` rows. `Pending` and
   `Processing` rows are already being served by the processor; `Succeeded`
   rows must not be re-dispatched.
4. **Retry semantics**: reset `Status→Pending`, `AttemptCount→0`,
   `NextAttemptAt=now`, `LastError=null`. AttemptCount is zeroed to give the
   item a fresh backoff budget — operator intervention is the explicit signal
   that automatic retry was exhausted. The existing `OutboxProcessor` picks
   the row up on its next poll tick; no second retry mechanism is introduced.
5. **Idempotency**: every outbox payload carries the outbox row id as a
   correlation token for downstream dedupe (documented in `OutboxProcessor`).
   Resetting and re-dispatching the same row is therefore safe.
6. **Audit for retry**: `IAuditAdapter.WriteEventAsync` is called directly,
   fire-and-forget (try/catch), after `SaveChangesAsync`. Using the outbox
   itself to audit an outbox row mutation would be circular.
7. **Payload visibility**: `PayloadJson` may contain admin-action reasons,
   operator email addresses, or workflow-correlation keys. The detail endpoint
   exposes only the first 300 characters as `PayloadSummary` to reduce noise
   and avoid inadvertent PII exposure. Full payload is intentionally not
   surfaced in this phase.
8. **Search parameter**: the list endpoint accepts an optional `?search=<guid>`
   that narrows to an exact outbox id — useful for copy-paste lookups.
9. **CC auth boundary**: outbox page uses `requirePlatformAdmin()`. The Flow
   backend additionally allows `TenantAdmin` scoped to their own tenant, but
   the Control Center surface remains PlatformAdmin-only for this phase.
10. **Ordering**: list endpoint sorts newest-first (`OrderByDescending CreatedAt`)
    so the most recently failed/dead-lettered items appear at the top.

## Repository / Architecture Notes

- Outbox entity: `Flow.Domain/Entities/OutboxMessage.cs`
- Status constants: `Flow.Domain/Common/OutboxStatus.cs`
- Processor: `Flow.Infrastructure/Outbox/OutboxProcessor.cs`
- DbSet: `IFlowDbContext.OutboxMessages`
- New controller: `Flow.Api/Controllers/V1/AdminOutboxController.cs`
- New CC components: `apps/control-center/src/components/outbox/`
- New CC page: `apps/control-center/src/app/operations/outbox/page.tsx`
- New BFF routes: `apps/control-center/src/app/api/admin/outbox/`

## Outbox Read Model Notes

The inspection model is read-only and server-projected — no new DB views or
materialized tables. All data comes directly from `flow_outbox_messages`.

Fields exposed in list view:
- `id`, `tenantId`, `workflowInstanceId`, `eventType`, `status`,
  `attemptCount`, `createdAt`, `updatedAt`, `nextAttemptAt`, `processedAt`,
  `lastError` (truncated to 200 chars in list; full in detail)

Additional fields in detail view:
- `payloadSummary` (first 300 chars of `PayloadJson`, truncated with `…`)
- `isRetryEligible` (true iff status is Failed or DeadLettered)

## Outbox List / Dead Letter Notes

Single unified endpoint `GET /api/v1/admin/outbox` with optional `?status=DeadLettered`
filter. The Control Center page includes a quick "Dead letters only" toggle that
sets this preset. A dedicated dead-letter tab/page was considered but the unified
filter approach keeps the surface smaller and is sufficient for triage.

Filters: `status`, `eventType`, `tenantId` (PlatformAdmin only), `workflowInstanceId`, `search` (exact id).

## Outbox Detail Notes

`GET /api/v1/admin/outbox/{id}` returns the full error message, timestamps,
workflow context, a payload summary, and an `isRetryEligible` flag. The drawer
uses this flag to conditionally show the retry button.

## Manual Retry Notes

`POST /api/v1/admin/outbox/{id}/retry` requires a `{ reason }` body.
- Eligibility gate: `Failed | DeadLettered` only → 409 `not_retryable` otherwise.
- Mutation: `Status=Pending, AttemptCount=0, NextAttemptAt=now, LastError=null`.
- Concurrency: `DbUpdateConcurrencyException` → 409 `concurrent_state_change`.
- Audit: `IAuditAdapter.WriteEventAsync` with action `outbox.manual_retry`,
  `EntityType=OutboxMessage`, fields include `eventType`, `workflowInstanceId`,
  `previousStatus`, `reason`, `performedBy`, `isPlatformAdmin`.
- The BFF route collects a reason from a confirmation dialog before calling
  the Flow backend.

## Metrics / Summary Notes

`GET /api/v1/admin/outbox/summary` returns grouped counts:
`pendingCount`, `processingCount`, `failedCount`, `deadLetteredCount`,
`succeededCount`. Displayed as summary cards at the top of the outbox page.
Not cached on the BFF (force-dynamic) so operators see current state.

## API Notes

| Method | Path                             | Auth              | Description                |
|--------|----------------------------------|-------------------|----------------------------|
| GET    | /api/v1/admin/outbox             | PlatformOrTenant  | Paginated list + filters   |
| GET    | /api/v1/admin/outbox/summary     | PlatformOrTenant  | Counts by status           |
| GET    | /api/v1/admin/outbox/{id}        | PlatformOrTenant  | Single item detail         |
| POST   | /api/v1/admin/outbox/{id}/retry  | PlatformOrTenant  | Governed manual retry      |

All routes bypass the EF tenant query filter and apply explicit tenant scoping:
PlatformAdmin sees all rows; TenantAdmin sees only their tenant's rows.

## Control Center Notes

- Page: `/operations/outbox` — requires PlatformAdmin
- Nav: "Outbox" added to new "OPERATIONS" section in CC sidebar
- Summary cards: Pending / Processing / Failed / Dead Letters / Processed counts
- Filter bar: status preset, event-type text, tenant id, workflow instance id
- Table: outbox id (truncated), event type, status badge, attempts, tenant,
  workflow instance link, error preview, created-at, retry eligibility indicator
- Detail drawer: slides in from the right; shows full error, timestamps,
  payload summary, workflow link, retry button (eligible items only)
- Retry dialog: confirm overlay with reason textarea (required, max 1000 chars)

## Validation Results

- dotnet build Flow.Api: **PASS** — 0 warnings, 0 errors (19 s, net8.0)
- tsc --noEmit control-center: **PASS** — no output, clean exit
- List endpoint responds with paginated outbox rows: verified by code review — EF projection, `IgnoreQueryFilters`, explicit tenant predicate, `OrderByDescending(o => o.CreatedAt)`, `Skip/Take` pagination
- Summary counts reflect actual DB state: verified — `GroupBy(o => o.Status).Select(g => new { Status=g.Key, Count=g.Count() })` aggregation; force-dynamic BFF route (no caching)
- Manual retry resets row and emits audit: verified — `Status=Pending, AttemptCount=0, NextAttemptAt=now, LastError=null` followed by direct `IAuditAdapter.WriteEventAsync`; `DbUpdateConcurrencyException → 409 concurrent_state_change`
- Cross-tenant isolation enforced: verified — PlatformAdmin: no predicate (all rows); TenantAdmin: `Where(o => o.TenantId == tenantId)`; `requirePlatformAdmin()` on the CC page additionally gates the entire UI surface

## Known Issues / Gaps

- Payload sanitization is minimal (first-300-chars truncation). A proper
  sanitizer that redacts known-sensitive fields (email, reason text) is deferred.
- No server-push / websocket refresh — operators must reload the page to see
  updated counts after a retry is applied.
- Bulk retry is explicitly deferred per spec.
- No search by partial event type (only exact match) — can be improved later.
- `?search=<guid>` only finds by exact outbox id. Keyword search across error
  messages is deferred.

## Post-Review Fixes Applied

Two reactive feedback gaps identified during architect review, both patched:

1. **`router.refresh()` after retry success** — `RetrySection` in `outbox-detail-drawer.tsx`
   now calls `router.refresh()` immediately after a successful POST so the RSC page
   re-renders, the summary cards reflect the new counts, and the table row shows the
   updated `Pending` status without requiring a manual page reload.

2. **`revalidateTag('cc:outbox')` in retry BFF route** — `POST /api/admin/outbox/{id}/retry`
   now calls `revalidateTag('cc:outbox')` after a successful upstream response so that
   any Next.js fetch cache entries tagged `cc:outbox` (list, summary) are invalidated
   server-side; on the next RSC render they fetch fresh data.

## Recommendation

E17 is complete and ready to merge. All backend and frontend builds are clean.
The outbox ops visibility surface, dead-letter filter, detail drawer, and governed
manual retry flow are all implemented and operationally sound. The two post-review
reactive feedback fixes have been applied. No blockers remain.
