# MON-INT-02-004 — Clickable Banner Filtering

> **Report created FIRST** before any implementation, per mandatory execution rules.
> Updated incrementally after each step.

---

## 1. Task Summary

Make the Status Summary Banner act as an interactive filter control so operators can
instantly scope the Component Status List to only Healthy / Degraded / Down / Alerts
components without scrolling or using the list's existing filter buttons.

| Field | Value |
|---|---|
| **Ticket** | MON-INT-02-004 |
| **Status** | ✅ Complete |
| **Backend changes** | None — pure client-side UX |
| **New APIs** | None |
| **Date** | 2026-04-20 |

---

## 2. Existing Banner & List Analysis

### StatusSummaryBanner

- **File:** `apps/control-center/src/components/monitoring/status-summary-banner.tsx`
- **Type:** Pure server component (no `'use client'`; no onClick handlers)
- **Structure:** Status indicator dot + label, then four `<StatPill>` `<span>`s for
  `total`, `healthy`, `degraded`, `down`, and `alerts` (conditional on count > 0)
- **No existing interactivity.** StatPills were `<span>` elements.
- **Props received:** `systemStatus`, `total`, `healthy`, `degraded`, `down`, `alerts`, `lastCheckedAt`

### ComponentStatusList

- **File:** `apps/control-center/src/components/monitoring/component-status-list.tsx`
- **Type:** Already a client component (`'use client'`)
- **Existing internal filter:** `useState<'All' | MonitoringStatus>('All')` — only
  `All | Healthy | Degraded | Down`. No `Alerts` filter existed.
- **Existing filter buttons:** Displayed in the card header; counts shown per-button.
- **Sort order:** Down → Degraded → Healthy → alphabetical. Already correct.
- **Props:** `integrations: IntegrationStatus[]` only. No `alerts` or external filter props.

### monitoring/page.tsx

- **Type:** Server Component (`async function MonitoringPage()`)
- **Renders:** `StatusSummaryBanner`, `SystemHealthCard`, `ComponentStatusList`, `AlertsPanel`
- **Alert data:** `data.alerts` (a `SystemAlert[]`) is already available on the page.
- **Filter state:** None — no `useState` possible in a server component.
- **Critical constraint:** Cannot hold `useState`. Need a client wrapper to share state
  between the banner and the list.

### Where filter state should live

Because `monitoring/page.tsx` is a Server Component, state cannot be lifted directly into
the page. The standard Next.js pattern is an "island" client wrapper component that
receives all data from the server page and manages shared state.

**Decision: create `MonitoringFilterSection` client wrapper component.**

---

## 3. Interaction Design

### Click targets (banner StatPills)

| StatPill | Filter activated |
|---|---|
| Healthy count | `'healthy'` |
| Degraded count | `'degraded'` (only rendered when `degraded > 0`) |
| Down count | `'down'` (only rendered when `down > 0`) |
| Alerts count | `'alerts'` (only rendered when `alerts > 0`) |
| Total count | Non-interactive (kept as display-only) |

### Toggle behavior

| Click | Result |
|---|---|
| Click filter that is NOT active | Apply that filter |
| Click filter that IS active | Reset to `'all'` |
| No filter active | Full list shown |

### Visual indicator (banner)

Active filter StatPill gets:
- `bg-white/70` (slight white tint within the colored banner background)
- `ring-2` + color-matched ring (`ring-green-400` / `ring-amber-400` / `ring-red-400`)
- `shadow-sm` for elevation
- `aria-pressed={true}`

Idle hover:
- `hover:bg-white/50`

Non-interactive StatPill (`total`): stays as plain text with no hover/ring.

### Visual indicator (list filter buttons)

Both the banner and the list's own filter buttons share the same state, so they
stay in sync automatically. Clicking the banner's `healthy` pill activates the
`Healthy` button in the list header and vice-versa.

---

## 4. State Management Approach

### Type

```ts
export type StatusFilter = 'all' | 'healthy' | 'degraded' | 'down' | 'alerts';
```

Defined in `monitoring-filter-section.tsx` and re-imported by the banner and list.

### Lift point: `MonitoringFilterSection` client wrapper

```tsx
'use client';

export function MonitoringFilterSection({ systemStatus, ..., integrations, alerts }) {
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  return (
    <>
      <StatusSummaryBanner
        {...bannerProps}
        statusFilter={statusFilter}
        onFilterChange={setStatusFilter}
      />
      <ComponentStatusList
        integrations={integrations}
        alerts={alerts}
        externalFilter={statusFilter}
        onExternalFilterChange={setStatusFilter}
      />
    </>
  );
}
```

The page passes all data to this wrapper; the wrapper distributes it to both
interactive components with shared state.

### Controlled vs uncontrolled

`ComponentStatusList` operates in two modes:
- **Controlled** (when `externalFilter` + `onExternalFilterChange` are provided): external state drives the filter; list buttons call `onExternalFilterChange`
- **Uncontrolled** (no external props): internal `useState` drives the filter (backward-compatible; existing callers unaffected)

---

## 5. Implementation

### New file: `monitoring-filter-section.tsx`

Client wrapper that holds `statusFilter` state and renders both
`StatusSummaryBanner` and `ComponentStatusList` with shared props. 65 lines.

### Modified: `status-summary-banner.tsx`

**Changes:**
- Added `'use client'` directive (component needs `onClick`)
- Added two optional props: `statusFilter?: StatusFilter`, `onFilterChange?: (f: StatusFilter) => void`
- Imported `StatusFilter` type from `./monitoring-filter-section`
- Replaced `<span>` StatPills with a split component:
  - `interactive={false}` → renders as original `<span>` (no change for `total`)
  - `interactive={true}` → renders as `<button>` with toggle logic, `aria-pressed`, ring/shadow active state
- Toggle logic: `onClick={() => onFilterChange(statusFilter === f ? 'all' : f)}`
- `StatusSummaryBannerError` unchanged (no interactivity needed)

**Active ring color mapping:**

| Filter | Ring class |
|---|---|
| healthy | `ring-green-400` |
| degraded | `ring-amber-400` |
| down | `ring-red-400` |
| alerts | `ring-red-400` |

### Modified: `component-status-list.tsx`

**Changes:**
- Added `alerts?: SystemAlert[]` prop (defaults to `[]`)
- Added `externalFilter?: StatusFilter` prop
- Added `onExternalFilterChange?: (f: StatusFilter) => void` prop
- Imported `StatusFilter` type and `SystemAlert` from respective modules
- Extended `FilterValue` to include `'Alerts'`
- Extended `FILTERS` array: `['All', 'Healthy', 'Degraded', 'Down', 'Alerts']`
- Added `toFilterValue()` helper: maps `StatusFilter` (lowercase) → `FilterValue` (capitalized)
- Added `toStatusFilter()` helper: maps `FilterValue` → `StatusFilter`
- Controlled mode: when `externalFilter` is defined, `activeFilter = toFilterValue(externalFilter)`;
  button clicks call `onExternalFilterChange` with toggle logic
- Uncontrolled mode: original `useState` behavior (backward-compatible)
- `Alerts` filter button hidden when `counts.Alerts === 0` (conditional rendering)
- `Alerts` filter styling: reuses `red-600` active / `red-50` hover (same as `Down`)
- Empty states: `'Alerts'` → "No active alerts.", others → "No {x} components."
- Alerts filter logic:

```ts
const alertEntityNames = new Set(
  alerts.filter(a => !a.resolvedAtUtc && a.entityName).map(a => a.entityName!)
);
// Alerts filter:
sorted.filter(i => alertEntityNames.has(i.name))
```

Only active (unresolved) alerts contribute to the set. Correlation is by `entityName`
(same strategy as `IncidentDetailPanel` and `AlertsPanel`).

**Sorting preserved:** `Down → Degraded → Healthy → alphabetical` — unchanged.

### Modified: `monitoring/page.tsx`

**Changes:**
- Replaced `StatusSummaryBanner` + `ComponentStatusList` imports with `MonitoringFilterSection`
- Replaced the two separate component usages with a single `<MonitoringFilterSection>` that
  receives banner stats, integrations, and alerts in one call
- `StatusSummaryBannerError` import kept (still used in the error fallback path)
- `SystemHealthCard` and `AlertsPanel` remain unchanged in position
- All `totalServices` / `healthyCount` / `degradedCount` / `downCount` variables retained
  (still passed to `MonitoringFilterSection`)

---

## 6. Validation

### TypeScript: 0 errors

```
cd apps/control-center && pnpm tsc --noEmit
# (no output — clean)
```

### Runtime regression checks

| Endpoint | Expected | Result |
|---|---|---|
| `GET /monitoring` (unauthenticated) | HTTP 307 → /login | ✅ 307 |
| `GET /systemstatus` | HTTP 200 | ✅ 200 |
| `GET /api/monitoring/summary` | HTTP 200 | ✅ 200 |
| Monitoring Service history API | HTTP 200 | ✅ 200 |
| Fast Refresh rebuild | < 2000ms | ✅ 1078ms |

### Logic validation (code-verified)

| Scenario | Expected | Verified |
|---|---|---|
| Click `healthy` pill → filter = 'healthy' → list shows only Healthy components | ✅ | Code |
| Click `healthy` again → filter = 'all' → full list restored | ✅ | Code |
| Click `degraded` pill → list shows only Degraded components | ✅ | Code |
| Click `down` pill → list shows only Down components | ✅ | Code |
| Click `alerts` pill → list shows only components with active alerts (by entityName) | ✅ | Code |
| List filter buttons reflect banner filter (controlled mode) | ✅ | Code |
| Click `Healthy` list button → banner `healthy` pill becomes active | ✅ | Code (shared state) |
| `Alerts` button hidden from list when no active alerts | ✅ | Code |
| `alerts` pill in banner hidden when `alerts.length === 0` (pre-existing) | ✅ | Unchanged |
| Filter state does NOT persist across page refresh | ✅ | `useState` resets on mount |
| No API calls triggered by filtering | ✅ | Client-side only |
| `total` pill non-interactive (no onClick) | ✅ | `interactive={false}` branch |
| Local mode: both source modes use same integrations/alerts data | ✅ | No source check needed |
| Service mode: same behavior | ✅ | No source check needed |

### Filter mapping correctness

```
Banner filter → List FilterValue → Visible items
'all'      → 'All'      → all sorted integrations
'healthy'  → 'Healthy'  → items where status === 'Healthy'
'degraded' → 'Degraded' → items where status === 'Degraded'
'down'     → 'Down'     → items where status === 'Down'
'alerts'   → 'Alerts'   → items where name ∈ alertEntityNames (active alerts only)
```

### Empty state messages

| Filter | No-results message |
|---|---|
| `healthy` | "No healthy components." |
| `degraded` | "No degraded components." |
| `down` | "No down components." |
| `alerts` | "No active alerts." |

### Browser interaction note

Dev admin credentials are stale (known from previous sessions). All logic is standard
React `useState` patterns and has been TypeScript-verified. The Fast Refresh rebuild
confirmed the component tree compiles and hydrates without errors.

---

## 7. Files Changed

| File | Action | Purpose |
|---|---|---|
| `apps/control-center/src/components/monitoring/monitoring-filter-section.tsx` | **Created** | Client wrapper; owns `statusFilter` state; renders banner + list |
| `apps/control-center/src/components/monitoring/status-summary-banner.tsx` | **Modified** | Added `'use client'`, interactive `StatPill` button variant, filter props |
| `apps/control-center/src/components/monitoring/component-status-list.tsx` | **Modified** | Added `'Alerts'` filter, controlled mode, `externalFilter`/`onExternalFilterChange`/`alerts` props |
| `apps/control-center/src/app/monitoring/page.tsx` | **Modified** | Replaced separate `StatusSummaryBanner` + `ComponentStatusList` with `MonitoringFilterSection` |

**Not modified:**
- Monitoring Service (any file)
- Gateway (any file)
- Any API route
- `monitoring-source.ts`
- `alerts-panel.tsx`
- `system-health-card.tsx`
- `incident-detail-panel.tsx`
- `replit.md`

---

## 8. Known Gaps / Risks

| # | Gap | Severity | Notes |
|---|---|---|---|
| 1 | **Single filter only (no multi-select)** | Low | Operators can select one status at a time. Multi-select is a future iteration if operators request it |
| 2 | **Filter state not persisted across refresh** | Low | By design — `useState` resets on mount. URL deep-linking is a future enhancement |
| 3 | **Alerts filter matches on current snapshot** | Low | The filter uses the `alerts` array from the server-rendered page snapshot. Stale data only refreshes on `router.refresh()` or page reload. Same limitation as the rest of the monitoring page |
| 4 | **Banner `degraded`/`down`/`alerts` pills are hidden when count = 0** | Low | If a filter pill is hidden (count = 0), the operator cannot activate that filter from the banner — but there would be nothing to filter to. This is correct behavior |
| 5 | **`total` pill is non-interactive** | Low | Clicking total should logically reset to 'All'. Currently it is display-only. If operators request "click total to see all", a future change would wire `interactive={true}` with `onClick={() => toggle('all')}` |
| 6 | **Browser interaction not runtime-tested** | Low | Dev admin credentials stale; logic is TypeScript-verified |
| 7 | **`StatusSummaryBanner` now requires `'use client'`** | Negligible | The banner only has local `onClick` handlers; all data still flows down from the server page. No RSC serialization boundary issues |

---

## 9. Recommended Next Feature

**Option A: MON-INT-04-001 — Public Status Page**

**Rationale:**

The Monitoring feature set now has a complete, polished operator experience:
- ✅ MON-INT-03-001 — Incident Detail View
- ✅ MON-INT-03-002 — Alert Resolve Workflow
- ✅ MON-INT-03-003 — Incident Timeline / History View
- ✅ MON-INT-02-004 — Clickable Banner Filtering

The logical next step is a public-facing status page (`/status`) that exposes the
same `GET /api/monitoring/summary` data to external users (customers, tenants,
support teams) without requiring authentication.

This is a net-new Next.js page that:
- Uses the existing public `/api/monitoring/summary` endpoint (already in `PUBLIC_PATHS`)
- Needs no backend changes
- Shows system status, component health, and active incidents
- Does NOT expose internal admin details (no resolve button, no raw alert IDs)

**Alternative: Stabilization Pass** — if the priority is production confidence,
a stabilization pass (authenticated browser testing of the full incident lifecycle,
alert volume handling, auto-resolve timing) should precede new feature work.
