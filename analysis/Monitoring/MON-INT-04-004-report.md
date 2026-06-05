# MON-INT-04-004 — Response Time Charts

> **Report created FIRST before any implementation work began.**

---

## 1. Task Summary

Add per-component latency sparkline charts to the internal `/monitoring` page, using real
`avgLatencyMs` data from the uptime aggregation engine's hourly history buckets.
Charts are internal-only; the public `/status` page is not modified.

---

## 2. Existing Latency Data Analysis

### Backend contract (Monitoring Service)

`UptimeHistoryBucketResponse` (C# record — `UptimeResponses.cs`):

| Field            | Type      | Notes                                      |
|------------------|-----------|--------------------------------------------|
| `BucketStartUtc` | DateTime  | Hour bucket boundary (UTC)                 |
| `UptimePercent`  | double?   | % up within the hour                       |
| `DominantStatus` | string    | Healthy / Degraded / Down / Unknown        |
| `AvgLatencyMs`   | double?   | **Primary latency field for charts**       |
| `MaxLatencyMs`   | long      | Optional — secondary indicator             |
| `InsufficientData` | bool    | True if < minimum required checks          |

`AvgLatencyMs` is nullable — buckets with zero checks have a null value.
Resolution: **1 hour**, window: **24 h** (same as availability bars).

### Public BFF strips latency

`/api/monitoring/uptime` intentionally excludes `avgLatencyMs` and `maxLatencyMs`
(documented in route file header). This is correct — latency is operational data.

### Conclusion

Real latency data is available from the aggregation engine.
No new backend changes are required.
A new authenticated BFF route will pass the data through for internal consumption only.

---

## 3. Chart Placement Decision

**Location**: Internal `/monitoring` page only, inside `component-status-list.tsx`.

**Pattern**: Expandable row. Each `ComponentRow` gains a chevron toggle button.
Clicking it reveals a latency sparkline panel below the row (no layout shift on the row itself).

**Rationale**:
- Latency is operational data, not for public consumption.
- Row-level drill-down matches how operators already think ("what is Service X doing?").
- Avoids crowding the list by keeping it opt-in.

---

## 4. Data Fetching / BFF Design

### New route: `/api/monitoring/latency`

- **Authentication**: Required (same pattern as `/api/monitoring/summary`).
- **MONITORING_SOURCE guard**: Returns empty if `local`.
- **Flow**:
  1. Fetch rollups → get `entityId` + `entityName` per component.
  2. Fetch history per entity in parallel (same as public uptime route).
  3. Map buckets: include `avgLatencyMs`, `maxLatencyMs`, `bucketStartUtc`, `insufficientData`.
  4. Strip `entityId` from response (internal UUID — never sent to browser).

### Response shape (InternalLatencyResponse)

```json
{
  "window": "24h",
  "components": [
    {
      "name": "Identity Service",
      "buckets": [
        {
          "bucketStartUtc": "2026-04-20T10:00:00Z",
          "avgLatencyMs": 142.5,
          "maxLatencyMs": 380,
          "insufficientData": false
        }
      ]
    }
  ]
}
```

No `entityId` exposed. `name` matches `IntegrationStatus.name` for correlation in the UI.

---

## 5. Chart Design

**Type**: SVG-based sparkline (no external chart library).
**Size**: Fixed height 40 px, full-width within the expanded panel.
**Window**: 24 hours (one point per bucket).
**Line**: avg latency.
**Shaded area**: subtle fill below avg line.
**Optional indicator**: max latency as faint dotted reference line (if space allows).
**Fallback**: "No latency history available" text when `buckets` is empty or all null.
**State handling**:
  - Loading: skeleton pulse.
  - Error: "Latency unavailable".
  - All-null data: "No latency history available".
  - Partial data: render available points only.

---

## 6. Implementation

### Files created

- `apps/control-center/src/app/api/monitoring/latency/route.ts` — authenticated BFF route
- `apps/control-center/src/components/monitoring/latency-sparkline.tsx` — SVG sparkline component

### Files modified

- `apps/control-center/src/components/monitoring/component-status-list.tsx` — expandable rows + chart integration

### Approach

- BFF fetched **server-side on demand** (client fetches once when expanded, cached in component state).
- `ComponentStatusList` fetches the whole latency payload once on first expansion, then slices per component — avoids N+1 client requests.
- `LatencySparkline` is a pure SVG component — renders synchronously once data is present.

---

## 7. Public/Internal Exposure Review

| Item                  | Exposed publicly? | Notes                                   |
|-----------------------|-------------------|-----------------------------------------|
| avgLatencyMs          | No                | New route is internal-only              |
| maxLatencyMs          | No                | Same                                    |
| entityId (UUID)       | No                | Stripped at BFF layer                   |
| /status page          | Unchanged         | No modifications to public-component-list.tsx |
| Component name        | Internal only     | Matches name already shown in internal list |

---

## 8. Validation

| Check | Result |
|-------|--------|
| TypeScript clean (`tsc --noEmit`, control-center) | **PASS — zero errors** |
| `/monitoring` page renders without crash | PASS (Fast Refresh clean) |
| Expanding a row triggers single `/api/monitoring/latency` fetch | PASS (verified in code: `fetchLatency` is guarded by `latencyState !== 'idle'`) |
| Sparkline renders from real data | Runtime-only (requires `MONITORING_SOURCE=service`) |
| Local mode empty state | PASS — returns `{ components: [] }` from BFF, sparkline shows "No latency history available" |
| Sparse null `avgLatencyMs` buckets | PASS — filter removes null values; gaps produce segment breaks in SVG |
| No changes to `/status` page | PASS — `public-component-list.tsx` untouched |
| No UUIDs exposed | PASS — `entityId` stripped in BFF; `name` field only in response |

---

## 9. Files Changed

| File | Action |
|------|--------|
| `apps/control-center/src/app/api/monitoring/latency/route.ts` | Created |
| `apps/control-center/src/components/monitoring/latency-sparkline.tsx` | Created |
| `apps/control-center/src/components/monitoring/component-status-list.tsx` | Modified |

---

## 10. Known Gaps / Risks

- **24h window only**: No selector for 7d/30d view (deferred to MON-INT-05 window selector).
- **avg latency only in the line** (`maxLatencyMs` shown as dotted reference — may omit if no room).
- **No tooltips**: SVG native `<title>` only; no interactive hover layer.
- **N+1 avoided but full payload fetched once**: if there are many components, the payload grows linearly with component count × 24 buckets. Acceptable for typical fleet sizes (<30 services).
- **Local mode**: sparkline shows empty state — no fabricated metrics.

---

## 11. Recommended Next Feature

**Option A — Stabilization / Production Validation (RECOMMENDED)**
Verify charts in a real `MONITORING_SOURCE=service` environment before adding more features.

**Option B — MON-INT-05-001: Window Selector (7d / 30d View)**
Extend the latency and availability BFF routes to accept a `window` query param and
surface a time-range selector in the internal monitoring UI.
