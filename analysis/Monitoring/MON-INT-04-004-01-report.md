# MON-INT-04-004-01 — Public Status Page Response Time Charts

> Report created FIRST before any implementation steps, per mandatory execution rules.
> Updated incrementally throughout implementation.

---

## 1. Task Summary

**Feature**: Add public-safe response-time (latency) sparkline charts to the `/status` page so external users can see recent response-time trends per component alongside the existing availability bars.

**Constraints**:
- Must reuse existing Monitoring → BFF → Public UI pipeline
- No unsafe data exposure (no entity IDs, alert IDs, raw check counts, internal URLs)
- No heavy chart libraries
- No full page redesign
- Real `avgLatencyMs` from monitoring history only

---

## 2. Existing Data Analysis

### Monitoring Service history endpoint

`GET /monitoring/monitoring/uptime/history?entityId=...&window=24h`

Each bucket contains:
```json
{
  "bucketStartUtc": "2026-04-20T06:00:00",
  "uptimePercent": 97.87,
  "dominantStatus": "Healthy",
  "upCount": 92,
  "degradedCount": 0,
  "downCount": 2,
  "unknownCount": 0,
  "avgLatencyMs": 4.82,
  "maxLatencyMs": 112,
  "insufficientData": false
}
```

### Current BFF `/api/monitoring/uptime`

Before this feature, the BFF explicitly stripped `avgLatencyMs`, per the header comment:
> `latency aggregates (internal metric)` — NOT exposed.

The `HistoryBucket` interface did not include `avgLatencyMs`, so it was silently dropped.

### Key findings

- `avgLatencyMs` is real, per-bucket, aggregated (avg of all checks in that hour/day)
- `maxLatencyMs` is also available but NOT needed for public display
- Both 24h (hourly) and 7d/30d (daily-aggregated) windows are supported
- The `aggregateLatencyToDaily` function already existed in `uptime-aggregation.ts`
- An existing internal `latency-sparkline.tsx` existed for `/monitoring` (auth-protected), marked "never rendered on /status"
- A blank `public-latency-sparkline.tsx` file did not exist — created new

---

## 3. Data Exposure Decision

**What is safe to expose publicly:**

| Field | Decision | Reason |
|---|---|---|
| `avgLatencyMs` per bucket | ✅ **SAFE** | Aggregated trend data — same as uptime %. No internal detail. |
| `maxLatencyMs` | ❌ **NOT exposed** | Diagnostic metric; exposes internal peak latency details |
| `upCount`, `downCount`, etc. | ❌ **NOT exposed** | Raw check counts — internal detail |
| `entityId` | ❌ **NOT exposed** | Internal UUID — already stripped by BFF |

**Reasoning**: `avgLatencyMs` per bucket is equivalent in sensitivity to `uptimePercent` per bucket — both are simple aggregated metrics. Exposing an avg latency trend is standard practice on all major public status pages (Stripe, GitHub, etc.). It provides user-visible value (trend visibility) without disclosing internal infrastructure details.

---

## 4. BFF Design

**Decision: Extend existing `/api/monitoring/uptime`** (preferred approach from spec).

Changes to `uptime/route.ts`:
1. Added `avgLatencyMs: number | null` to the `HistoryBucket` internal interface (to capture from monitoring service response)
2. Added `avgLatencyMs: number | null` to `PublicUptimeBucket` (exported public type)
3. Added `safeLatency()` sanitizer to reject non-finite or negative values
4. For **24h**: passes `avgLatencyMs` through directly (rounded to 1dp)
5. For **7d/30d**: runs `aggregateLatencyToDaily()` on the same raw history buckets (separate pass alongside existing `aggregateUptimeToDaily()`), then merges by day key
6. Explicitly does NOT include `maxLatencyMs` in any public output

No new BFF route was needed.

---

## 5. Chart Design

**Type**: Pure SVG sparkline — no charting library dependency.

**Dimensions**:
- Height: 32px (default, configurable)
- Width: 100% (responsive via `viewBox="0 0 240 32"` + `preserveAspectRatio="none"`)

**Visual elements**:
- Thin baseline (gray-200)
- Indigo-500 polyline segments (avg latency trend)
- Indigo fill under the line (very light, 10% opacity)
- Single dot for single-point segments
- Invisible hit targets with `<title>` for native tooltip (`"HH:MM UTC — Xms avg"`)
- Minimal label row: `— Avg response time · peak Xms`

**Gap handling**: Null or `insufficientData` buckets cause a gap in the line (no fabricated values).

**Fallback states**:
- No valid data → `"No response time history"` text
- Some null buckets → partial line rendered (remaining valid points still shown)

---

## 6. Implementation

### Files changed

**`apps/control-center/src/app/api/monitoring/uptime/route.ts`** — BFF extended:
- `HistoryBucket` interface: added `avgLatencyMs: number | null`
- `PublicUptimeBucket` interface: added `avgLatencyMs: number | null`
- Imported `aggregateLatencyToDaily` from `uptime-aggregation`
- Added `safeLatency()` helper
- For 24h path: `avgLatencyMs: safeLatency(b.avgLatencyMs)`
- For 7d/30d path: separate `aggregateLatencyToDaily()` pass, merged into daily buckets by key

**`apps/control-center/src/components/monitoring/public-latency-sparkline.tsx`** — Created new:
- `'use client'` (SVG rendering)
- `PublicLatencyBucket` interface (exported): `{ bucketStartUtc, avgLatencyMs, insufficientData }`
- `PublicLatencySparkline` component: pure SVG, 32px height, indigo line + fill
- Handles empty, partial, and full data states
- Native `<title>` tooltips (no JS tooltip library)
- No `maxLatencyMs`, no internal labels

**`apps/control-center/src/components/monitoring/public-component-list.tsx`** — Updated:
- Imported `PublicLatencySparkline`
- `ComponentRow`: added `hasLatency` check (true if any bucket has valid non-null `avgLatencyMs`)
- Renders sparkline below availability bars when `hasLatency === true`
- Falls back gracefully when no latency data

---

## 7. Public Safety Review

| Check | Result |
|---|---|
| No `entityId` in response | ✅ 0 UUID matches in full 24h response |
| No `maxLatencyMs` in response | ✅ 0 `maxLatencyMs` fields in BFF output |
| No raw check counts | ✅ `upCount`, `downCount` not present |
| No admin controls | ✅ Read-only display only |
| No direct browser → service calls | ✅ All data flows through BFF |
| No fabricated latency values | ✅ Null/insufficient buckets produce gaps, not estimates |
| `avgLatencyMs` is aggregated trend only | ✅ Per-hour or per-day average — no per-request detail |

---

## 8. Validation

All checks passed after restart:

| Check | Result |
|---|---|
| A. `/status` loads | ✅ HTTP 200 |
| B. Availability bars still render | ✅ Confirmed (no regression) |
| C. `avgLatencyMs` in 24h BFF response | ✅ `"avgLatencyMs":4.8`, `"avgLatencyMs":8.5`, etc. |
| D. `avgLatencyMs` in 7d BFF response | ✅ `"avgLatencyMs":5.9`, `"avgLatencyMs":490.5`, etc. |
| E. `avgLatencyMs` in 30d BFF response | ✅ Real daily averages present |
| F. No UUIDs in public response | ✅ 0 UUID matches |
| G. No `maxLatencyMs` in public response | ✅ 0 `maxLatencyMs` fields |
| H. 11 components returned | ✅ |
| I. TypeScript compilation clean | ✅ `✓ Compiled /api/monitoring/uptime in 4.7s` |
| J. Sparkline label in rendered HTML | ✅ `"Avg response time"` found 5× in `/status` HTML |
| K. Window selector changes data | ✅ BFF returns window-appropriate buckets for 24h/7d/30d |

**Sample BFF output (24h, Audit component, 2 buckets)**:
```json
{
  "name": "Audit",
  "uptimePercent": 95.02,
  "buckets": [
    {
      "bucketStartUtc": "2026-04-20T06:00:00",
      "dominantStatus": "Healthy",
      "uptimePercent": 97.87,
      "insufficientData": false,
      "avgLatencyMs": 4.8
    },
    {
      "bucketStartUtc": "2026-04-20T07:00:00",
      "dominantStatus": "Healthy",
      "uptimePercent": 96.95,
      "insufficientData": false,
      "avgLatencyMs": 8.5
    }
  ]
}
```

---

## 9. Files Changed

| File | Change |
|---|---|
| `apps/control-center/src/app/api/monitoring/uptime/route.ts` | Extended to include `avgLatencyMs` per bucket |
| `apps/control-center/src/components/monitoring/public-latency-sparkline.tsx` | Created — public-safe SVG sparkline |
| `apps/control-center/src/components/monitoring/public-component-list.tsx` | Updated — renders sparkline below availability bars |

No other files changed. No new dependencies. No library additions.

---

## 10. Known Gaps / Risks

1. **Limited windows**: Only 24h / 7d / 30d supported (matches availability bar windows — consistent UX).
2. **Avg only, no percentiles**: p95/p99 latency would require monitoring service changes and are not safe to expose without further security review.
3. **No hover tooltips**: Uses native SVG `<title>` elements. A full JS tooltip layer would require client-side state management — deferred to a future polish pass.
4. **Sparse data**: When the monitoring service has < 1 day of history (as in 7d/30d), only 1 daily bar shows. The sparkline renders whatever buckets are available.
5. **`ServiceToken-Test-Entity`**: Still appears as a component in the public response. It should be removed from monitored entities if not intended for public display (pre-existing issue, out of scope).

---

## 11. Recommended Next Feature

**Option A — Stabilization / Production Validation (RECOMMENDED)**

Set `MONITORING_SOURCE=service` in the production/deployment environment secrets so the availability bars and latency charts are live for external users. Verify no data quality issues before showing latency trends to customers.

**Option B — MON-INT-05-002: Public Incident Drill-Down**

Add a lightweight incident timeline view accessible from the incidents panel, showing resolved events and impact timeline.

**Option C — MON-INT-05-003: Public Incident History (Resolved)**

Expose the last 30 days of resolved incidents on the public `/status` page for full transparency.
