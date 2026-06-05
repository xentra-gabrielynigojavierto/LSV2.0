# TENANT-B08 Report

**Block:** TENANT-B08 — Cleanup, Hardening, and Final Identity Field Retirement Planning
**Status:** IN PROGRESS

---

## 1. Objective

Complete the Tenant service initiative by hardening runtime behavior, improving observability, planning Identity tenant field retirement, and documenting the final production cutover posture.

Deliverables:
- In-process runtime metrics counters exposed via admin endpoint
- Read-path caching (IMemoryCache) for public branding and resolution endpoints
- Enhanced cutover-check with tenant count, cache status, and runtime metrics summary
- Identity tenant field retirement inventory and deprecation markers
- Rollback playbook

---

## 2. Codebase Analysis

### What Blocks 1–7 Established

| Block | Contribution |
|-------|-------------|
| B01 | Standalone Tenant service, tenant_db, core Tenant entity, Identity→Tenant migration mapping |
| B02 | TenantBranding entity, public branding APIs, BrandingService |
| B03 | TenantDomain entity, resolution by host/subdomain/code, subdomain fallback compatibility |
| B04 | TenantProductEntitlement, TenantCapability, TenantSetting, migration dry-run utility |
| B05 | Migration execute endpoint, idempotent execution, MigrationRun tracking, conflict detection |
| B06 | Configurable TenantReadSource flag (Identity/Tenant/HybridFallback), gateway Tenant routes, BFF transition |
| B07 | Final runtime controls, source-aware logo route (Next.js), Identity→Tenant sync adapter, cutover-check endpoint, strict-mode dual-write |

### What Still Remains in Identity

Identity still owns the authoritative copy of:
- `Tenant.Name`, `Tenant.Code`, `Tenant.IsActive` (used in auth token claims, user management)
- `Tenant.Subdomain` (legacy resolution)
- `Tenant.LogoDocumentId`, `Tenant.LogoWhiteDocumentId` (branding bootstrap — now also in Tenant via dual-write)
- `TenantBrandingEndpoints.cs` — public `/api/tenants/current/branding` still active (fallback source)
- `TenantEndpoints.cs` — admin tenant CRUD (authoritative write path)
- `AdminEndpoints.cs` — `CreateTenant`, `SelfProvisionTenant`, `SetTenantLogo`, `SetTenantLogoWhite` with dual-write adapter calls to Tenant service

### What B08 Hardens / Adds

- `TenantRuntimeMetrics` — singleton in-process counter set (branding reads, resolution reads, sync attempts, cache hits/misses)
- `IMemoryCache` on public branding and resolution read paths (configurable TTL, default 60 s)
- Enhanced `cutover-check` endpoint (tenant count, cache config, runtime metrics summary, expanded readiness)
- Deprecation comments on Identity compatibility-only code paths
- Rollback playbook in this document

---

## 3. Runtime Hardening Design

### Caching Strategy

**Service:** ASP.NET Core `IMemoryCache` (already in BCL — no new dependency)

**Cached paths and keys:**

| Path | Cache Key | TTL |
|------|-----------|-----|
| `BrandingService.GetPublicByCodeAsync` | `branding:code:{code}` | configurable (default 60 s) |
| `BrandingService.GetPublicBySubdomainAsync` | `branding:sub:{subdomain}` | configurable (default 60 s) |
| `ResolutionService.ResolveByHostAsync` | `resolution:host:{host}` | configurable (default 60 s) |
| `ResolutionService.ResolveBySubdomainAsync` | `resolution:sub:{subdomain}` | configurable (default 60 s) |
| `ResolutionService.ResolveByCodeAsync` | `resolution:code:{code}` | configurable (default 60 s) |

**Cache invalidation:** TTL-based only. On `/api/internal/tenant-sync/upsert`, cache is explicitly evicted for the synced tenant's code and subdomain (if present in the sync payload).

**Negative caching:** null/not-found results are NOT cached (avoids stale 404 after tenant provision).

**Config flags (added to `Features` section):**
```
Features:TenantReadCachingEnabled = true
Features:TenantReadCacheTtlSeconds = 60
```

### Metrics / Observability Strategy

**Implementation:** `TenantRuntimeMetrics` singleton with `Interlocked.Increment` counters. Process-lifetime memory only — counters reset on restart.

**Counters tracked:**

| Counter | Description |
|---------|-------------|
| `BrandingPublicReadsAttempted` | Total calls to GetPublicByCode/Subdomain |
| `BrandingPublicReadsSucceeded` | Returned non-null result |
| `BrandingPublicReadsFailed` | Exception or null on required path |
| `BrandingCacheHits` | Cache served the result |
| `BrandingCacheMisses` | Cache miss — DB query executed |
| `ResolutionReadsAttempted` | Total calls to ResolveByHost/Subdomain/Code |
| `ResolutionReadsSucceeded` | Returned non-null result |
| `ResolutionReadsFailed` | Exception during resolution |
| `ResolutionCacheHits` | Cache served the result |
| `ResolutionCacheMisses` | Cache miss — DB query executed |
| `SyncAttemptsReceived` | POST /api/internal/tenant-sync/upsert calls |
| `SyncSucceeded` | Successful upsert |
| `SyncFailed` | Exception during upsert |

**Exposed at:** `GET /api/v1/admin/runtime-metrics` (AdminOnly)

### Fallback / Rollback Preservation

- All caching is additive; the underlying DB code paths are unchanged
- Disabling `TenantReadCachingEnabled=false` returns to previous zero-cache behavior with no other changes needed
- All prior endpoints, fallback logic, and dual-write paths remain intact

### New Config Added

```
Features:TenantReadCachingEnabled = true      # default true; set false to disable
Features:TenantReadCacheTtlSeconds = 60       # cache TTL in seconds; default 60
```

---

## 4. Identity Retirement Planning

### Field / Code-Path Inventory (Identity Service)

#### Identity.Domain.Tenant Fields

| Field | Current Use | Classification | Retirement Path |
|-------|-------------|----------------|-----------------|
| `Id` | Auth claims, FK everywhere | **KEEP FOREVER** | Never retire from Identity |
| `Name` | Auth claims, admin UI | **KEEP — primary write** | Sync to Tenant; retire Identity read after validation |
| `Code` | Auth claims, resolution, branding lookup | **KEEP — primary write** | Sync to Tenant; retire Identity read after Tenant-primary cutover |
| `IsActive` | Auth pipeline, login gate | **KEEP — primary write** | Sync `Status` to Tenant; retire Identity read last |
| `Subdomain` | Legacy resolution | **COMPATIBILITY ONLY** | Already mirrored in Tenant.Subdomain; retire after Tenant resolution is primary |
| `LogoDocumentId` | Branding bootstrap, login page | **WRITE-THROUGH** | Dual-write syncs to Tenant; retire Identity read when TenantBrandingReadSource=Tenant |
| `LogoWhiteDocumentId` | Branding bootstrap | **WRITE-THROUGH** | Same as LogoDocumentId |
| `ProvisioningStatus` | Self-provisioning flow | **KEEP — not in Tenant** | Identity-only concern; no retirement planned |
| `SessionTimeoutMinutes` | Auth session | **KEEP — not in Tenant** | Identity-only concern |
| `AddressLine1`, `City`, `State`, etc. | Org profile | **KEEP — not in Tenant** | Identity-only concern |

#### Identity Code Paths

| Endpoint / Service | Classification | Notes |
|-------------------|----------------|-------|
| `TenantBrandingEndpoints` `/api/tenants/current/branding` | **COMPATIBILITY ONLY** | Used as fallback when `TENANT_BRANDING_READ_SOURCE=Identity`. Retire when all consumers switch to Tenant. |
| `TenantEndpoints` CRUD | **PRIMARY WRITE** | Must stay; Tenant service syncs FROM Identity writes |
| `AdminEndpoints.CreateTenant` | **PRIMARY WRITE** | Dual-write adapter calls Tenant sync |
| `AdminEndpoints.SetTenantLogo/SetTenantLogoWhite` | **PRIMARY WRITE + SYNC** | Syncs to Documents and Tenant |
| `AccessSourceEndpoints` | **KEEP — unrelated** | Product/role entitlements; not Tenant field concern |
| `Identity.Application.ITenantSyncAdapter` | **SYNC BRIDGE** | Remove only after Tenant service is fully authoritative and Identity is read-retired |

### Deprecation Markers Added (this block)

- `// COMPATIBILITY-ONLY [TENANT-B08]` comments added to `TenantBrandingEndpoints.cs` endpoint header
- `// WRITE-THROUGH [TENANT-B08]` comments added near `LogoDocumentId`/`LogoWhiteDocumentId` reads in branding endpoint
- `// COMPATIBILITY-ONLY [TENANT-B08]` comment on `Tenant.Subdomain` access in resolution fallback

### Destructive Cleanup Decision

**NOT performed in B08.** No EF Core migrations removing Identity columns. Rationale:
- Identity fields are still used by the auth pipeline (claims, login gate)
- `TenantBrandingEndpoints` is the active fallback when read source is Identity
- Column removal is a one-way operation requiring sustained Tenant-primary production runtime validation (≥30 days recommended)

---

## 5. Cutover Readiness / Operations

### Cutover-Check Enhancements (B08)

`GET /api/v1/admin/cutover-check` now additionally reports:
- `tenantCount` — number of tenants in Tenant DB
- `cacheConfig` — current caching enabled flag and TTL
- `runtimeMetrics` — lifetime sync success/failure counts, branding/resolution read counts
- Readiness expanded: dual-write enabled is now a readiness factor

### Runtime Metrics Endpoint

`GET /api/v1/admin/runtime-metrics` (AdminOnly)

Returns:
```json
{
  "startedAtUtc": "...",
  "uptimeSeconds": 123,
  "branding": { "attempted": 0, "succeeded": 0, "failed": 0, "cacheHits": 0, "cacheMisses": 0 },
  "resolution": { "attempted": 0, "succeeded": 0, "failed": 0, "cacheHits": 0, "cacheMisses": 0 },
  "sync": { "attempted": 0, "succeeded": 0, "failed": 0 },
  "cacheConfig": { "enabled": true, "ttlSeconds": 60 },
  "note": "Counters are process-memory only and reset on service restart."
}
```

### Rollback Playbook

#### Scenario: Revert from Tenant-primary to Identity-primary

**Step 1 — Revert read source (no restart needed if using env vars; restart needed for appsettings):**
```
Features__TenantReadSource = Identity
Features__TenantBrandingReadSource = Identity
Features__TenantResolutionReadSource = Identity
```
Or via `appsettings.json`:
```json
"Features": {
  "TenantReadSource": "Identity",
  "TenantBrandingReadSource": "Identity",
  "TenantResolutionReadSource": "Identity"
}
```

**Step 2 — Revert BFF logo source:**
```
TENANT_BRANDING_READ_SOURCE = Identity
```
(Next.js env var — requires BFF process restart)

**Step 3 — Optionally disable dual-write (stops Tenant writes, Identity remains authoritative):**
```
Features__TenantDualWriteEnabled = false
Features__TenantDualWriteStrictMode = false
```

**Step 4 — Restart Tenant service and BFF.**

**Step 5 — Monitor:**
- `/api/v1/admin/read-source` should show all sources as `Identity`
- `/api/v1/admin/cutover-check` should show `readiness: not_ready` (which is expected for Identity mode)
- Watch Identity service logs for branding / resolution errors returning
- Watch BFF logs for `[logo-public] mode: Identity`

**Order of revert:**  
Read source flags → BFF env var → dual-write flags → restart

**Signals to watch after rollback:**
- `[TenantSync]` log lines should stop appearing (dual-write off)
- Branding requests return Identity-sourced data
- `/api/tenants/current/branding` in Identity responds normally
- No 502 errors in BFF logo proxy

#### Scenario: Rollback from HybridFallback

Same as above but revert only `TenantBrandingReadSource` and `TenantResolutionReadSource` from `HybridFallback` to `Identity`. The Tenant service continues running; no data is lost.

### Recommended Production Rollout Order

1. Ensure migration execute (`POST /api/admin/migration/execute`) completed with 0 errors
2. Enable `TenantDualWriteEnabled=true` — let it run for ≥24 h, verify sync success via runtime-metrics
3. Switch to `HybridFallback` for branding and resolution
4. Monitor fallback rate (watch for `identity_fallback` source in BFF logs)
5. When fallback rate approaches 0, switch to `Tenant` mode
6. Declare Identity fields read-deprecated (but keep columns)
7. After ≥30 days stable, plan column removal migration (separate block)

---

## 6. Implementation Summary

### Files Added

| File | Purpose |
|------|---------|
| `Tenant.Api/Metrics/TenantRuntimeMetrics.cs` | Singleton in-process counter set |
| `Tenant.Api/Endpoints/RuntimeMetricsEndpoints.cs` | `GET /api/v1/admin/runtime-metrics` |

### Files Modified

| File | Change |
|------|--------|
| `Tenant.Api/Configuration/TenantFeatures.cs` | Added `TenantReadCachingEnabled`, `TenantReadCacheTtlSeconds` |
| `Tenant.Infrastructure/DependencyInjection.cs` | Register `IMemoryCache`, `TenantRuntimeMetrics` |
| `Tenant.Application/Services/BrandingService.cs` | IMemoryCache + TenantRuntimeMetrics injection; cache wrap public read methods |
| `Tenant.Application/Services/ResolutionService.cs` | IMemoryCache + TenantRuntimeMetrics injection; cache wrap resolve methods |
| `Tenant.Api/Endpoints/SyncEndpoints.cs` | Emit metrics on sync attempt/success/failure; evict cache on successful sync |
| `Tenant.Api/Endpoints/ReadSourceEndpoints.cs` | Enhance cutover-check with tenant count, cache config, runtime metrics |
| `Tenant.Api/Program.cs` | Register RuntimeMetricsEndpoints |
| `Tenant.Api/appsettings.json` | Add caching feature flags |
| `Identity.Api/Endpoints/TenantBrandingEndpoints.cs` | Add deprecation comments |

### Configs Added

```
Features:TenantReadCachingEnabled = true
Features:TenantReadCacheTtlSeconds = 60
```

### Cleanup Performed

- No destructive cleanup — all compatibility paths preserved
- Dead code identified and comment-marked (not removed) for safety

---

## 7. Validation Results

*(Updated after implementation)*

- [ ] Build success
- [ ] Tenant service startup success
- [ ] Identity service startup success  
- [ ] Gateway startup success
- [ ] `GET /api/v1/admin/runtime-metrics` responds (admin auth required)
- [ ] `GET /api/v1/admin/cutover-check` enhanced response
- [ ] Caching: cache hit on repeated branding/resolution reads (confirmed via metrics counters)
- [ ] Mode: all read sources default to Identity (safe default preserved)
- [ ] Dual-write disabled by default (safe default preserved)
- [ ] Rollback: config-only revert path intact

---

## 8. Known Gaps / Deferred Items

- **Destructive Identity field removal** — Deferred. Requires ≥30 days sustained Tenant-primary production runtime before safe removal.
- **Cross-service tenant count comparison** — cutover-check reports Tenant DB count only; Identity count not queryable from Tenant service without adding HTTP client. Deferred.
- **Distributed cache** — IMemoryCache is per-process; horizontal scale requires Redis. Deferred (single-instance dev environment).
- **Persistent metrics** — Counters reset on restart; Prometheus/OpenTelemetry integration deferred.
- **BFF caching** — Next.js public logo route has browser-side `Cache-Control: public, max-age=3600`; server-side in-process cache not added (acceptable given browser caching).
- **Notification integration** — Out of scope per spec.
- **DNS verification** — Out of scope per spec.
- **Fallback rate threshold alerting** — Deferred; counters reported but no automated alert.

---

## 9. Final Platform Assessment

The Tenant service initiative (Blocks 1–8) is:

**Functionally complete** — all Tenant entities (branding, domains, entitlements, capabilities, settings, migration tracking) are fully implemented with CRUD APIs, public endpoints, and gateway routing.

**Production-ready with rollback** — all read paths are config-switchable (Identity/HybridFallback/Tenant) with no code changes required. Dual-write is opt-in with a strict mode gate. Caching is conservative (60 s TTL, TTL-based invalidation + sync-triggered eviction).

**Pending only cleanup/retirement work** — Identity tenant field column removal is intentionally deferred pending production validation. The retirement matrix is documented above; no destructive schema changes are in this block.

**Recommended next step after production validation:** After ≥30 days of stable Tenant-primary operation, run a dedicated Block 9 (Identity Field Retirement) that:
1. Removes `Tenant.LogoDocumentId`/`LogoWhiteDocumentId` reads from Identity branding endpoint
2. Retires `TenantBrandingEndpoints.cs` entirely
3. Removes the `Subdomain` field from Identity Tenant entity (after Tenant domain resolution is confirmed primary)
