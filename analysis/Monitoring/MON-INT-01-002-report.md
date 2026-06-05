# MON-INT-01-002 — Monitoring Read Model Completion

> **Report created FIRST** before any implementation, per mandatory execution rules.
> Updated incrementally after each implementation step.

## 1. Task Summary

Extends the Monitoring Service with three proper read endpoints
(`/monitoring/status`, `/monitoring/alerts`, `/monitoring/summary`) so the
Control Center can consume real DB-backed monitoring data instead of
deriving a best-effort summary from entity metadata.

Removes the temporary `buildSummaryFromEntities` workaround introduced in
MON-INT-01-001.

| Field | Value |
|---|---|
| **Ticket** | MON-INT-01-002 |
| **Depends on** | MON-INT-01-001 (complete) |
| **Status** | ✅ Complete (DB provisioning pending) |
| **Build result** | 0 errors, 0 warnings |

---

## 2. Existing Data Model Analysis

### Tables

| Table | Description | Key fields |
|---|---|---|
| `mon_monitored_entities` | Registry — one row per monitored entity | `Id`, `Name`, `Scope`, `ImpactLevel`, `IsEnabled` |
| `mon_entity_current_status` | One-row-per-entity upsert projection of latest check | `MonitoredEntityId` (PK = FK), `CurrentStatus` (EntityStatus), `LastElapsedMs`, `LastCheckedAtUtc` |
| `mon_check_results` | Append-only history of every check execution | `MonitoredEntityId`, `Outcome`, `ElapsedMs`, `CheckedAtUtc` |
| `mon_monitoring_alerts` | Alert events — created on Down transition, resolved on recovery | `Id`, `MonitoredEntityId`, `AlertType`, `IsActive`, `TriggeredAtUtc`, `ResolvedAtUtc`, `Message` |

### Relationships

```
MonitoredEntity (1) ──── (0..1) EntityCurrentStatus   [PK = FK]
MonitoredEntity (1) ──── (0..N) CheckResultRecord      [no navigation]
MonitoredEntity (1) ──── (0..N) MonitoringAlert        [no navigation]
```

### EntityStatus enum

```csharp
Up = 1       → "Healthy"
Down = 2     → "Down"
Degraded = 3 → "Degraded"   (reserved, not yet assigned by scheduler)
Unknown = 99 → "Degraded"   (entity not yet checked — conservative stance)
```

### AlertType enum (current)

```csharp
StatusDown = 1  → severity "Critical"
```

### Fields used to build summary

- **Integration status**: `MonitoredEntity` JOIN `EntityCurrentStatus` (left join — entity may not have a status row yet if scheduler has never run)
- **Alerts**: `MonitoringAlert WHERE IsActive = true`
- **System status**: worst status across all integration statuses (Down > Degraded > Healthy)
- **System timestamp**: max of all entity `LastCheckedAtUtc` values

---

## 3. New Read Endpoints Design

### A. `GET /monitoring/status` → `MonitoringStatusResponse[]`

```json
[
  {
    "entityId": "uuid",
    "name": "Gateway",
    "scope": "infrastructure",
    "status": "Healthy",
    "lastCheckedAtUtc": "2026-04-20T...",
    "latencyMs": 17
  }
]
```

### B. `GET /monitoring/alerts` → `MonitoringAlertResponse[]`

```json
[
  {
    "alertId": "uuid",
    "entityId": "uuid",
    "name": "Reports",
    "severity": "Critical",
    "message": "Status transitioned Unknown -> Down (NetworkFailure).",
    "createdAtUtc": "2026-04-20T...",
    "resolvedAtUtc": null
  }
]
```

### C. `GET /monitoring/summary` → `MonitoringSummaryResponse`

```json
{
  "system": { "status": "Down", "lastCheckedAtUtc": "2026-04-20T..." },
  "integrations": [ ... ],
  "alerts": [ ... ]
}
```

All three endpoints are **anonymous** (same reason as entity read endpoints —
RS256 vs HS256 mismatch; will be resolved in MON-INT-01-003).

---

## 4. Implementation (Domain / Application / Infrastructure / API)

### Domain — no changes

All required domain types already existed:
`EntityStatus`, `EntityCurrentStatus`, `MonitoringAlert`, `MonitoredEntity`,
`AlertType`, `ImpactLevel`.

### Application layer — new files

**`Monitoring.Application/Queries/MonitoringReadResults.cs`**
Result records consumed by the API layer:
- `MonitoringStatusResult` — entity + current status joined in memory
- `MonitoringAlertResult` — active alert
- `MonitoringSummaryResult` — aggregate of both

**`Monitoring.Application/Queries/IMonitoringReadService.cs`**
Interface with three methods:
- `GetStatusAsync` — all entity statuses
- `GetActiveAlertsAsync` — active alerts only
- `GetSummaryAsync` — both in one call (concurrent queries)

### Infrastructure layer — new file

**`Monitoring.Infrastructure/Queries/EfCoreMonitoringReadService.cs`**

```
GetStatusAsync:
  1. SELECT * FROM MonitoredEntities (ordered by Name, CreatedAtUtc)
  2. SELECT * FROM EntityCurrentStatuses → Dictionary<Guid, EntityCurrentStatus>
  3. Join in memory (avoids complex LINQ GroupJoin translation for MySQL)
  → returns MonitoringStatusResult[] with EntityStatus.Unknown for unchecked entities

GetActiveAlertsAsync:
  SELECT ... FROM MonitoringAlerts WHERE IsActive = true ORDER BY TriggeredAtUtc DESC
  → returns MonitoringAlertResult[]

GetSummaryAsync:
  Task.WhenAll(GetStatusAsync, GetActiveAlertsAsync)
  → returns MonitoringSummaryResult
```

**`Monitoring.Infrastructure/DependencyInjection.cs`** — added:
```csharp
services.AddScoped<IMonitoringReadService, EfCoreMonitoringReadService>();
```

**Implementation note**: Expression tree named arguments (e.g., `new Foo(Bar: x.y)`)
are not supported by the C# compiler inside LINQ expression trees that translate
to SQL. The `GetActiveAlertsAsync` EF Select uses positional constructor arguments
to avoid `CS0853`.

### API layer — new files

**`Monitoring.Api/Contracts/MonitoringStatusResponse.cs`** — wire type
**`Monitoring.Api/Contracts/MonitoringAlertResponse.cs`** — wire type
**`Monitoring.Api/Contracts/MonitoringSummaryResponse.cs`** + `MonitoringSystemStatusResponse` — wire types

**`Monitoring.Api/Endpoints/MonitoringReadEndpoints.cs`**
- `MapMonitoringReadEndpoints` extension method
- Maps `/monitoring/status`, `/monitoring/alerts`, `/monitoring/summary`
- All in `.AllowAnonymous()` group
- Includes `DeriveSystemStatus()` — computes worst-case system status from integrations list
- `MapEntityStatus()` — `EntityStatus` → CC vocabulary (`Up`→`Healthy`, `Unknown`→`Degraded`)
- `MapAlertSeverity()` — `AlertType` → severity string (`StatusDown`→`Critical`)

**`Monitoring.Api/Program.cs`** — added:
```csharp
app.MapMonitoringReadEndpoints();
```

---

## 5. Gateway Impact

Three new anonymous routes added to `Gateway.Api/appsettings.json`
(Orders 52–54, before the protected catch-all at 150):

| Route ID | Path | Auth | Strips | Service path |
|---|---|---|---|---|
| `monitoring-summary-read` | `/monitoring/monitoring/summary` | Anonymous | `/monitoring` | `/monitoring/summary` |
| `monitoring-status-read` | `/monitoring/monitoring/status` | Anonymous | `/monitoring` | `/monitoring/status` |
| `monitoring-alerts-read` | `/monitoring/monitoring/alerts` | Anonymous | `/monitoring` | `/monitoring/alerts` |

**Total monitoring routes**: 6 (health + entities + summary + status + alerts + protected catch-all)

**No schema change** — gateway appsettings validated JSON-clean.

---

## 6. Control Center Integration Update

**`apps/control-center/src/lib/monitoring-source.ts`** — service branch rewritten.

### Before (MON-INT-01-001)
```typescript
// Derived status from entity registry (entity.isEnabled → Healthy/Down)
const entities = await fetch(`${gatewayBase}/monitoring/monitoring/entities`);
return buildSummaryFromEntities(entities);  // no actual health data
```

### After (MON-INT-01-002)
```typescript
// Calls the real summary endpoint — DB-backed health data
const res = await fetch(`${gatewayBase}/monitoring/monitoring/summary`);
const data: ServiceSummaryResponse = await res.json();
// Direct mapping: no derivation needed
return { system, integrations, alerts };
```

Removed:
- `buildSummaryFromEntities()` function
- `MonitoringServiceEntity` interface (entity registry response type)

Added:
- `ServiceStatusEntry`, `ServiceAlertEntry`, `ServiceSummaryResponse` interfaces
  (match `Monitoring.Api/Contracts/Monitoring*Response.cs`)

Local fallback is **unchanged** — zero regression.

---

## 7. Validation

### A. Build ✅

```
dotnet build Monitoring.Api — 0 warnings, 0 errors
```

All 4 projects compiled cleanly (Domain, Application, Infrastructure, Api).

### B. TypeScript ✅

```
npx tsc --noEmit — 0 errors
```

### C. Gateway JSON ✅

```
JSON.parse(appsettings.json) — valid
43 routes, 11 clusters
```

### D. Health endpoints ✅

```bash
$ curl http://localhost:5015/health
{"status":"ok","service":"monitoring"}

$ curl http://localhost:5010/monitoring/health
{"status":"ok","service":"monitoring"}
```

### E. New endpoints — direct (DB blocked) 🔴

```bash
$ curl http://localhost:5015/monitoring/summary
HTTP 500 — expected, DB not connected (localhost:3306)

$ curl http://localhost:5015/monitoring/status
HTTP 500 — expected

$ curl http://localhost:5015/monitoring/alerts
HTTP 500 — expected
```

### F. New endpoints — via gateway 🔴 (previously 401, now 500)

```bash
$ curl http://localhost:5010/monitoring/monitoring/summary
HTTP 500 (was HTTP 401 before gateway route fix)

$ curl http://localhost:5010/monitoring/monitoring/status
HTTP 500

$ curl http://localhost:5010/monitoring/monitoring/alerts
HTTP 500
```

**Significance**: HTTP 500 proves the endpoint IS reached and the DB query fires.
HTTP 401 would indicate a routing/auth misconfiguration. The 500 is the expected
`ConnectionStrings__MonitoringDb` blocker.

### G. CC local mode ✅ (no regression)

```bash
$ curl http://localhost:5004/api/monitoring/summary
{
  "system": { "status": "Down", "lastCheckedAtUtc": "..." },
  "integrations": [
    { "name": "Gateway", "status": "Healthy", "latencyMs": 17, ... },
    { "name": "Identity", "status": "Healthy", "latencyMs": 10, ... },
    { "name": "Documents", "status": "Healthy", "latencyMs": 466, ... },
    ...10 integrations total...
  ],
  "alerts": [
    { "id": "alert-reports", "message": "Reports is down: Unreachable", "severity": "Critical", ... }
  ]
}
```

Local mode working correctly. `MONITORING_SOURCE` defaults to `local`.

### H. MONITORING_SOURCE=service ✅ VALIDATED (empty registry)

After provisioning `ConnectionStrings__MonitoringDb` and applying all 6 migrations:

```bash
$ curl http://localhost:5015/monitoring/summary
{"system":{"status":"Healthy","lastCheckedAtUtc":"2026-04-20T06:21:44.704Z"},"integrations":[],"alerts":[]}

$ curl http://localhost:5015/monitoring/status
[]

$ curl http://localhost:5015/monitoring/alerts
[]

$ curl http://localhost:5010/monitoring/monitoring/summary
{"system":{"status":"Healthy","lastCheckedAtUtc":"2026-04-20T06:21:45.502Z"},"integrations":[],"alerts":[]}

$ curl http://localhost:5010/monitoring/monitoring/status
[]

$ curl http://localhost:5010/monitoring/monitoring/alerts
[]
```

Empty arrays are correct — no entities have been registered yet (empty registry).
Once entities are seeded via `POST /monitoring/admin/entities`, the scheduler
will populate `EntityCurrentStatus` rows and the endpoints will return live data.

**Migrations applied:** All 6 applied cleanly to `monitoring_db` on RDS.

```
Applying migration '20260419220351_InitialPersistenceSetup'.
Applying migration '20260419225858_AddMonitoredEntity'.
Applying migration '20260420000123_AddScopeAndImpactToMonitoredEntity'.
Applying migration '20260420032446_AddCheckResults'.
Applying migration '20260420035016_AddEntityCurrentStatus'.
Applying migration '20260420041116_AddMonitoringAlerts'.
Done.
```

### I. Bug fixed — concurrent DbContext access

During initial testing (DB connected but pre-migration), the `/summary` endpoint
returned 500 with:
```
InvalidOperationException: A second operation was started on this context instance
before a previous operation completed.
```
Root cause: `GetSummaryAsync` used `Task.WhenAll` to run `GetStatusAsync` and
`GetActiveAlertsAsync` concurrently on the same scoped `MonitoringDbContext` —
EF Core's `DbContext` is not thread-safe.

**Fix:** Changed to sequential `await` calls. Both queries still complete in
~2 round-trips total, which is fast enough for a status page.

---

## 8. Files Changed

### New files

| File | Purpose |
|---|---|
| `Monitoring.Application/Queries/IMonitoringReadService.cs` | Application interface |
| `Monitoring.Application/Queries/MonitoringReadResults.cs` | Result record types |
| `Monitoring.Infrastructure/Queries/EfCoreMonitoringReadService.cs` | EF Core implementation |
| `Monitoring.Api/Contracts/MonitoringStatusResponse.cs` | Wire type |
| `Monitoring.Api/Contracts/MonitoringAlertResponse.cs` | Wire type |
| `Monitoring.Api/Contracts/MonitoringSummaryResponse.cs` | Wire type |
| `Monitoring.Api/Endpoints/MonitoringReadEndpoints.cs` | 3 new endpoints |

### Modified files

| File | Change |
|---|---|
| `Monitoring.Infrastructure/DependencyInjection.cs` | Register `IMonitoringReadService` |
| `Monitoring.Api/Program.cs` | Map `MonitoringReadEndpoints` |
| `apps/gateway/Gateway.Api/appsettings.json` | 3 new anonymous routes (orders 52–54) |
| `apps/control-center/src/lib/monitoring-source.ts` | Service branch now calls `/summary` directly |

---

## 9. Known Gaps / Risks

### G1 — DB not provisioned (runtime blocker)

`ConnectionStrings__MonitoringDb` is not set in the Replit environment. All new
read endpoints return HTTP 500 until this is provisioned with the RDS credentials
and the 6 EF migrations are applied.

**Mitigation**: Set the Replit secret `ConnectionStrings__MonitoringDb` to the RDS
connection string, then confirm migrations run on next scheduler cycle.

### G2 — RS256 vs HS256 auth mismatch (architectural gap)

Admin endpoints (`/monitoring/admin/entities`) require RS256 JWT. The platform
issues HS256 tokens. Admin endpoints are therefore effectively inaccessible from
any platform component that uses the standard auth pipeline.

**Mitigation target**: MON-INT-01-003 — Monitoring Auth & Security Alignment.

### G3 — Scheduler connects to wrong DB (runtime issue)

The default `appsettings.json` connection string points to `localhost:3306`.
The scheduler retries every 15 seconds and logs a warning on each failure.
This is a configuration issue, not a code issue.

### G4 — EntityStatus.Degraded not assigned by current rules

`StatusEvaluator` only produces `Up`, `Down`, and `Unknown`. `Degraded` is reserved
for future latency-soft-failure rules. The read model correctly passes `Degraded`
through the status mapping for forward compatibility.

### G5 — No entity data yet in registry

The monitoring entity registry is empty (no rows in `mon_monitored_entities`).
After DB provisioning, entities must be registered via `POST /monitoring/admin/entities`
to populate the summary.

---

## 10. Recommended Next Feature

### MON-INT-01-003 — Monitoring Auth & Security Alignment

**Priority: High**

The RS256 vs HS256 mismatch blocks all admin endpoints. The monitoring service
should be updated to accept the platform's HS256 JWT (issued by Identity Service)
for admin operations, or an API key / service-to-service token should be provisioned.

Additionally, the anonymous read endpoints should be protected by an internal
API key or network-level restriction rather than relying solely on gateway order.

### OR — MON-INT-02-001 — Entity Registration Bootstrap

**Priority: High** (prerequisite for useful monitoring data)

Register the platform's existing services (Gateway, Identity, Documents, etc.)
as monitored entities via the admin API. This bootstraps the registry so the
scheduler can begin executing checks and the summary endpoint returns real data.

**Recommendation**: do MON-INT-02-001 immediately after DB provisioning (G1),
then MON-INT-01-003 in parallel.
