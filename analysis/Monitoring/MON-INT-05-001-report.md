# MON-INT-05-001 — Window Selector (7d / 30d View)

> **Report created FIRST before any implementation work began.**

---

## 1. Task Summary

Extend the monitoring UI to support selectable time windows so operators and users
can view availability bars and latency trends over 24h, 7d, and 30d periods.
Driven by real `uptime_hourly_rollups` data from the Monitoring Service.
No fabricated history. No change to uptime aggregation formulas.

---

## 2. Existing Window-Capable API Analysis

### Monitoring Service endpoints

Both backend endpoints already support `24h | 7d | 30d | 90d`:

| Endpoint | Query param | Default | Notes |
|----------|-------------|---------|-------|
| `/monitoring/uptime/rollups?window=` | ✅ | `24h` | Returns per-component rollup summary |
| `/monitoring/uptime/history?entityId=&window=` | ✅ | `24h` | Returns **hourly** buckets for any window |

Both default to `24h` if `window` is missing or invalid (validated server-side in `UptimeReadEndpoints.cs`).
The history endpoint **always returns hourly buckets** regardless of window size.

### Current BFF routes (hardcoded to 24h)

| Route | Hardcoded window | Change needed |
|-------|-----------------|---------------|
| `/api/monitoring/uptime` | `window=24h` | Accept `?window` param |
| `/api/monitoring/latency` | `window=24h` | Accept `?window` param |

### Window support summary

- `24h` → 24 hourly buckets → render 24 bars (already working).
- `7d` → up to 168 hourly buckets from backend → **aggregate to 7 daily buckets in BFF**.
- `30d` → up to 720 hourly buckets from backend → **aggregate to 30 daily buckets in BFF**.
- `90d` → deferred. Excluded from this feature to keep UI readable.

Aggregation performed at the BFF layer using a shared utility. The browser always receives
≤ 30 sanitized buckets regardless of window. No large payloads to the browser.

---

## 3. Window Selector UX Decision

**Surfaces in scope**:
1. **Public `/status` page** — window selector drives availability bars.
2. **Internal `/monitoring` page** — same window selector drives latency sparklines.

**Selector options**: `24h · 7d · 30d` (90d deferred).

**Default**: `24h` (backward compatible).

**Public page implementation**: URL query param (`?window=24h|7d|30d`).
Server-side rendering reads `searchParams.window`. Page is bookmarkable and shareable.
Window selector is 3 pill-style anchor links — no client JavaScript required.

**Internal page implementation**: Client-side state in `ComponentStatusList` (already a client component).
Window selector added inline in the component. Latency data re-fetched with the selected window.

---

## 4. Data Fetching / BFF Changes

### Shared aggregation utility — `src/lib/uptime-aggregation.ts`

Implements `aggregateToDaily(hourlyBuckets)`:
- Groups hourly buckets by UTC day (`YYYY-MM-DD`).
- For each day:
  - `dominantStatus` = worst status in the day (Down > Degraded > Healthy > Unknown).
  - `uptimePercent` = average of non-null hourly values.
  - `avgLatencyMs` = average of non-null hourly values.
  - `maxLatencyMs` = max of all hourly values.
  - `insufficientData` = true if all hours in the day have `insufficientData = true`.
  - `bucketStartUtc` = midnight UTC of the day.

### `/api/monitoring/uptime` changes

- Accept `?window=24h|7d|30d` (invalid → `24h`).
- For `24h`: current behavior (hourly buckets, totalBars=24).
- For `7d` / `30d`: aggregate hourly → daily; pass totalBars accordingly.
- Add `totalBars: number` field to `PublicUptimeResponse` for bar strip sizing.
- Existing exposed fields unchanged; no new internal data leaked.

### `/api/monitoring/latency` changes

- Accept `?window=24h|7d|30d` (invalid → `24h`).
- Same aggregation logic applied for latency buckets.
- Response shape unchanged; just more or fewer buckets.

---

## 5. Public Page Implementation

### `status/page.tsx`

- Accept `searchParams: Promise<{ window?: string }>` (Next.js 15 async searchParams).
- Validate window on server side; default to `24h`.
- Pass validated window to both `fetchUptimeHistory(window)` calls.
- Add `WindowSelector` component: 3 anchor `<a>` links (`?window=24h|7d|30d`).
- Active window highlighted with filled pill style.

### `public-component-list.tsx`

- Accept `totalBars?: number` and `windowLabel?: string` props.
- Pass `totalBars` to `AvailabilityBars`.
- Show window label in section subtitle (e.g., "7 services monitored · last 7 days").

---

## 6. Internal Monitoring Impact

### `component-status-list.tsx`

- Add `selectedWindow` state (`'24h' | '7d' | '30d'`), default `'24h'`.
- Add `WindowSelector` sub-component inline (same 3-option pill design).
- Reset latency cache when window changes (`latencyCache`, `latencyState`).
- Pass window param to `/api/monitoring/latency?window=` fetch.

---

## 7. Validation

| Check | Result |
|-------|--------|
| TypeScript clean | ✅ `npx tsc --noEmit` — zero errors |
| `/status` loads without auth | ✅ Confirmed public — middleware PUBLIC_PATHS already includes `/status` |
| Default `24h` behavior unchanged | ✅ `curl /status` → "last 24 hours" subtitle, 24h pill highlighted |
| `?window=7d` shows correct subtitle | ✅ `curl /status?window=7d` → "last 7 days" in Components subtitle |
| `?window=30d` shows correct subtitle | ✅ `curl /status?window=30d` → "last 30 days" |
| Invalid `?window=bad` → 24h | ✅ `curl /status?window=invalid` → "last 24 hours" fallback |
| BFF `?window=7d` response shape | ✅ `{"window":"7d","totalBars":7,"components":[]}` |
| BFF `?window=30d` response shape | ✅ `{"window":"30d","totalBars":30,"components":[]}` |
| No entityId exposed | ✅ Stripped in BFF (unchanged) |
| No latency exposed on `/status` | ✅ Latency BFF is internal-only; unauthenticated access redirects to /login |
| `/status` window selector renders | ✅ `24h · 7d · 30d` pills in HTML output |
| Internal monitoring page not broken | ✅ ComponentStatusList compiles and renders; window state added only |

### Tested routes
- `GET /status` — default (24h), 7d, 30d, invalid (→24h fallback)
- `GET /api/monitoring/uptime?window=24h|7d|30d` — all return correct shape + totalBars
- `GET /api/monitoring/latency?window=7d` — correctly gate-kept by auth middleware
- `npx tsc --noEmit` — zero type errors

---

## 8. Files Changed

| File | Action | Purpose |
|------|--------|---------|
| `apps/control-center/src/lib/uptime-aggregation.ts` | Created | Daily aggregation utility |
| `apps/control-center/src/app/api/monitoring/uptime/route.ts` | Modified | Accept window param, aggregate daily |
| `apps/control-center/src/app/api/monitoring/latency/route.ts` | Modified | Accept window param, aggregate daily |
| `apps/control-center/src/app/status/page.tsx` | Modified | searchParams, window selector UI |
| `apps/control-center/src/components/monitoring/public-component-list.tsx` | Modified | totalBars + windowLabel props |
| `apps/control-center/src/components/monitoring/component-status-list.tsx` | Modified | Internal window selector + latency re-fetch |

---

## 9. Known Gaps / Risks

- **90d deferred**: Excluded to keep the UI readable and because historical data credibility at 90d is unverified.
- **Hourly resolution at 7d/30d**: The backend stores hourly buckets. Aggregation to daily is correct but a single gap-hour won't make a whole day `insufficientData` — only all-hours-gap does.
- **Local mode**: Both BFF routes return empty in local mode. No fabrication. Window selector renders but bars show "History unavailable".
- **7d/30d sparse history**: Early-stage deployments may have limited history. Partial days render as available; missing days show gray bars.
- **Performance**: 720 hourly buckets fetched from backend for 30d across N components. Acceptable for typical fleet sizes (<30 services). Parallel `Promise.allSettled` used for fan-out.
- **Internal window selector**: Client-state only; not reflected in URL. Operators cannot share a specific window view of the internal monitoring page. Deferred.

---

## 10. Recommended Next Feature

**Option A — Stabilization / Production Validation (RECOMMENDED)**

The monitoring and status experience is now feature-complete for the current scope.
Before adding more features, validate charts and window selection against a live
`MONITORING_SOURCE=service` environment to confirm data quality and rendering at 7d/30d.

**Option B — MON-INT-05-002: Public Incident Drill-Down**

Only if status page UX is stable and the next value is deeper public incident transparency
(e.g., linking from an alert to a timeline of the outage window).
