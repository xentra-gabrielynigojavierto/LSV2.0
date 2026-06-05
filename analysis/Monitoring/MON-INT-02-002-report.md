# MON-INT-02-002 — Status Summary Banner

> **Report created FIRST** before any implementation, per mandatory execution rules.
> Updated incrementally after each step.

---

## 1. Task Summary

Add a system-wide status summary banner at the top of the Control Center
monitoring page that displays the overall platform health using real data from
the Monitoring Service.

| Field | Value |
|---|---|
| **Ticket** | MON-INT-02-002 |
| **Status** | ✅ Complete |
| **Depends on** | MON-INT-01-003 (auth alignment — complete) |
| **Date** | 2026-04-20 |

---

## 2. Existing Monitoring UI Analysis

### Page structure (pre-feature)

File: `apps/control-center/src/app/monitoring/page.tsx`

The monitoring page is a **Next.js server component** (`force-dynamic`) that:
1. Calls `/api/monitoring/summary` (CC BFF route) on every request
2. Renders via `CCShell` → `max-w-4xl` container
3. Shows a header row (h1 + critical-alert badge + "Manage Services" link)
4. Then (if data): `SystemHealthCard` → `IntegrationStatusTable` (infra) → `IntegrationStatusTable` (products) → `AlertsPanel`
5. On error: plain red error box

### Existing components

| Component | File | What it shows |
|---|---|---|
| `SystemHealthCard` | `system-health-card.tsx` | Overall status with colored background, pulsing dot, label ("All Systems Operational" / etc.), last checked time |
| `StatusBadge` | `system-health-card.tsx` | Small colored pill showing status value |
| `IntegrationStatusTable` | `integration-status-table.tsx` | Per-service list with status dot, name, latency, badge |
| `AlertsPanel` | `alerts-panel.tsx` | Active alerts list |

### Gap analysis

The `SystemHealthCard` shows the **overall status label and timestamp**, but does NOT show **counts** (total services, healthy, degraded, down, active alerts).

The new `StatusSummaryBanner` fills this gap — a compact horizontal strip immediately above the `SystemHealthCard` with:
- Status indicator (color-coded)
- Counts: total / healthy / (degraded if > 0) / (down if > 0) / (alerts if > 0)
- Last checked timestamp

**Placement decision:** Banner is inserted as the **first element** inside the `data` section (`space-y-5`), directly above `SystemHealthCard`. This puts it "above existing content" without reordering the page structure.

### MonitoringSummary type

```ts
type MonitoringStatus = 'Healthy' | 'Degraded' | 'Down';

interface SystemHealthSummary  { status: MonitoringStatus; lastCheckedAtUtc: string; }
interface IntegrationStatus    { name: string; status: MonitoringStatus; latencyMs?: number; lastCheckedAtUtc: string; category?: string; }
interface SystemAlert          { id: string; message: string; severity: AlertSeverity; createdAtUtc: string; }
interface MonitoringSummary    { system: SystemHealthSummary; integrations: IntegrationStatus[]; alerts: SystemAlert[]; }
```

---

## 3. Banner Design

### Visual layout (horizontal strip)

```
[ ● Down ]  |  10 total  9 healthy  1 down  |  1 alert  ·····  Checked 07:09:00 UTC
```

### Color mapping

| Status | Background | Border | Dot | Text |
|---|---|---|---|---|
| `Healthy` | `bg-green-50` | `border-green-200` | `bg-green-500` | `text-green-700` |
| `Degraded` | `bg-amber-50` | `border-amber-200` | `bg-amber-500` | `text-amber-700` |
| `Down` | `bg-red-50` | `border-red-200` | `bg-red-600` | `text-red-700` |

### Stats displayed

| Stat | Always shown | Condition |
|---|---|---|
| Total services | ✅ | always |
| Healthy count | ✅ | always |
| Degraded count | ❌ | only if degraded > 0 |
| Down count | ❌ | only if down > 0 |
| Alert count | ❌ | only if alerts > 0 (with separator) |
| Last checked timestamp | ✅ | always |

**No status logic derived in CC.** `systemStatus` comes directly from `summary.system.status` (Monitoring Service computes it). Counts are simple `.filter().length` tallies over the `integrations` array — no business logic.

### Error fallback

`StatusSummaryBannerError` — gray "Monitoring unavailable" banner; renders instead of the normal banner when the data fetch fails. Sits above the existing red error box.

---

## 4. Implementation

### Stat computation (page.tsx)

```ts
const totalServices = data?.integrations.length ?? 0;
const healthyCount  = data?.integrations.filter(i => i.status === 'Healthy').length  ?? 0;
const degradedCount = data?.integrations.filter(i => i.status === 'Degraded').length ?? 0;
const downCount     = data?.integrations.filter(i => i.status === 'Down').length      ?? 0;
```

`system.status` is passed through directly — no recomputation.

### Banner injection (page.tsx)

Error path:
```tsx
<div className="space-y-4">
  <StatusSummaryBannerError />
  <div className="bg-red-50 ...">...</div>  {/* existing error box */}
</div>
```

Data path (new first element inside `space-y-5`):
```tsx
<StatusSummaryBanner
  systemStatus={data.system.status}
  total={totalServices}
  healthy={healthyCount}
  degraded={degradedCount}
  down={downCount}
  alerts={data.alerts.length}
  lastCheckedAt={data.system.lastCheckedAtUtc}
/>
<SystemHealthCard summary={data.system} />  {/* unchanged — still renders below */}
```

### Loading state

This page is a Next.js server component with `force-dynamic`. There is no intermediate loading state from the client's perspective — the page renders fully on the server and the complete HTML is sent. If the fetch fails, `fetchError` is set and `StatusSummaryBannerError` renders. If data is null and no error (unreachable in practice), the banner simply does not render (the `: null` branch).

---

## 5. Data Flow Validation

### Live data at time of validation

```
GET /api/monitoring/summary (CC BFF → Monitoring Service)

system.status:    Down
lastCheckedAtUtc: 2026-04-20T07:09:00.265Z
integrations:     10 total
  → healthy:  9
  → degraded: 0
  → down:     1
alerts:           1 (severity: Critical)
```

### Banner would render with this data

```
[ ● Down ]  |  10 total  9 healthy  1 down  |  1 alert  ·····  Checked 07:09:00 UTC
```

Status pill: red background, "Down" label.
Alert count shown (> 0), degraded NOT shown (= 0).
Last checked timestamp from `data.system.lastCheckedAtUtc`.

### Both modes confirmed

| Mode | API response | Banner data source |
|---|---|---|
| `MONITORING_SOURCE=local` | CC probes each service directly, builds summary | Same `/api/monitoring/summary` route; same `MonitoringSummary` type |
| `MONITORING_SOURCE=service` | CC calls Gateway → Monitoring Service summary | Same `/api/monitoring/summary` route; same `MonitoringSummary` type |

The banner is agnostic to the mode — it reads from `MonitoringSummary` regardless of where the data originated.

---

## 6. UI Validation

| Check | Method | Result |
|---|---|---|
| TypeScript types compile clean | `pnpm tsc --noEmit` | ✅ 0 errors, 0 output |
| Next.js build compiles + type-checks | `pnpm next build` (partial) | ✅ "Compiled successfully" + "Linting and checking validity of types" |
| Banner props match MonitoringSummary type | Reviewed source | ✅ all props derived directly from summary fields |
| Status styles exhaustive (all 3 values) | Reviewed STATUS_STYLES map | ✅ Healthy / Degraded / Down all covered |
| Error fallback renders without crash | Reviewed StatusSummaryBannerError | ✅ pure static JSX, no props |
| Existing components untouched | Diffed SystemHealthCard, IntegrationStatusTable, AlertsPanel | ✅ no changes |
| API endpoint still returns 200 | `curl /api/monitoring/summary` | ✅ HTTP 200, 10 integrations, 1 alert |
| No monitoring logic reintroduced to CC | Reviewed page.tsx counts | ✅ only `.filter().length` tallies; no status computation |

---

## 7. Files Changed

| File | Action | Purpose |
|---|---|---|
| `apps/control-center/src/components/monitoring/status-summary-banner.tsx` | **Created** | New banner component + error fallback |
| `apps/control-center/src/app/monitoring/page.tsx` | Modified | Import banner; compute counts; inject banner above SystemHealthCard; wrap error state with StatusSummaryBannerError |

**No other files changed.** Existing components (`system-health-card.tsx`, `integration-status-table.tsx`, `alerts-panel.tsx`) untouched. No new dependencies added. No styling system changes.

---

## 8. Known Gaps / Risks

| # | Gap | Severity | Notes |
|---|---|---|---|
| 1 | **No real-time updates** — banner reflects the data at page load time | Low | Expected; page is `force-dynamic` so each navigation reloads; future work could add auto-refresh via polling |
| 2 | **Stale data between scheduler cycles** — Monitoring Service probes every 15 s; CC page may show data up to 15 s old | Low | Acceptable; `lastCheckedAtUtc` is shown so operators can see data age |
| 3 | **Banner not clickable** — counts don't link to filtered views | Low | Future enhancement (MON-INT-02-003 will add the component list below the banner) |
| 4 | **Dev login credentials stale** — unable to screenshot authenticated page in dev (README_DEV.md says `Admin1234!` but Identity returns 401) | Low | Build compile + TS checks passed; data validation via direct API calls confirmed counts are correct; UI screenshots can be taken by an operator with valid credentials |
| 5 | **Error fallback not runtime-tested** — `StatusSummaryBannerError` is simple static JSX; no separate E2E for the error path | Low | Code is trivially simple; no logic to test; risk is negligible |

---

## 9. Recommended Next Feature

**MON-INT-02-003 — Component Status List**

**Rationale:**
- The banner provides the at-a-glance summary the operator needs when landing on the monitoring page
- The natural next step is a consolidated, sortable list of all monitored components on the same page — showing each service's current status, latency, and last-checked time, with the ability to filter by status
- This builds directly on the same `integrations` data already fetched by the page; no new API endpoints needed
- MON-INT-02-003 replaces the two separate `IntegrationStatusTable` cards (infra + product) with a unified, richer component list that ties directly into the banner's count pills (future: clicking "1 down" filters the list)
