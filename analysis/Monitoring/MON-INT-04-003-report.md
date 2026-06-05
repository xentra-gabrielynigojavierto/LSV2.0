# MON-INT-04-003 — Availability Timeline Bars

> **Report created FIRST**, before any implementation code was written. Updated incrementally.

---

## 1. Task Summary

Add 24-hour availability timeline bars to the public `/status` page so external users can see historical availability per monitored component, derived from real `check_results` data via the uptime aggregation engine (MON-INT-04-002).

This is a pure presentation-layer feature. No Monitoring Service endpoints were added or modified. The implementation adds:
1. A new public-safe BFF route (`/api/monitoring/uptime`) in the Control Center
2. A new `AvailabilityBars` presentational component
3. An update to `PublicComponentList` to embed bars per component row
4. An update to `status/page.tsx` to fetch uptime data server-side in parallel
5. A middleware whitelist addition for the new public BFF route

---

## 2. Existing Uptime History API Analysis

### Available Monitoring Service endpoints

| Endpoint | Used by feature |
|---|---|
| `GET /monitoring/uptime/rollups?window=24h` | Yes — to get all entityIds and overall uptimePercent per component |
| `GET /monitoring/uptime/history?entityId={guid}&window=24h` | Yes — to get per-entity hourly bucket breakdown |

Both are accessible via the YARP gateway with double-prefix pattern:
- `gateway:5010/monitoring/monitoring/uptime/rollups`
- `gateway:5010/monitoring/monitoring/uptime/history`

### History endpoint response shape (relevant fields)

```json
{
  "entityId": "...",
  "entityName": "Gateway",
  "window": "24h",
  "buckets": [
    {
      "bucketStartUtc": "2026-04-20T06:00:00",
      "uptimePercent":   100.0,
      "dominantStatus":  "Healthy",
      "insufficientData": false
    }
  ]
}
```

**`dominantStatus`** is exactly what the bars need (`Healthy | Degraded | Down | Unknown`).

### Key challenge: entityId not available in Control Center IntegrationStatus

The existing `IntegrationStatus` type used by the public page only carries `name`, not `entityId`. The `serviceGetMonitoringSummary` mapping drops `entityId`. The uptime history endpoint requires an entityId GUID.

**Resolution**: The BFF route calls the rollups endpoint first (which returns entityIds), then parallel-fetches history for each entity by ID, and returns a sanitized response keyed by `name`. The entityId never reaches the browser.

### Data sufficiency
- The history endpoint already returns `dominantStatus`, `uptimePercent`, and `insufficientData` per bucket — exactly what bar rendering requires
- No backend changes were needed

---

## 3. Availability Bar Design

### Chosen window: 24 hours / 24 hourly bars

**Rationale:**
- 24h is the shortest meaningful window for a status page
- The Monitoring Service produces one rollup row per entity per hour — exactly 24 bars per day
- Dev environment has ~24h of history; longer windows would show the same data with empty leading bars
- Bar count of 24 is compact and scannable on desktop (each bar ~4px wide in a row)

### Color mapping

| DominantStatus | Color | Class |
|---|---|---|
| `Healthy` | Green | `bg-green-400` |
| `Degraded` | Amber | `bg-amber-400` |
| `Down` | Red | `bg-red-500` |
| `Unknown` / `insufficientData` / no bucket | Gray | `bg-gray-200` |

### Per-component row layout

```
[dot] Component Name                    [timestamp]  [Operational]
      ████████████████████████  24 h             98.0%
```

- Name + status badge on the top line (unchanged from original)
- 24-bar strip + uptime % label on the second line, indented to align with the name
- A legend at the bottom of the Components card (shown only when bars are present)

### Legend items
`■ Operational  ■ Degraded  ■ Outage  ■ No data`
(rendered with the matching color blocks)

### Accessibility
- The bar strip has `role="img"` and `aria-label="24-hour availability history"`
- Each bar has a native `title` attribute: `"14:00 UTC — Healthy · 100.0%"` (visible on hover)
- Status meaning is conveyed by both color and position — no hover-only interaction required for basic understanding

---

## 4. Data Fetching / BFF Design

### BFF route: `GET /api/monitoring/uptime`

**Location:** `apps/control-center/src/app/api/monitoring/uptime/route.ts`
**Auth:** Public (whitelisted in Next.js middleware)
**Source toggle:** `MONITORING_SOURCE` env var (same toggle as the summary endpoint)

#### Local mode (`MONITORING_SOURCE !== 'service'`)
Returns `{ window: "24h", components: [] }`. No bars shown. Graceful degradation.

#### Service mode (`MONITORING_SOURCE=service`)
1. `GET {GATEWAY_URL}/monitoring/monitoring/uptime/rollups?window=24h` — get all components with entityId
2. `Promise.allSettled([...])` — parallel `GET /history?entityId=...` for each entity
3. Build sanitized response: strip entityId, round uptimePercent to 2dp, normalize dominantStatus

#### Sanitization (what is NOT in the response)
- No `entityId` UUIDs
- No raw check counts (upCount, downCount, etc.)
- No latency aggregates
- No backend URLs or service tokens

#### What IS in the response
```typescript
{
  window:     "24h",
  components: [
    {
      name:          "Gateway",
      uptimePercent: 100.0,      // null if insufficientData
      buckets: [
        {
          bucketStartUtc:  "2026-04-20T06:00:00",
          dominantStatus:  "Healthy",
          uptimePercent:   100.0,
          insufficientData: false
        }
      ]
    }
  ]
}
```

### Component identity mapping
The bars are keyed by `name` (string) on both sides:
- BFF response keyed by `entityName` from Monitoring Service
- `PublicComponentList` does `uptimeByName.get(item.name)` — exact string match

**Tradeoff acknowledged**: A naming inconsistency between services would silently suppress bars for that component (not crash). Acceptable in service mode where both sides source names from the same Monitoring Service.

### Fetch pattern
All fetching is server-side in the Next.js Server Component. No client-side waterfall. The status page uses `Promise.all([fetchSummary(), fetchUptimeHistory()])` to parallelize the two BFF calls.

---

## 5. Implementation

### Files created

**`apps/control-center/src/app/api/monitoring/uptime/route.ts`**
- BFF route: `GET /api/monitoring/uptime`
- Checks `MONITORING_SOURCE`, returns empty in local mode
- In service mode: fetches rollups, parallel-fetches history, sanitizes and returns public-safe JSON
- Exports `PublicUptimeBucket` and `PublicUptimeResponse` types for use in components

**`apps/control-center/src/components/monitoring/availability-bars.tsx`**
- `AvailabilityBars` — renders 24 hourly color-coded bars with ARIA label and title tooltips
- `AvailabilityLegend` — 4-item legend placed once at the bottom of the Components card
- Pure presentational; no server calls, no admin controls

### Files modified

**`apps/control-center/src/components/monitoring/public-component-list.tsx`**
- Accepts optional `uptimeByName?: Map<string, { uptimePercent, buckets }>` prop
- Each `ComponentRow` receives its matching uptime data
- Bars + uptime % label rendered below the name/badge row when data is present
- "History unavailable" shown per-component when entry exists but buckets are empty
- `AvailabilityLegend` rendered at the bottom of the card when bars are shown
- Original row layout (dot + name + timestamp + badge) unchanged

**`apps/control-center/src/app/status/page.tsx`**
- Adds `fetchUptimeHistory()` — calls `/api/monitoring/uptime` with try/catch (returns null on failure)
- Uses `Promise.all([fetchMonitoringSummary(), fetchUptimeHistory()])` for parallel server-side fetch
- Builds `uptimeByName` Map from the response
- Passes `uptimeByName` to `PublicComponentList`
- Status page still loads if uptime fetch fails (graceful degradation)

**`apps/control-center/src/middleware.ts`**
- Added `/api/monitoring/uptime` to `PUBLIC_PATHS` array
- Without this, the route returned 302 → `/login` for unauthenticated requests

---

## 6. Public Data Exposure Review

### Fields now publicly exposed

| Field | Source | Justification |
|---|---|---|
| `name` | MonitoringService.entityName | Already public (same field used for component list) |
| `uptimePercent` | MonitoringService rollups | Rounded to 2dp; standard status-page disclosure |
| `buckets[].dominantStatus` | MonitoringService history | Categorical label only (Healthy/Degraded/Down/Unknown) |
| `buckets[].uptimePercent` | MonitoringService history | Rounded, no internal count detail |
| `buckets[].insufficientData` | MonitoringService history | Boolean flag; acceptable |

### Fields intentionally excluded

| Field | Reason |
|---|---|
| `entityId` (UUID) | Internal identifier; stripped at BFF layer |
| `upCount`, `downCount`, `degradedCount`, `unknownCount` | Internal performance metrics |
| `avgLatencyMs`, `maxLatencyMs` | Internal performance metrics |
| `totalCountable` | Internal metric |

### Verified: zero UUIDs in page HTML
```
curl .../status | grep -Eo '[0-9a-f]{8}-...' | wc -l  →  0
```

### Verified: no admin controls in rendered markup
No admin/delete/edit/manage links or buttons in the page HTML.

---

## 7. Validation

### Validation matrix

| Check | Method | Result |
|---|---|---|
| A. `/status` loads without auth | `curl -w "%{http_code}"` | **200** ✓ |
| B. Component rows render | Count "Operational" in HTML | **22** (11 components × 2) ✓ |
| C. No entityId UUIDs in HTML | `grep -Eo UUID pattern \| wc -l` | **0** ✓ |
| D. No admin controls in HTML | Count admin/delete/edit/manage | **0** in markup ✓ |
| E. `/api/monitoring/uptime` is public | `curl -w "%{http_code}"` | **200** ✓ |
| F. BFF returns correct structure | JSON key inspection | `"window"` + `"components"` keys present ✓ |
| G. `/api/monitoring/summary` still works | `curl -w "%{http_code}"` | **200** ✓ |
| H. Monitoring Service endpoints still work | Direct port 5015 calls | **200** ✓ |
| I. BFF in local mode returns empty | JSON inspection | `{"window":"24h","components":[]}` ✓ |
| J. Gateway rollups — 11 components with entityIds | Direct gateway call | **11 components** ✓ |
| K. Gateway history — hourly buckets present | Direct gateway call for Audit entity | `dominantStatus: "Healthy"`, `insufficientData: false` ✓ |
| L. TypeScript clean build | `tsc --noEmit` | **0 errors, 0 warnings** ✓ |

### Sparse-data behavior (code-verified)
- Empty components → `uptimeByName` map is empty → bars silently absent; page renders normally
- 11 buckets in a 24-bar window → 11 real bars + 13 gray leading bars (sorted chronologically)
- "History unavailable" fallback: shown per-component when `uptime` entry exists but `buckets` is empty

### Local mode vs service mode
**Dev default (local mode):** BFF returns `{ components: [] }` → bars not shown → page renders correctly as before.
**Service mode (`MONITORING_SOURCE=service`):** BFF fetches from gateway → real bars rendered per component. Validated through direct gateway calls above.

---

## 8. Files Changed

### Created
| File | Purpose |
|---|---|
| `apps/control-center/src/app/api/monitoring/uptime/route.ts` | Public BFF route — proxies and sanitizes uptime data |
| `apps/control-center/src/components/monitoring/availability-bars.tsx` | `AvailabilityBars` + `AvailabilityLegend` components |

### Modified
| File | Change |
|---|---|
| `apps/control-center/src/components/monitoring/public-component-list.tsx` | Accept `uptimeByName` prop; render bars per row; show legend |
| `apps/control-center/src/app/status/page.tsx` | Parallel-fetch uptime; build map; pass to `PublicComponentList` |
| `apps/control-center/src/middleware.ts` | Add `/api/monitoring/uptime` to `PUBLIC_PATHS` |

### Removed
None.

---

## 9. Known Gaps / Risks

| Gap | Detail |
|---|---|
| **Bars absent in local mode** | `MONITORING_SOURCE=local` returns empty — bars only appear with `MONITORING_SOURCE=service`. Page degrades gracefully; bars simply not shown. |
| **Name-based matching** | Uptime data matched to `IntegrationStatus` by `name` string. A naming inconsistency would silently suppress bars (not crash). |
| **N+1 fetch pattern** | BFF makes 1 rollups call + N history calls in parallel. Fine for 11 entities; may need a server-aggregated endpoint if entity count reaches hundreds. |
| **No hover details on mobile** | Native `title` attributes not shown on touch devices. Color + legend still convey meaning. |
| **24h window only** | No window toggle implemented. BFF and component both support `window` as a parameter — UI toggle is a straightforward addition. |
| **No loading skeleton** | Bars are server-rendered; no spinner or skeleton state. If BFF fails, page loads cleanly without bars. |
| **Latency not included** | `avgLatencyMs`/`maxLatencyMs` available in history endpoint but not surfaced (deferred to MON-INT-04-004). |

---

## 10. Recommended Next Feature

**Option A: MON-INT-04-004 — Response Time Charts** ← recommended

The availability bars are complete and validated. The uptime history endpoint already returns `avgLatencyMs` and `maxLatencyMs` per hourly bucket. MON-INT-04-004 would add latency sparklines per component on the internal monitoring dashboard (not the public page — latency is an internal metric).

**Option B: Stabilization**

If the current status page experience is feature-complete for the near term:
1. Set `MONITORING_SOURCE=service` to activate real bars in production
2. Validate N+1 fetch performance under production entity counts
3. Consider adding a 7-day window toggle to the status page
