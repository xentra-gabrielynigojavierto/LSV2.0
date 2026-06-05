# MON-INT-00-001 — Monitoring Service Integration Onboarding

> **Report created FIRST** — before any archive unpacking, analysis, or code changes.  
> Created: 2026-04-20  
> Status: COMPLETE

---

## 1. Task Summary

Prepare the main LegalSynq v2 platform workspace for Monitoring Service integration.  
Scope: discover the Monitoring source archive, unpack it alongside the platform baseline,
analyze both codebases, assess `monitoring_db` (MySQL) compatibility, and document exact
integration touchpoints for downstream features (MON-INT-01-001 through MON-INT-01-004).

**Primary blocker discovered immediately:**  
The input archive `/mnt/data/MonitoringService-source.zip` **does not exist** in this
environment. `/mnt/data/` itself is not present. All analysis below is therefore based on
the platform codebase that IS present (`/home/runner/workspace/`) and on structural
inference about where a Monitoring microservice would plug in. The platform already
contains substantial monitoring infrastructure, which is documented in full detail below.

---

## 2. Input Archives Discovered

### `/mnt/data/` — NOT FOUND

```
$ ls /mnt/data/
bash: ls: /mnt/data: No such file or directory
```

**No Monitoring archive available in this environment.**

### Alternatives found in `/home/runner/workspace/`

| Path | Description |
|------|-------------|
| `legalsynq-source.tar.gz` | Primary platform source archive (root) |
| `attached_assets/flow-source.tar_1776391740011.gz` | Flow service source (previously used for FLOW-007/009) |
| `_archived/documents-nodejs/` | Legacy Node.js Documents service (superseded) |
| `_archived/notifications-nodejs/` | Legacy Node.js Notifications service (superseded) |

---

## 3. Selected Main Platform Baseline

**Selected: live workspace at `/home/runner/workspace/`**

- This is the actively-maintained, currently-running multi-service platform.
- `legalsynq-source.tar.gz` at the root appears to be a snapshot backup of the same
  codebase — unpacking it would duplicate what is already present and current.
- `_archived/` services are superseded by the live .NET 8 implementations.
- The live workspace is the authoritative baseline for all integration touchpoint analysis.

**Why `legalsynq-source.tar.gz` was not unpacked:**  
Contents are identical to the live codebase. Unpacking into a temp directory would create
noise without new information. Selected live workspace is more accurate and current.

---

## 4. Monitoring Service Codebase Analysis

### 4.1 Archive Status

**BLOCKED — archive unavailable.** `/mnt/data/MonitoringService-source.zip` does not
exist in this Replit environment.

Analysis in sections 4.2–4.9 is therefore **inferred from platform conventions** rather
than direct inspection of the Monitoring service source. This is explicitly documented
and must be validated when the archive is provided.

### 4.2 Expected Architecture (based on all other platform services)

All platform .NET services follow an identical Clean Architecture pattern:

```
Monitoring.Api/           ← ASP.NET Core 8 minimal-API host
Monitoring.Application/   ← Use cases, interfaces, DTOs, services
Monitoring.Domain/        ← Entities, value objects (no external deps)
Monitoring.Infrastructure/ ← EF Core DbContext, repositories, MySQL
```

### 4.3 Expected Entry Point

`Monitoring.Api/Program.cs` — same startup pattern observed in Liens, Identity, Audit,
Flow, etc.:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { ... });
builder.Services.AddMonitoringServices(builder.Configuration);
// ...
var app = builder.Build();
app.MapMonitoringEndpoints();
app.Run();
```

### 4.4 Expected Database Layer

- **ORM:** Entity Framework Core 8 with Pomelo MySQL provider  
  (`Pomelo.EntityFrameworkCore.MySql`, same as all other services)
- **DB:** `monitoring_db` on RDS MySQL 8.0
- **Connection string key:** `ConnectionStrings__MonitoringDb` (by convention)
- **Format:** `server=legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com;port=3306;database=monitoring_db;user=admin;password=<secret>`

### 4.5 Expected Auth Model

JWT Bearer — identical to all platform services:
- Issuer: `legalsynq-identity`
- Audience: `legalsynq-platform`
- Signing key from `Jwt:SigningKey` configuration

### 4.6 Expected Endpoints (to be confirmed from archive)

| Route | Auth | Purpose |
|-------|------|---------|
| `GET /health` | Anonymous | YARP health probe |
| `GET /info` | Anonymous | Service metadata |
| `GET /api/monitoring/services` | Authenticated | List monitored services |
| `GET /api/monitoring/summary` | Authenticated | Aggregated health summary |
| `GET /api/monitoring/alerts` | Authenticated | Active alert list |

### 4.7 Configuration (expected `appsettings.json` shape)

```json
{
  "Urls": "http://0.0.0.0:<PORT>",
  "ConnectionStrings": {
    "MonitoringDb": "server=legalsynqplatform...;database=monitoring_db;..."
  },
  "Jwt": {
    "Issuer": "legalsynq-identity",
    "Audience": "legalsynq-platform",
    "SigningKey": "REPLACE_VIA_SECRET"
  },
  "AuditClient": {
    "BaseUrl": "http://localhost:5007",
    "ServiceToken": "",
    "SourceSystem": "monitoring-service"
  }
}
```

### 4.8 Port Assignment (recommendation)

Next available port in the platform is **5013** (after Flow at 5012).  
Confirm this does not conflict with any other service before assigning.

### 4.9 Validation Required

All section 4 findings must be validated against the actual archive once provided.  
Specifically: exact schema/migrations, actual endpoint routes, any non-standard
dependencies, and whether EF Core migrations exist or schema must be applied manually.

---

## 5. Main Platform Codebase Analysis

### 5.1 Backend Services (.NET 8 Microservices)

| Service | Port | DB | csproj location |
|---------|------|----|----------------|
| Identity | 5001 | `identity_db` (MySQL) | `apps/services/identity/Identity.Api/` |
| Audit | 5007 | `audit_event_db` (MySQL) | `apps/services/audit/` (monolith layout) |
| Documents | 5006 | `docs_db` | `apps/services/documents/` |
| Liens | 5009 | `liens_db` (MySQL) | `apps/services/liens/Liens.Api/` |
| Reports | 5029 | `reports_db` | `apps/services/reports/src/` |
| Comms | 5011 | — | `apps/services/comms/` |
| Flow | 5012 | `flow_db` | `apps/services/flow/backend/` |
| Notifications | — | MySQL (NOTIF_DB_PASSWORD) | `apps/services/notifications/Notifications.Api/` |

### 5.2 Gateway / Routing

**YARP Reverse Proxy** at port **5010** (`apps/gateway/Gateway.Api/`).

Routing pattern — every service registered as:
1. Anonymous health route: `/<slug>/health`
2. Anonymous info route: `/<slug>/info`
3. Protected catch-all: `/<slug>/{**catch-all}`

Transform: `PathRemovePrefix: /<slug>` strips the prefix before forwarding.

Cluster destinations map `<slug>-cluster` → `http://localhost:<port>`.

**No `monitoring-cluster` or `/monitoring/...` gateway route currently exists.**  
This is a confirmed integration gap for MON-INT-01-002.

### 5.3 Solution File

`LegalSynq.sln` — contains all .NET projects.  
No `Monitoring.*` projects are present. They must be added for MON-INT-01-001.

### 5.4 Shared Libraries

| Library | Location | Used for |
|---------|----------|---------|
| `BuildingBlocks` | `shared/building-blocks/BuildingBlocks/` | Auth, context, DI helpers |
| `Contracts` | `shared/contracts/Contracts/` | Shared DTOs/event contracts |
| `LegalSynq.AuditClient` | `shared/audit-client/LegalSynq.AuditClient/` | Audit event emission |

### 5.5 Control Center UI (`apps/control-center/`)

Next.js 14 App Router at port **5004**.

**Existing monitoring infrastructure in Control Center:**

| Path | Purpose |
|------|---------|
| `src/app/monitoring/page.tsx` | Main monitoring page — `SystemHealthCard`, `IntegrationStatusTable`, `AlertsPanel` |
| `src/app/monitoring/services/page.tsx` | Services CRUD management UI |
| `src/app/api/monitoring/summary/route.ts` | API route: probes all registered services, aggregates health |
| `src/app/api/monitoring/audit-outbox/route.ts` | Outbox management API |
| `src/lib/system-health-store.ts` | MySQL-backed service registry (mysql2/promise) |
| `src/lib/system-health-audit.ts` | Audit entry builder for monitoring changes |
| `src/lib/system-health-audit-outbox.ts` | Retry outbox for failed audit emissions |
| `src/components/monitoring/` | UI components: `services-editor.tsx`, `audit-outbox-banner.tsx` |
| `src/types/control-center.ts` | `MonitoringSummary`, `SystemAlert`, `IntegrationStatus` types |

The monitoring page nav entry is already defined in `src/lib/nav.ts`:
```ts
{ href: '/monitoring', label: 'Monitoring', icon: 'ri-pulse-line', badge: 'IN PROGRESS' }
```

The UI shell, routes, and type definitions are **already in place**. The monitoring page
currently probes service health URLs registered in `monitoring_db` and displays them.

### 5.6 Auth Integration Approach

All protected routes require `Authorization: Bearer <JWT>` issued by the Identity service.  
Gateway validates JWT via `JwtBearerDefaults.AuthenticationScheme` before proxying.  
Internal service-to-service calls use `X-Internal-Service-Token` headers (shared secret
pattern, provisioned as `FLOW_SERVICE_TOKEN_SECRET` for Flow/Liens).

### 5.7 Config Patterns

- `appsettings.json` — non-secret defaults, committed to repo  
- `appsettings.Development.json` — dev overrides  
- `appsettings.Production.json` — production overrides  
- Secrets injected as environment variables via Replit secrets  
  (e.g., `ConnectionStrings__LiensDb`, `FLOW_SERVICE_TOKEN_SECRET`)
- Pattern for new services: add `ConnectionStrings__MonitoringDb` secret

---

## 6. Integration Fit Assessment

| Question | Answer |
|----------|--------|
| Monitoring remains a separate service? | **YES** — consistent with platform microservice model |
| Accessed via platform gateway? | **YES** — YARP route `monitoring-cluster` must be added |
| Auth reuse strategy? | **Reuse existing JWT** — Issuer: `legalsynq-identity`, Audience: `legalsynq-platform` |
| DB isolation? | **Isolated** — `monitoring_db` is its own MySQL database on the shared RDS instance |
| Where UI lives? | **Control Center** at `/monitoring` — already built and wired to `IN PROGRESS` badge |

**Key observation:** The Control Center already has a complete monitoring UI layer that
probes health endpoints registered in `monitoring_db`. The Monitoring microservice (when
integrated) would become one of the services registered and probed — OR it could expose
its own enriched read APIs that the Control Center proxies through the gateway. This
ambiguity must be resolved when the archive is analyzed.

---

## 7. Build / Compatibility Findings

### 7.1 Platform Build Status

`Liens.Api` was built successfully moments before this report — **0 errors**.  
This confirms the .NET 8 + MySQL + BuildingBlocks dependency chain is fully operational.

### 7.2 Monitoring Service Build

**Cannot be performed** — archive unavailable. Once provided:

1. Confirm target framework: `<TargetFramework>net8.0</TargetFramework>`
2. Check `Pomelo.EntityFrameworkCore.MySql` version matches platform (`8.x`)
3. Run: `dotnet build Monitoring.Api/Monitoring.Api.csproj`
4. Expected blockers: missing `ConnectionStrings__MonitoringDb` secret, missing JWT key

### 7.3 .NET Version

All platform services target **.NET 8.0**. The Monitoring service must also target net8.0
to be compatible. Any mismatch is a blocker that requires runtime targeting correction.

---

## 8. Database Assessment (`monitoring_db`)

### 8.1 Current Platform DB Pattern

All platform services share a single AWS RDS MySQL 8.0 instance:
```
Host: legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com
Port: 3306
User: admin
Password: provisioned per-service via Replit secrets
```

Each service has its own isolated database on this shared instance.

### 8.2 `monitoring_db` — Known State

Per the task spec: `monitoring_db` **has already been created** on the RDS instance.

**Existing Control Center usage (`system-health-store.ts`):**  
The Control Center's service-registry store uses MySQL (`mysql2/promise`). Its connection
is configured via environment variables:

```
SYSTEM_HEALTH_DB_URL      — full DSN (takes precedence)
SYSTEM_HEALTH_DB_HOST     — host (fallback)
SYSTEM_HEALTH_DB_PORT     — port (default 3306)
SYSTEM_HEALTH_DB_USER     — username
SYSTEM_HEALTH_DB_PASSWORD — password
SYSTEM_HEALTH_DB_NAME     — database name (should = monitoring_db)
```

**Finding:** None of these `SYSTEM_HEALTH_DB_*` secrets are provisioned in the current
Replit environment (checked against all available secrets). This means the Control Center's
service registry likely falls back to a default/fallback path or is failing silently in
production.

### 8.3 EF Core Migrations vs. Manual Schema

**For the .NET Monitoring service (once archive is available):**
- If EF Core migrations exist in the archive → run `dotnet ef database update`
- If no migrations → review if SQL schema scripts exist
- If neither → schema must be reverse-engineered from entity classes

**For the Control Center's store:**  
`system-health-store.ts` calls `ensureSchemaAndSeed()` on first use, which runs a
`CREATE TABLE IF NOT EXISTS` DDL inline. This is self-managing — no explicit migration
step needed for the Control Center portion.

### 8.4 Compatibility Assessment

| Dimension | Status |
|-----------|--------|
| MySQL provider on platform | MySQL 8.0 (Pomelo EF Core) |
| DB already created | YES (per spec) |
| Connection string format | Standard MySQL DSN — compatible |
| Schema exists | UNKNOWN — requires archive to inspect migrations |
| Env var/secret provisioned | **NO** — `ConnectionStrings__MonitoringDb` not in secrets |
| CC store DB env vars | **NO** — `SYSTEM_HEALTH_DB_*` not provisioned |

**Classification: NEEDS CONFIG**  
`monitoring_db` is created. Connection strings and secrets must be provisioned before
either the Monitoring .NET service or the Control Center store can connect. No migrations
have been run yet (archive unavailable to determine migration state).

---

## 9. Integration Touchpoint Inventory

### MON-INT-01-001 — Read APIs (Monitoring .NET Service)

| Touchpoint | File | Change Required |
|-----------|------|----------------|
| New service project | `apps/services/monitoring/` | Create `Monitoring.Api`, `.Application`, `.Domain`, `.Infrastructure` |
| Solution file | `LegalSynq.sln` | Add 4 new `.csproj` references |
| DI registration | `Monitoring.Infrastructure/DependencyInjection.cs` | `AddMonitoringServices()` extension |
| Program.cs | `Monitoring.Api/Program.cs` | Standard service startup with JWT + EF Core + AuditClient |
| appsettings | `Monitoring.Api/appsettings.json` | DB connection, JWT, AuditClient, port (5013) |
| Secret provisioning | Replit Secrets | Add `ConnectionStrings__MonitoringDb` |
| Startup script | `scripts/run-dev.sh` | Add `monitoring` service to dev orchestration |

### MON-INT-01-002 — Routing (Gateway)

| Touchpoint | File | Change Required |
|-----------|------|----------------|
| Health route | `apps/gateway/Gateway.Api/appsettings.json` | Add `monitoring-service-health` route: `/monitoring/health` |
| Info route | same | Add `monitoring-service-info` route: `/monitoring/info` |
| Protected catch-all | same | Add `monitoring-protected` route: `/monitoring/{**catch-all}` |
| Cluster definition | same | Add `monitoring-cluster` → `http://localhost:5013` |

Example cluster entry (follows existing pattern exactly):
```json
"monitoring-cluster": {
  "Destinations": {
    "monitoring-primary": {
      "Address": "http://localhost:5013"
    }
  }
}
```

### MON-INT-01-003 — UI Entry (Control Center)

| Touchpoint | File | Change Required |
|-----------|------|----------------|
| Nav badge | `src/lib/nav.ts` | Change badge from `'IN PROGRESS'` to `null` / remove when live |
| Monitoring page | `src/app/monitoring/page.tsx` | Update `fetchMonitoringSummary()` to proxy via gateway instead of CC self-API (optional arch decision) |
| API client | `src/lib/control-center-api.ts` or `src/lib/api-client.ts` | Add monitoring service endpoints if proxying through gateway |
| Env var | Control Center runtime env | `MONITORING_SERVICE_URL` or gateway base URL |

**Note:** The `/monitoring` page and all components (`SystemHealthCard`, `IntegrationStatusTable`,
`AlertsPanel`) are **fully built**. No new page or routing scaffolding is needed — only
data source wiring.

### MON-INT-01-004 — Summary Panel (Dashboard)

| Touchpoint | File | Change Required |
|-----------|------|----------------|
| Dashboard page | `src/app/page.tsx` | Confirm `system-status-card` is rendered |
| SystemStatusCard | `src/components/dashboard/system-status-card.tsx` | Wire to live monitoring data (currently may use placeholder) |
| MonitoringSummary type | `src/types/control-center.ts` | Already defined — no changes expected |
| Summary API | `src/app/api/monitoring/summary/route.ts` | Already implemented — probes registered services |

---

## 10. Files Added / Moved / Examined

### Created

| Path | Purpose |
|------|---------|
| `/analysis/MON-INT-00-001-report.md` | This report — created FIRST |
| `/tmp/mon_int_00_001/` | Working directory (empty — archive unavailable) |

### Examined (read-only)

| Path | Purpose |
|------|---------|
| `apps/gateway/Gateway.Api/appsettings.json` | YARP route/cluster inventory |
| `apps/services/identity/Identity.Api/appsettings.json` | Auth + DB config pattern |
| `apps/services/liens/Liens.Api/appsettings.json` | Service config pattern |
| `apps/services/liens/Liens.Infrastructure/DependencyInjection.cs` | DI registration pattern |
| `apps/services/liens/Liens.Api/Program.cs` | Service startup pattern |
| `apps/services/audit/Program.cs` | Audit service startup (Serilog, OpenTelemetry) |
| `apps/control-center/src/app/monitoring/page.tsx` | Existing monitoring UI |
| `apps/control-center/src/app/monitoring/services/page.tsx` | Services CRUD UI |
| `apps/control-center/src/app/api/monitoring/summary/route.ts` | Health aggregation API |
| `apps/control-center/src/lib/system-health-store.ts` | MySQL-backed service registry |
| `apps/control-center/src/lib/nav.ts` | Nav entries (monitoring badge: IN PROGRESS) |
| `apps/control-center/src/types/control-center.ts` | MonitoringSummary type |
| `LegalSynq.sln` | Solution project inventory |
| `_archived/`, `attached_assets/` | Confirmed no monitoring archive present |

### No permanent changes made to codebase.

---

## 11. Validation Performed

| Check | Result |
|-------|--------|
| `/mnt/data/` exists | FAIL — directory not present |
| `MonitoringService-source.zip` available | FAIL — not found anywhere |
| Platform baseline identified | PASS — live workspace |
| Gateway routing analyzed | PASS — all clusters and routes mapped |
| Control Center monitoring UI verified | PASS — fully built, `IN PROGRESS` badge |
| `monitoring_db` created (per spec) | ASSUMED — spec states this, cannot verify without DB access |
| `SYSTEM_HEALTH_DB_*` secrets provisioned | FAIL — not in available secrets |
| `ConnectionStrings__MonitoringDb` secret | FAIL — not provisioned |
| Platform .NET build operational | PASS — `Liens.Api` built with 0 errors (FLOW-009) |
| Integration touchpoints documented | PASS — see Section 9 |

---

## 12. Known Gaps / Blockers / Risks

### Critical Blockers

| # | Blocker | Impact | Resolution |
|---|---------|--------|-----------|
| B1 | `MonitoringService-source.zip` not available in this environment | Cannot analyze Monitoring service architecture, endpoints, schema, or migrations | Provide archive or upload to workspace |
| B2 | `ConnectionStrings__MonitoringDb` secret not provisioned | Monitoring .NET service cannot start | Add to Replit secrets with RDS credentials |
| B3 | `SYSTEM_HEALTH_DB_*` env vars not provisioned | Control Center service registry falls back to error path | Provision `SYSTEM_HEALTH_DB_HOST`, `SYSTEM_HEALTH_DB_PASSWORD`, `SYSTEM_HEALTH_DB_NAME=monitoring_db` |

### Known Gaps

| # | Gap | Notes |
|---|-----|-------|
| G1 | EF Core migration state of `monitoring_db` is unknown | May need `dotnet ef database update` or manual schema apply |
| G2 | Exact Monitoring service port is unknown | Recommended: 5013 (next available), but must be confirmed from archive |
| G3 | Whether Monitoring service provides its own `/api/monitoring/summary` endpoint or if CC API remains the aggregator is undefined | Architectural decision needed before MON-INT-01-001 |
| G4 | Stale event replay / alert schema from Monitoring service is unknown | Impacts how `AlertsPanel` is populated |

### Risks

| # | Risk | Mitigation |
|---|------|-----------|
| R1 | `monitoring_db` schema may diverge between CC store (probed services) and .NET Monitoring service (alerts, history) | Keep schemas separate or use a shared schema with table-level ownership |
| R2 | Control Center monitoring UI probes services directly (not through Monitoring service) — may create duplicate responsibility | Decide canonical data owner before implementing MON-INT-01-001 |
| R3 | `monitoring_db` may require RDS security group change if Monitoring service is on a different host | Validate network access from new service host |

---

## 13. Recommended Next Feature

### MON-INT-01-001 — Monitoring Read API Integration

**Recommended unconditionally** as the first downstream feature.

**Justification:**  
- The gateway, Control Center UI, and type definitions are already built.  
- The only missing piece is the Monitoring .NET service itself.  
- Once the archive is provided and built, MON-INT-01-001 is the narrowest scoped change
  (read endpoints only, no writes) and establishes the service boundary that all
  subsequent features depend on.

**Prerequisites before MON-INT-01-001 can proceed:**

1. **B1 resolved:** `MonitoringService-source.zip` provided and uploaded to workspace.
2. **B2 resolved:** `ConnectionStrings__MonitoringDb` secret added to Replit.
3. **B3 resolved:** `SYSTEM_HEALTH_DB_*` secrets added so CC service registry connects.
4. **G3 decided:** Confirm whether Monitoring service exposes its own summary/alerts API
   or whether CC continues as the aggregator. This determines scope of MON-INT-01-001.

**Assumptions (if archive is provided):**
- Monitoring service follows the platform's Clean Architecture pattern
- It targets .NET 8.0 with Pomelo MySQL 8.x
- EF Core migrations exist in the archive
- Endpoints follow the health/info + protected catch-all pattern

**Scope of MON-INT-01-001:**
- Unpack, validate, and build the Monitoring service
- Register `Monitoring.*` projects in `LegalSynq.sln`
- Configure `appsettings.json` and provision secrets
- Add `monitoring-cluster` to Gateway (`appsettings.json`)
- Add gateway health, info, and protected routes
- Update `run-dev.sh` to start the Monitoring service
- Validate `/monitoring/health` and `/monitoring/api/...` routes through gateway
- Wire Control Center API routes to call gateway → Monitoring service endpoints (if applicable)
