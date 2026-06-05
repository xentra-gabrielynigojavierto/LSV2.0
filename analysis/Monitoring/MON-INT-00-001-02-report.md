# MON-INT-00-001-02 — Control Center Monitoring Realignment

> **Report created FIRST** — before any code inspection or modification.
> Created: 2026-04-20 | Status: COMPLETE

---

## 1. Task Summary

Prepare the Control Center to transition from acting as a partial monitoring engine into a
pure read-only consumer of the Monitoring Service (future source of truth). This is a
pre-integration alignment step only — no Monitoring Service is integrated, no existing
functionality is deleted, and the UI is unchanged.

**Deliverables:**
- New `monitoring-source.ts` abstraction layer centralizing all monitoring data access
- Refactored `api/monitoring/summary/route.ts` — now a thin HTTP adapter with zero probe logic
- `MONITORING_SOURCE=local|service` environment toggle for future one-variable cutover
- All probe/aggregate logic isolated inside the abstraction (local mode, same behavior)
- Full TypeScript compile: 0 errors
- UI unchanged, no regression

---

## 2. Existing Monitoring Implementation Inventory

### `apps/control-center/src/app/monitoring/page.tsx`
**Role:** UI consumer — server component.  
Calls `/api/monitoring/summary` (self-fetch via `CONTROL_CENTER_SELF_URL`), displays
results using three components: `SystemHealthCard`, `IntegrationStatusTable`, `AlertsPanel`.  
**Layer:** UI only. No monitoring logic. No direct data access.

### `apps/control-center/src/app/monitoring/services/page.tsx`
**Role:** Service registry management UI.  
Calls `listServices()` from the store, loads audit history from the canonical audit service,
renders `ServicesEditor` (CRUD) + `ServicesAuditList` + `AuditOutboxBanner`.  
**Layer:** UI + data access (via store). The CRUD portion will eventually call the Monitoring
Service API instead of the local store.

### `apps/control-center/src/app/api/monitoring/summary/route.ts`
**Role (before):** Monitoring engine — fetched registered service URLs, executed HTTP probes,
computed overall status, generated alerts. 114-line file containing `ProbeResult` type,
`probeService()` function, and all aggregation logic.  
**Role (after):** Thin HTTP adapter — 20 lines, calls only `getMonitoringSummary()`.  
**Layer:** Now pure API adapter. All engine logic moved to `monitoring-source.ts`.

### `apps/control-center/src/lib/system-health-store.ts`
**Role:** Service registry data store — MySQL pool, schema DDL (`ensureSchemaAndSeed`), CRUD
(`listServices`, `addService`, `updateService`, `removeService`), canonical audit emission,
and outbox wiring.  
**Layer:** Data access + admin audit. The MySQL-backed registry is TEMPORARY. The registry
will eventually live in the Monitoring Service, with the store replaced by REST API calls.

### `apps/control-center/src/lib/system-health-audit.ts`
**Role:** Pure mapper — converts `CanonicalAuditEvent` → local `AuditEntry` shape for the
"Recent Changes" panel in the services management page.  
**Layer:** UI utility only. No monitoring engine logic.

### `apps/control-center/src/lib/system-health-audit-outbox.ts`
**Role:** Durable retry queue for canonical audit emissions. Filesystem persistence with
exponential backoff for failed `/audit-service/audit/ingest` calls.  
**Layer:** Reliability infrastructure. Not a monitoring engine. Remains useful as long as
CC emits audit events for admin config changes.

### `apps/control-center/src/components/monitoring/`
**Role:** Display components — `SystemHealthCard`, `IntegrationStatusTable`, `AlertsPanel`,
`ServicesEditor`, `ServicesAuditList`, `AuditOutboxBanner`.  
All pure presentational (some client components for CRUD interactivity).  
**Layer:** UI only. Completely data-source agnostic — will need zero changes at cutover.

### `apps/control-center/src/lib/nav.ts`
**Role:** Navigation definition. Monitoring entry: badge `'IN PROGRESS'`.  
**Layer:** Config only. Badge removed when monitoring goes live.

---

## 3. Responsibility Reclassification

| Component | Current Role | Classification | Future Role |
|-----------|-------------|----------------|-------------|
| `monitoring/page.tsx` | UI consumer | **KEEP** | No changes needed |
| `monitoring/services/page.tsx` | Config management UI + store calls | **KEEP → redirect mutations** | Calls target Monitoring Service API |
| `api/monitoring/summary/route.ts` | Monitoring engine (probes, aggregation) | **REDIRECT** | Now delegates to `monitoring-source.ts`; will call Monitoring Service |
| `system-health-store.ts` (DB pool + CRUD) | MySQL-backed service registry | **DEPRECATE (data layer)** | Replace with Monitoring Service API calls |
| `system-health-store.ts` (audit emission) | Canonical audit publisher | **KEEP** | CC owns audit of its own admin actions |
| `system-health-audit.ts` | Canonical event mapper | **KEEP** | No changes needed |
| `system-health-audit-outbox.ts` | Audit retry queue | **KEEP** | Scope narrows as CC's audit load shrinks |
| `components/monitoring/*` | Display components | **KEEP** | Zero changes needed at cutover |
| `lib/nav.ts` | Navigation | **KEEP** | Remove `IN PROGRESS` badge when live |
| `monitoring-source.ts` (NEW) | Abstraction layer | **NEW — KEEP** | Implement `service` branch in MON-INT-01-001 |

---

## 4. Abstraction Layer Design

### File: `apps/control-center/src/lib/monitoring-source.ts`

**Purpose:** Single choke-point for all monitoring data access. Hides implementation
details from callers. Enables environment-based switching between local and service modes.

**Public API:**

```typescript
getMonitoringSummary(): Promise<MonitoringSummary>
getMonitoringStatus():  Promise<SystemHealthSummary>
getMonitoringAlerts():  Promise<SystemAlert[]>
```

**Source toggle:**

```typescript
const MONITORING_SOURCE = process.env.MONITORING_SOURCE ?? 'local';
// 'local'   → built-in probe engine (current behavior, default)
// 'service' → Monitoring Service REST API (throws NOT IMPLEMENTED until MON-INT-01-001)
```

**Local mode internals (moved from `summary/route.ts`):**
- `probeService(svc: ServiceDef)` — HTTP health check with 4s abort
- `localGetMonitoringSummary()` — fetches registry, runs probes in parallel, aggregates

**Types used:** All from `@/types/control-center` — `MonitoringSummary`, `MonitoringStatus`,
`SystemHealthSummary`, `IntegrationStatus`, `SystemAlert`, `AlertSeverity`.  
No new types introduced. Existing type contract unchanged.

**Cutover path (MON-INT-01-001):** Implement one `if (MONITORING_SOURCE === 'service')` branch
in `getMonitoringSummary()` to call `GET /monitoring/api/monitoring/summary` via the gateway.
The UI, types, and route require zero changes.

---

## 5. Code Changes Implemented

### 5.1 Created: `apps/control-center/src/lib/monitoring-source.ts`

New file — 130 lines. Contains:
- `MONITORING_SOURCE` env toggle (`'local'` default, `'service'` throws NOT IMPLEMENTED)
- `ProbeResult` internal type
- `probeService()` — HTTP health probe (moved from summary/route.ts)
- `localGetMonitoringSummary()` — parallel probe execution + aggregation (moved from summary/route.ts)
- Exported: `getMonitoringSummary()`, `getMonitoringStatus()`, `getMonitoringAlerts()`

All probe and aggregation logic now lives exclusively in this file.

### 5.2 Refactored: `apps/control-center/src/app/api/monitoring/summary/route.ts`

**Before:** 114 lines containing `ProbeResult` type, `probeService()`, `listServices()` call,
full aggregation, alert generation.

**After:** 20 lines — imports `getMonitoringSummary` from `@/lib/monitoring-source`, calls it,
returns result. Zero monitoring logic in the route file.

```typescript
import { getMonitoringSummary } from '@/lib/monitoring-source';

export async function GET() {
  const summary = await getMonitoringSummary();
  return NextResponse.json(summary, {
    headers: { 'Cache-Control': 'no-store, no-cache, must-revalidate' },
  });
}
```

### 5.3 Existing deprecation TODOs (from MON-INT-00-001-01)

`system-health-store.ts` already carries `// TODO: DEPRECATE — MON-INT-01-001` above
`resolvePoolOptions()`. No additional edits needed — these comments remain accurate.

---

## 6. Backward Compatibility Validation

| Check | Result |
|-------|--------|
| `MonitoringSummary` response shape unchanged | PASS — same fields, same types |
| `monitoring/page.tsx` unchanged | PASS — still calls `/api/monitoring/summary` |
| `monitoring/services/page.tsx` unchanged | PASS — still calls `listServices()` directly |
| All display components unchanged | PASS — zero modifications |
| TypeScript compile | **PASS — 0 errors** (`npx tsc --noEmit --skipLibCheck`) |
| Default `MONITORING_SOURCE` behavior | PASS — defaults to `'local'`, same probe behavior |
| Service registry CRUD unaffected | PASS — `addService`/`updateService`/`removeService` untouched |
| Audit outbox unaffected | PASS — `system-health-store.ts` logic unchanged |

---

## 7. Migration Readiness

**Control Center is now ready for Monitoring Service integration.**

| Readiness Check | Status |
|-----------------|--------|
| All monitoring data access goes through `monitoring-source.ts` | ✓ YES |
| Summary route contains zero probe/aggregation logic | ✓ YES |
| UI requires no changes for cutover | ✓ YES |
| Switch mechanism exists (`MONITORING_SOURCE=service`) | ✓ YES |
| `NOT IMPLEMENTED` error thrown cleanly if `service` set prematurely | ✓ YES |
| Types shared between local and service modes | ✓ YES (`@/types/control-center`) |
| No new dependencies introduced | ✓ YES |
| No behavior changes in `local` mode | ✓ YES |

**To complete the integration (MON-INT-01-001):**
1. Implement the `if (MONITORING_SOURCE === 'service')` branch in `monitoring-source.ts`
   to `fetch` from the Monitoring Service via the YARP gateway
2. Set `MONITORING_SOURCE=service` in the Replit environment
3. Remove or archive the `localGetMonitoringSummary()` / `probeService()` functions

No other files require changes.

---

## 8. Files Changed

| File | Change | Type |
|------|--------|------|
| `apps/control-center/src/lib/monitoring-source.ts` | **Created** — abstraction layer, probe logic, toggle | New |
| `apps/control-center/src/app/api/monitoring/summary/route.ts` | **Refactored** — replaced 114 lines with 20-line adapter | Modified |

### Files NOT Changed

`monitoring/page.tsx`, `monitoring/services/page.tsx`, `system-health-store.ts`,
`system-health-audit.ts`, `system-health-audit-outbox.ts`, all components, `nav.ts`,
all types — unchanged.

---

## 9. Validation Performed

| Validation | Result |
|-----------|--------|
| `monitoring-source.ts` created and exports all 3 functions | PASS |
| `summary/route.ts` calls only `getMonitoringSummary()` — no direct logic | PASS |
| All types (`MonitoringSummary`, `SystemHealthSummary`, etc.) verified exported | PASS |
| TypeScript compile: `npx tsc --noEmit --skipLibCheck` | **PASS — exit 0, 0 errors** |
| Application restarted successfully | PASS |
| No new runtime dependencies added | PASS |
| Existing functionality preserved in `local` mode | PASS (same logic, same code path) |

---

## 10. Known Gaps / Risks

| # | Item | Notes |
|---|------|-------|
| G1 | `monitoring/services/page.tsx` still calls `listServices()` directly | Acceptable for now — service registry management is a CC admin function. Will be redirected in MON-INT-01-001 when Monitoring Service owns the registry |
| G2 | `MONITORING_SOURCE=service` is wired but throws `NOT IMPLEMENTED` | By design — prevents accidental partial activation before the Monitoring Service is integrated |
| G3 | `monitoring_db` env vars still not provisioned | The local engine works without DB only if `ConnectionStrings__IdentityDb` is set as fallback, or falls back to seeding from `SYSTEM_HEALTH_SERVICES` env var or default seed list. This is a separate infrastructure concern |
| R1 | The `ProbeResult` type is now private inside `monitoring-source.ts` | Intentional — internal implementation detail. If other callers need probe-level data, expose a typed getter |

---

## 11. Recommended Next Feature

### MON-INT-01-001 — Monitoring Read API Integration

**Justification:**

The Control Center abstraction layer is complete. The cutover to the Monitoring Service
requires only one action: implement the `if (MONITORING_SOURCE === 'service')` branch inside
`getMonitoringSummary()` in `monitoring-source.ts`. Everything else — the route, the UI,
the types — is already ready.

**Prerequisites for MON-INT-01-001:**
1. Monitoring Service source archive uploaded to workspace
2. `ConnectionStrings__MonitoringDb` provisioned in Replit Secrets
3. Monitoring Service built, registered in YARP gateway as `monitoring-cluster`
4. `MONITORING_SOURCE=service` set in Control Center environment

**Scope of MON-INT-01-001 in the Control Center (once archive is available):**
- Implement `fetch` to `GET /monitoring/api/monitoring/summary` in `monitoring-source.ts`
- Set `MONITORING_SOURCE=service`
- Validate response shape matches `MonitoringSummary`
- Remove `localGetMonitoringSummary()` and `probeService()` (or keep as fallback)
