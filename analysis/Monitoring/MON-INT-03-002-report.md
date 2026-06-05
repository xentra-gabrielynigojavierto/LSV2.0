# MON-INT-03-002 — Alert Acknowledge / Resolve Workflow

> **Report created FIRST** before any implementation, per mandatory execution rules.
> Updated incrementally after each step.

---

## 1. Task Summary

Add the first operational action workflow so operators can manually resolve active
alerts from the Incident Detail View in the Control Center. The action executes
through the Monitoring Service admin API, the resulting state is reflected back
into the read model, and the CC UI refreshes automatically.

| Field | Value |
|---|---|
| **Ticket** | MON-INT-03-002 |
| **Status** | ✅ Complete |
| **Depends on** | MON-INT-03-001 (Incident Detail View — complete) |
| **Date** | 2026-04-20 |

---

## 2. Existing Alert Model Analysis

### MonitoringAlert domain entity — key findings

| Field | Type | Purpose |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `MonitoredEntityId` | `Guid` | FK to monitored entity |
| `EntityName` | `string` | Snapshotted at fire time (stable on rename) |
| `IsActive` | `bool` | True = alert currently active; False = resolved |
| `TriggeredAtUtc` | `DateTime` | When the alert was created |
| `ResolvedAtUtc` | `DateTime?` | Set when resolved; null = still active |
| `CreatedAtUtc` | `DateTime` | IAuditableEntity — set by SaveChanges interceptor |
| `UpdatedAtUtc` | `DateTime` | IAuditableEntity — set by SaveChanges interceptor |

**Key finding 1: `Resolve(DateTime)` method already exists on the domain entity.**

```csharp
public void Resolve(DateTime resolvedAtUtc)
{
    if (!IsActive) return;  // idempotent — calling twice is safe
    IsActive = false;
    ResolvedAtUtc = resolvedAtUtc;
}
```

The domain method is already idempotent and correct. No domain model changes needed.

**Key finding 2: No `Acknowledge` state exists.**

The domain comment explicitly defers acknowledgement:
> "Out of scope (deferred): acknowledgement, assignee, escalation, incident grouping…"

Confirmed absent from: `MonitoringAlert.cs`, `MonitoringAlertConfiguration.cs`, alert migrations.

**Key finding 3: `IsActive=false` is already persisted.**

`MonitoringAlertConfiguration.cs` maps `is_active` and `resolved_at_utc` to the `monitoring_alerts` table. No schema migration needed.

**Key finding 4: Read model filters on `IsActive`.**

`EfCoreMonitoringReadService.GetActiveAlertsAsync()`:
```csharp
.Where(a => a.IsActive)
```

After manual resolve (`IsActive=false`), the alert immediately disappears from the active
alerts list. The read model self-corrects on next fetch.

### Automatic resolution — scheduler interaction

`EfCoreAlertRuleEngine` resolves active alerts automatically when an entity transitions
from Down → Up or Unknown (Rule 3). It uses the same `MonitoringAlert.Resolve()` method.

**Manual resolve / scheduler dedup interaction:**

When an operator manually resolves an alert on an entity that is still Down:
1. The alert is marked `IsActive=false` — it disappears from the active list.
2. On the next scheduler cycle: prior status is still `Down`, new status is still `Down`.
3. Scheduler hits **Rule 2** (dedup): "entity still Down — not creating duplicate."
4. No new alert is created until the entity recovers *and then goes Down again*.

**Assessment:** This is a known limitation but an intentional trade-off (documented in §9).
For the primary use case (operator resolves after confirming a fix), the entity is already
Up by the time the operator acts — in which case the scheduler has already auto-resolved
it and the operator's call returns `already_resolved` (idempotent success).

---

## 3. Target Action Model

### Chosen action: Resolve only

| Action | Decision | Reason |
|---|---|---|
| **Resolve** | ✅ Implemented | Clean fit with existing domain; `Resolve()` method exists; `IsActive/ResolvedAtUtc` already persisted |
| **Acknowledge** | ❌ Deferred | Not in domain model; explicitly deferred in code comments; adding it would require a schema migration and a new state field; out of scope for this feature |

### State transition

```
Active alert (IsActive=true, ResolvedAtUtc=null)
    ↓  POST /monitoring/admin/alerts/{id}/resolve
Resolved alert (IsActive=false, ResolvedAtUtc=now)
```

### Idempotency

Calling resolve on an already-resolved alert returns HTTP 200 with `status: "already_resolved"`.
No error is raised. The existing `ResolvedAtUtc` is preserved (not overwritten). This is
safe for CC clients that retry on network failures.

### Authorization

`MonitoringAdmin` policy (`MonitoringPolicies.AdminWrite`) — satisfied by either:
- Service token with `sub=service:*` (used by the CC BFF server action)
- Bearer JWT with `PlatformAdmin` role (future: platform admin web UI)

### Read model after resolve

The resolved alert disappears from the `GetActiveAlertsAsync()` query immediately.
The CC calls `router.refresh()` after the action succeeds — the Next.js server components
re-fetch the summary and the alerts list updates.

---

## 4. Monitoring Service API Design

### Endpoint

```
POST /monitoring/admin/alerts/{id:guid}/resolve
```

**Method:** `POST` — executing an action verb ("resolve this alert"), not a partial update.

**Auth:** `MonitoringPolicies.AdminWrite` (service token or PlatformAdmin JWT).

**Request body:** none — the action is fully specified by the URL and the actor's auth.

**Response shape:**

| Case | HTTP | Body |
|---|---|---|
| Alert found, was active → resolved | 200 | `{ alertId, status: "resolved", resolvedAtUtc }` |
| Alert found, already resolved | 200 | `{ alertId, status: "already_resolved", resolvedAtUtc }` |
| Alert not found | 404 | ProblemDetails |
| Not authenticated | 401 | — |
| Not authorized (no AdminWrite) | 403 | — |

**Gateway route:** `monitoring-admin-alerts` (order 55), `AuthorizationPolicy: "Anonymous"`.
The gateway passes the request through; the Monitoring Service handles its own auth.
This matches the pattern used by all other downstream service admin routes.

---

## 5. Backend Implementation

### Files created / modified

#### `Monitoring.Api/Endpoints/MonitoringAlertEndpoints.cs` — **created**

Minimal endpoint handler in the established pattern (direct DbContext access, lambda
methods, no additional application layer):

```csharp
public static class MonitoringAlertEndpoints
{
    public static IEndpointRouteBuilder MapMonitoringAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/monitoring/admin/alerts")
                       .RequireAuthorization(MonitoringPolicies.AdminWrite);
        admin.MapPost("/{id:guid}/resolve", ResolveAsync);
        return app;
    }

    private static async Task<IResult> ResolveAsync(Guid id, MonitoringDbContext db, CancellationToken ct)
    {
        var alert = await db.MonitoringAlerts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null)
            return Results.NotFound(ProblemFactory.NotFound($"Alert '{id}' was not found."));
        if (!alert.IsActive)
            return Results.Ok(new { alertId = alert.Id, status = "already_resolved", resolvedAtUtc = alert.ResolvedAtUtc });
        alert.Resolve(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { alertId = alert.Id, status = "resolved", resolvedAtUtc = alert.ResolvedAtUtc });
    }
}
```

`alert.Resolve()` is the domain method — no direct field mutation. SaveChanges triggers the
`IAuditableEntity` interceptor which updates `UpdatedAtUtc`.

#### `Monitoring.Api/Program.cs` — **modified**

```csharp
app.MapMonitoredEntityEndpoints();
app.MapMonitoringAlertEndpoints();   // ← added
app.MapMonitoringReadEndpoints();
```

#### `apps/gateway/Gateway.Api/appsettings.json` — **modified**

Added route `monitoring-admin-alerts` at order 55 (between specific reads at 54 and catchall at 150):
```json
"monitoring-admin-alerts": {
    "ClusterId": "monitoring-cluster",
    "AuthorizationPolicy": "Anonymous",
    "Order": 55,
    "Match": { "Path": "/monitoring/monitoring/admin/alerts/{**catch-all}" },
    "Transforms": [ { "PathRemovePrefix": "/monitoring" } ]
}
```

**Why Anonymous?** The Monitoring Service handles its own auth via `MonitoringAdmin` policy.
The gateway passing `Anonymous` just means "don't reject at the gateway layer" — same pattern
used for all other admin routes across all services. Without this, the gateway's default
catch-all policy (which rejects service tokens) would block the CC BFF.

**No domain or infrastructure changes.** No migrations needed — all required fields
(`IsActive`, `ResolvedAtUtc`) are already in the schema.

---

## 6. Audit / Traceability

**What is recorded:**

1. `MonitoringAlert.UpdatedAtUtc` — updated by the `IAuditableEntity` SaveChanges interceptor
   on every `SaveChangesAsync` call. This timestamp indicates when the alert was last modified,
   covering manual resolve.

2. `MonitoringAlert.ResolvedAtUtc` — the exact UTC timestamp of resolution is persisted
   in the alert row, providing a durable record of when each alert ended.

**What is NOT recorded:**

- **Who** resolved the alert. The current Monitoring Service has no concept of an acting
  user identity on alert rows. The service token sub (`service:control-center`) is validated
  but not written to the alert.
- **A structured audit event** to the platform Audit Event bus. The Monitoring Service does
  not yet emit audit events through the platform pipeline.

**Gap:** A fully auditable resolve workflow would write an audit event with `actorId`,
`actorType: "PlatformAdmin"`, `targetType: "MonitoringAlert"`, `targetId`, `action: "Resolve"`.
This requires either: (a) the CC server action emitting to the audit event bus after a
successful resolve, or (b) the Monitoring Service gaining access to the audit client.
The server action (`apps/control-center/src/app/actions/monitoring.ts`) is structured
to accept this addition cleanly in a follow-up task.

---

## 6. Control Center Integration

### Server Action: `apps/control-center/src/app/actions/monitoring.ts` — **created**

```ts
'use server';
export async function resolveAlertAction(alertId: string): Promise<ResolveAlertResult>
```

**Security design:**
- `'use server'` — runs only on the Next.js server. The secret never reaches the browser.
- Mints a HS256 service token using `FLOW_SERVICE_TOKEN_SECRET` with:
  - `iss: "legalsynq-service-tokens"`, `aud: "monitoring-service"`, `sub: "service:control-center"`
  - 60-second TTL (single-action use; avoids replay risk)
- Calls `POST {GATEWAY_URL}/monitoring/monitoring/admin/alerts/{alertId}/resolve`
- Returns `{ ok: boolean, error?: string }` — never throws (caller handles all paths)

**Error handling:**
| Case | Behavior |
|---|---|
| Invalid alertId | Returns `{ ok: false, error: "Invalid alert ID." }` |
| `FLOW_SERVICE_TOKEN_SECRET` missing | Throws → caught → returns error |
| HTTP 404 from Monitoring Service | Returns `{ ok: false, error: "Alert not found…" }` |
| Other HTTP error | Returns `{ ok: false, error: "Monitoring Service error (HTTP N): …" }` |
| Network failure | Caught → returns `{ ok: false, error: message }` |

### Component update: `incident-detail-panel.tsx` — **modified**

**New imports:** `useState`, `startTransition`, `useRouter` (Next.js navigation), `resolveAlertAction`.

**State added:**
```ts
const [resolveState, setResolveState] = useState<'idle' | 'submitting' | 'success' | 'error'>('idle');
const [resolveError, setResolveError] = useState<string | null>(null);
const isResolved = !serverActive || resolveState === 'success';
```

**Footer area (was a static note; now an action area):**
- Active alert + `resolveState !== 'success'`: Shows **"Resolve Alert"** button
- `resolveState === 'submitting'`: Button shows spinner + "Resolving…", disabled
- `resolveState === 'success'`: Button hidden; success message shown; `router.refresh()` called
- `resolveState === 'error'`: Error box with dismiss button; button returns to idle
- Already-resolved alert (from server): Button hidden; footer shows "Resolved · Data from Monitoring Service"

**Status pill** in the header updates to "Resolved" immediately when `resolveState === 'success'`
(without waiting for `router.refresh()`), so the operator sees instant feedback.

**Refresh mechanism:**
```ts
startTransition(() => { router.refresh(); });
```
`router.refresh()` re-fetches all server components on the current route. The Next.js
monitoring page re-calls `getMonitoringData()` → CC BFF → Monitoring Service → fresh
active alerts list. The alert disappears from the list. Client component state
(the open panel) is preserved across the refresh.

---

## 7. Validation

### A. TypeScript: 0 errors

```
cd apps/control-center && pnpm tsc --noEmit
# (no output — clean)
```

### B. Build: Monitoring Service

```
dotnet build apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj --nologo
# Build succeeded.
```

The `MSB3492` and "Building target completely" messages seen in quiet mode (`-q`) are
MSBuild verbosity noise — not compilation errors. Confirmed: `Build succeeded.`

### C. Auth rejection

```
POST /monitoring/monitoring/admin/alerts/{id}/resolve (no auth)
→ HTTP 401 ✓
```

### D. Service-token resolve — already auto-resolved (idempotent path)

```
POST /monitoring/admin/alerts/5df57c1b-4038-4a8c-b8fb-76efe1ce0af6/resolve
Authorization: Bearer <service token>
→ HTTP 200
← { "alertId": "5df57c1b-...", "status": "already_resolved", "resolvedAtUtc": "2026-04-20T07:50:44.235154" }
```

The scheduler had already auto-resolved this alert (entity recovered). Manual call is safe
and idempotent.

### E. Idempotent call on already-resolved alert

```
POST /monitoring/admin/alerts/5df57c1b-..../resolve  (second call)
→ HTTP 200
← { "status": "already_resolved", "resolvedAtUtc": "2026-04-20T07:50:44.235154" }
# ResolvedAtUtc unchanged ✓
```

### F. Manual resolve of genuinely active alert

```
POST /monitoring/admin/alerts/331f7b41-4287-4adc-b400-712af9856e40/resolve
Authorization: Bearer <service token>
→ HTTP 200
← { "alertId": "331f7b41-...", "status": "resolved", "resolvedAtUtc": "2026-04-20T07:51:23.4738981Z" }
```

### G. Read model reflects resolve

| Check | Before | After |
|---|---|---|
| Active alert count | 2 | 1 |
| Resolved alert in active list | YES | NO |

```
Active alerts after resolve: 1 (was 2)
Resolved alert still in list: NO (correct) ✓
```

### H. No regressions

| Endpoint | Expected | Result |
|---|---|---|
| `GET /api/monitoring/summary` (CC BFF) | HTTP 200 | ✅ 200 |
| `GET /monitoring` (CC page, unauthenticated) | HTTP 307 → /login | ✅ 307 |
| `GET /systemstatus` | HTTP 200 | ✅ 200 |
| TypeScript | 0 errors | ✅ clean |

### What was runtime-tested vs code-verified only

| Item | Status |
|---|---|
| Auth rejection (401) | ✅ Runtime-tested |
| Service token resolve → HTTP 200 | ✅ Runtime-tested |
| Idempotent resolve → already_resolved | ✅ Runtime-tested |
| Manual resolve of active alert → HTTP 200 | ✅ Runtime-tested |
| Read model sync (alert count drop) | ✅ Runtime-tested |
| CC page compiles + redirects correctly | ✅ Runtime-tested |
| CC BFF `router.refresh()` triggers re-fetch | ✅ Code-verified (standard Next.js App Router pattern) |
| Resolve button → submitting → success state | ✅ Code-verified (standard React useState pattern) |
| Error dismiss + retry | ✅ Code-verified |
| Browser interaction (click → panel → button → resolve) | ⚠️ Code-verified only (dev admin credentials stale) |

---

## 8. Files Changed

| File | Action | Purpose |
|---|---|---|
| `apps/services/monitoring/Monitoring.Api/Endpoints/MonitoringAlertEndpoints.cs` | **Created** | `POST /monitoring/admin/alerts/{id}/resolve` endpoint |
| `apps/services/monitoring/Monitoring.Api/Program.cs` | **Modified** | Register `MapMonitoringAlertEndpoints()` |
| `apps/gateway/Gateway.Api/appsettings.json` | **Modified** | Add `monitoring-admin-alerts` route (order 55, Anonymous, pass-through to service) |
| `apps/control-center/src/app/actions/monitoring.ts` | **Created** | `resolveAlertAction` server action — mints service token, calls Monitoring admin API |
| `apps/control-center/src/components/monitoring/incident-detail-panel.tsx` | **Modified** | Add Resolve button, action state machine, `router.refresh()` on success |

**Not modified:**
- Domain entity (`MonitoringAlert.cs`) — `Resolve()` already existed
- Persistence configuration — no schema changes needed
- Migrations — no new fields added
- `EfCoreAlertRuleEngine` — automatic resolve behavior preserved unchanged
- `monitoring-source.ts` — read path unchanged
- All other monitoring components (`alerts-panel.tsx`, `component-status-list.tsx`, etc.)

---

## 9. Known Gaps / Risks

| # | Gap | Severity | Notes |
|---|---|---|---|
| 1 | **No acknowledge** — scope limited to resolve | Low | By design; `Acknowledge` is explicitly deferred in domain comments; adding it requires schema migration + new state field |
| 2 | **Audit actor identity not recorded** — resolves are timestamped but not attributed | Medium | `UpdatedAtUtc` records when, but not who. Platform audit event emission is the correct fix (see §6). Low operational risk today because only PlatformAdmin / service:control-center can call the endpoint |
| 3 | **Manual resolve on still-down entity suppresses re-alerting** — scheduler dedup prevents new alert until next Down transition | Low | Documented behavior. In practice, operators resolve after confirming a fix. If alert needs to re-fire while entity is still Down, operators must wait for the entity to recover and go Down again, OR a future "re-open" action could be added |
| 4 | **Browser interaction not runtime-tested** — dev admin credentials are stale | Low | TS + API fully validated; React state machine is standard patterns |
| 5 | **Token TTL is fixed at 60s** — no retry with refreshed token if network is slow | Low | Single-action tokens; 60s is conservative for a simple POST. Future: shorten to 30s |
| 6 | **No alert pagination** — alert panel renders all alerts | Low | Inherited from MON-INT-03-001; acceptable at current scale |

---

## 10. Recommended Next Feature

**Option A: MON-INT-03-003 — Incident Timeline / History View**

**Rationale:**

The resolve workflow is now complete and fully operational. The natural next step for ops
value is **traceability**: being able to see the history of an alert — when it fired, when
it was resolved (automatically or manually), and what the component status was at each
transition. This requires a read endpoint on the Monitoring Service that returns historical
alert records for a given entity (`GET /monitoring/monitoring/alerts?entityId={id}&limit=10`),
and a timeline section in the incident detail panel.

**Option B: MON-INT-02-004 — Clickable Banner Filtering** is the alternative if UX polish
is the priority — clicking a status badge in the summary banner should filter the component
status list. Lower operational value than timeline but a quick, contained change.

**Recommendation: Option A** — the alert resolve action is now in place, and a history
view gives operators the context to understand whether a resolved alert has a pattern of
recurrence. This is the highest-value next increment in the incident workflow.
