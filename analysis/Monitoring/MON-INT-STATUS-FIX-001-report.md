# MON-INT-STATUS-FIX-001 — Public Status Page Availability Bars Fix

> Report created FIRST before any diagnosis steps, per mandatory execution rules.
> Incrementally updated through each step.

---

## 1. Task Summary

**Problem**: The public `/status` page on the Control Center rendered correctly (summary, status labels, incidents, window selector) but availability bars did NOT render in any window (24h / 7d / 30d).

**Goal**: Identify the actual root cause in the data/config/runtime pipeline and apply the smallest correct fix so the bars render from real uptime history data.

---

## 2. Runtime Diagnosis

### MONITORING_SOURCE env var

- `apps/control-center/.env.local` did NOT contain `MONITORING_SOURCE`.
- `scripts/run-dev.sh` started Control Center as:
  ```
  GATEWAY_URL=http://localhost:5010 exec node ... next dev -p 5004
  ```
  No `MONITORING_SOURCE` was passed.
- The BFF route `apps/control-center/src/app/api/monitoring/uptime/route.ts` line 48:
  ```ts
  const MONITORING_SOURCE = process.env.MONITORING_SOURCE ?? 'local';
  ```
  defaulted to `'local'`.

### BFF behaviour when MONITORING_SOURCE=local

Lines 129-132 in the uptime route:
```ts
if (MONITORING_SOURCE !== 'service') {
  const empty: PublicUptimeResponse = { window, totalBars: bars, components: [] };
  return NextResponse.json(empty, { headers: NO_STORE });
}
```
The route returned `{"window":"24h","totalBars":24,"components":[]}` immediately — zero network calls were made to the monitoring service.

### BFF response before fix (confirmed by curl)

```
GET http://localhost:5004/api/monitoring/uptime?window=24h
→ HTTP 200
→ {"window":"24h","totalBars":24,"components":[]}
```

### Monitoring service status

- Monitoring service running on `:5015` (`appsettings.json: "Urls": "http://0.0.0.0:5015"`).
- Gateway routes for uptime correctly configured in `appsettings.json`:
  - `monitoring-uptime-rollups`: `/monitoring/monitoring/uptime/rollups`
  - `monitoring-uptime-history`: `/monitoring/monitoring/uptime/history`
- Direct test of rollups via gateway returned 11 components with real uptimePercent values.
- History bucket test (Audit entity) returned 12 hourly buckets for 24h window.
- **Conclusion**: Data existed, pipeline was healthy. BFF was simply never calling it.

---

## 3. API / BFF Findings

| Check | Finding |
|---|---|
| `MONITORING_SOURCE` in runtime | `'local'` (default — not set anywhere) |
| BFF `/api/monitoring/uptime?window=24h` | `{"components":[]}` — early return before any fetch |
| Gateway routing for uptime rollups | Correctly configured, returns real data |
| Gateway routing for uptime history | Correctly configured, returns real buckets |
| Monitoring service on `:5015` | Running, 11 components with real history |
| Rollups endpoint data | 11 components with full uptime stats |
| History endpoint buckets | 12 buckets per entity per 24h window |
| UUIDs exposed in BFF output | None (stripped by BFF sanitization layer) |

---

## 4. Root Cause

**Root Cause A — Wrong env/config (`MONITORING_SOURCE` not set, defaulting to `'local'`)**

The environment variable `MONITORING_SOURCE` was absent from both `apps/control-center/.env.local` and `scripts/run-dev.sh`. The BFF route defaulted to `'local'` and returned an empty payload on every request, without ever contacting the monitoring service.

The monitoring service, gateway routing, and data pipeline were all fully operational. No code bugs were present. The issue was purely a missing runtime configuration value.

---

## 5. Fix Implemented

**Minimal, reversible — 2 config-only changes. No code changes.**

**Change 1 — `apps/control-center/.env.local`** (primary fix)

Added:
```dotenv
# Monitoring data source.
# local   = built-in ephemeral probe engine (no real history bars on /status)
# service = delegate to Monitoring Service via gateway (required for availability bars)
MONITORING_SOURCE=service
```

**Change 2 — `scripts/run-dev.sh`** (consistency / resilience)

Updated the Control Center startup line from:
```bash
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://localhost:5010 exec "$NODE" ... dev -p 5004)
```
to:
```bash
(cd "$ROOT/apps/control-center" && GATEWAY_URL=http://localhost:5010 MONITORING_SOURCE=service exec "$NODE" ... dev -p 5004)
```

This makes the configuration explicit in the startup script and ensures the value is available even if `.env.local` is absent or not loaded.

---

## 6. Validation

All validation checks passed after workflow restart:

| Check | Result |
|---|---|
| A. 24h: BFF returns non-empty components | ✅ 11 components with real buckets |
| B. 7d: BFF returns non-empty components | ✅ 11 components (1 daily bucket — limited history) |
| C. 30d: BFF returns non-empty components | ✅ 11 components (1 daily bucket — limited history) |
| D. No UUIDs exposed in BFF output | ✅ 0 UUID matches in full 24h response |
| E. Status page loads without auth | ✅ HTTP 200 at `/status` on control-center port |
| F. Total buckets across 24h | ✅ 121 bucket entries across all 11 components |

**BFF response after fix (partial — 24h, first component)**:
```json
{
  "window": "24h",
  "totalBars": 24,
  "components": [
    {
      "name": "Audit",
      "uptimePercent": 95.02,
      "buckets": [
        { "bucketStartUtc": "2026-04-20T06:00:00", "dominantStatus": "Healthy", "uptimePercent": 97.87, "insufficientData": false },
        { "bucketStartUtc": "2026-04-20T07:00:00", "dominantStatus": "Healthy", "uptimePercent": 96.95, "insufficientData": false }
      ]
    }
    ... 10 more components
  ]
}
```

---

## 7. Files Changed

| File | Change | Reason |
|---|---|---|
| `apps/control-center/.env.local` | Added `MONITORING_SOURCE=service` | Primary fix — Next.js loads this at startup |
| `scripts/run-dev.sh` | Added `MONITORING_SOURCE=service` inline to Control Center startup line | Consistency and resilience if `.env.local` absent |

No other files changed. No code changes. No UI changes. No fake data introduced.

---

## 8. Remaining Risks / Notes

1. **Deployed environment**: For Replit production deployments, `MONITORING_SOURCE=service` must be set as an environment secret separately — `.env.local` is not committed and is not used in production builds.

2. **7d/30d limited bars**: The 7d and 30d windows show only 1 daily bar each because the monitoring service has only ~1 day of history accumulated. As the service runs, more bars will fill in automatically — no code change is needed.

3. **`ServiceToken-Test-Entity`** appears as the 7th component in the public BFF response. This is a test monitoring entity in the database. If it should not be shown publicly, it should be deleted from monitored entities or filtered in the BFF — out of scope for this fix.

4. **No temporary diagnostic code** was added at any point. All investigation was done via direct curl calls against live endpoints.
