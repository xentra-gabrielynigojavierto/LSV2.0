# Monitoring 500 Bug — Root Cause Analysis
**Date:** 2026-04-20  
**Reported:** CC `/monitoring` page shows "Monitoring unavailable — Health probe failed: 500"  
**Environment:** `controlcenter-dev.legalsynq.com` (Replit production deployment)

---

## Symptoms

- `/monitoring` page renders the `StatusSummaryBannerError` banner with the text
  "Failed to load monitoring data" and sub-text "Health probe failed: 500".
- All other CC pages (Tenants, Users, Notifications, Support) work normally.
- The Monitoring Service itself (`http://localhost:5015/monitoring/summary`) is healthy
  and returns correct JSON in dev.
- The YARP gateway (`http://localhost:5010/monitoring/monitoring/summary`) proxies
  correctly in dev.

---

## Root Causes

### RC-1 (primary): Unnecessary loopback self-request in `page.tsx`

`apps/control-center/src/app/monitoring/page.tsx` `fetchMonitoringSummary()` makes
an HTTP GET to `http://127.0.0.1:5004/api/monitoring/summary` — i.e., the same
Next.js process fetches itself at its own route handler.

```
Browser → CC Next.js (port 5004) → renders MonitoringPage
  → fetchMonitoringSummary() → fetch("http://127.0.0.1:5004/api/monitoring/summary")
  → [same Next.js process] → route.ts GET handler
  → serviceGetMonitoringSummary() → gateway (5010) → monitoring service (5015)
```

Problems with this pattern:

1. **Startup race condition.** During the first seconds after `next dev` starts,
   route modules may not be compiled yet. An uncompiled route returns Next.js's
   default 500 error response — before the route's own `try/catch` can run. The CC
   page catches this 500 via `if (!res.ok) throw new Error("Health probe failed: 500")`.

2. **Extra failure domain.** The loopback introduces a second HTTP layer that can fail
   independently of the monitoring service. Even if the monitoring service is healthy,
   the self-request can fail due to port conflicts, network policy, or IPv6/IPv4
   resolution differences in the deployed environment (Node.js resolves `localhost` to
   `::1` on some Linux systems; the server binds to IPv4 only).

3. **Unnecessary complexity.** `MonitoringPage` is a Server Component. It can call
   `getMonitoringSummary()` directly — no HTTP round-trip is needed. The self-request
   exists only because the route BFF abstraction was copied from client-side usage.

**Fix:** Replace `fetchMonitoringSummary()` with a direct call to `getMonitoringSummary()`
from `@/lib/monitoring-source`. Eliminates the HTTP layer, the 500 race, and the
IPv4/IPv6 ambiguity in one change.

### RC-2 (secondary): Test entity inflates system status to "Down"

`MonitoringReadEndpoints.DeriveSystemStatus()` computes the aggregate system status
as the worst individual status across **all** entities — including `scope = "test"`.

The entity `ServiceToken-Test-Entity` (scope="test") was provisioned to verify
service-token authentication. Its target URL is now permanently unreachable, so its
status is perpetually `"Down"`.

Because `DeriveSystemStatus` does not exclude test-scope entities, the returned
`system.status` is always `"Down"` even when every production integration is `"Healthy"`.
This masks the true platform health and would cause a false "Down" banner even if RC-1
were fixed.

**Fix:** Exclude entities with `scope = "test"` (and any future test-scope entities)
from the system status aggregation in `DeriveSystemStatus`.

### RC-3 (contributing): No fetch timeout on gateway call

`serviceGetMonitoringSummary()` calls the gateway with no `AbortController` timeout.
If the gateway or monitoring service is temporarily slow (e.g., during startup or a DB
hiccup), the `fetch()` hangs indefinitely. Next.js applies a per-request timeout in
production that, when exceeded, returns a 504-style error — which bubbles up as another
non-503 response to the monitoring page.

**Fix:** Add a 10-second `AbortController` timeout to the gateway `fetch()` call in
`serviceGetMonitoringSummary()`.

---

## Deployment log evidence

From Replit deployment logs at startup:
```
HttpRequestException: Connection refused (localhost:5015)
```
This shows YARP (the gateway) trying to reach the monitoring service before it is up —
consistent with the startup race in RC-1. The connection error would have been caught by
the route's try/catch and returned 503 — but the self-request layer adds an extra 500
window before the route even compiles.

---

## What was NOT the cause

- **IPv4/IPv6 mismatch in gateway `appsettings.json`**: All clusters use `localhost:PORT`.
  In .NET 8, `HttpClient` resolves `localhost` to `127.0.0.1` (IPv4) by default. This
  works correctly in dev. The IPv4/IPv6 risk is for Node.js (CC), not .NET (YARP).
- **Contract mismatch between monitoring service and CC**: Both sides are correctly
  aligned. The service emits `scope`, the CC maps it to `category`. Field names verified.
- **mysql2 not available in production**: `mysql2` is in root `node_modules` and is
  correctly resolved by Node.js module-resolution walk-up from the CC's working directory.
- **`getEnv()` throwing in production**: `getEnv()` is defined but not called at module
  level by any file in the monitoring route's import chain.

---

## Fixes Applied

| Fix | File | Description |
|-----|------|-------------|
| RC-1 | `apps/control-center/src/app/monitoring/page.tsx` | Call `getMonitoringSummary()` directly; remove self-request |
| RC-2 | `apps/services/monitoring/Monitoring.Api/Endpoints/MonitoringReadEndpoints.cs` | Exclude `scope = "test"` entities from `DeriveSystemStatus` |
| RC-3 | `apps/control-center/src/lib/monitoring-source.ts` | Add 10-second `AbortController` timeout to gateway fetch |
