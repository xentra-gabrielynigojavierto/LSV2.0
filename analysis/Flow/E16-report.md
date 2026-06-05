# E16 Report

> Status: **COMPLETE** — backend + frontend implementation merged,
> typecheck and runtime smoke green. This file is the single source of
> truth for E16 design decisions, mapping rules, and known limitations.

## Scope Executed

E16 unifies the existing audit/event sources (E10.1 admin actions,
E10.2 outbox, E10.3 SLA, E14.2 assignment audit) into a deterministic,
read-only timeline visible from the tenant portal task drawer and the
Control Center workflow drawer. **No new write system is introduced.**

What this phase delivers:

- Backend `WorkflowHistoryQuery` helper that pulls audit rows from the
  E13.1 audit-query seam, merges across the two entity-type names the
  Flow producers historically use for tasks (`WorkflowTask` and
  `Task` — see [Mapping notes](#mapping--transformation-notes)), and
  normalizes through the existing `AuditTimelineNormalizer`.
- New tenant-scoped APIs:
  - `GET /api/v1/workflow-tasks/{id}/timeline`
  - `GET /api/v1/workflow-instances/{id}/timeline` (tenant-portal
    variant; the existing
    `/api/v1/admin/workflow-instances/{id}/timeline` is unchanged.)
- `AuditTimelineNormalizer` extended with explicit constants and
  category classification for assignment audit
  (`workflow.task.claim`, `workflow.task.reassign`) and lifecycle audit
  (`task.assigned`, `task.completed`), plus deterministic fallback
  summaries when the producer left `Description` blank.
- Reusable web `<Timeline />` component + typed API client
  (`apps/web/src/lib/timeline`).
- Web task drawer integration: the placeholder "Timeline" section in
  `task-detail-drawer.tsx` is replaced with the live, normalized
  history.
- Control Center workflow drawer is **already** wired to a richer
  in-house `TimelineSection` against the admin endpoint shipped in
  E13.1. E16 leaves it untouched (would be a no-op redesign and is out
  of scope per the spec's "no major UI redesign" rule).

## Assumptions

1. **Audit is the only source of truth.** No new event store, no
   editable history, no synthetic entries. If an event is not in the
   audit service it is not on the timeline.
2. **Tenant isolation is gated on the entity row, not the audit row.**
   Both new endpoints look up the parent task / workflow in the
   tenant-scoped EF context first; an out-of-scope id always returns
   404 (no existence leakage).
3. **Ordering is oldest-first**, tied-broken by `eventId` (string
   ordinal), exactly as `AuditTimelineNormalizer` already does for the
   admin endpoint. Documented in the response shape.
4. **No new audit emissions.** This phase is strictly read-only;
   producers (E10.2/E10.3/E14.2) already write the rows we consume.
5. **Producer entity-type drift.** `WorkflowTaskAssignmentService`
   writes audit with `EntityType="WorkflowTask"`; the older
   `FlowEventDispatcher` writes `EntityType="Task"` for
   `task.assigned` / `task.completed`. The task-timeline endpoint
   queries **both** entity-type names and merges results to give a
   complete picture without renaming legacy producers in this phase.

## Repository / Architecture Notes

- Reused: `IAuditQueryAdapter` + `HttpAuditQueryAdapter` (E13.1 seam).
- Reused: `AuditTimelineNormalizer` (deterministic ordering, category
  classification, JSON metadata flattening).
- Extended (not forked): added task action constants and a fallback
  summary helper to the normalizer — every existing call-site keeps
  working because the public surface is unchanged.
- Added: `WorkflowHistoryQuery` (Application layer, pure helper).
- Added: two `[HttpGet("{id:guid}/timeline")]` actions on the existing
  tenant-scoped controllers (`WorkflowTasksController`,
  `WorkflowInstancesController`).

## Timeline Read Model Notes

The DTO shape returned to clients matches the existing
`TimelineEvent` record from `AuditTimelineNormalizer.cs` so the CC
TimelineSection and the new web `<Timeline />` consume the same
contract:

```
TimelineEvent {
  eventId: string                 // stable id from audit service
  auditId: guid? | null           // internal audit row id
  occurredAtUtc: ISO timestamp    // ordering key
  category: string                // classified bucket
  action: string                  // raw audit action verb
  source: string                  // sourceSystem (defaults to "flow")
  actor: { id, name, type } | null
  performedBy: string | null      // friendly form (name ?? id)
  summary: string | null          // human-readable
  previousStatus: string | null   // for state transitions
  newStatus: string | null
  metadata: Record<string, string|null>
}
```

`category` values produced by the normalizer:

| Category               | Examples |
|------------------------|----------|
| `workflow.created`     | `workflow.created` |
| `workflow.state_changed` | `workflow.state_changed` |
| `workflow.completed`   | `workflow.completed` |
| `workflow.admin.cancel` / `retry` / `force_complete` | E10.1 admin actions |
| `workflow.admin`       | other `workflow.admin.*` |
| `workflow.sla`         | `workflow.sla.dueSoon|overdue|escalated` |
| `workflow.task.claim`  | E14.2 |
| `workflow.task.reassign` | E14.2 |
| `task.assigned`        | E11.4 lifecycle |
| `task.completed`       | E11.4 lifecycle |
| `task`                 | other `task.*` |
| `workflow`             | other `workflow.*` |
| `notification`         | `notification.*` |
| `other`                | unknown / unclassified |

## Task Timeline Notes

`GET /api/v1/workflow-tasks/{id}/timeline`:

- Tenant-scoped via the existing `WorkflowTask` global query filter on
  `IFlowDbContext`. Cross-tenant ids surface as 404.
- Fetches audit twice (`EntityType in {"WorkflowTask", "Task"}`),
  merges, normalizes once. The double-fetch is documented in
  [Assumption 5](#assumptions).
- Response oldest-first, deterministic.

## Workflow Timeline Notes

`GET /api/v1/workflow-instances/{id}/timeline`:

- Tenant-scoped (non-admin). The admin endpoint at
  `/api/v1/admin/workflow-instances/{id}/timeline` keeps its
  PlatformAdmin/TenantAdmin cross-scope behaviour unchanged.
- Same `EntityType="WorkflowInstance"` audit query and same
  normalization as the admin endpoint, so the two endpoints return
  identical shapes for the same row — admin just has wider read.

## Mapping / Transformation Notes

Mapping is centralized in `AuditTimelineNormalizer.NormalizeOne`:

- `category` is derived from `action`. Unknown actions fall back to
  `eventCategory` if the audit producer set one, otherwise `other`.
- `summary` uses `audit.Description` verbatim when present; for known
  task actions a deterministic fallback summary is generated when the
  producer left it blank.
- `metadata` is a flat string→string map: top-level scalar keys from
  `MetadataJson` are merged in, with always-on enrichments
  (`sourceSystem`, `correlationId`, `severity`, …). Nested
  objects/arrays are intentionally dropped to keep the bag flat — full
  detail remains in the audit service for deep dives.
- `previousStatus` / `newStatus` are extracted from
  `workflow.state_changed` descriptions ("X → Y") and from admin
  metadata keys (`previousStatus`, `newStatus`).
- Raw `action` is preserved on every entry so UIs can fall back to the
  literal verb if the friendly summary is empty.

## API Notes

| Endpoint | Auth | Tenant scoping | Ordering |
|---|---|---|---|
| `GET /api/v1/workflow-tasks/{id}/timeline` | AuthenticatedUser | EF query filter on `WorkflowTask`, 404 on cross-tenant | oldest-first |
| `GET /api/v1/workflow-instances/{id}/timeline` | AuthenticatedUser | EF query filter on `WorkflowInstance`, 404 on cross-tenant | oldest-first |
| `GET /api/v1/admin/workflow-instances/{id}/timeline` | TenantAdmin / PlatformAdmin (existing E13.1) | PlatformAdmin sees all, TenantAdmin only own tenant | oldest-first |

All three endpoints return the same response envelope:

```
{
  "totalCount": number,
  "truncated":  boolean,
  "events":     TimelineEvent[]
}
```

## UI Component Notes

`apps/web/src/components/timeline/timeline.tsx`:

- Stateless, presentational. Takes `events`, `loading`, `error`,
  `truncated`.
- Vertical rail with category dot, action verb, summary, actor, and
  relative timestamp; metadata key/value chips render only when present.
- Empty / loading / error states; no virtualization (lists are
  expected to be ≲ a few hundred rows for a single task or workflow).

## Task UI Notes

`apps/web/src/components/my-work/task-detail-drawer.tsx`:

- Old "Timeline" section (raw created/started/completed/cancelled
  fields) is replaced with the live `<Timeline />` against
  `/api/v1/workflow-tasks/{id}/timeline`.
- Section is collapsible; collapsed by default to avoid overwhelming
  the drawer for long-running tasks.
- Lifecycle timestamps move into the existing "SLA" / state badges and
  remain visible on the task card.

## Control Center Notes

The Control Center workflow drawer already consumes the admin timeline
endpoint via its in-house `TimelineSection` component
(`apps/control-center/src/components/workflows/workflow-detail-drawer.tsx`).
That component is more feature-rich than the new web component
(filters by category and actor, date grouping, severity styling) and
is left untouched by E16. The two implementations consume the same
DTO shape, so future consolidation is straightforward.

## Validation Results

**Build:**
- `dotnet build apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj`
  → 0 errors, 0 warnings (after removing the pre-existing orphan
  duplicate migration file
  `20260418000000_AddWorkflowTaskSlaE10_3.cs` left over from the prior
  E10.3 regeneration; the surviving
  `20260418024120_AddWorkflowTaskSlaE10_3.cs` + paired Designer remain).
- `npx tsc --noEmit` for `apps/web` → 0 errors.
- `npx tsc --noEmit` for `apps/control-center` → 0 errors (no CC files
  changed by E16).

**Runtime:**
- `Start application` workflow restarted cleanly; no startup errors,
  Fast Refresh successfully picked up the drawer change.

**Functional spot-checks (read-side, no new writes):**
- Endpoints registered:
  - `GET /api/v1/workflow-tasks/{id}/timeline` (tenant-scoped)
  - `GET /api/v1/workflow-instances/{id}/timeline` (tenant-scoped)
  - `GET /api/v1/admin/workflow-instances/{id}/timeline` (unchanged)
- Tenant isolation: each new endpoint resolves the parent entity via
  the EF query-filtered `IFlowDbContext`; cross-tenant ids return 404
  before any audit query is issued.
- Mapping: existing `AuditTimelineNormalizer` snapshot tests remain
  green (existing categories unchanged); new task-action constants
  produce explicit categories instead of falling into the generic
  `task` / `workflow` buckets.

**Files created:**
- `apps/services/flow/backend/src/Flow.Application/Adapters/AuditAdapter/WorkflowHistoryQuery.cs`
- `apps/web/src/lib/timeline/timeline.types.ts`
- `apps/web/src/lib/timeline/timeline.api.ts`
- `apps/web/src/lib/timeline/index.ts`
- `apps/web/src/components/timeline/timeline.tsx`
- `analysis/E16-report.md` (this file)

**Files modified:**
- `apps/services/flow/backend/src/Flow.Application/Adapters/AuditAdapter/AuditTimelineNormalizer.cs`
  — task action constants, category branches, fallback summary helper.
- `apps/services/flow/backend/src/Flow.Api/Controllers/V1/WorkflowTasksController.cs`
  — `GET {id}/timeline` + `TaskTimelineResponse`.
- `apps/services/flow/backend/src/Flow.Api/Controllers/V1/WorkflowInstancesController.cs`
  — `GET {id}/timeline` + `WorkflowInstanceTimelineResponse`.
- `apps/web/src/components/my-work/task-detail-drawer.tsx`
  — collapsible `<TaskHistorySection />` wired to the new endpoint.

**Files removed:**
- `apps/services/flow/backend/src/Flow.Infrastructure/Persistence/Migrations/20260418000000_AddWorkflowTaskSlaE10_3.cs`
  (pre-existing orphan duplicate, not E16-related; deletion required
  to unblock the Flow build).

## Architect Review — Findings & Resolutions

The architect review (`evaluate_task` with `includeGitDiff=true`)
flagged two HIGH-severity issues that have since been fixed:

1. **Intra-tenant authorization gap on the task timeline endpoint**
   (HIGH, security). The first cut of `WorkflowTasksController.Timeline`
   only enforced the tenant query filter, which would have allowed a
   tenant user to read history for tasks they could not see in
   `GET /workflow-tasks/{id}` (e.g. another team's role-queue task).
   **Resolution:** the endpoint now calls
   `IMyTasksService.GetTaskDetailAsync` first — the same per-user
   eligibility gate used by the detail endpoint (platform-admin OR
   direct assignee OR holder of the task's role-queue role OR member
   of the task's org). Cross-tenant ids, missing ids, and ineligible
   ids all collapse to 404 via `NotFoundException`, with no existence
   leakage.

2. **Frontend race / staleness in `TaskHistorySection`** (HIGH,
   correctness). The drawer is reused across rows, so switching tasks
   while the drawer remained open could leave the previous task's
   history visible, and a slow in-flight fetch for an old taskId could
   overwrite the current task's history when it eventually resolved.
   **Resolution:** added (a) a `useEffect` keyed on `taskId` that
   resets local history state on every task change, and (b) a
   `useRef`-backed sequence counter so only the latest fetch may
   commit `events`, `truncated`, `loaded`, `loading`, and `error`
   state. Stale responses no-op silently.

## Known Issues / Gaps

- Producer entity-type drift (`Task` vs `WorkflowTask`) is masked by
  double-fetch on the read side; cleaner long-term fix is to renormalize
  the producer in a dedicated follow-up task.
- SLA event coverage depends on the outbox dispatcher actually emitting
  `workflow.sla.*` rows — `WorkflowSlaEvaluator` stages outbox messages
  and the dispatcher fans them out to audit. If the audit pipeline is
  degraded (Audit:BaseUrl empty), the timeline returns 200 OK with an
  empty list per the existing E13.1 baseline behaviour.
- The new web component is intentionally simpler than the CC
  TimelineSection (no per-category filter chips, no date grouping). A
  future iteration could promote the CC component into a shared package.

## Recommendation

**Ready for ops visibility / escalation phase.** The unified timeline
is live for tasks and workflows, deterministic, tenant-isolated, and
strictly read-only. The shape returned by all three timeline endpoints
(admin workflow, tenant workflow, tenant task) is identical, so any
follow-on ops or escalation feature can build against one DTO. No new
write system was introduced — the work is fully reversible by deleting
the new files and reverting the four edited files.

Suggested follow-ups (out of scope for E16, file as separate tasks):

1. Renormalize the legacy `task.assigned` / `task.completed`
   producer in `FlowEventDispatcher` to use
   `EntityType="WorkflowTask"`, then drop the double-fetch in
   `WorkflowHistoryQuery.GetForTaskAsync`.
2. Promote the Control Center `TimelineSection` (date grouping +
   per-category filter chips) into a shared package consumed by both
   web and CC, replacing the simpler new component.
3. Add automated coverage of the two new endpoints (tenant 200,
   cross-tenant 404, ordering invariant) once a Flow.Api test project
   exists.

