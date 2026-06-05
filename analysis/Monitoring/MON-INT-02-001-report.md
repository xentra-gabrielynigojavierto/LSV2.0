# MON-INT-02-001 — Entity Registration Bootstrap

> **Report created FIRST** before any implementation, per mandatory execution rules.
> Updated incrementally at each step.

---

## 1. Task Summary

Bootstrap the Monitoring Service with an initial set of real monitored entities
so the scheduler can begin producing actual check results, current-status rows,
and alerts. This makes the Monitoring read-model operationally useful and
allows the Control Center to consume real data instead of deriving it from
probes.

| Field | Value |
|---|---|
| **Ticket** | MON-INT-02-001 |
| **Status** | ✅ Complete |
| **Depends on** | MON-INT-01-001 (write path), MON-INT-01-002 (read endpoints) |
| **Date** | 2026-04-20 |

**Done condition met:**
- ✅ Report created first
- ✅ 10 real entities registered through a Monitoring-owned path (domain constructor)
- ✅ `/monitoring/status` returns 10 non-empty entries with real latency data
- ✅ `/monitoring/summary` integrations list is non-empty, system status is live
- ✅ One live Critical alert (Reports is down — correct)
- ✅ CC local mode unaffected
- ✅ CC service mode gateway path (`/monitoring/monitoring/summary`) validated end-to-end
- ✅ No direct SQL inserts used

---

## 2. Monitoring Service Write Path Analysis

### Options evaluated

| # | Method | Verdict |
|---|---|---|
| 1 | Admin API `POST /monitoring/admin/entities` via Gateway | ❌ Blocked — gateway requires HS256 JWT; service requires RS256 JWT. No platform component can mint an RS256 token in this environment (MON-INT-01-003 not yet landed). |
| 2 | Admin API direct on port 5015 | ❌ Still blocked — service's `RequireAuthorization()` middleware enforces RS256 regardless of whether the request comes through the gateway or direct. |
| 3 | Direct SQL `INSERT` into `monitoring_db` | ❌ Explicitly prohibited by architecture rules. Bypasses domain invariants. |
| 4 | Internal startup bootstrap (`IHostedService`) | ✅ Chosen. Runs inside the Monitoring Service process, uses the domain `MonitoredEntity` constructor (full validation enforced), persists via `MonitoringDbContext.SaveChangesAsync` (audit timestamps auto-stamped). |

### Chosen method: `MonitoringEntityBootstrap` (IHostedService)

**Why it is canonical:**
- Uses the same `MonitoredEntity(id, name, entityType, monitoringType, target, scope, impactLevel)` constructor that the admin API uses internally via `MonitoredEntityService`
- All domain invariants enforced: non-null name/target, length guards, `Enum.IsDefined` checks for EntityType/MonitoringType/ImpactLevel, non-empty Guid
- `MonitoringDbContext.SaveChangesAsync` stamps `CreatedAtUtc`/`UpdatedAtUtc` via the interceptor — identical behaviour to any other write path
- Idempotent: checks `db.MonitoredEntities.AnyAsync()` and skips if any row exists

**Why other methods were rejected:**
- Admin API is blocked until MON-INT-01-003 (RS256 ↔ HS256 alignment) lands
- Direct SQL bypasses domain validation, doesn't stamp audit timestamps correctly, and violates the architecture rule that Monitoring Service = source of truth

---

## 3. Initial Entity Set Design

Derived from two sources:
1. `apps/control-center/src/lib/system-health-store.ts` — the existing CC local
   probe list (the ground truth for "what services exist")
2. Live health probe results (pre-seed, to confirm reachability)

### Pre-registration health check results

| Service | Health URL | HTTP Status |
|---|---|---|
| Gateway | `http://127.0.0.1:5010/health` | 200 ✅ |
| Identity | `http://127.0.0.1:5001/health` | 200 ✅ |
| Documents | `http://127.0.0.1:5006/health` | 200 ✅ |
| Notifications | `http://127.0.0.1:5008/health` | 200 ✅ |
| Audit | `http://127.0.0.1:5007/health` | 200 ✅ |
| Reports | `http://127.0.0.1:5029/api/v1/health` | 000 ❌ (not running) |
| Workflow | `http://127.0.0.1:5012/health` | 200 ✅ |
| Synq Fund | `http://127.0.0.1:5002/health` | 200 ✅ |
| Synq CareConnect | `http://127.0.0.1:5003/health` | 200 ✅ |
| Synq Liens | `http://127.0.0.1:5009/health` | 200 ✅ |

Reports is not currently running but is a registered platform service — it is
**intentionally included** so the scheduler can detect and alert on its absence.
Monitoring an "expected but absent" service is the correct monitoring behaviour.

### Final entity set

| Name | Target | EntityType | MonitoringType | ImpactLevel | Scope |
|---|---|---|---|---|---|
| Gateway | `http://127.0.0.1:5010/health` | InternalService | Http | **Blocking** | infrastructure |
| Identity | `http://127.0.0.1:5001/health` | InternalService | Http | **Blocking** | infrastructure |
| Documents | `http://127.0.0.1:5006/health` | InternalService | Http | Degraded | infrastructure |
| Notifications | `http://127.0.0.1:5008/health` | InternalService | Http | Degraded | infrastructure |
| Audit | `http://127.0.0.1:5007/health` | InternalService | Http | Degraded | infrastructure |
| Reports | `http://127.0.0.1:5029/api/v1/health` | InternalService | Http | Degraded | infrastructure |
| Workflow | `http://127.0.0.1:5012/health` | InternalService | Http | Degraded | infrastructure |
| Synq Fund | `http://127.0.0.1:5002/health` | InternalService | Http | Degraded | product |
| Synq CareConnect | `http://127.0.0.1:5003/health` | InternalService | Http | Degraded | product |
| Synq Liens | `http://127.0.0.1:5009/health` | InternalService | Http | Degraded | product |

**ImpactLevel rationale:**
- `Blocking` for Gateway and Identity — without these, core auth and routing fail
- `Degraded` for all other services — failures reduce capability but do not fully block the platform

**Targets use `127.0.0.1` (not `localhost`)** — consistent with rest of platform.
Node.js resolves `localhost` to `::1` (IPv6) first; .NET services bind to `0.0.0.0`
(IPv4 only). Using `127.0.0.1` avoids connection failures.

**Not included:** Monitoring Service itself (self-monitoring via HTTP is a
circular dependency; typically excluded from self-managed health registries).

---

## 4. Registration Implementation

### File created

`apps/services/monitoring/Monitoring.Infrastructure/Bootstrap/MonitoringEntityBootstrap.cs`

### How it works

1. Registered as `IHostedService` in DI (runs after `DatabaseConnectivityHostedService`)
2. On `StartAsync`:
   - Reads `MonitoringBootstrap:Enabled` from configuration (defaults `true`)
   - Creates a dedicated `IAsyncServiceScope` (avoids sharing the scoped `DbContext` with the request pipeline)
   - Calls `db.MonitoredEntities.AnyAsync()` — **skips if any row exists** (idempotent)
   - Creates 10 `MonitoredEntity` objects via the domain constructor (all invariants enforced)
   - Calls `db.SaveChangesAsync()` — `CreatedAtUtc`/`UpdatedAtUtc` auto-stamped by the `DbContext` interceptor
3. Logs at `Information` level: entity count, individual entities at `Debug`

### DI registration

```csharp
// Monitoring.Infrastructure/DependencyInjection.cs
services.AddHostedService<DatabaseConnectivityHostedService>();
// One-shot startup seed: idempotent, disable via MonitoringBootstrap__Enabled=false
services.AddHostedService<MonitoringEntityBootstrap>();
```

### Build result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 5. Runtime Validation

After restart, the bootstrap seeded all 10 entities and the scheduler completed
its first check cycle (~15 seconds).

### GET /monitoring/entities (entity registry)

```
Count: 10
 - Audit         | http://127.0.0.1:5007/health | enabled: true | impact: Degraded  | scope: infrastructure
 - Documents     | http://127.0.0.1:5006/health | enabled: true | impact: Degraded  | scope: infrastructure
 - Gateway       | http://127.0.0.1:5010/health | enabled: true | impact: Blocking  | scope: infrastructure
 - Identity      | http://127.0.0.1:5001/health | enabled: true | impact: Blocking  | scope: infrastructure
 - Notifications | http://127.0.0.1:5008/health | enabled: true | impact: Degraded  | scope: infrastructure
 - Reports       | http://127.0.0.1:5029/api/v1/health | enabled: true | impact: Degraded | scope: infrastructure
 - Synq CareConnect | http://127.0.0.1:5003/health | enabled: true | impact: Degraded | scope: product
 - Synq Fund     | http://127.0.0.1:5002/health | enabled: true | impact: Degraded  | scope: product
 - Synq Liens    | http://127.0.0.1:5009/health | enabled: true | impact: Degraded  | scope: product
 - Workflow      | http://127.0.0.1:5012/health | enabled: true | impact: Degraded  | scope: infrastructure
```

### GET /monitoring/status (EntityCurrentStatus rows)

```
Count: 10
 - Audit         | Healthy | lastChecked: 2026-04-20T06:28:08Z
 - Documents     | Healthy | lastChecked: 2026-04-20T06:28:09Z
 - Gateway       | Healthy | lastChecked: 2026-04-20T06:28:09Z
 - Identity      | Healthy | lastChecked: 2026-04-20T06:28:10Z
 - Notifications | Healthy | lastChecked: 2026-04-20T06:28:11Z
 - Reports       | Down    | lastChecked: 2026-04-20T06:28:11Z
 - Synq CareConnect | Healthy | lastChecked: 2026-04-20T06:28:12Z
 - Synq Fund     | Healthy | lastChecked: 2026-04-20T06:28:12Z
 - Synq Liens    | Healthy | lastChecked: 2026-04-20T06:28:12Z
 - Workflow      | Healthy | lastChecked: 2026-04-20T06:28:13Z
```

### GET /monitoring/alerts (MonitoringAlert rows)

```json
[{
  "alertId":      "7c4b2e21-184d-44c7-97dd-52e78ac14bbd",
  "entityId":     "c3412103-09c4-42d3-82be-2ead6623fc46",
  "name":         "Reports",
  "severity":     "Critical",
  "message":      "Status transitioned Unknown -> Down (network failure).",
  "createdAtUtc": "2026-04-20T06:27:27.762812",
  "resolvedAtUtc": null
}]
```

### GET /monitoring/summary

```json
{
  "system": { "status": "Down", "lastCheckedAtUtc": "..." },
  "integrations": [ /* 10 entries */ ],
  "alerts": [ /* 1 Critical — Reports */ ]
}
```

**Summary:** 9 Healthy, 1 Down (Reports — correctly detected as unreachable).
`integrations` list is **non-empty** (was `[]` before bootstrap). One Critical
alert is live — correct behaviour for a service registered but not running.

### CheckResult history

Produced by the scheduler after each 15-second cycle. Confirmed via the `status`
endpoint returning `lastCheckedAtUtc` timestamps that advance with each cycle.

---

## 6. Control Center Service-Mode Validation

### Local mode (default, unchanged)

```
GET http://127.0.0.1:5004/api/monitoring/summary → HTTP 200
system.status: Down
integrations: 10 (Gateway/Identity/Documents/Notifications/Audit/Reports/
               Workflow/Fund/CareConnect/Liens)
alerts: 1 (Reports is down — detected by both modes independently)
```

Local probe engine and Monitoring Service agree on the platform state.

### Service mode gateway path

The CC service branch calls `{GATEWAY_URL}/monitoring/monitoring/summary`.
Validated directly against the same gateway URL:

```
GET http://127.0.0.1:5010/monitoring/monitoring/summary → HTTP 200
system.status: Down
integrations: 10 entries (all with real latency data from DB-backed status)
alerts: 1 Critical (Reports | "Status transitioned Unknown -> Down (network failure).")
```

```
GET http://127.0.0.1:5010/monitoring/monitoring/status → HTTP 200
Count: 10 — all 10 entities with latencyMs populated
```

```
GET http://127.0.0.1:5010/monitoring/monitoring/alerts → HTTP 200
Count: 1 — Reports | Critical
```

**The CC service mode (`MONITORING_SOURCE=service`) can consume Monitoring
Service data without any UI changes.** The field mapping in `monitoring-source.ts`
(`ServiceAlertEntry.name → SystemAlert.message`, `ServiceStatusEntry.scope → category`)
is correct for the live JSON response.

---

## 7. Rollback / Fallback

### Switch CC back to local mode

Set or unset `MONITORING_SOURCE` in `apps/control-center/.env.local`:

```bash
# In apps/control-center/.env.local:
# MONITORING_SOURCE=service   ← change to local or remove entirely
MONITORING_SOURCE=local
```

`local` is the default when the variable is unset. No code changes needed.

### Disable bootstrap

Two options:

**Option A — Config flag** (does not remove seeded data)

```bash
# In appsettings.json or env:
MonitoringBootstrap__Enabled=false
```

The bootstrap skips immediately on startup. Entities already seeded remain in DB
and continue to be probed.

**Option B — Non-empty check** (natural retirement)

Once any entity is registered via the admin API, the bootstrap's
`db.MonitoredEntities.AnyAsync()` returns `true` and the seed is skipped
automatically on all future restarts.

### Failure isolation

- Bootstrap failures are logged and bubble as `IHostedService` startup exceptions
  (ASP.NET cancels the host startup if a hosted service throws in `StartAsync`)
- If the Monitoring DB is unreachable, EF's retry policy (3 retries, configured
  in `DependencyInjection.cs`) handles transient failures before surfacing as
  an error — the rest of the platform continues normally

---

## 8. Known Gaps / Risks

| # | Gap | Severity | Mitigation |
|---|---|---|---|
| 1 | **Auth mismatch** — admin API requires RS256; gateway uses HS256. MON-INT-01-003 not yet done. | Medium | Bootstrap bypasses this via internal hosted service. Once auth is aligned, bootstrap can be replaced by a seed script calling the real API. |
| 2 | **Bootstrap is a temporary path** — not the intended production registration flow. | Low | Documented clearly; guarded by `MonitoringBootstrap:Enabled` config; naturally retired once any entity is registered via the admin API. |
| 3 | **Reports service is currently not running** (HTTP 000 on port 5029). | Low | This is correct monitoring behaviour — the scheduler detects it as `Down` and raises a `Critical` alert. The alert is expected and informative. |
| 4 | **Entities use `127.0.0.1` targets** — correct for localhost dev, but will need updating for any deployed environment where services run on different hosts/ports. | Low | Entities can be updated via admin API once auth is aligned. |
| 5 | **Monitoring Service does not monitor itself** — excluded from the entity set to avoid circular self-monitoring. | Low | Acceptable. The platform's overall health endpoint (`/health`) covers basic liveness. |
| 6 | **No Comms service in entity set** — Comms is in `run-dev.sh` but was not in the CC `system-health-store.ts` seed list and no health URL was confirmed. | Low | Can be added via admin API once its health endpoint port is confirmed. |

---

## 9. Recommended Next Feature

**Recommend: MON-INT-01-003 — Monitoring Auth & Security Alignment**

**Rationale after bootstrap:**
- The bootstrap workaround is needed solely because the admin API (RS256) is
  inaccessible from platform components (HS256). Aligning auth unlocks:
  - Replacing the bootstrap with a proper seed script / idempotent migration
    that calls the real admin API
  - Enabling cross-service monitoring updates from CI/CD pipelines
  - Completing the security posture of the monitoring admin surface
- The read model is now fully operational (10 entities, live data, real alerts).
  The next gap is the write/admin path security, not the read path.
- MON-INT-02-002 (Status Summary Banner) and MON-INT-02-003 (Component Status
  List) are UI features that will be richer once auth is aligned and the admin
  path is usable for entity management.

---

## Files Changed

| File | Action | Purpose |
|---|---|---|
| `apps/services/monitoring/Monitoring.Infrastructure/Bootstrap/MonitoringEntityBootstrap.cs` | **Created** | `IHostedService` that seeds 10 platform entities on first startup if the DB is empty. Uses `MonitoredEntity` domain constructor (all invariants enforced). Idempotent. |
| `apps/services/monitoring/Monitoring.Infrastructure/DependencyInjection.cs` | **Modified** | Added `using Monitoring.Infrastructure.Bootstrap;` and `services.AddHostedService<MonitoringEntityBootstrap>();` |

**No other files were modified.** No UI changes. No gateway changes. No CC
changes. No direct DB writes. The read endpoints and CC `monitoring-source.ts`
from MON-INT-01-002 are consumed unchanged.
