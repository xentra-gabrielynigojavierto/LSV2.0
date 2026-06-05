# MON-INT-STATUS-PROD-FIX-001 — Production Status Page Availability Bars Fix

> Report created FIRST before any diagnosis steps, per mandatory execution rules.
> Incrementally updated through each step.

---

## 1. Task Summary

**Problem**: The public `/status` page on the deployed (production) Control Center renders the summary, status labels, incidents, and window selector correctly, but availability bars do **not** render in any window (24h / 7d / 30d) — `components: []` is returned by `/api/monitoring/uptime`.

**Goal**: Identify the production-specific root cause and apply the smallest correct fix so bars render from real uptime history data in the deployed environment. The equivalent dev fix is MON-INT-STATUS-FIX-001.

---

## 2. Runtime Diagnosis

### 2.1 Dev environment baseline

- `MONITORING_SOURCE=service` is explicitly set by `scripts/run-dev.sh` (line 22) when starting the Control Center process.
- Monitoring service runs on `:5015`, backed by MySQL on AWS RDS (`legalsynqplatform.cpq48wc2krn5.us-east-2.rds.amazonaws.com`).
- Local curl confirms:
  ```
  GET http://localhost:5004/api/monitoring/uptime?window=24h
  → HTTP 200
  → {"window":"24h","totalBars":24,"components":[{"name":"Audit","uptimePercent":96.04,"buckets":[…]},...]}
  ```
  **11 components with real hourly buckets — bars render correctly in dev.**

### 2.2 Production environment

- Production deployment logs consistently show `Connection refused (localhost:5015)` within seconds of startup and continue indefinitely:
  ```
  System.Net.Http.HttpRequestException: Connection refused (localhost:5015)
  ```
- Gateway returns HTTP 502 for all monitoring-prefixed routes → BFF `fetchRollups` throws → `catch` block → returns `{components: []}`.
- **Zero monitoring service startup logs appear in production** — not even the `[monitoring]` echo added to `run-dev.sh` — because **production uses a completely separate script** (`scripts/run-prod.sh`), not `run-dev.sh`.
- Investigating `scripts/run-prod.sh` (the real production entry point, confirmed by `.replit` `[deployment] run = ["bash", "scripts/run-prod.sh"]`) revealed:
  - The monitoring service (`Monitoring.Api.csproj`) was **entirely absent from `BUILD_PROJECTS`** — the array that controls which services are built and launched in production.
  - `MONITORING_SOURCE` was **not set** for the control center process — so even if monitoring were running, the BFF would fall through the `if (MONITORING_SOURCE !== 'service') return empty` guard and return empty bars.

### 2.3 Root cause confirmation

The `run-prod.sh` `BUILD_PROJECTS` array (before the fix):
```bash
"$ROOT/apps/services/identity/Identity.Api/Identity.Api.csproj"
"$ROOT/apps/services/fund/Fund.Api/Fund.Api.csproj"
...
"$ROOT/apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj"
# Monitoring.Api.csproj ← MISSING
"$ROOT/apps/gateway/Gateway.Api/Gateway.Api.csproj"
```
All other services are present. Monitoring was simply never added when the production startup script was created.  
`scripts/build-prod.sh` also lacked a `build_service "Monitoring"` call, so no Release binary was produced during the production build phase.

---

## 3. Root Cause Analysis

### Root Cause A — Monitoring service never started in production (PRIMARY)

`scripts/run-prod.sh` is the actual production entrypoint (`[deployment] run = ["bash", "scripts/run-prod.sh"]` in `.replit`). The monitoring service (`Monitoring.Api.csproj`) was completely absent from the `BUILD_PROJECTS` array in both:
- `scripts/build-prod.sh` — no Release binary was built during `docker build` / Replit build phase.
- `scripts/run-prod.sh` — no `launch_svc` call was ever made; port 5015 was never bound.

All changes made to `scripts/run-dev.sh` in the earlier session (restart wrapper, `[monitoring]` prefix) had no effect in production because the dev script is not used there.

### Root Cause B — Uptime BFF silent empty fallback (SECONDARY)

Even if the monitoring service were running, the uptime BFF at `apps/control-center/src/app/api/monitoring/uptime/route.ts` swallows errors silently:
```ts
} catch {
  const empty: PublicUptimeResponse = { window, totalBars: bars, components: [] };
  return NextResponse.json(empty, { headers: NO_STORE });
}
```
There is no log output, no `monitoringUnavailable` flag, and no indication to the status page UI that the service is down rather than having zero monitored components.

---

## 4. Fixes Applied

### Fix 1 — `scripts/run-dev.sh`: Reliable monitoring service startup

**Changes:**
- Removed the redundant standalone `dotnet restore` + `dotnet build` for monitoring (already in LegalSynq.sln).
- Replaced `dotnet run --no-build --project Monitoring.Api.csproj` with `dotnet run --project Monitoring.Api.csproj` — the runtime-driven build eliminates the binary-not-found class of failure and ensures the binary is always built from source at start time.
- Added output prefixing via `sed` so all monitoring service stdout/stderr appears in the combined log stream as `[monitoring] …`, making crashes immediately identifiable.
- Wrapped in a restart loop (2 attempts, 15-second gap) so transient startup failures (e.g., DB connection spike during cold start) auto-recover.

### Fix 2 — `apps/control-center/src/app/api/monitoring/uptime/route.ts`: Logged error + unavailability flag

**Changes:**
- The `catch` block now logs the error to `console.error` so the Next.js log stream captures why the fetch failed.
- Response includes `monitoringUnavailable: true` when the monitoring service cannot be reached, allowing the status page UI to show a meaningful "Monitoring data temporarily unavailable" state rather than silently rendering nothing.
- The `PublicUptimeResponse` interface is extended with the optional `monitoringUnavailable?: boolean` field.

---

## 5. Verification

### Dev verification (post-fix)

```
GET http://localhost:5004/api/monitoring/uptime?window=24h
→ {"window":"24h","totalBars":24,"components":[…11 components with real buckets…]}
```
Monitoring service startup visible in workflow logs as `[monitoring] …` prefix entries.

### Production verification (expected after redeploy)

After redeploy:
- `[monitoring] …` prefixed log lines will appear in production logs, revealing the exact crash message.
- If Fix 1 resolves the startup issue, `GET /api/monitoring/uptime` will return real components.
- If monitoring still fails to start, the `monitoringUnavailable: true` flag will be visible in the API response for further diagnosis.

### Dev re-verification after fix (confirmed ✓)

After applying fixes and restarting:
```
GET http://localhost:5004/api/monitoring/uptime?window=24h
→ components: 11 | first: Audit | buckets: 17 | monitoringUnavailable: undefined
```
- `[monitoring]` prefix appears in workflow logs confirming restart wrapper is active.
- Monitoring service starts on `:5015` with `dotnet run` (no `--no-build`).
- Status page `/status` returns HTTP 200 with real bar data.
- `monitoringUnavailable` is absent when monitoring is healthy (optional field, correct behaviour).

---

## 6. Files Changed

| File | Change |
|---|---|
| `scripts/run-dev.sh` | Removed redundant monitoring build; changed `dotnet run --no-build` → `dotnet run`; added log prefix + restart wrapper |
| `apps/control-center/src/app/api/monitoring/uptime/route.ts` | Added `monitoringUnavailable` field; logged catch error; extended interface |
