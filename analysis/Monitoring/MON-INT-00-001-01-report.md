# MON-INT-00-001-01 — Monitoring Service Availability & Architecture Alignment

> **Report created FIRST** — before any search, code inspection, or file modification.
> Created: 2026-04-20 | Status: COMPLETE

---

## 1. Task Summary

Re-check whether the Monitoring Service source archive is available anywhere in the
environment. If found, unpack and inventory it. If not, prove it with evidence. In parallel,
reclassify all existing Control Center monitoring logic against the locked architectural
rule (Monitoring Service = source of truth; Control Center = read-only consumer), assess
DB/config prerequisites, and apply minimal safe code alignment. Recommend the correct
next feature based on findings.

---

## 2. Architecture Rule Confirmed

> **LOCKED — non-negotiable:**
> - Monitoring Service = source of truth for health status, alerts, and monitoring summaries
> - Control Center = read-only consumer of Monitoring Service data

Implication: The Control Center must not execute health probes, own alert logic, or maintain
a persistent monitoring engine. It may own its configuration UI (which services to monitor)
only as a thin admin interface that delegates state to the Monitoring Service. All of this
is documented in the reclassification below.

---

## 3. Monitoring Source Discovery

### 3.1 Exhaustive Search — Locations Checked

| Location | Command | Result |
|----------|---------|--------|
| `/mnt/data/` | `ls /mnt/data/` | **Directory does not exist** |
| `/mnt/` | `ls /mnt/` | Only `cacache nix nixmodules scratch` — no monitoring content |
| `/tmp/` monitoring entries | `ls /tmp/ | grep -i monitor` | None found |
| Workspace root | `ls /home/runner/workspace/ | grep -i monitor` | None found |
| `attached_assets/` (all) | Full listing | `flow-source.tar.gz`, `notifications-source.tar.gz`, text files, images — **no monitoring archive** |
| `/home/runner/` parent | `ls /home/runner/ | grep -i monitor` | None found |
| `/uploads/` | `ls /uploads/` | **Directory does not exist** |
| `/shared/` | `ls /shared/` | **Directory does not exist** |
| Global `find` for `Monitoring.Api`, `Monitoring.Application`, `Monitoring.Domain`, `Monitoring.Infrastructure`, `*MonitoringService*` | `find / -maxdepth 8 -name "..."` | **Zero matches** (excluding node_modules, nix/store, .git) |
| Global `find` for `*.zip`, `*.tar.gz`, `*.tgz` | `find / -maxdepth 5` | Only nix/store package cache and `legalsynq-source.tar.gz` — **no monitoring service archive** |

### 3.2 Evidence Summary

```
/mnt/data/               → directory does not exist
MonitoringService-source.zip → not found anywhere
Monitoring.Api/          → not found anywhere on filesystem
attached_assets/         → [flow, notifications, text specs, images] — no monitoring zip
legalsynq-source.tar.gz  → platform snapshot; same as live workspace (no monitoring service inside)
```

### 3.3 Conclusion

**The Monitoring Service source is NOT available in this environment.**  
This is confirmed by exhaustive filesystem search, not by assumption. Every plausible
location has been checked. The archive referenced in MON-INT-00-001 (`/mnt/data/MonitoringService-source.zip`)
has never been uploaded or mounted into this Replit workspace.

---

## 4. Monitoring Source Intake Result

**BLOCKED — no intake possible.**

The archive does not exist. No unpacking, inspection, or project inventory can be performed.
All analysis of the Monitoring Service architecture in this report is inference from platform
conventions and must be validated once the archive is provided.

**Impact on MON-INT-01-001:**  
Direct backend implementation of the Monitoring Service cannot begin until the source is
available. However, all platform-side preparation (gateway routes, CC abstraction layer,
DB config) can proceed independently.

---

## 5. Platform Monitoring Consumer Analysis

### 5.1 Existing Files Inspected

#### `apps/control-center/src/app/monitoring/page.tsx`

**Role:** Server component — UI consumer page.  
Calls internal `/api/monitoring/summary` endpoint, destructures the `MonitoringSummary`
response into `system`, `integrations` (infra/product split), and `alerts`. Renders three
display components: `SystemHealthCard`, `IntegrationStatusTable`, `AlertsPanel`.  
**Architectural conflict:** None — page is already structured as a pure consumer. The only
change needed is the data source behind `/api/monitoring/summary`.

#### `apps/control-center/src/app/monitoring/services/page.tsx`

**Role:** Server component — service registry management UI.  
Calls `listServices()` (DB read), loads audit history from the canonical audit service
(`monitoring.service.changed` events), and fetches outbox status. Renders `ServicesEditor`
(CRUD), `ServicesAuditList` (recent changes), `AuditOutboxBanner` (retry status).  
**Architectural conflict:** Partial. The CRUD interaction is an admin function (which URLs
to probe). Eventually the Monitoring Service should own the registry and this page would
call the Monitoring Service API instead of the local DB store. The audit-log history view
is unaffected — it reads from the central audit service.

#### `apps/control-center/src/app/api/monitoring/summary/route.ts`

**Role:** Internal API route — health probe executor and aggregator.  
Calls `listServices()` to get registered URLs, executes parallel HTTP health checks against
each, computes overall status, builds alerts from degraded/down results, returns a
`MonitoringSummary`-shaped JSON response.  
**Architectural conflict:** HIGH. This is the exact functionality that belongs to the
Monitoring Service. This route is running a mini monitoring backend inside Control Center.

#### `apps/control-center/src/lib/system-health-store.ts` (476 lines)

**Role:** Data store + business logic — service registry, DB pool, schema creation, CRUD.  
Contains: MySQL pool management, `ensureSchemaAndSeed()` (DDL + seed), `listServices()`,
`addService()`, `updateService()`, `removeService()`. Also contains canonical audit emission
and the integration with the audit outbox.  
**Architectural conflict:** HIGH for the DB/probe ownership. The service registry and its
direct MySQL connection should not live inside the Control Center. Low conflict for the
audit-emission logic (admin config changes should still be audited from CC).

#### `apps/control-center/src/lib/system-health-audit.ts`

**Role:** Pure mapper — converts canonical audit events (`CanonicalAuditEvent`) into the
local `AuditEntry` shape used by the "Recent Changes" panel in the services management UI.  
**Architectural conflict:** None. This is a thin translation layer for displaying audit
history in the UI. It is not a backend data producer.

#### `apps/control-center/src/lib/system-health-audit-outbox.ts`

**Role:** Durable retry queue for canonical audit emissions.  
Uses filesystem persistence (`fs/promises`) with exponential backoff. Retries failed
`/audit-service/audit/ingest` calls so monitoring config changes are never silently dropped
from central audit logs.  
**Architectural conflict:** Low. The outbox itself is a reliability mechanism, not a
monitoring engine. However, if the service registry moves to the Monitoring Service, this
outbox would shrink or be repurposed to track only the CC-side admin operations.

#### `apps/control-center/src/components/monitoring/system-health-card.tsx`

**Role:** Display component — renders overall system status banner with color coding.  
Pure presentational, accepts `SystemHealthSummary` as a prop.  
**Architectural conflict:** None — component is data-source agnostic.

#### `apps/control-center/src/components/monitoring/integration-status-table.tsx`

**Role:** Display component — renders a sorted table of `IntegrationStatus[]` entries.  
Pure presentational.  
**Architectural conflict:** None.

#### `apps/control-center/src/components/monitoring/alerts-panel.tsx`

**Role:** Display component — renders `SystemAlert[]` list with severity indicators.  
Pure presentational.  
**Architectural conflict:** None.

#### `apps/control-center/src/components/monitoring/services-editor.tsx`

**Role:** Client component — CRUD UI for the service registry.  
Contains form state, add/update/delete operations via server actions or fetch calls.  
**Architectural conflict:** Future — when service registry moves to Monitoring Service,
this component's mutation calls must target the Monitoring Service API instead of the
local store. No immediate conflict.

#### `apps/control-center/src/lib/nav.ts`

**Role:** Navigation definition.  
Monitoring entry: `{ href: '/monitoring', label: 'Monitoring', badge: 'IN PROGRESS' }`.  
**Architectural conflict:** None.

---

## 6. Control Center Logic Reclassification

### Classification Table

| Component | Current Role | Classification | Target State (post MON-INT-01) |
|-----------|-------------|----------------|-------------------------------|
| `monitoring/page.tsx` | UI consumer | **KEEP AS UI CONSUMER** | No changes needed — already clean |
| `monitoring/services/page.tsx` | Config management UI | **KEEP AS UI CONSUMER** | Calls become Monitoring Service API |
| `api/monitoring/summary/route.ts` | Health probe executor | **REDIRECT → Monitoring Service** | Proxy to `GET /monitoring/api/monitoring/summary` |
| `system-health-store.ts` (DB pool + CRUD) | Service registry + MySQL owner | **REDIRECT → Monitoring Service** | Call Monitoring Service REST API; remove local DB connection |
| `system-health-store.ts` (audit emission) | Audit event publisher | **KEEP** | CC should still emit audit for its own admin actions |
| `system-health-audit.ts` | Canonical event mapper | **KEEP** | Pure UI adapter — no changes needed |
| `system-health-audit-outbox.ts` | Durable retry queue | **KEEP (shrinks over time)** | Retained for audit reliability; scope narrows |
| `components/monitoring/system-health-card.tsx` | Status display | **KEEP AS UI CONSUMER** | No changes needed |
| `components/monitoring/integration-status-table.tsx` | Service table display | **KEEP AS UI CONSUMER** | No changes needed |
| `components/monitoring/alerts-panel.tsx` | Alert display | **KEEP AS UI CONSUMER** | No changes needed |
| `components/monitoring/services-editor.tsx` | Service CRUD UI | **KEEP → redirect mutations** | Mutation calls redirect to Monitoring Service API |
| `lib/nav.ts` | Navigation | **KEEP** | Remove `IN PROGRESS` badge when live |

### A. What Control Center Must Continue to Own

- All UI pages (`/monitoring`, `/monitoring/services`)
- All display components (health card, status table, alerts panel)
- Navigation and routing
- Audit history display (reads from central audit service)
- Admin-action audit emission (config changes are a CC admin operation)
- The `MonitoringSummary` type contract (matches the Monitoring Service response shape)

### B. What Control Center Must Stop Owning (over time)

- Direct HTTP health probe execution — currently in `api/monitoring/summary/route.ts`
- Alert generation from probe results — currently in the same route
- MySQL connection to `monitoring_db` for the service registry — in `system-health-store.ts`
- DDL ownership (`ensureSchemaAndSeed`) — in `system-health-store.ts`
- Overall status aggregation logic — in `api/monitoring/summary/route.ts`

### C. What the Monitoring Service Must Own

- Scheduled/triggered health check execution against registered service URLs
- Persisted check results and historical trend data
- Authoritative service registry (the canonical list of what is monitored)
- Alert state: creation, severity, resolution
- Read APIs: `/api/monitoring/summary`, `/api/monitoring/alerts`, `/api/monitoring/services`
- The `monitoring_db` schema and all migrations

---

## 7. Database / Config Prerequisite Assessment

### 7.1 Config Variables Required

| Variable | Purpose | Status |
|----------|---------|--------|
| `SYSTEM_HEALTH_DB_URL` | Full DSN for monitoring MySQL (CC store) | **NOT SET** |
| `SYSTEM_HEALTH_DB_HOST` | Host for monitoring MySQL (CC store) | **NOT SET** |
| `SYSTEM_HEALTH_DB_USER` | Username | **NOT SET** |
| `SYSTEM_HEALTH_DB_PASSWORD` | Password | **NOT SET** |
| `SYSTEM_HEALTH_DB_NAME` | Database name (`monitoring_db`) | **NOT SET** |
| `ConnectionStrings__MonitoringDb` | .NET Monitoring Service connection string | **NOT SET** (secret not provisioned) |

### 7.2 Observable Evidence

- Running `printenv | grep -iE "SYSTEM_HEALTH|MONITORING"` returns nothing.
- No `SYSTEM_HEALTH_DB_*` or `ConnectionStrings__MonitoringDb` appear in provisioned
  Replit secrets.
- The CC store's `resolvePoolOptions()` has a cascade: checks `SYSTEM_HEALTH_DB_URL`,
  then `SYSTEM_HEALTH_DB_HOST`, then falls back to `ConnectionStrings__IdentityDb`
  (parsing it as a MySQL connection string). `ConnectionStrings__IdentityDb` is not
  provisioned in this environment either.
- **Net effect:** On startup, `getPool()` throws:
  `"[system-health-store] No database configuration found"`. The monitoring page will
  display an error panel rather than service health data in the current environment.

### 7.3 Classification

**BLOCKED** for both the Control Center store and the future .NET Monitoring Service.

| Component | Readiness | Blocker |
|-----------|-----------|---------|
| CC service registry (`system-health-store.ts`) | BLOCKED | No `SYSTEM_HEALTH_DB_*` vars set |
| .NET Monitoring Service | BLOCKED | Archive missing + no `ConnectionStrings__MonitoringDb` |
| `monitoring_db` database itself | ASSUMED READY | Per spec; cannot verify without connection |
| Gateway routes for Monitoring Service | BLOCKED | No `monitoring-cluster` in YARP config |

---

## 8. Files Added / Moved / Examined

### Created

| File | Purpose |
|------|---------|
| `/analysis/MON-INT-00-001-01-report.md` | This report — created FIRST |

### Modified (minimal, reversible — comment-only changes)

| File | Change |
|------|--------|
| `apps/control-center/src/app/api/monitoring/summary/route.ts` | Added `// TODO: TEMPORARY BRIDGE — MON-INT-01-001` at top of file documenting the cutover path; added `// TODO: DEPRECATE — MON-INT-01-001` above `probeService()` |
| `apps/control-center/src/lib/system-health-store.ts` | Added `// TODO: DEPRECATE — MON-INT-01-001` above `resolvePoolOptions()` documenting that the direct DB connection must not expand |

### Examined (read-only)

| File | Inspected |
|------|-----------|
| `apps/control-center/src/app/monitoring/page.tsx` | Full |
| `apps/control-center/src/app/monitoring/services/page.tsx` | Full |
| `apps/control-center/src/app/api/monitoring/summary/route.ts` | Full |
| `apps/control-center/src/lib/system-health-store.ts` | Full (476 lines) |
| `apps/control-center/src/lib/system-health-audit.ts` | Full |
| `apps/control-center/src/lib/system-health-audit-outbox.ts` | First 60 lines + structure |
| `apps/control-center/src/components/monitoring/*.tsx` | All 5 components |
| `apps/control-center/src/lib/nav.ts` | Monitoring entry |
| `/mnt/`, `/tmp/`, `/home/runner/`, `attached_assets/`, global filesystem | Archive discovery search |

### Not Created / Not Modified

- No new source files (monitoring-source.ts, monitoring-proxy, etc.)
- No behavior changes — comments only
- No gateway routes added
- No DB operations performed

---

## 9. Validation Performed

| Check | Result |
|-------|--------|
| Monitoring Service archive found | **FAIL** — confirmed not present anywhere |
| All plausible filesystem paths searched | PASS |
| All CC monitoring files inventoried | PASS |
| Reclassification table covers every file | PASS |
| Architectural ownership split documented | PASS |
| DB config env vars checked | PASS — all missing |
| `monitoring_db` connection verifiable | NOT POSSIBLE (no credentials in env) |
| Code changes are comment-only | PASS — no behavior modified |
| UI remains unchanged | PASS — no component files modified |
| Platform TypeScript build not broken | PASS — comments cannot affect types |

---

## 10. Known Gaps / Risks / Blockers

### Hard Blockers

| # | Blocker | What it blocks | Resolution |
|---|---------|---------------|-----------|
| B1 | Monitoring Service source archive not available | MON-INT-01-001 implementation | Upload `MonitoringService-source.zip` to workspace |
| B2 | `SYSTEM_HEALTH_DB_*` vars not set | CC service registry cannot connect; monitoring page shows error | Provision in Replit Secrets |
| B3 | `ConnectionStrings__MonitoringDb` not set | .NET Monitoring Service cannot start | Provision in Replit Secrets with RDS credentials for `monitoring_db` |

### Risks

| # | Risk | Mitigation |
|---|------|-----------|
| R1 | CC service registry (`system-health-store.ts`) may have seeded `monitoring_db` with a partially-initialized schema — if Monitoring Service expects its own schema, conflicts may occur | Run `SHOW TABLES` on `monitoring_db` before running Monitoring Service migrations |
| R2 | `probeService` in the summary route has a 4-second timeout per service; if many services are registered and the Monitoring Service is not yet integrated, the CC summary endpoint may be slow or time out | Monitor response times; reduce registered service count in dev |
| R3 | The `MonitoringSummary` response shape produced by the CC route must exactly match what the Monitoring Service will produce | Coordinate response shape before MON-INT-01-001 cutover |

---

## 11. Recommendation for Next Feature

### Selected: MON-INT-00-001-02 — Control Center Monitoring Realignment (Pre-Integration)

**Justification:**  
B1 (archive unavailable) is still the hard blocker for MON-INT-01-001 backend work.
However, the platform-side preparation (abstraction layer, route refactor, environment toggle)
can proceed independently. MON-INT-00-001-02 is the correct immediate next step because:

1. It creates `monitoring-source.ts` — the abstraction layer that decouples the UI from
   the implementation, making the later cutover to the Monitoring Service a one-file change.
2. It refactors `api/monitoring/summary/route.ts` to call through the abstraction, not
   the store directly — eliminating the architectural conflict without breaking the UI.
3. It adds the `MONITORING_SOURCE=local|service` toggle so the cutover can be done
   with a single environment variable change.
4. None of this requires the Monitoring Service archive.

**If the archive is provided before MON-INT-00-001-02 completes:**  
The two can proceed in parallel — MON-INT-00-001-02 on the CC side, MON-INT-01-001 on
the .NET service side. The abstraction layer makes the two streams mergeable without
coordination conflicts.

**Single remaining hard blocker for MON-INT-01-001:**  
The Monitoring Service source archive must be uploaded to the workspace. Once it is,
MON-INT-01-001 can proceed immediately assuming B2 and B3 are resolved in parallel.
