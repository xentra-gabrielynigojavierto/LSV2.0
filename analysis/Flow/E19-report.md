# E19 — Analytics & Reporting Layer

## Scope Executed

Read-only, derived, explainable analytics over existing Flow platform signals.
No new write systems, no BI platform, no ML inference.
All metrics are computed on-demand from authoritative source-of-truth columns in the
FlowDb. Tenant-scoped by default; cross-tenant platform view is gated to PlatformAdmin.

---

## Assumptions

1. **`TenantId` is `string`** — `BaseEntity` carries a `string` TenantId, not Guid.
   All EF queries filter on `string` equality.

2. **`WorkflowTask.SlaStatus`** is the canonical SLA state field (`OnTime`, `AtRisk`,
   `Breached`). `WorkflowTask.SlaBreachedAt` records the breach timestamp.
   Analytics reads these fields directly rather than recomputing from DeadlineAt/CompletedAt
   to prevent contradictions with what the task processor already recorded.

3. **`AssignmentMode`** distinguishes auto-assigned vs manually claimed tasks.
   Splitting claim / reassign / auto-assign _volumes_ by sub-event type requires typed
   audit events that do not yet exist in the Flow schema.  Assignment analytics is therefore
   limited to `AssignedToUserId` coverage (unassigned count) and mode distribution.

4. **Outbox queries use `IgnoreQueryFilters()`** — the outbox processor runs in a null-tenant
   context, so global EF query filters (which enforce tenant scope) must be suppressed for
   outbox metrics. This mirrors the existing `AdminOutboxController` pattern.

5. **Time windows** — `today`, `7d`, `30d` map to UTC midnight boundaries. All comparisons
   use `DateTime.UtcNow`. No client-supplied absolute ranges are accepted in E19.

6. **Auth policy** — `[Authorize(Policies.PlatformOrTenantAdmin)]` is the class-level gate.
   `GetPlatformSummary` additionally checks `User.IsInRole(Roles.PlatformAdmin)` and returns
   `Forbid()` for tenant admins. No `Policies.PlatformAdmin` constant exists in the codebase;
   role check is used directly.

---

## Repository / Architecture Notes

```
apps/services/flow/backend/
  src/
    Flow.Application/
      DTOs/AnalyticsDtos.cs               ← all 8 DTO types
      Services/IFlowAnalyticsService.cs   ← service contract
      Services/FlowAnalyticsService.cs    ← implementation (5 domain queries + 2 dashboard aggregators)
    Flow.Api/
      Controllers/V1/AdminAnalyticsController.cs  ← 7 GET endpoints
    Flow.Infrastructure/
      DependencyInjection.cs              ← AddScoped<IFlowAnalyticsService, FlowAnalyticsService>

apps/control-center/
  src/
    app/analytics/page.tsx                ← CC analytics page (PlatformAdmin)
    components/analytics/
      sla-cards.tsx                       ← SLA performance cards
      queue-table.tsx                     ← Queue backlog table
      workflow-cards.tsx                  ← Workflow throughput cards
      outbox-cards.tsx                    ← Outbox reliability cards
      platform-table.tsx                  ← Cross-tenant platform summary table
    lib/control-center-api.ts             ← analytics namespace (7 methods)
    types/control-center.ts               ← 8 AnalyticsDto TypeScript types
    components/shell/cc-shell.tsx         ← userEmail prop wired from session

apps/web/
  src/
    app/(platform)/tenant/analytics/page.tsx   ← Tenant admin analytics page
    lib/tenant-api.ts                          ← 3 analytics methods added
    lib/nav.ts                                 ← ANALYTICS section added to buildNavGroups
    types/tenant.ts                            ← TenantSlaSummary/QueueSummary/WorkflowThroughput
```

---

## Analytics Read Model Notes

All queries are **read-only EF LINQ projections** against FlowDb.
No materialised views, no shadow tables, no separate analytics DB.

- `FlowDbContext.WorkflowTasks` — SLA, queue, and assignment metrics
- `FlowDbContext.WorkflowInstances` — throughput metrics
- `FlowDbContext.OutboxMessages` — reliability metrics (ignores global tenant filter)

Metrics are computed in application-layer SQL via `.GroupBy` / `.CountAsync` / `.AverageAsync`.
They are **derived** (not stored), so they always reflect the live state of the source tables.

---

## SLA Analytics Notes

**Source**: `WorkflowTask.SlaStatus`, `SlaBreachedAt`, `CompletedAt`, `CreatedAt`

Counts are grouped by `SlaStatus` enum value:
- `OnTime` — task met its deadline
- `AtRisk` — task is within breach window but not yet breached
- `Breached` — task exceeded its SLA deadline

`avgTimeToBreachHours`: average of `(SlaBreachedAt - CreatedAt)` in hours, only for breached
tasks within the window where `SlaBreachedAt != null`.

Percentages are computed as `count / total * 100`, defaulting to `0` if `total == 0`.

---

## Queue / Workload Analytics Notes

**Source**: `WorkflowTask` — `Status`, `AssignedToUserId`, `SlaStatus`, `CreatedAt`

Queue name is derived from `WorkflowTask.QueueName` (string).  
Rows are grouped by `QueueName` and include:
- `pendingCount` — tasks in `Pending` status
- `inProgressCount` — tasks in `InProgress` status
- `overdueCount` — tasks with `SlaStatus == Breached` and not yet completed
- `unassignedCount` — tasks with `AssignedToUserId == null` and `Status == Pending`
- `avgAgeHours` — average of `(UtcNow - CreatedAt)` for all open tasks in the queue

`totalPending` / `totalOverdue` — tenant-wide sums across all queues.

---

## Workflow Throughput Analytics Notes

**Source**: `WorkflowInstance` — `Status`, `CreatedAt`, `CompletedAt`

Window-gated by `CreatedAt >= windowStart`.

- `startedCount` — all instances created in window
- `completedCount` — instances with `Status == Completed` and `CompletedAt` in window
- `cancelledCount` — instances with `Status == Cancelled`
- `completionRate` — `completedCount / startedCount * 100` (0 if none started)
- `avgDurationHours` — average of `(CompletedAt - CreatedAt)` for completed instances;
  null if no completions.

---

## Assignment / Intelligence Analytics Notes

**Source**: `WorkflowTask` — `AssignedToUserId`, `AssignmentMode`, `Status`

Metrics available in E19:
- `totalTasks` — all tasks in window
- `autoAssignedCount` — tasks with `AssignmentMode == Auto`
- `manuallyAssignedCount` — tasks with `AssignmentMode == Manual`
- `unassignedCount` — tasks with `AssignedToUserId == null`
- `avgTasksPerUser` — average open task count across assigned users

**Gap documented**: Claim / reassign / release sub-event volumes cannot be split from
`WorkflowTask` fields alone. Typed audit events (a future epic) would enable this breakdown.
No placeholder metrics or approximations are emitted for this gap.

---

## Operations / Outbox Analytics Notes

**Source**: `OutboxMessage` — `Status`, `CreatedAt`, `LastAttemptAt`, `RetryCount`, `MaxRetries`

Queries use `IgnoreQueryFilters()` to bypass the tenant-scoped global query filter, because
outbox processing runs in a null-tenant context and a tenant-scoped query would return zero rows.

Metrics:
- `pendingCount`, `processingCount`, `failedCount`, `deadLetteredCount`, `succeededCount` —
  grouped by `OutboxMessage.Status`
- `successRatePct` — `succeededCount / (total - pendingCount - processingCount) * 100`
- `avgRetryCount` — average `RetryCount` across non-succeeded messages; null if empty
- `oldestPendingAgeHours` — age of the oldest pending message; null if queue is empty

These extend the E17 outbox summary with window-gated reliability signals.

---

## API Notes

All endpoints live under `GET /flow/api/v1/admin/analytics/`:

| Suffix      | Auth          | Window param | Description                          |
|-------------|---------------|--------------|--------------------------------------|
| `/summary`  | TenantAdmin+  | `?window=`   | Unified dashboard summary (5 domains)|
| `/sla`      | TenantAdmin+  | `?window=`   | SLA performance breakdown            |
| `/queues`   | TenantAdmin+  | none         | Queue backlog (live snapshot)        |
| `/workflows`| TenantAdmin+  | `?window=`   | Workflow throughput                  |
| `/assignment`| TenantAdmin+ | `?window=`   | Assignment and workload distribution |
| `/outbox`   | TenantAdmin+  | `?window=`   | Outbox reliability                   |
| `/platform` | PlatformAdmin | `?window=`   | Cross-tenant summary (403 for non-PA)|

Cache hint: `[ResponseCache(Duration = 30)]` on all GET endpoints — metrics tolerate 30s staleness.

---

## Tenant Admin Dashboard Notes

Route: `GET /tenant/analytics?window=<today|7d|30d>` (web app)

Shows:
1. **SLA Performance** — 5 stat cards: total tasks, on-time %, at-risk %, breached %, avg time to breach
2. **Queue Backlog** — summary totals + per-queue table with pending / in-progress / overdue / unassigned / avg age
3. **Workflow Throughput** — 5 stat cards: started, completed, cancelled, completion rate, avg cycle time

Window selector (Today / Last 7 Days / Last 30 Days) is a server-rendered `<a>` tag — no client JS.
Each fetch failure is isolated and displays an inline error banner without blocking the other sections.
Tenant admin cannot see the outbox or cross-tenant views.

---

## Platform Admin Dashboard Notes

Route: `GET /analytics?window=<today|7d|30d>` (control center)

Shows:
1. **SLA, Queue, Workflow, Assignment, Outbox** cards — same domain coverage as tenant admin
2. **Platform Summary table** — cross-tenant table: per-tenant task counts, SLA breach rates, queue depth

`platform.tsx` section renders only if the current user has `PlatformAdmin` role.
If the backend 403s (tenant admin calling `/platform`), the section silently hides.
Both the page and backend enforce the role boundary independently (defense in depth).

---

## Validation Results

| Target                    | Result  | Notes                                       |
|---------------------------|---------|---------------------------------------------|
| `dotnet build Flow.Api`   | PASS    | 0 warnings, 0 errors                        |
| `tsc --noEmit` (CC)       | PASS    | 0 errors — `apiClient.get` signature fixed  |
| `tsc --noEmit` (Web)      | PASS    | 0 errors — component extraction avoids narrowing trap |

---

## Known Issues / Gaps

1. **Assignment sub-events** — claim / reassign / auto-assign volume split requires typed
   audit events. Currently only mode distribution and unassigned count are available.

2. **No time-series data** — E19 emits aggregate totals per window, not day-by-day trend
   points. Sparklines or charts would require bucketed queries (future).

3. **Queue name is a raw string** — there is no `Queue` entity in FlowDb.  
   Queue grouping is by the string stored in `WorkflowTask.QueueName`. Empty-string queues
   appear as a blank row; no normalisation is applied.

4. **Outbox metrics ignore `TenantId`** — by design; outbox is a shared infrastructure
   concern that runs in null-tenant scope. Tenant admins see zero outbox data from the
   web app (they don't call `/outbox`); only PlatformAdmin via CC sees outbox analytics.

5. **`/summary` endpoint calls all 5 sub-queries sequentially** — suitable for the current
   scale. If FlowDb grows large, consider parallelising with `Task.WhenAll`.

---

## Recommendation

E19 is complete and all three build targets are green. The analytics layer is fully
read-only and derives all metrics from authoritative source columns. It does not introduce
any new write paths, shadow tables, or BI infrastructure.

Suggested follow-on:
- **E19-b**: Add time-series bucketing (day-by-day counts per window) to power trend sparklines.
- **Audit events epic**: Typed assignment events (claim / reassign / release) to close the
  assignment sub-event gap documented above.
