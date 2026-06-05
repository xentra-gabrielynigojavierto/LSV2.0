# MON-INT-03-001 — Incident / Alert Detail View

> **Report created FIRST** before any implementation, per mandatory execution rules.
> Updated incrementally after each step.

---

## 1. Task Summary

Add a read-only Incident / Alert Detail View to the Control Center monitoring page so
operators can click any alert row and immediately see the key incident context in a
slide-over panel: severity, affected component + current status, timestamps, and
alert reference ID. No mutation flows introduced.

| Field | Value |
|---|---|
| **Ticket** | MON-INT-03-001 |
| **Status** | ✅ Complete |
| **Depends on** | MON-INT-02-003 (Component Status List — complete) |
| **Date** | 2026-04-20 |

---

## 2. Existing Alert UI Analysis

### AlertsPanel (pre-feature)

File: `apps/control-center/src/components/monitoring/alerts-panel.tsx`

- **Component type:** Pure server component (no interactivity)
- **Props:** `{ alerts: SystemAlert[] }`
- **Renders:** Sorted alert rows (Critical → Warning → Info, then newest-first), severity badge, icon, message, timestamp
- **Row element:** `<div>` — not clickable, no selection state

### SystemAlert type (pre-feature)

```ts
export interface SystemAlert {
  id:           string;
  message:      string;
  severity:     AlertSeverity;
  createdAtUtc: string;
  // MISSING: entityName, resolvedAtUtc
}
```

### Raw alert payload from Monitoring Service

```json
{
  "alertId":       "7c4b2e21-184d-44c7-97dd-52e78ac14bbd",
  "entityId":      "c3412103-09c4-42d3-82be-2ead6623fc46",
  "name":          "Reports",
  "severity":      "Critical",
  "message":       "Status transitioned Unknown -> Down (network failure).",
  "createdAtUtc":  "2026-04-20T06:27:27.762812",
  "resolvedAtUtc": null
}
```

**Finding:** The raw Monitoring Service alert contains `name` and `resolvedAtUtc` — but `monitoring-source.ts` was stripping both fields when mapping to `SystemAlert`. They never reached the CC type or UI.

### Local mode alert structure

In local mode, `monitoring-source.ts` builds synthetic alerts from probe results:
```ts
{
  id:           `alert-${r.name.toLowerCase()}`,  // e.g., "alert-reports"
  message:      `${r.name} is ${r.status}...`,
  severity:     ...,
  createdAtUtc: r.lastCheckedAtUtc,
  // entityName: NOT passed through (field available in the probe result)
}
```

**Finding:** In local mode, `r.name` (the component name) was available but not mapped to `SystemAlert`. Now it is.

### Correlation key analysis

| Mode | Alert has | Integrations have | Correlation |
|---|---|---|---|
| service | `name` (component display name) | `name` (display name) | name → name ✓ |
| local | `r.name` in probe result | `name` (display name) | name → name ✓ |

**Decision:** Name-based correlation (`alert.entityName` ↔ `integration.name`). ID-based correlation (`entityId`) was considered but `IntegrationStatus` does not include `entityId` in the CC type — it only has the display name. Name-based correlation works reliably because both alert and integration use the same display name (e.g., `"Reports"`, `"Gateway"`) in both modes.

### Second consumer found

`apps/control-center/src/app/systemstatus/page.tsx` also uses `AlertsPanel` — discovered during TypeScript check. Made `integrations` optional with a default of `[]` so this caller is not broken.

---

## 3. Detail Interaction Pattern Decision

**Chosen: Slide-over panel (right side of viewport)**

| Pattern | Decision | Reason |
|---|---|---|
| Slide-over | ✅ Chosen | Preserves full monitoring page context; operator can still see the alerts list while reading details; standard ops UX for "drill-down without navigation" |
| Modal | Rejected | Blocks page entirely; more disruptive for a read-only detail view |
| Inline section | Rejected | Pushes content down and breaks the page layout; harder to dismiss |

**Slide-over specifics:**
- Fixed position, right side of viewport (`inset-y-0 right-0`)
- `max-w-md` (384px) width — readable detail without obscuring the left 2/3 of the page
- Semi-transparent dark backdrop — makes the page context still visible
- Dismiss on: backdrop click, `×` button, or `Escape` key
- Red/amber/blue severity accent bar at the top of the panel for instant visual context

---

## 4. Data Mapping / Correlation Strategy

### Type extension: `SystemAlert`

Added two optional fields:

```ts
export interface SystemAlert {
  id:            string;
  message:       string;
  severity:      AlertSeverity;
  createdAtUtc:  string;
  entityName?:   string;        // component display name; used for correlation with integrations
  resolvedAtUtc?: string | null; // null or absent = still active
}
```

`entityName` and `resolvedAtUtc` are optional to preserve backward compatibility with any existing serialized data or callers that don't set them.

### monitoring-source.ts changes

**Service mode mapping (before → after):**
```ts
// Before:
{ id: a.alertId, message: a.message, severity: ..., createdAtUtc: a.createdAtUtc }

// After:
{ id: a.alertId, message: a.message, severity: ..., createdAtUtc: a.createdAtUtc,
  entityName: a.name, resolvedAtUtc: a.resolvedAtUtc }
```

**Local mode mapping (before → after):**
```ts
// Before:
{ id: `alert-${r.name}`, message: `${r.name} is...`, severity: ..., createdAtUtc: ... }

// After:
{ id: `alert-${r.name}`, message: `${r.name} is...`, severity: ..., createdAtUtc: ...,
  entityName: r.name, resolvedAtUtc: undefined }
```

### Correlation logic (in AlertsPanel)

```ts
const relatedIntegration = selectedAlert?.entityName
  ? integrations.find(i => i.name === selectedAlert.entityName)
  : undefined;
```

**Fallback if correlation fails:** IncidentDetailPanel shows the `entityName` text (if present) with the message "component not found in integration list — current status unavailable". If no `entityName` at all: "No component information available." Page does not crash.

### Live correlation validation

```
Alert: { entityName: "Gateway", ... }
integrations.find(i => i.name === "Gateway") → Gateway [Down] ✓

Correlation field: entityName: "Gateway" ← threaded through correctly
```

---

## 5. Implementation

### Component: `IncidentDetailPanel` (new)

File: `apps/control-center/src/components/monitoring/incident-detail-panel.tsx`

- `'use client'` — manages `Escape` key listener via `useEffect`
- Props: `{ alert: SystemAlert; integration: IntegrationStatus | undefined; onClose: () => void }`
- Fixed slide-over from right, `z-50`
- Backdrop at `z-40` for click-to-dismiss
- Sections: Incident Message, Affected Component (status + category + latency + last checked), Timestamps (created + resolved/unresolved), Alert Reference (ID for ops)
- Severity accent bar (red/amber/blue) and Active/Resolved status pill in header
- Read-only footer note: "Read-only view · Data from Monitoring Service"
- No mutation actions

### Component: `AlertsPanel` (converted to client component)

File: `apps/control-center/src/components/monitoring/alerts-panel.tsx`

Changes:
- Added `'use client'` directive
- Added `integrations?: IntegrationStatus[]` prop (optional, defaults to `[]`)
- Added `useState<SystemAlert | null>(null)` for selected alert
- Alert rows converted from `<div>` to `<button>` — click toggles selection (click same alert again to deselect)
- Selected row gets `ring-1 ring-inset ring-gray-300` highlight
- Each row shows `entityName` (if present) in the timestamp sub-line for quick context
- Renders `IncidentDetailPanel` when `selectedAlert !== null`
- "Click an alert to view incident details" hint shown in footer when alerts exist

### Data flow

```
MonitoringSummary.alerts → page.tsx (server component)
  └── AlertsPanel (client, 'use client')
        ├── useState: selectedAlert
        ├── Renders alert rows (buttons)
        └── When selectedAlert set:
              ├── Correlates: integrations.find(i => i.name === selectedAlert.entityName)
              └── Renders IncidentDetailPanel (slide-over)
                    ├── alert: SystemAlert (with entityName, resolvedAtUtc)
                    └── integration: IntegrationStatus | undefined
```

---

## 6. Validation

### TypeScript

```
pnpm tsc --noEmit → 0 errors, 0 output
```

Previously found and fixed: `systemstatus/page.tsx` was a second consumer of `AlertsPanel` that would have broken. Fixed by making `integrations` optional with default `[]`.

### Live data validation (local mode)

```
GET /api/monitoring/summary → HTTP 200

Alert[0]:
  entityName:    "Gateway"          ← new field threaded through ✓
  resolvedAtUtc: null/absent        ← local mode (no resolution tracking) ✓
  correlates to: Gateway [Down]     ← name-based correlation works ✓
```

### Endpoint health

| Endpoint | Expected | Result |
|---|---|---|
| `GET /api/monitoring/summary` | HTTP 200 | ✅ 200 |
| `GET /monitoring` (unauthenticated) | HTTP 307 → /login | ✅ 307 |
| `GET /systemstatus` | HTTP 200 | ✅ 200 (AlertsPanel without integrations works) |

### Interaction paths (code-verified)

| Scenario | Handling |
|---|---|
| No alerts | Alert list shows "No active alerts."; no detail trigger; AlertsPanel renders empty state |
| Alert clicked | `setSelectedAlert(alert)`; panel opens; selected row highlighted |
| Same alert clicked again | `setSelectedAlert(null)`; panel closes |
| Backdrop clicked | `onClose()` → `setSelectedAlert(null)` |
| `×` button clicked | `onClose()` → `setSelectedAlert(null)` |
| `Escape` key pressed | `useEffect` listener → `onClose()` |
| Correlation succeeds | Integration status + latency + last checked shown |
| Correlation fails (entityName present but not in list) | "Component not found in integration list" fallback |
| No entityName on alert | "No component information available" fallback |
| resolvedAtUtc is null / absent | "Unresolved" shown in red |
| resolvedAtUtc is a timestamp | Resolution timestamp shown in green |
| systemstatus page (no integrations prop) | Defaults to `[]`; slide-over opens but shows "No component information available" fallback |

**What was not runtime-tested:** The authenticated browser interaction flow (click alert row → slide-over opens → dismiss) could not be browser-tested because dev admin credentials in README_DEV.md are stale. All prop flows, TypeScript compilation, and live data enrichment confirmed. The interaction logic is implemented in standard React `useState`/`useEffect` patterns with no novel rendering paths.

---

## 7. Files Changed

| File | Action | Purpose |
|---|---|---|
| `apps/control-center/src/types/control-center.ts` | Modified | Extended `SystemAlert` with `entityName?: string` and `resolvedAtUtc?: string \| null` |
| `apps/control-center/src/lib/monitoring-source.ts` | Modified | Thread through `entityName` and `resolvedAtUtc` in both service and local modes |
| `apps/control-center/src/components/monitoring/incident-detail-panel.tsx` | **Created** | Slide-over detail panel — severity, component status, timestamps, alert ref |
| `apps/control-center/src/components/monitoring/alerts-panel.tsx` | Modified | Converted to client component; added selection state; alert rows become buttons; integrates `IncidentDetailPanel` |
| `apps/control-center/src/app/monitoring/page.tsx` | Modified | Pass `data.integrations` to `AlertsPanel` for correlation |

**Not modified:** `systemstatus/page.tsx` (backward compat via optional prop), Monitoring Service, gateway, `status-summary-banner.tsx`, `component-status-list.tsx`, `system-health-card.tsx`.

---

## 8. Known Gaps / Risks

| # | Gap | Severity | Notes |
|---|---|---|---|
| 1 | **No acknowledge / resolve actions** — panel is read-only | Low | By design; mutation flows are future work (MON-INT-03-002) |
| 2 | **Name-based correlation** — not ID-based | Low | `IntegrationStatus` does not carry `entityId` in the CC type; name-based is reliable for current entity set (all unique names). Future: add `entityId` to `IntegrationStatus` for exact matching |
| 3 | **No historical timeline** — only current status, not trend | Low | Future work; requires historical status snapshots from Monitoring Service |
| 4 | **resolvedAtUtc always null in local mode** — local probe engine doesn't track resolution | Low | Documented; local mode is temporary (to be replaced by Monitoring Service) |
| 5 | **Browser interaction not runtime-tested** — dev admin credentials stale | Low | TypeScript + API payload fully validated; interaction logic is standard React patterns |
| 6 | **systemstatus page shows degraded incident detail** — `integrations=[]` means correlation always fails | Low | `systemstatus/page.tsx` is a public-facing read-only status page; it can pass integrations if desired in a follow-up |
| 7 | **No pagination in AlertsPanel** — all alerts rendered at once | Low | Acceptable for current alert volumes; add pagination/virtual scroll if alerts exceed 20–30 |

---

## 9. Recommended Next Feature

**MON-INT-03-002 — Alert Acknowledge / Resolve Workflow**

**Rationale:**
- The detail view is complete and stable — operators can now open any alert and immediately see the full incident context
- The natural operational follow-up is the ability to **acknowledge** (mark as seen) and **resolve** (mark as fixed) an alert from the same panel
- This requires a new admin API endpoint on the Monitoring Service (`PATCH /monitoring/admin/alerts/{id}/resolve`) and a minimal CC BFF action — the platform auth already supports this via the `MonitoringAdmin` policy (ServiceToken or Bearer+PlatformAdmin)
- MON-INT-03-002 is higher priority than MON-INT-02-004 (Clickable Banner Filtering) because resolving alerts provides direct operational value; the banner filter is a polish item
