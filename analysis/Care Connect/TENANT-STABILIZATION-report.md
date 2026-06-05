# TENANT-STABILIZATION Report
## Post-Migration Stabilization & B13 Gate Clearance

**Started:** 2026-04-23  
**Completed:** 2026-04-23  
**Status:** ✅ COMPLETE — Observation window may begin

---

## 1. Objective

Clear the six blockers identified in the B13 gate report so the platform can enter the
2–4 week runtime observation window required before Identity physical cleanup (B13).

**This task does NOT remove Identity endpoints, fallback paths, or DB columns.**  
It hardens the system so that Tenant is the unambiguous default source, Identity usage
is observable, and Control Center no longer routes tenant-management operations directly
to Identity.

---

## 2. B13 Blocker Remediation Map

| # | Blocker | Resolution |
|---|---|---|
| 1 | Runtime duration < 2 weeks | Time-only. Earliest B13: 2026-05-07 |
| 2 | `logo/public` defaults to Identity | ✅ Fixed — `TENANT_BRANDING_READ_SOURCE` default changed to `'Tenant'` |
| 3 | Hardcoded Identity branding reads (careconnect proxy + public-network-api) | ✅ Fixed — both switched to `/tenant/api/v1/public/resolve/by-*` |
| 4 | HybridFallback not instrumented | ✅ Fixed — `branding-metrics.ts` module + structured log tags added |
| 5 | Control Center hits Identity directly for 3 ops | ✅ Fixed — Tenant proxy endpoints added; CC BFF switched to `/tenant/api/v1/admin/...` |
| 6 | No fallback metrics | ✅ Fixed — web-side `GET /api/admin/branding-metrics`; Tenant-service `identityProxy` counters in runtime-metrics |

**Earliest B13 entry date: 2026-05-07** (14-day observation window)

---

## 3. Changes Made

### A. Web BFF (`apps/web/`)

#### `src/lib/branding-metrics.ts` — NEW
- Module-level process counters for branding and logo read-source activity
- `tenantBrandingCounters` and `logoCounters` objects for incrementing per-path
- `getBrandingMetricsSnapshot()` for the admin endpoint
- Covers: Tenant reads, HybridFallback activations (with reason breakdown), Identity fallback success/fail, Identity mode reads

#### `src/app/api/branding/logo/public/route.ts` — MODIFIED
- Changed `TENANT_BRANDING_READ_SOURCE` default from `'Identity'` → `'Tenant'` (blocker #2)
- Added `logoCounters` instrumentation for all three mode paths (Tenant / HybridFallback / Identity)
- Identity mode logs DEPRECATION warning

#### `src/app/api/tenant-branding/route.ts` — MODIFIED
- Added `tenantBrandingCounters` instrumentation throughout
- HybridFallback activations now increment `hybridTriggered` + `hybridReason(reason)` with category breakdown
- Identity fallback/mode reads increment their respective counters

#### `src/app/api/public/careconnect/[...path]/route.ts` — MODIFIED
- `resolveTenantIdFromHost()` switched from Identity branding endpoint to `/tenant/api/v1/public/resolve/by-host?host=...` (blocker #3)
- Identity fallback enabled only via `TENANT_RESOLUTION_FALLBACK_IDENTITY=true` (default: false)

#### `src/lib/public-network-api.ts` — MODIFIED
- `resolveTenantFromCode()` switched from Identity branding endpoint to `/tenant/api/v1/public/resolve/by-code/{code}` (blocker #3)
- Identity fallback enabled only via `TENANT_RESOLUTION_FALLBACK_IDENTITY=true` (default: false)

#### `src/providers/tenant-branding-provider.tsx` — MODIFIED
- Updated stale JSDoc comment; implementation was already calling `/api/tenant-branding` (correct)

#### `src/app/api/admin/branding-metrics/route.ts` — NEW
- `GET /api/admin/branding-metrics` — protected by `X-Admin-Key` header (blocker #6 — web-side)
- Returns `getBrandingMetricsSnapshot()` plus `b13GateStatus` summary

---

### B. Tenant Service (`apps/services/tenant/`)

#### `Tenant.Application/Interfaces/IIdentityCompatAdapter.cs` — MODIFIED
- Added `SetSessionTimeoutAsync(Guid, int?, CancellationToken)` returning `Task<bool>`

#### `Tenant.Infrastructure/Services/HttpIdentityCompatAdapter.cs` — MODIFIED
- Implemented `SetSessionTimeoutAsync` — proxies `PATCH /api/admin/tenants/{id}/session-settings` (5s timeout, best-effort)

#### `Tenant.Application/Interfaces/IIdentityProvisioningAdapter.cs` — MODIFIED
- Added `RetryProvisioningAsync(Guid, CancellationToken)` returning `Task<ProvisioningRetryResult>`
- Added `RetryVerificationAsync(Guid, CancellationToken)` returning `Task<ProvisioningRetryResult>`
- Added `ProvisioningRetryResult` record (maps Identity response shape)

#### `Tenant.Infrastructure/Services/HttpIdentityProvisioningAdapter.cs` — MODIFIED
- Implemented both retry methods via shared `ProxyRetryAsync` helper (15s timeout)

#### `Tenant.Api/Endpoints/TenantAdminEndpoints.cs` — MODIFIED
- Added `PATCH /{id}/session-settings` — proxies via `IIdentityCompatAdapter.SetSessionTimeoutAsync` (blocker #5)
- Added `POST /{id}/provisioning/retry` — proxies via `IIdentityProvisioningAdapter.RetryProvisioningAsync` (blocker #5)
- Added `POST /{id}/verification/retry` — proxies via `IIdentityProvisioningAdapter.RetryVerificationAsync` (blocker #5)
- All three endpoints inject `TenantRuntimeMetrics` and increment identity proxy counters

#### `Tenant.Application/Metrics/TenantRuntimeMetrics.cs` — MODIFIED
- Added 6 identity proxy counters: `IdentityProxySessionSettings{Ok,Fail}`, `IdentityProxyRetryProvisioning{Ok,Fail}`, `IdentityProxyRetryVerification{Ok,Fail}`
- `MetricsSnapshot` record extended to include all 6 new counters

#### `Tenant.Api/Endpoints/RuntimeMetricsEndpoints.cs` — MODIFIED
- `identityProxy` section added to runtime-metrics response (blocker #6 — Tenant-side)
- `cutoverCheck` section added summarising B13 gate prerequisites per service

---

### C. Control Center BFF (`apps/control-center/`)

#### `src/lib/control-center-api.ts` — MODIFIED
- `tenants.updateSessionSettings` switched from `/identity/api/admin/...` → `/tenant/api/v1/admin/.../session-settings` (blocker #5)
- `tenants.retryProvisioning` switched from `/identity/api/admin/...` → `/tenant/api/v1/admin/.../provisioning/retry` (blocker #5)
- `tenants.retryVerification` switched from `/identity/api/admin/...` → `/tenant/api/v1/admin/.../verification/retry` (blocker #5)

---

## 4. Rollback Instructions

All changes are config-driven or additive. Nothing has been removed.

| What to roll back | How |
|---|---|
| Logo/branding default | Set `TENANT_BRANDING_READ_SOURCE=Identity` in web env |
| careconnect + public-network-api resolution | Set `TENANT_RESOLUTION_FALLBACK_IDENTITY=true` in web env to re-enable Identity fallback |
| CC session-settings | Revert the one-liner in `control-center-api.ts` back to `/identity/api/admin/...` |
| CC retry operations | Revert two one-liners in `control-center-api.ts` back to `/identity/api/admin/...` |
| Identity admin endpoints | Were never removed — still exist and fully functional |
| HybridFallback | Was never removed — still active on `TENANT_BRANDING_READ_SOURCE=HybridFallback` |

---

## 5. B13 Gate Observation Checklist

Monitor these over the 2-week window (2026-04-23 → 2026-05-07):

### Web BFF — `GET /api/admin/branding-metrics` (via `X-Admin-Key`)
- [ ] `tenantBranding.hybridFallbackTriggered = 0` for ≥7 consecutive days
- [ ] `tenantBranding.identityModeReads = 0` in production at all times
- [ ] `logo.hybridFallbackTriggered = 0` for ≥7 consecutive days
- [ ] `logo.identityModeReads = 0` in production at all times
- [ ] `b13GateStatus.hybridFallbackZero = true`
- [ ] `b13GateStatus.identityModeZero = true`

### Tenant Service — `GET /api/v1/admin/runtime-metrics`
- [ ] `identityProxy.sessionSettings.ok > 0` (proves traffic flows through proxy)
- [ ] `identityProxy.retryProvisioning.ok > 0` (proves traffic flows through proxy)
- [ ] `identityProxy.retryVerification.ok > 0` (proves traffic flows through proxy)
- [ ] `identityProxy.proxyFailRate < 5%` over ≥7 days
- [ ] `cutoverCheck.branding.brandingProxyActive = true`
- [ ] `cutoverCheck.resolution.resolutionActive = true`
- [ ] `cutoverCheck.identityProxyRouting.sessionSettingsRouted = true` (always true after STABILIZATION)

### Log scanning (structured)
- [ ] `[DEPRECATION]` tag does NOT appear in production logs
- [ ] `HybridFallback:` warn-level entries are zero or decreasing
- [ ] No `ERROR` from Identity proxy calls

---

## 6. What B13 Will Do Next (Not Yet)

After the observation window is cleared, B13 will:
1. Remove Identity deprecated admin endpoints (CreateTenant, UpdateEntitlement, UpdateSessionSettings, RetryProvisioning, RetryVerification)
2. Drop Identity-side tenant columns that are now owned by Tenant service
3. Remove HybridFallback mode entirely (Tenant is the sole source)
4. Remove Identity branding read paths from all web BFF routes
5. Archive this stabilization report

**B13 requires a separate, explicit task approval. Nothing in this stabilization task performs any cleanup.**
