# MON-INT-04-001 — Public Status Page

> **Report created FIRST** before any implementation, per mandatory execution rules.
> Updated incrementally after each step.

---

## 1. Task Summary

Create a public, read-only `/status` page that displays real-time platform health
(overall system status, component list, active incidents) using the existing
Control Center monitoring summary API, accessible without authentication.

| Field | Value |
|---|---|
| **Ticket** | MON-INT-04-001 |
| **Status** | ✅ Complete |
| **Backend changes** | None |
| **New APIs** | None |
| **New route** | `/status` (Control Center, public) |
| **Date** | 2026-04-20 |

---

## 2. Existing Public-Safe Monitoring API Analysis

### `/api/monitoring/summary` — already public-safe

The route at `apps/control-center/src/app/api/monitoring/summary/route.ts` is a thin
adapter over `getMonitoringSummary()` with no auth check. It was already in
`PUBLIC_PATHS` in `middleware.ts`:

```ts
const PUBLIC_PATHS = [
  `${BASE_PATH}/login`,
  `${BASE_PATH}/systemstatus`,
  '/_next',
  // ...
  '/api/monitoring/summary',  // ← already public
];
```

**The API is already public-safe and sufficient for the status page.** No new endpoint needed.

### MonitoringSummary data shape

```ts
interface MonitoringSummary {
  system: {
    status:           MonitoringStatus;  // 'Healthy' | 'Degraded' | 'Down'
    lastCheckedAtUtc: string;
  };
  integrations: Array<{
    name:             string;
    status:           MonitoringStatus;
    latencyMs?:       number;            // internal metric
    lastCheckedAtUtc: string;
    category?:        string;
  }>;
  alerts: Array<{
    id:            string;               // internal UUID — DO NOT EXPOSE
    message:       string;
    severity:      'Info' | 'Warning' | 'Critical';
    createdAtUtc:  string;
    entityName?:   string;
    resolvedAtUtc?: string | null;
  }>;
}
```

### Field safety classification

| Field | Public-safe | Decision |
|---|---|---|
| `system.status` | ✅ | Shown prominently |
| `system.lastCheckedAtUtc` | ✅ | Shown in SystemHealthCard |
| `integration.name` | ✅ | Shown (display name only) |
| `integration.status` | ✅ | Shown |
| `integration.lastCheckedAtUtc` | ✅ | Shown (helps users judge freshness) |
| `integration.latencyMs` | ⚠️ Internal | **Excluded** — internal perf metric |
| `integration.category` | ⚠️ Internal | **Excluded** — not meaningful to external users |
| `alert.id` | ❌ Internal UUID | **Excluded** — not shown anywhere |
| `alert.severity` | ✅ | Shown |
| `alert.message` | ✅ | Shown (human-readable) |
| `alert.entityName` | ✅ | Shown (component display name) |
| `alert.createdAtUtc` | ✅ | Shown as "Since …" |
| `alert.resolvedAtUtc` | N/A | Active alerts only (null for active) |

No additional sanitization layer needed — exclusions are enforced in the page/component
layer without modifying the API.

### Existing `/systemstatus` page

A public page at `/systemstatus` already existed using the same summary data.
It imports `SystemHealthCard`, `IntegrationStatusTable`, and `AlertsPanel`.

**Why `/status` is created separately rather than reusing `/systemstatus`:**
1. `AlertsPanel` was modified in MON-INT-03-002 to open `IncidentDetailPanel`, which
   contains a resolve button (admin action). Reusing it on a public page would expose
   admin controls.
2. `/status` is the canonical public URL; `/systemstatus` is the legacy internal path.
3. The new page has a cleaner, more external-facing design with no internal nav.

### Root layout — no auth imposed on children

The root `apps/control-center/src/app/layout.tsx` contains no auth guards:
```tsx
export default function RootLayout({ children }) {
  return (
    <html lang="en"><body><ClientProviders>{children}</ClientProviders></body></html>
  );
}
```
Any page route placed under `src/app/` will inherit this clean layout. Adding
`/status` requires only middleware exemption.

---

## 3. Public Status Page Design

### Route and layout decision

**Route:** `apps/control-center/src/app/status/page.tsx`

**Layout:** Standalone page with its own header and footer (no `CCShell`, no internal
nav, no operator controls). Middleware updated to include `/status` in `PUBLIC_PATHS`.

This keeps public and internal concerns cleanly separated — the operator monitoring
page retains its full feature set without modification.

### Page sections

**A. Header**
- LegalSynq wordmark (links to `/`)
- "Status" nav link (self)
- "Sign in" CTA → `/login`

**B. Page title**
- "System Status" h1
- "Current platform availability and active incidents." subtitle

**C. SystemHealthCard (reused)**
- Shows `All Systems Operational` / `Partial Degradation` / `Major Outage`
- Pulsing green dot for Healthy, solid dot for other states
- Last checked timestamp
- Pure server component — safe to reuse, no admin controls

**D. PublicComponentList (new)**
- All integrations from `summary.integrations`
- Sorted: Down → Degraded → Healthy → alphabetical
- Columns: name, last-checked (compact), status badge
- External-friendly badge labels: "Operational" / "Degraded" / "Outage"
  (avoids "Down" which implies complete unavailability to external users)
- Latency excluded (internal metric)
- No filter controls, no category UI, no admin links

**E. PublicIncidentsPanel (new)**
- Only renders when `alerts.filter(a => !a.resolvedAtUtc).length > 0`
- Per incident: severity badge + component name + message + "Since {timestamp}"
- No alert IDs, no resolve buttons, no entity UUIDs
- Red tint header with incident count

**F. "No active incidents" strip**
- Renders when there are no active alerts (incidents panel is hidden)
- Green dot + "No active incidents."

**G. Footer**
- Copyright + "Status" + "Sign in" links

### External-friendly badge labels

Internal monitoring uses `Healthy | Degraded | Down` (technical terms).
Public users see `Operational | Degraded | Outage` — same status, friendlier framing.
`SystemHealthCard` already uses "All Systems Operational" / "Partial Degradation" /
"Major Outage" for the overall banner — consistent with this approach.

---

## 4. Implementation

### Middleware — `middleware.ts` (modified)

Added `/status` to `PUBLIC_PATHS`:
```ts
const PUBLIC_PATHS = [
  `${BASE_PATH}/login`,
  `${BASE_PATH}/systemstatus`,
  `${BASE_PATH}/status`,    // ← added
  '/_next',
  // ...
];
```

`BASE_PATH` is the app's configured base path. Using `${BASE_PATH}/status` ensures
consistency with how `systemstatus` is exempted.

### `apps/control-center/src/app/status/page.tsx` (created)

Server component. Fetches from `/api/monitoring/summary` (same pattern as
`systemstatus/page.tsx`). No `requirePlatformAdmin` call. No `CCShell`.

```tsx
export default async function StatusPage() {
  let data: MonitoringSummary | null = null;
  let fetchError = false;
  try { data = await fetchMonitoringSummary(); } catch { fetchError = true; }
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <header>...</header>
      <main>
        {fetchError ? <StatusUnavailable /> : data ? (
          <>
            <SystemHealthCard summary={data.system} />
            <PublicComponentList integrations={data.integrations} />
            <PublicIncidentsPanel alerts={data.alerts} />
            {active.length === 0 && <NoActiveIncidents />}
          </>
        ) : null}
      </main>
      <footer>...</footer>
    </div>
  );
}
```

**Error handling:**
- Fetch failure → `StatusUnavailable` ("Status Unavailable. Please try again shortly.")
- No stack traces, no internal error messages, no HTTP status codes exposed
- No alert/integration data → empty states handled per component

### `public-component-list.tsx` (created)

Pure server component. No `'use client'`. No interactivity.

```
Sort:    Down(0) → Degraded(1) → Healthy(2), then alphabetical by name
Columns: status dot | name | last-checked time | status badge
Badge:   Operational / Degraded / Outage
Empty:   "No components are currently being monitored."
```

### `public-incidents-panel.tsx` (created)

Pure server component. No `'use client'`. No action handlers.

```
Filter:  alerts.filter(a => !a.resolvedAtUtc)  (active only)
Show:    entityName (if present) → message → "Since {timestamp}"
Badges:  Critical (red) / Warning (amber) / Info (blue)
Key:     createdAtUtc + index (no alert.id in DOM)
Hidden:  when active.length === 0
```

---

## 5. Public Data Exposure Review

### Fields rendered on `/status`

| Component | Fields exposed | Notes |
|---|---|---|
| `SystemHealthCard` | `system.status`, `system.lastCheckedAtUtc` | Reused as-is; already public-safe |
| `PublicComponentList` | `integration.name`, `integration.status`, `integration.lastCheckedAtUtc` | Latency excluded |
| `PublicIncidentsPanel` | `alert.severity`, `alert.message`, `alert.entityName`, `alert.createdAtUtc` | ID excluded |

### Verified NOT present in rendered HTML

| Item | Method | Result |
|---|---|---|
| `alert.id` (UUID) | `grep -c UUID_PATTERN` in full page HTML | ✅ 0 found |
| `resolveAlert`, `/monitoring/admin/alerts` | `grep -c` in full page HTML | ✅ 0 found |
| `alertId`, `entityId` | `grep -c` in full page HTML | ✅ 0 found |
| `latency` / `ms` unit | `grep -c` in full page HTML | ✅ 0 found |

### No admin routing

The page has no links to `/monitoring/*`, no operator navigation, and no control surfaces.

---

## 6. Validation

### A. `/status` accessible without authentication

```
GET http://127.0.0.1:5004/status  →  HTTP 200  ✓
(no session cookie sent)
```

### B. Page renders real monitoring data

```
Page HTML contains:
  "System Status"         ✓  (page title)
  "Major Outage"          ✓  (real status from monitoring service)
  "Components"            ✓  (component list section rendered)
  "Active Incidents"      ✓  (incidents section rendered — 1 active alert)
```

Active alert confirmed from API:
```json
{
  "entityName": "Reports",
  "severity":   "Critical",
  "message":    "Reports is down: Unreachable"
}
```

Incident rendered with:
- "Reports" entity name
- "Critical" badge
- "Since Apr 20, 13:42 UTC" timestamp

### C. No admin controls in rendered HTML

```
grep -c "resolveAlert|/monitoring/admin/alerts|alertId|entityId" /status HTML
→ 0  ✓
```

### D. No internal UUIDs in rendered HTML

```
grep UUID pattern in /status HTML
→ 0 UUIDs found  ✓
```

### E. Regressions — all pass

| Endpoint | Expected | Result |
|---|---|---|
| `GET /status` (no auth) | HTTP 200 | ✅ 200 |
| `GET /systemstatus` | HTTP 200 | ✅ 200 |
| `GET /monitoring` (no auth) | HTTP 307 | ✅ 307 |
| `GET /api/monitoring/summary` | HTTP 200 | ✅ 200 |
| TypeScript | 0 errors | ✅ clean |
| Fast Refresh rebuild | < 2000ms | ✅ 272ms |

### F. Both local and service modes

The page fetches from `/api/monitoring/summary`, which already supports both
`MONITORING_SOURCE=local` (in-process probe engine) and `MONITORING_SOURCE=service`
(Monitoring Service backend). The public page inherits this automatically —
no source-specific handling needed.

### What was runtime-tested vs code-verified

| Item | Status |
|---|---|
| `/status` returns HTTP 200 without auth | ✅ Runtime-tested |
| Page HTML contains correct section headings | ✅ Runtime-tested |
| Alert data flows from API to rendered page | ✅ Runtime-tested |
| No UUIDs in page HTML | ✅ Runtime-tested |
| No admin controls in page HTML | ✅ Runtime-tested |
| `/systemstatus` and `/monitoring` regressions | ✅ Runtime-tested |
| Error fallback (StatusUnavailable) | ✅ Code-verified |
| Empty component list state | ✅ Code-verified |
| No-incidents strip | ✅ Code-verified (active alert present in test env) |
| Visual rendering in browser | ⚠️ Preview pane shows tenant login overlay — curl confirmed correct HTML |

---

## 7. Files Changed

| File | Action | Purpose |
|---|---|---|
| `apps/control-center/src/middleware.ts` | **Modified** | Added `/status` to `PUBLIC_PATHS` |
| `apps/control-center/src/app/status/page.tsx` | **Created** | Public status page — fetches summary, renders public layout |
| `apps/control-center/src/components/monitoring/public-component-list.tsx` | **Created** | Read-only component list with external-friendly labels |
| `apps/control-center/src/components/monitoring/public-incidents-panel.tsx` | **Created** | Read-only active incidents panel (no admin controls) |

**Not modified:**
- Any Monitoring Service file
- Gateway configuration
- Any existing API route
- Internal monitoring page (`monitoring/page.tsx`)
- `systemstatus/page.tsx`
- `AlertsPanel`, `IncidentDetailPanel`, or any admin component
- Any type definitions

---

## 8. Known Gaps / Risks

| # | Gap | Severity | Notes |
|---|---|---|---|
| 1 | **No uptime percentages** | Low | No historical availability data is tracked yet. Cannot compute 30-day/90-day uptime. Requires MON-INT-04-002 (Uptime Aggregation Engine) |
| 2 | **No response time charts** | Low | Latency is excluded from public display. A chart would require historical storage, not just the live snapshot |
| 3 | **No historical availability bars** | Low | The "90-day availability bar" pattern (e.g., Atlassian Statuspage) requires bucketed daily snapshots — not yet implemented |
| 4 | **No public incident drill-down** | Low | Clicking an incident row does nothing. A future public incident detail view could show a timeline of updates |
| 5 | **Alert messages may be technical** | Low | Messages like "Reports is down: Unreachable" are human-readable but technical. No message sanitization or public-friendly rewriting was introduced — existing messages are displayed as-is |
| 6 | **`/systemstatus` and `/status` coexist** | Low | Both public pages exist. `/status` is the cleaner canonical public URL. `/systemstatus` retains its legacy path. A redirect from `/systemstatus` → `/status` could be added in a cleanup pass |
| 7 | **Visual browser validation blocked** | Low | Preview pane shows tenant login overlay; curl confirmed HTTP 200 and correct HTML. No data correctness issue |
| 8 | **No auto-refresh** | Low | Page is `force-dynamic` (always fresh on load) but does not auto-poll. Operators and external users must manually refresh for updates |
| 9 | **Local mode vs service mode** | Low | In local mode, all components report Healthy (in-process probes). In service mode, real Monitoring Service data is used. This difference is visible on the public page — expected behavior documented |

---

## 9. Recommended Next Feature

**Option C: Stabilization / Production Validation**

**Rationale:**

The Monitoring platform feature set is now functionally complete:

| Feature | Status |
|---|---|
| MON-INT-03-001 — Incident Detail View | ✅ Complete |
| MON-INT-03-002 — Alert Resolve Workflow | ✅ Complete |
| MON-INT-03-003 — Incident Timeline / History | ✅ Complete |
| MON-INT-02-004 — Clickable Banner Filtering | ✅ Complete |
| MON-INT-04-001 — Public Status Page | ✅ Complete |

Before introducing new features (uptime aggregation, timeline bars), a stabilization
pass is the highest-value next step:
- Authenticated browser testing of the full incident lifecycle end-to-end
- Validate auto-resolve + manual-resolve don't conflict
- Confirm alert history is correct after multiple resolve/re-trigger cycles
- Verify the public status page under the deployed environment

**If metrics/UX is the priority instead:** MON-INT-04-002 (Uptime Aggregation Engine)
would be the natural next step — building the per-day availability percentages that
power both the operator page and the public status page's "90-day history" bars.
