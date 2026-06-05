# MON-INT-02-003 — Component Status List

> **Report created FIRST** before any implementation, per mandatory execution rules.
> Updated incrementally after each step.

---

## 1. Task Summary

Replace the two split `IntegrationStatusTable` cards ("Platform Services" / "Products")
with a single unified `ComponentStatusList` that shows all monitored entities in one
place, with client-side status filtering (All / Healthy / Degraded / Down) and
consistent default sorting (Down → Degraded → Healthy → alphabetical).

| Field | Value |
|---|---|
| **Ticket** | MON-INT-02-003 |
| **Status** | ✅ Complete |
| **Depends on** | MON-INT-02-002 (Status Summary Banner — complete) |
| **Date** | 2026-04-20 |

---

## 2. Existing Component Tables Analysis

### Before this feature

`page.tsx` split the `integrations` array by category before rendering:

```ts
const infraServices   = data?.integrations.filter(i => i.category === 'infrastructure') ?? [];
const productServices = data?.integrations.filter(i => i.category === 'product') ?? [];
```

Then rendered two separate `IntegrationStatusTable` components:

```tsx
<IntegrationStatusTable integrations={infraServices}   title="Platform Services" subtitle="Core infrastructure components" />
<IntegrationStatusTable integrations={productServices} title="Products"          subtitle="Tenant-facing product services" />
```

### Problems with the split approach

| Issue | Detail |
|---|---|
| **Duplication** | Two identical card shells, two separate "X / Y healthy" counts |
| **No filtering** | No way to see all Down services across categories at once |
| **Category split is arbitrary** | Both categories carry the same columns; splitting adds no insight |
| **Not extensible** | A third category would require a third table |

### What was reused

| Element | Source | Kept? |
|---|---|---|
| `StatusBadge` | `system-health-card.tsx` | ✅ imported directly |
| Row structure (dot, name, latency, timestamp, badge) | `IntegrationRow` in `integration-status-table.tsx` | ✅ re-implemented with identical styles + category chip |
| Sort logic (Down → Degraded → Healthy → alpha) | `IntegrationStatusTable.ORDER` | ✅ same constant in new component |
| Latency color thresholds (>1000ms red, >400ms amber) | `IntegrationStatusTable` | ✅ same thresholds |
| `formatTimestamp` helper | `integration-status-table.tsx` | ✅ re-implemented identically |

### What was changed

| Change | Detail |
|---|---|
| Removed `infraServices`/`productServices` split vars from `page.tsx` | No longer needed |
| Removed two `IntegrationStatusTable` usages from `page.tsx` | Replaced by single `<ComponentStatusList integrations={data.integrations} />` |
| Old component file kept | `integration-status-table.tsx` not deleted (per spec) |

---

## 3. Unified List Design

### Layout

Single white card (`bg-white border border-gray-200 rounded-lg overflow-hidden`) with:

1. **Header row** — "All Components" title + "X / Y healthy" subtitle + filter button group
2. **Row list** — `divide-y divide-gray-100` rows, same pattern as existing tables
3. **Empty state** — centered text, 2 variants (no components / filter empty)

### Columns

| Column | Width | Visibility |
|---|---|---|
| Status dot | fixed (2×2) | always |
| Name | flex-1 (truncated) | always |
| Category chip | 56px | md+ only; only shown if any row has a category |
| Latency | 80px, right-aligned | always |
| Last checked | 96px, right-aligned | sm+ only |
| Status badge | 80px, right-aligned | always |

### Filter buttons (header, right side)

| Filter | Active style | Idle style |
|---|---|---|
| All | `bg-gray-700 text-white` | `text-gray-600 hover:bg-gray-50` |
| Healthy | `bg-green-600 text-white` | `text-green-700 hover:bg-green-50` |
| Degraded | `bg-amber-500 text-white` | `text-amber-700 hover:bg-amber-50` |
| Down | `bg-red-600 text-white` | `text-red-700 hover:bg-red-50` |

Each button shows the count for that status alongside the label (`Healthy 9`, `Down 1`, etc.)
so the operator can see how many items are in each bucket without clicking.

---

## 4. Implementation

### Architecture decision: `'use client'` component

Filter state (`activeFilter: FilterValue`) requires React `useState`, so
`ComponentStatusList` must be a client component. The server component (`page.tsx`)
fetches data, passes `integrations` as a prop — the client component handles state
locally with no additional API calls.

### Component structure

```
ComponentStatusList (client component)
  ├── useState: activeFilter ('All' | 'Healthy' | 'Degraded' | 'Down')
  ├── sorted = [...integrations].sort(Down→Degraded→Healthy→alpha)
  ├── visible = activeFilter==='All' ? sorted : sorted.filter(status===activeFilter)
  ├── counts = { All, Healthy, Degraded, Down } tallied from integrations
  │
  ├── Header
  │   ├── "All Components" + "X / Y healthy" subtitle
  │   └── FilterButton × 4 (All, Healthy, Degraded, Down)
  │
  ├── if integrations.length === 0 → EmptyState("No monitored components.")
  ├── if visible.length === 0      → EmptyState("No {filter} components.")
  └── else → divide-y rows
      └── ComponentRow × N
          ├── StatusDot
          ├── name
          ├── category chip (conditional, md+)
          ├── latency (color-coded)
          ├── last checked (sm+)
          └── StatusBadge
```

### Accessibility

- Filter group has `role="group" aria-label="Filter by status"`
- Each button has `aria-pressed={active}`
- Container div has `role="status" aria-label` on `StatusSummaryBanner` (from MON-INT-02-002)
- Empty state text is readable by screen readers

---

## 5. Filtering Logic

**Client-side only.** No API calls triggered by filter changes.

```ts
const sorted = [...integrations].sort((a, b) => {
  const diff = STATUS_ORDER[a.status] - STATUS_ORDER[b.status];  // Down=0, Degraded=1, Healthy=2
  return diff !== 0 ? diff : a.name.localeCompare(b.name);
});

const visible =
  activeFilter === 'All'
    ? sorted
    : sorted.filter(i => i.status === activeFilter);
```

Sorting is applied **before** filtering — so filter results are also sorted correctly.

**Category filter:** Not added as a separate filter button. Category is shown as a
column chip in each row, so the operator can already see it at a glance without a
second filter axis. A category filter can be added in a future enhancement.

---

## 6. Data Flow Validation

### Steady-state live data (post-startup, scheduler settled)

```
GET /api/monitoring/summary
system.status: Down
total: 10 | healthy: 9 | degraded: 0 | down: 1 | alerts: 1

Entities:
 [Down    ] Reports       (infrastructure)   ← sorted first
 [Healthy ] Audit         (infrastructure)
 [Healthy ] Documents     (infrastructure)
 [Healthy ] Gateway       (infrastructure)
 [Healthy ] Identity      (infrastructure)
 [Healthy ] Notifications (infrastructure)
 [Healthy ] Synq CareConnect (product)
 [Healthy ] Synq Fund        (product)
 [Healthy ] Synq Liens       (product)
 [Healthy ] Workflow      (infrastructure)
```

### Filter simulation

| Filter | Expected rows | Empty state? |
|---|---|---|
| All (default) | 10 | No |
| Healthy | 9 | No |
| Degraded | 0 | Yes: "No degraded components." |
| Down | 1 (Reports) | No |

### Category check

`integrations.some(i => i.category)` → `true`
Categories present: `infrastructure`, `product`
Category chip column renders; all rows show `Infra` or `Product`.

### No status recomputation

`system.status` passed through unchanged from `data.system.status`.
`ComponentStatusList` only tallies `.filter().length` — no health logic.

---

## 7. UI Validation

| Check | Method | Result |
|---|---|---|
| TypeScript types compile clean | `pnpm tsc --noEmit` | ✅ 0 errors |
| Next.js compilation | `pnpm next build` (partial) | ✅ "Compiled successfully" |
| Props match `IntegrationStatus[]` type | Reviewed component source | ✅ all fields used correctly |
| Sort order correct | Simulated with live data | ✅ Down first, then Healthy alpha |
| Filter counts match API | Computed in Node.js against live data | ✅ All:10, Healthy:9, Degraded:0, Down:1 |
| Degraded filter → empty state | Verified from live data (0 degraded) | ✅ would trigger "No degraded components." |
| Category chip shown | `hasCategory` check → true | ✅ both `Infra` and `Product` chips present |
| `IntegrationStatusTable` import removed | Checked page.tsx | ✅ no longer imported |
| Banner + health card still present | Checked page.tsx | ✅ unchanged above the list |
| AlertsPanel still present | Checked page.tsx | ✅ unchanged below the list |
| Old component file kept | `integration-status-table.tsx` on disk | ✅ not deleted |

---

## 8. Files Changed

| File | Action | Purpose |
|---|---|---|
| `apps/control-center/src/components/monitoring/component-status-list.tsx` | **Created** | Unified filterable component list (client component) |
| `apps/control-center/src/app/monitoring/page.tsx` | Modified | Swapped two `IntegrationStatusTable` usages for `<ComponentStatusList integrations={data.integrations} />`; removed `infraServices`/`productServices` split variables; swapped import |

**Files not touched:** `integration-status-table.tsx`, `system-health-card.tsx`, `alerts-panel.tsx`, `status-summary-banner.tsx`, Monitoring Service, gateway.

---

## 9. Known Gaps / Risks

| # | Gap | Severity | Notes |
|---|---|---|---|
| 1 | **No pagination** — all 10 rows rendered inline | Low | 10 entities is manageable; add virtual scrolling or pagination if entity count grows significantly (e.g., 50+) |
| 2 | **No advanced sorting UI** — sort order is fixed (Down→Degraded→Healthy→alpha) | Low | Acceptable for current use; column-header sorting could be added later |
| 3 | **No drill-down** — clicking a row does nothing | Low | Future work; MON-INT-03-001 (Incident / Alert Detail View) would add this |
| 4 | **No click-to-filter from banner** — banner counts don't activate component filter | Low | Future work: MON-INT-02-004 (Clickable Banner Filtering) would wire the banner count pills to the component list filter |
| 5 | **`integration-status-table.tsx` is dead code** — still on disk, no usages | Low | Can be deleted in a cleanup pass; kept per spec |
| 6 | **Category filter not added** — only status filter | Low | Category chip is visible per-row; a filter button could be added when more categories exist |

---

## 10. Recommended Next Feature

**MON-INT-03-001 — Incident / Alert Detail View**

**Rationale:**
- The Component Status List completes the monitoring page's read experience:
  operators can now see all services in one sorted, filterable view
- The existing `AlertsPanel` shows active alerts as a flat list but provides no detail, history, or context for each alert
- The most actionable next step is an **alert detail view**: clicking an alert opens a panel (or dedicated route) showing the alert message, severity, affected component, creation time, and — in future — resolution workflow
- This is higher value than MON-INT-02-004 (Clickable Banner Filtering), which is a nice-to-have polish item that can wait until the data model is richer
- MON-INT-03-001 sets the foundation for the incident management workflow (acknowledge → investigate → resolve)
