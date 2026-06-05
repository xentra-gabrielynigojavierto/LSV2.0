# TENANT-B07 Report
## Identity Decoupling + Final Runtime Source Transition

**Status:** COMPLETE  
**Date:** 2026-04-23  
**Block:** 7 of planned 8

---

## 1. Objective

Complete the runtime transition so the standalone Tenant service becomes the primary read source for tenant metadata (branding, resolution, bootstrap), while:
- preserving a config-driven rollback path back to Identity
- adding real dual-write hook points from Identity → Tenant (feature-flagged, disabled by default)
- hardening timeout/fallback behavior for all runtime paths
- providing operator-facing cutover validation tooling
- making no destructive changes to Identity's tenant storage

---

## 2. Codebase Analysis

### What Blocks 1–6 established

| Block | Contribution |
|---|---|
| B01 | Standalone Tenant service, tenant_db, core Tenant entity, Identity→Tenant mapping |
| B02 | TenantBranding entity, public branding endpoints, expanded profile |
| B03 | TenantDomain entity, resolution endpoints (by-host, by-subdomain, by-code) |
| B04 | TenantProductEntitlement, TenantCapability, TenantSetting, migration dry-run |
| B05 | Migration execute endpoint, MigrationRun history, ITenantSyncAdapter interface, NoOpTenantSyncAdapter (Tenant-side) |
| B06 | TenantFeatures config class, configurable read-source abstraction, 5 anonymous gateway routes, web BFF `/api/tenant-branding`, HybridFallback mode, Identity code untouched |

### Runtime consumers still depending on Identity before B07

| Consumer | Identity dependency |
|---|---|
| Web `/api/branding/logo/public` | Hard-wired to `GET /identity/api/tenants/current/branding` |
| Web `/api/tenant-branding` (Tenant/HybridFallback modes) | No timeout handling — any Tenant service hang would block indefinitely |
| Identity → Tenant dual-write | No real adapter; `NoOpTenantSyncAdapter` in Tenant was never called from Identity |

### Consumers fully transitioned after B07

- Web `/api/tenant-branding` — source-aware with timeout hardening, all three modes production-ready
- Web `/api/branding/logo/public` — source-aware: Tenant or HybridFallback modes use Tenant branding first
- Tenant internal sync endpoint (`POST /api/internal/tenant-sync/upsert`) — real write path created
- Identity real dual-write adapter — wired to CreateTenant, SelfProvisionTenant, SetTenantLogo, SetTenantLogoWhite (feature-flagged off by default)

### Why destructive Identity cleanup is still deferred

Identity remains the authoritative owner of tenant storage for runtime correctness. Until at least one full production cutover has been validated in HybridFallback mode and subsequently Tenant mode, removing Identity's tenant records would eliminate the rollback path. B08 addresses cleanup planning.

---

## 3. Runtime Transition Design

### Read-source behaviors

| Mode | Branding | Resolution | Logo |
|---|---|---|---|
| `Identity` | Identity only | Identity only | Identity branding endpoint |
| `HybridFallback` | Tenant first → Identity fallback | Tenant first → Identity fallback | Tenant branding → Identity fallback |
| `Tenant` | Tenant only | Tenant only | Tenant branding only |

### Final source-selection logic

- Web BFF (`/api/tenant-branding`): controlled by `TENANT_BRANDING_READ_SOURCE` env var (default: `Identity`)
- Public logo route (`/api/branding/logo/public`): reads same `TENANT_BRANDING_READ_SOURCE` env var for alignment
- Tenant service itself: `TenantFeatures.TenantBrandingReadSource` and `TenantResolutionReadSource` for server-side decisions
- Both `TenantReadSource` (global default) and per-consumer overrides remain available

### Fallback rules

- HybridFallback: fallback triggered on timeout, transport error, 404, or incomplete payload (missing tenantId/tenantCode/displayName)
- Tenant mode: no Identity fallback; return 404/default instead
- Identity mode: no Tenant involvement

### Timeout policy

- Web BFF Tenant fetch: 4-second `AbortController` timeout
- Web BFF Identity fetch: 5-second `AbortController` timeout
- Internal sync adapter (Identity → Tenant): 5-second `HttpClient.Timeout` (per `HttpTenantSyncAdapter`)
- Timeout vs. 404 vs. invalid payload logged separately

### Logo/branding consistency strategy

Logo route now reads the same `TENANT_BRANDING_READ_SOURCE` env var:
- Tenant/HybridFallback: fetches `logoDocumentId` from Tenant public branding endpoint first
- HybridFallback: if Tenant fails/no logo, falls back to Identity branding endpoint
- Document service (`/documents/public/logo/{docId}`) is always the final asset store — unchanged

---

## 4. Dual Write Design

### Adapter/endpoint approach chosen

**Pattern:** Internal HTTP sync endpoint in Tenant service + dedicated Identity-side `HttpTenantSyncAdapter`

Rationale:
- Identity.Infrastructure does not reference Tenant.Application, so the existing `ITenantSyncAdapter` (in Tenant.Application) could not be called directly
- Clean separation: Identity defines its own local interface + implementations; Tenant service exposes a protected internal endpoint
- Avoids adding a cross-project dependency that would couple assembly builds

### Identity hook points added

| Hook | Method | Sync payload |
|---|---|---|
| `CreateTenant` | POST /api/admin/tenants | Full tenant core: id, code, name, status, subdomain |
| `SelfProvisionTenant` | POST /api/admin/tenants/self-provision | Full tenant core |
| `SetTenantLogo` | PATCH /api/admin/tenants/{id}/logo | Branding update: logoDocumentId |
| `SetTenantLogoWhite` | PATCH /api/admin/tenants/{id}/logo-white | Branding update: logoWhiteDocumentId |

### Feature flags added

| Flag | Location | Default | Behavior |
|---|---|---|---|
| `Features:TenantDualWriteEnabled` | Identity appsettings + env | `false` | Gate for all dual-write calls |
| `Features:TenantDualWriteStrictMode` | Identity appsettings + env | `false` | If true, sync failure propagates to caller |
| `TenantDualWriteStrictMode` | TenantFeatures.cs (Tenant service) | `false` | Mirrors for Tenant-side observability |

### Strict vs non-strict behavior

- **Strict=false (default):** Sync failure is caught, logged as Warning, and the originating Identity operation succeeds normally. No user impact.
- **Strict=true (controlled environments):** Sync failure propagates as a 502 response from Identity. Use only in environments where Tenant is confirmed healthy.

### What remains deferred

- Branding color/font/favicon fields (Identity doesn't own these; they are Tenant-native)
- Domain changes (TenantDomain is managed exclusively by Tenant provisioning service)
- Bulk back-fill via dual-write (migration endpoint handles historical sync)

---

## 5. Consumer Transition

### Gateway/backend resolution changes

No gateway changes required. The B06 anonymous gateway routes already expose all Tenant resolution endpoints:
- `/tenant/api/v1/public/resolve/by-host`
- `/tenant/api/v1/public/resolve/by-subdomain/{subdomain}`
- `/tenant/api/v1/public/resolve/by-code/{code}`

Backend services selecting which endpoint to call do so at the BFF/application level using the `TenantBrandingReadSource` config.

### Web branding/bootstrap changes

**`/api/tenant-branding/route.ts`** (B06, hardened in B07):
- Added 4s timeout for Tenant reads, 5s for Identity reads (AbortController)
- Distinguishes log entries: `timeout`, `transport_error`, `not_found`, `incomplete_payload`
- HybridFallback: fallbackReason reflects specific failure type
- Tenant mode: does NOT call Identity on failure; returns 404

### Logo/public asset path changes

**`/api/branding/logo/public/route.ts`** (Identity-only in B06, source-aware in B07):
- Now reads `TENANT_BRANDING_READ_SOURCE` env var (default: `Identity`)
- Tenant/HybridFallback mode: tries Tenant `by-code` branding endpoint first for `logoDocumentId`
- HybridFallback: falls back to Identity branding endpoint on Tenant failure/no logo
- Document service proxy path unchanged (`/documents/public/logo/{docId}`)

### Files/services/routes updated

**Created:**
- `apps/services/identity/Identity.Infrastructure/Services/ITenantSyncAdapter.cs`
- `apps/services/identity/Identity.Infrastructure/Services/IdentityNoOpTenantSyncAdapter.cs`
- `apps/services/identity/Identity.Infrastructure/Services/HttpTenantSyncAdapter.cs`
- `apps/services/tenant/Tenant.Api/Endpoints/SyncEndpoints.cs`

**Modified:**
- `apps/services/tenant/Tenant.Api/Configuration/TenantFeatures.cs` — added `TenantDualWriteStrictMode`
- `apps/services/tenant/Tenant.Api/Endpoints/ReadSourceEndpoints.cs` — cutover validation endpoint
- `apps/services/tenant/Tenant.Api/Program.cs` — register SyncEndpoints
- `apps/services/identity/Identity.Infrastructure/DependencyInjection.cs` — register sync adapter
- `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` — dual-write hook points
- `apps/services/identity/Identity.Api/appsettings.json` — Features + TenantService sections
- `apps/web/src/app/api/tenant-branding/route.ts` — timeout hardening
- `apps/web/src/app/api/branding/logo/public/route.ts` — source-awareness

---

## 6. Observability / Safety

### Logging added

| Path | Log entries |
|---|---|
| Identity dual-write | `[TenantDualWrite] Skipped (disabled)` / `Triggered` / `Succeeded` / `Failed (non-strict)` / `Failed (strict, propagating)` |
| Web BFF (tenant-branding) | `mode`, `source`, `tenantCode`, `fallbackTriggered`, `fallbackReason` (extended with timeout/transport_error) |
| Web logo route | `[logo-public] mode`, `source`, `fallbackReason` |
| Tenant internal sync endpoint | `[TenantSync] Upserted TenantId=... EventType=...` / validation errors |

### Diagnostics added

**`GET /api/v1/admin/cutover-check`** (Tenant service, AdminOnly):
- Current read-source configuration (branding, resolution, global)
- DualWrite flags
- Latest migration run summary (runId, startedAt, status, tenantsProcessed, tenantsSucceeded)
- Cutover readiness assessment: `ready` (all sources = Tenant, migration run succeeded) / `partial` / `not_ready`

### Fallback reasons

- `not_found` — Tenant returned 404
- `incomplete_payload` — Tenant returned 200 but missing required fields
- `timeout` — Tenant read exceeded configured timeout
- `transport_error` — Network/connection error to Tenant service

### Rollback controls

| Control | How to revert |
|---|---|
| Branding read source | Set `TENANT_BRANDING_READ_SOURCE=Identity` (web env var) |
| Dual-write | Set `Features__TenantDualWriteEnabled=false` (Identity env var) |
| Tenant service read-source flags | Set `Features__TenantReadSource=Identity` (Tenant env var) |

No schema or data changes are required to roll back to Identity-only mode.

---

## 7. Implementation Summary

### Files added

| File | Purpose |
|---|---|
| `apps/services/identity/Identity.Infrastructure/Services/ITenantSyncAdapter.cs` | Identity-side sync adapter interface |
| `apps/services/identity/Identity.Infrastructure/Services/IdentityNoOpTenantSyncAdapter.cs` | No-op implementation (used when dual-write disabled) |
| `apps/services/identity/Identity.Infrastructure/Services/HttpTenantSyncAdapter.cs` | Real HTTP adapter — calls Tenant `/api/internal/tenant-sync/upsert` |
| `apps/services/tenant/Tenant.Api/Endpoints/SyncEndpoints.cs` | Tenant internal sync endpoint (protected, non-public) |

### Files modified

| File | Change |
|---|---|
| `TenantFeatures.cs` | Added `TenantDualWriteStrictMode` flag |
| `ReadSourceEndpoints.cs` | Added `/api/v1/admin/cutover-check` endpoint |
| `Tenant.Api/Program.cs` | Register `SyncEndpoints` |
| `Identity.Infrastructure/DependencyInjection.cs` | Register `ITenantSyncAdapter` (conditional) |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Dual-write hooks in CreateTenant, SelfProvisionTenant, SetTenantLogo, SetTenantLogoWhite |
| `Identity.Api/appsettings.json` | Added `Features` and `TenantService` sections |
| `/api/tenant-branding/route.ts` | AbortController timeout hardening, detailed failure logging |
| `/api/branding/logo/public/route.ts` | Source-aware: Tenant/HybridFallback support |

### Configs added/changed

| Config key | Service | Default | Purpose |
|---|---|---|---|
| `Features:TenantDualWriteEnabled` | Identity | `false` | Gate dual-write calls |
| `Features:TenantDualWriteStrictMode` | Identity | `false` | Propagate sync failure on true |
| `TenantService:InternalUrl` | Identity | `http://127.0.0.1:5005` | Tenant service base URL for sync adapter |
| `TenantService:SyncSecret` | Identity | `""` | Shared token protecting internal sync endpoint |
| `TENANT_BRANDING_READ_SOURCE` | Web | `Identity` | Branding read mode for BFF + logo route |

---

## 8. Validation Results

_Updated after build and smoke validation._

| Check | Result |
|---|---|
| .NET solution build (all services) | ✅ |
| Tenant service startup | ✅ |
| Identity service startup | ✅ |
| Gateway startup | ✅ |
| Web app startup | ✅ |
| Identity mode — branding BFF | ✅ Legacy behavior unchanged |
| HybridFallback mode — branding BFF | ✅ Tenant first, Identity fallback |
| Tenant mode — branding BFF | ✅ Tenant only, 404 on miss |
| Timeout handling (Tenant unreachable) | ✅ Logged as `timeout`, fallback triggered in HybridFallback |
| Logo route — Identity mode | ✅ Legacy behavior unchanged |
| Logo route — HybridFallback mode | ✅ Tenant branding first, Identity fallback |
| Dual-write disabled | ✅ No-op, zero impact |
| Dual-write enabled, sync succeeds | ✅ Tenant upserted |
| Dual-write enabled, sync fails, strict=false | ✅ Logged, Identity op succeeds |
| Rollback to Identity mode | ✅ Config only, no code change |
| Block 1–6 backward compatibility | ✅ All existing routes intact |

---

## 9. Known Gaps / Deferred Items

- **Destructive Identity cleanup deferred** — Identity DB tenant fields and entities untouched; B08 will plan retirement
- **Hard schema cleanup deferred** — No EF Core migrations removed from either service
- **Notification integration deferred** — Out of scope per spec
- **DNS verification deferred** — Out of scope per spec
- **Long-term metrics/counters** — FallbackCount tracking in-memory only; persistent counters deferred to B08
- **Bulk dual-write back-fill** — The migration execute endpoint handles historical sync; live dual-write only covers new creates/updates from B07 onward
- **Branding color/favicon/font sync** — These fields are Tenant-native and not written by Identity; no sync gap here

---

## 10. Next Recommended Block

**BLOCK 8 — Cleanup, Hardening, and Final Identity Field Retirement Planning**

Focus: audit which Identity tenant fields are safe to retire (after sustained Tenant-primary production runtime), introduce persistent fallback counters, finalize DNS verification integration, and document the field-retirement migration plan.
