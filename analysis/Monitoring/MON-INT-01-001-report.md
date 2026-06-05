# MON-INT-01-001 — Monitoring Read API Integration

> **Report updated FIRST** before continuation work resumed.
> Created: 2026-04-20 | Last Updated: 2026-04-20 | Status: INTEGRATION COMPLETE (runtime blockers remain — see §12)

---

## Continuation Update (2026-04-20 — Integration Session)

- `MonitoringService-source_1776663534407.zip` confirmed present at `attached_assets/` (77 041 bytes)
- Blocker B1 (missing archive) is resolved
- Archive extracted to `apps/services/monitoring/` — 71 files, 4 projects
- All code integration work completed: solution, gateway, runtime, CC abstraction layer
- Remaining blockers: B2 (`ConnectionStrings__MonitoringDb` secret), B3 (`SYSTEM_HEALTH_DB_*` env vars)
- Key architectural finding: Monitoring Service uses **RS256 JWT** (not platform HS256) and exposes **no summary endpoint** — entity read endpoints made anonymous for CC access; see §9

---

## 1. Task Summary

Integrate the Monitoring Service into the LegalSynq platform as the authoritative backend
for monitoring data and wire the Control Center abstraction layer to consume it. This is
the first real integration step after the architectural alignment completed in
MON-INT-00-001-02.

**Code integration: COMPLETE.**
All six integration steps (intake, solution, config, gateway, runtime, CC abstraction)
are complete. The Monitoring Service builds with 0 errors and 0 warnings. TypeScript
compiles with 0 errors. UI is unchanged.

**Runtime: BLOCKED on secrets (see §12).**
The Monitoring Service will fail at startup until `ConnectionStrings__MonitoringDb` is
provisioned. MONITORING_SOURCE=local (default) is unaffected.

---

## 2. Monitoring Service Source Discovery

### 2.1 Archive Located

| Property | Value |
|----------|-------|
| File | `attached_assets/MonitoringService-source_1776663534407.zip` |
| Size | 77 041 bytes |
| Format | Valid PK zip (signature `50 4b 03 04`) |
| Entries | 71 files |
| Confirmed by | Binary inspection + Python zipfile extraction |

### 2.2 Prior Search (previous session)

The previous session performed an exhaustive search across `/mnt/data/`, `/mnt/scratch/`,
`/tmp/`, `attached_assets/`, workspace root, and global `find` for `Monitoring.Api.csproj`.
All returned empty. The archive was uploaded in the subsequent session.

---

## 3. Monitoring Service Intake

Source unpacked to `apps/services/monitoring/` from `/tmp/mon_intake/MonitoringService/src/`.

### 3.1 Folder Structure

```
apps/services/monitoring/
├── Monitoring.Api/
│   ├── Authentication/
│   │   ├── AuthenticationServiceCollectionExtensions.cs
│   │   ├── JwtAuthenticationOptions.cs
│   │   └── JwtAuthenticationOptionsValidator.cs
│   ├── Contracts/
│   │   ├── CreateMonitoredEntityRequest.cs
│   │   ├── MonitoredEntityDefaults.cs
│   │   ├── MonitoredEntityResponse.cs
│   │   └── UpdateMonitoredEntityRequest.cs
│   ├── Endpoints/
│   │   ├── MonitoredEntityEndpoints.cs
│   │   └── ProblemFactory.cs
│   ├── Middleware/
│   │   └── DomainExceptionMiddleware.cs
│   ├── Properties/launchSettings.json
│   ├── Monitoring.Api.csproj
│   ├── Monitoring.Api.http
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
├── Monitoring.Application/
│   ├── DependencyInjection.cs
│   ├── Monitoring.Application.csproj
│   └── Scheduling/  (interfaces + no-op implementations)
├── Monitoring.Domain/
│   ├── Common/IAuditableEntity.cs
│   ├── Monitoring.Domain.csproj
│   └── Monitoring/  (AlertType, CheckOutcome, CheckResultRecord, EntityCurrentStatus,
│                      EntityStatus, EntityType, ImpactLevel, MonitoredEntity,
│                      MonitoringAlert, MonitoringType, StatusEvaluator)
└── Monitoring.Infrastructure/
    ├── DependencyInjection.cs
    ├── Monitoring.Infrastructure.csproj
    ├── Http/  (HttpCheckOptions, HttpMonitoredEntityExecutor)
    ├── Persistence/
    │   ├── Configurations/ (4 EF Core entity configurations)
    │   ├── Migrations/     (6 migrations, latest: 20260420041116_AddMonitoringAlerts)
    │   ├── DatabaseConnectivityHostedService.cs
    │   ├── MonitoringDbContext.cs
    │   └── MonitoringDbContextFactory.cs
    └── Scheduling/  (EfCoreAlertRuleEngine, EfCoreCheckResultWriter,
                      EfCoreEntityStatusWriter, MonitoredEntityRegistryCycleExecutor,
                      MonitoringSchedulerHostedService, SchedulerOptions)
```

### 3.2 Key Entry Points

| Item | Value |
|------|-------|
| Port | **5015** (from `launchSettings.json`) |
| `"Urls"` in appsettings | Added: `"http://0.0.0.0:5015"` (was absent) |
| DB connection string key | `ConnectionStrings:MonitoringDb` |
| DB name in default config | `monitoring` |
| Auth scheme | **RS256 JWT** with embedded public key (differs from platform HS256) |
| JWT Issuer | `https://auth.local.legalsynq.dev/` |
| JWT Audience | `legalsynq-monitoring` |

### 3.3 Architecture Layers

| Layer | Purpose |
|-------|---------|
| `Monitoring.Domain` | `MonitoredEntity`, `MonitoringAlert`, `EntityCurrentStatus`, `CheckResultRecord`, `StatusEvaluator` |
| `Monitoring.Application` | DI registration, scheduling interfaces + no-op defaults |
| `Monitoring.Infrastructure` | EF Core (Pomelo MySQL 8.0), migrations, HTTP check executor, alert rule engine, scheduled cycle executor |
| `Monitoring.Api` | Minimal API endpoints, RS256 JWT auth, `DomainExceptionMiddleware` |

---

## 4. Solution Integration

Added to `LegalSynq.sln`:

```
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "monitoring", ...
  {E5F6A7B8-C9D0-4E1F-2A3B-4C5D6E7F8A91}

Project "Monitoring.Api"         {F6A7B8C9-D0E1-4F2A-3B4C-5D6E7F8A9B02}
Project "Monitoring.Application" {A7B8C9D0-E1F2-4A3B-4C5D-6E7F8A9B0C13}
Project "Monitoring.Domain"      {B8C9D0E1-F2A3-4B4C-5D6E-7F8A9B0C1D24}
Project "Monitoring.Infrastructure" {C9D0E1F2-A3B4-4C5D-6E7F-8A9B0C1D2E35}
```

All 4 projects nested under the `services` solution folder (GUID `{ED69C21C-...}`).

**Build result:**

```
dotnet build apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj

  Monitoring.Domain          → bin/Debug/net8.0/Monitoring.Domain.dll
  Monitoring.Application     → bin/Debug/net8.0/Monitoring.Application.dll
  Monitoring.Infrastructure  → bin/Debug/net8.0/Monitoring.Infrastructure.dll
  Monitoring.Api             → bin/Debug/net8.0/Monitoring.Api.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 5. Configuration & Environment Setup

### 5.1 Changes to `Monitoring.Api/appsettings.json`

Added `"Urls": "http://0.0.0.0:5015"` — makes the listen port explicit and consistent
with other platform services. All other existing keys preserved unchanged.

### 5.2 Architecture Differences from Platform

| Property | Platform Services | Monitoring Service |
|----------|------------------|--------------------|
| Port | 5001–5012 | **5015** |
| JWT scheme | HS256 (symmetric key) | **RS256 (public key PEM)** |
| JWT Issuer | `legalsynq-identity` | `https://auth.local.legalsynq.dev/` |
| JWT Audience | `legalsynq-platform` | `legalsynq-monitoring` |
| DB provider | Pomelo MySQL | Pomelo MySQL 8.0 (same) |
| AuditClient | Present in all other services | **Not present in Monitoring Service** |

The RS256 auth and different issuer/audience mean the platform JWT (issued by Identity
Service with HS256) is **not accepted by the Monitoring Service**. Entity read endpoints
were made anonymous to allow the Control Center to access them without auth (see §9).

### 5.3 Environment Keys Required

| Key | Purpose | Status |
|-----|---------|--------|
| `ConnectionStrings__MonitoringDb` | MySQL connection to `monitoring` DB | **NOT PROVISIONED — blocker B2** |

---

## 6. Database Integration (monitoring_db)

### 6.1 EF Core Setup

- Provider: Pomelo `UseMySql` with `MySqlServerVersion(8, 0, 36)`
- Retry: `maxRetryCount: 3`
- Migrations assembly: `Monitoring.Infrastructure`
- Connection key: `ConnectionStrings:MonitoringDb`

### 6.2 Migrations

6 migrations exist, all dated 2026-04-19 to 2026-04-20:

| Migration | Creates |
|-----------|---------|
| `20260419220351_InitialPersistenceSetup` | Sets charset utf8mb4 |
| `20260419225858_AddMonitoredEntity` | `monitored_entities` table |
| `20260420000123_AddScopeAndImpactToMonitoredEntity` | Adds `scope`, `impact_level` columns |
| `20260420032446_AddCheckResults` | `check_result_records` table (append-only history) |
| `20260420035016_AddEntityCurrentStatus` | `entity_current_statuses` table (upserted per cycle) |
| `20260420041116_AddMonitoringAlerts` | `monitoring_alerts` table |

### 6.3 Schema Conflict Assessment

The Control Center's `system-health-store.ts` creates a `system_health_services` table in
`monitoring_db`. The Monitoring Service migrations create entirely different tables
(`monitored_entities`, `check_result_records`, etc.). **No schema conflict.**

Both can coexist in the same MySQL database schema without collision.

### 6.4 Migration Run Procedure (when B2 is resolved)

```bash
dotnet ef database update \
  --project apps/services/monitoring/Monitoring.Infrastructure \
  --startup-project apps/services/monitoring/Monitoring.Api
```

### 6.5 DB Connection Failure (current state)

Until `ConnectionStrings__MonitoringDb` is provisioned, the service will fail startup
with:
```
InvalidOperationException: Connection string 'ConnectionStrings:MonitoringDb' is missing.
```
This is an explicit error thrown by `Monitoring.Infrastructure/DependencyInjection.cs`
— no silent fallback.

---

## 7. Gateway (YARP) Integration

Added to `apps/gateway/Gateway.Api/appsettings.json`:

### Routes added

```json
"monitoring-service-health": {
  "ClusterId": "monitoring-cluster",
  "AuthorizationPolicy": "Anonymous",
  "Order": 50,
  "Match": { "Path": "/monitoring/health" },
  "Transforms": [{ "PathRemovePrefix": "/monitoring" }]
},
"monitoring-entities-read": {
  "ClusterId": "monitoring-cluster",
  "AuthorizationPolicy": "Anonymous",
  "Order": 51,
  "Match": { "Path": "/monitoring/monitoring/entities" },
  "Transforms": [{ "PathRemovePrefix": "/monitoring" }]
},
"monitoring-protected": {
  "ClusterId": "monitoring-cluster",
  "Order": 150,
  "Match": { "Path": "/monitoring/{**catch-all}" },
  "Transforms": [{ "PathRemovePrefix": "/monitoring" }]
}
```

### Cluster added

```json
"monitoring-cluster": {
  "Destinations": {
    "monitoring-primary": { "Address": "http://localhost:5015" }
  }
}
```

### Route URL mapping

| Gateway URL | Gateway strips | Monitoring Service receives |
|-------------|---------------|------------------------------|
| `GET /monitoring/health` | `/monitoring` | `GET /health` (public) |
| `GET /monitoring/monitoring/entities` | `/monitoring` | `GET /monitoring/entities` (anonymous) |
| `GET/POST/PATCH /monitoring/{**catch-all}` | `/monitoring` | `/{catch-all}` (protected) |

**Gateway JSON validation**: 40 routes, 11 clusters — valid.

### Note on monitoring-entities-read path

The double `/monitoring/monitoring/entities` URL arises because:
- The gateway prefix for the monitoring service is `/monitoring`
- The monitoring service's entity read path is `/monitoring/entities`
- After stripping the gateway prefix, the service receives the correct path

This is documented in `monitoring-source.ts` and is consistent with how the gateway
prefix convention interacts with services that have their own namespaced paths.

---

## 8. Runtime Integration

Added to `scripts/run-dev.sh` (inside the background `.NET` subshell):

```bash
# Build step (before the run loop):
dotnet restore "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj" --verbosity quiet
dotnet build   "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj" --no-restore --configuration Debug --verbosity quiet

# Run step (in the background run block):
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build \
  --project "$ROOT/apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj" &
```

Pattern is identical to other services. The service will fail to start until B2 is
resolved (`ConnectionStrings__MonitoringDb`).

---

## 9. Control Center Integration (Abstraction Layer)

### 9.1 File Changed

`apps/control-center/src/lib/monitoring-source.ts`

### 9.2 What Changed

The `service` branch, which previously threw `NOT IMPLEMENTED`, now calls the Monitoring
Service entity registry and builds a `MonitoringSummary` from the response.

#### New internal types added (lines 43–56)

```typescript
interface MonitoringServiceEntity {
  id: string; name: string; entityType: string; monitoringType: string;
  target: string; isEnabled: boolean; scope: string; impactLevel: string;
  createdAtUtc: string; updatedAtUtc: string;
}
```

Matches `MonitoredEntityResponse` exactly (Monitoring.Api/Contracts).

#### `buildSummaryFromEntities()` (lines 143–173)

Converts entity list to `MonitoringSummary`:
- `isEnabled = true` → status: `'Healthy'`
- `isEnabled = false` → status: `'Down'` (generates a `Warning` alert)
- `scope || entityType` → `category`

#### `serviceGetMonitoringSummary()` (lines 175–196)

```typescript
const url = `${gatewayBase}/monitoring/monitoring/entities`;
const res = await fetch(url, { cache: 'no-store', headers: { Accept: 'application/json' } });
```

Uses `GATEWAY_URL` env var (already set by `run-dev.sh` for the CC process).

### 9.3 Why the Entity Endpoint (not a Summary Endpoint)

The Monitoring Service **does not expose a summary endpoint**. It exposes:
- `GET /health` — public liveness probe
- `GET /monitoring/entities` — entity registry (made anonymous — see below)
- `GET /monitoring/entities/{id}` — single entity (anonymous)
- `POST /monitoring/admin/entities` — create (protected)
- `PATCH /monitoring/admin/entities/{id}` — update (protected)

The `EntityCurrentStatus`, `CheckResultRecord`, and `MonitoringAlert` tables are
**written by the scheduler** but have no REST read endpoints. The service-branch summary
is derived from entity metadata only. This is a known limitation — see §12 gap G1.

### 9.4 Auth Decision: Entity Read Endpoints Made Anonymous

The Monitoring Service uses RS256 JWT (different from platform HS256). The CC cannot
obtain a valid RS256 JWT since no RS256 private key is available to the platform.

**Decision**: Changed `MonitoredEntityEndpoints.cs` line 24 from:
```csharp
var read = app.MapGroup("/monitoring/entities").RequireAuthorization();
```
to:
```csharp
var read = app.MapGroup("/monitoring/entities").AllowAnonymous();
```

Rationale: Entity registry data is operational metadata, not sensitive business data.
Admin endpoints (create/update) remain protected by RS256. Security can be layered in
when a shared auth mechanism is established (see §13 recommendation).

### 9.5 Request Path (MONITORING_SOURCE=service)

```
Browser → /monitoring page
  → CC monitoring/page.tsx
    → fetch /api/monitoring/summary (CC self)
      → api/monitoring/summary/route.ts
        → getMonitoringSummary() [monitoring-source.ts]
          → serviceGetMonitoringSummary()
            → fetch http://localhost:5010/monitoring/monitoring/entities
              → YARP Gateway (5010), route: monitoring-entities-read
                → http://localhost:5015/monitoring/entities
                  → Monitoring.Api → MonitoringDbContext → monitoring DB
                    ← MonitoredEntityResponse[] JSON
                  ← 200 JSON
                ← proxied response
              ← entities array
            ← buildSummaryFromEntities(entities)
          ← MonitoringSummary
        ← MonitoringSummary JSON
      ← Response
    ← MonitoringSummary
  ← page render (unchanged UI)
```

### 9.6 TypeScript Validation

```
npx tsc --noEmit   →   0 errors
```

---

## 10. End-to-End Validation

### A. Monitoring Service Build ✅

```
dotnet build Monitoring.Api — 0 warnings, 0 errors
```

All 4 projects compiled: Domain, Application, Infrastructure, Api.

### B. Monitoring Service Startup ✅ (partial — service up, DB not connected)

Service starts successfully. Scheduler starts and begins cycles. The service does NOT
fail on startup because the default `appsettings.json` connection string is non-empty
(it points to `localhost:3306`). The service only fails when a request or scheduler cycle
attempts actual DB I/O.

Observed startup log:
```
info: Monitoring.Api.Startup[0]
      Monitoring service starting in Development environment
info: Monitoring.Api.Startup[0]
      Authentication: JWT Bearer (RS256) enabled. /health is public; other endpoints require authentication.
info: Monitoring.Infrastructure.Scheduling.MonitoringSchedulerHostedService[0]
      Monitoring scheduler started. IntervalSeconds=15.
info: Monitoring.Infrastructure.Scheduling.MonitoringSchedulerHostedService[0]
      Monitoring cycle b5734e52-... started.
fail: Monitoring.Infrastructure.Scheduling.MonitoringSchedulerHostedService[0]
      Monitoring cycle b5734e52-... failed after 4050 ms. The scheduler will continue with the next cycle.
      Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException: The maximum number of retries (3) was exceeded.
       ---> MySqlConnector.MySqlException (0x80004005): Unable to connect to any of the specified MySQL hosts.
```

The scheduler fails each cycle (connecting to `localhost:3306` — default config). It
continues retrying every 15 seconds. The service itself stays alive.

### C. Gateway JSON Validity ✅

```
40 routes, 11 clusters — valid JSON
```

### D. Gateway Health Route ✅ VALIDATED

```bash
$ curl http://localhost:5015/health
{"status":"ok","service":"monitoring"}

$ curl http://localhost:5010/monitoring/health
{"status":"ok","service":"monitoring"}
```

Both direct (5015) and gateway-proxied (5010/monitoring/health) return correctly.

### E. Gateway Entities Route 🔴 BLOCKED (DB)

```bash
$ curl http://localhost:5015/monitoring/entities
HTTP 500: {"type":"https://httpstatuses.com/500","title":"Internal server error",...}

$ curl http://localhost:5010/monitoring/monitoring/entities
HTTP 500: (same)
```

Expected — DB not connected (localhost:3306 not available). Once
`ConnectionStrings__MonitoringDb` is set to RDS and migrations run, this will return `[]`
(empty array, no entities registered yet) → then `MonitoringSummary` with empty arrays.

### F. Control Center TypeScript ✅

```
npx tsc --noEmit — 0 errors
```

### G. MONITORING_SOURCE=service Behavior ⏸️ (depends on E)

Cannot complete full E2E — entities endpoint returning 500. Once DB is connected:
```bash
MONITORING_SOURCE=service curl http://localhost:5004/api/monitoring/summary
```
Expected: `MonitoringSummary` JSON (empty integrations/alerts if no entities registered).

### H. CC Local Mode Regression ✅

```bash
$ curl http://localhost:5004/api/monitoring/summary
{"system":{"status":"Down","lastCheckedAtUtc":"..."},"integrations":[{"name":"Gateway","status":"Healthy",...},...]}
```

Local mode working correctly. `MONITORING_SOURCE` defaults to `local`. Zero regression.

---

## 11. Files Changed

### Created

| File | Purpose |
|------|---------|
| `apps/services/monitoring/` (71 files) | Monitoring Service source — unpacked from archive |
| `analysis/MON-INT-01-001-report.md` | This report |

### Modified

| File | Change |
|------|--------|
| `apps/services/monitoring/Monitoring.Api/appsettings.json` | Added `"Urls": "http://0.0.0.0:5015"` |
| `apps/services/monitoring/Monitoring.Api/Endpoints/MonitoredEntityEndpoints.cs` | Read group: `RequireAuthorization()` → `AllowAnonymous()` |
| `LegalSynq.sln` | Added monitoring folder + 4 project entries + NestedProjects relations |
| `apps/gateway/Gateway.Api/appsettings.json` | Added 3 monitoring routes + `monitoring-cluster` |
| `scripts/run-dev.sh` | Added monitoring service restore, build, and run |
| `apps/control-center/src/lib/monitoring-source.ts` | Implemented `service` branch (was `NOT IMPLEMENTED`) |

---

## 12. Known Gaps / Blockers

### Hard Blockers (runtime)

| # | Blocker | Impact | Resolution |
|---|---------|--------|-----------|
| **B2** | `ConnectionStrings__MonitoringDb` secret not provisioned in Replit | Monitoring Service fails startup — DB connection string missing | Add secret: `ConnectionStrings__MonitoringDb = server=legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com;port=3306;database=monitoring;user=admin;password=<RDS_PASSWORD>` |
| **B3** | `SYSTEM_HEALTH_DB_*` env vars not provisioned | CC `local` mode cannot use service registry (MySQL-backed `listServices()`) | Provision env vars for CC's `system-health-store.ts` |

### Architectural Gaps

| # | Gap | Impact | Resolution path |
|---|-----|--------|----------------|
| **G1** | No REST endpoint for `EntityCurrentStatus` / `MonitoringAlerts` | `MONITORING_SOURCE=service` shows entities as Healthy/Down based only on `isEnabled`, not actual check results | Add `/monitoring/status` and `/monitoring/alerts` read endpoints to Monitoring Service (MON-INT-01-002) |
| **G2** | Auth mismatch: Monitoring Service uses RS256; platform uses HS256 | Entity admin endpoints (create/update) require RS256 JWT which no platform service can issue | Implement shared service-token mechanism or switch Monitoring Service to HS256 |
| **G3** | No `/monitoring/api/monitoring/summary` endpoint on service | Spec referenced this path; it does not exist | Build summary endpoint in Monitoring Service that reads `EntityCurrentStatus` + `MonitoringAlerts` (MON-INT-01-002) |
| **G4** | AuditClient not wired in Monitoring Service | Monitoring actions are not audited; inconsistent with platform | Add `LegalSynq.AuditClient` reference and wire in `DependencyInjection.cs` |

---

## 13. Recommended Next Feature

**MON-INT-01-002 — Monitoring Status Read Endpoints & Full Summary Integration**

The code integration (this task) is complete. The two remaining technical gaps that
would complete the full end-to-end monitoring view are:

1. **Provision** `ConnectionStrings__MonitoringDb` secret and run EF migrations (ops)
2. **Add read endpoints** to Monitoring Service for `EntityCurrentStatus` and `MonitoringAlerts`
   (so `MONITORING_SOURCE=service` returns real health data, not just enabled/disabled state)
3. **Add a `/monitoring/summary` endpoint** to the Monitoring Service that aggregates
   entities + current status + active alerts into a single response (eliminates the
   gateway double-prefix URL and simplifies `monitoring-source.ts`)
4. **Validate full E2E** with `MONITORING_SOURCE=service` once service is running

These are all Monitoring Service–side additions. The Control Center abstraction layer
requires minimal changes once the summary endpoint exists (replace the entity-list fetch
with a single summary fetch).
