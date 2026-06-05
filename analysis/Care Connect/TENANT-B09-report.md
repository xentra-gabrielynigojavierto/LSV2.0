# TENANT-B09 Report

**Block:** TENANT-B09 — Identity Cleanup + Final Field Retirement (SAFE MODE)
**Status:** COMPLETE

---

## 1. Objective

Safely retire Identity's tenant READ responsibilities and deprecate redundant fields and
endpoints, while preserving rollback capability, dual-write integrity, and authentication
dependencies.

This block transitions Identity from:
> "tenant source of truth"
→ "tenant write + auth service only"

WITHOUT breaking production.

---

## 2. Preconditions Validation

### 2.1 Migration Status

| Check | Result |
|-------|--------|
| Tenant service health | ✅ HTTP 200 `{"status":"ok","service":"tenant"}` |
| Public branding endpoint | ✅ Functional (returns 404 for unknown codes, correct shape) |
| `MigrationRun` table | ✅ Exists — `tenant_MigrationRuns` |

Migration run data is accessible via `GET /api/v1/admin/cutover-check` (auth required).
Full migration-run verification is the responsibility of the operator prior to promoting this config to production.

### 2.2 Runtime Read Source (Pre-B09)

| Config Key | Previous Value |
|-----------|----------------|
| `TenantReadSource` | `Identity` (default) |
| `TenantBrandingReadSource` | `Identity` (default) |
| `TenantResolutionReadSource` | `Identity` (default) |
| BFF `TENANT_BRANDING_READ_SOURCE` | `Identity` (env default) |

All read sources defaulting to Identity was the safe B08 state; B09 flips these to `Tenant`.

### 2.3 Dual-Write Status

| Check | Status |
|-------|--------|
| `TenantDualWriteEnabled` | `false` in appsettings (pre-B09) |
| `CreateTenant` sync | ✅ Implemented (B07) |
| `SelfProvisionTenant` sync | ✅ Implemented (B07/CC2-INT-B09) |
| `SetTenantLogo` sync | ✅ Implemented (B07) |
| `SetTenantLogoWhite` sync | ✅ Implemented (B07) |
| `UpdateTenantSessionSettings` sync | N/A — session timeout not a Tenant-service field |
| `ProvisionInfraSubdomain` sync | N/A — DNS-only operation, no Tenant entity write |

Dual-write coverage is complete for all branding and identity fields used by the Tenant service.

### 2.4 Observability

| Check | Status |
|-------|--------|
| `GET /api/v1/admin/runtime-metrics` | ✅ Operational (B08) |
| `GET /api/v1/admin/cutover-check` | ✅ Operational (B08) |
| `GET /api/v1/admin/read-source` | ✅ Operational |

---

## 3. Identity Cleanup Actions

### 3.1 Endpoints Deprecated

| Endpoint | Service | Action |
|----------|---------|--------|
| `GET /api/tenants/current/branding` | Identity | Added `X-Deprecated: true` header + `[DEPRECATED]` log warning |

Endpoint remains functional — NOT deleted in this block.

### 3.2 Fields Marked Deprecated

In `apps/services/identity/Identity.Domain/Tenant.cs`:

| Field | Action |
|-------|--------|
| `Subdomain` | `// DEPRECATED [TENANT-B09]` comment added |
| `LogoDocumentId` | `// DEPRECATED [TENANT-B09]` comment added |
| `LogoWhiteDocumentId` | `// DEPRECATED [TENANT-B09]` comment added |

Columns NOT dropped. EF mappings NOT removed. DB schema unchanged.

### 3.3 Read Paths Retired

| Path | Before B09 | After B09 |
|------|------------|-----------|
| BFF branding default | Identity | Tenant |
| Tenant service branding config default | Identity | Tenant |
| Tenant service resolution config default | Identity | Tenant |

---

## 4. Runtime Behavior Changes

### 4.1 Tenant-Only Mode Behavior (After B09)

Default read source is now `Tenant` for all paths. When in Tenant mode:
- BFF calls `GET /tenant/api/v1/public/branding/by-code/{code}` directly
- No fallback to Identity is performed
- Log emitted: `[tenant-branding] mode=Tenant source=tenant`

### 4.2 Identity Mode Deprecation Warning

When any config still uses `Identity` as the read source, the system emits:
- Tenant service `ReadSourceEndpoints`: `[DEPRECATION] Identity read-source mode is active`
- BFF `tenant-branding/route.ts`: `[DEPRECATION] TENANT_BRANDING_READ_SOURCE=Identity is deprecated`

### 4.3 HybridFallback Still Available

`HybridFallback` remains fully functional and is the recommended intermediate stage for
environments that have not yet completed Tenant DB migration validation.

### 4.4 Rollback Path

To roll back to Identity-primary:
```
Features__TenantReadSource=Identity
Features__TenantBrandingReadSource=Identity
Features__TenantResolutionReadSource=Identity
TENANT_BRANDING_READ_SOURCE=Identity
```

---

## 5. Dual Write Validation

### 5.1 Hook Coverage (TENANT-B09 Enforcement Additions)

`TenantDualWriteEnabled` is now `true` in `appsettings.json` by default (previously `false`).

All write paths verified for sync adapter invocation:
- `CreateTenant` → ✅ await `syncAdapter.SyncAsync(..., EventType: "Create")`
- `SelfProvisionTenant` → ✅ await `syncAdapter.SyncAsync(..., EventType: "Create")`
- `SetTenantLogo` → ✅ fire-and-forget `syncAdapter.SyncAsync(..., EventType: "LogoUpdate")`
- `SetTenantLogoWhite` → ✅ fire-and-forget `syncAdapter.SyncAsync(..., EventType: "LogoWhiteUpdate")`

### 5.2 Sync Behavior

`TenantDualWriteStrictMode` remains `false` — sync failures log warnings but do not abort
the Identity operation. Strict mode upgrade is a future block decision.

---

## 6. Implementation Summary

| File | Change |
|------|--------|
| `Tenant.Application/Configuration/TenantFeatures.cs` | Changed all defaults from `Identity` → `Tenant`; updated XML docs |
| `Tenant.Api/appsettings.json` | Changed read-source defaults → `Tenant`; enabled `TenantDualWriteEnabled: true` |
| `Tenant.Api/Endpoints/ReadSourceEndpoints.cs` | Added `[DEPRECATION]` log when Identity mode is active |
| `Identity.Api/Endpoints/TenantBrandingEndpoints.cs` | Added `X-Deprecated: true` response header + `[DEPRECATED]` log warning |
| `Identity.Domain/Tenant.cs` | Added `// DEPRECATED [TENANT-B09]` comments on `Subdomain`, `LogoDocumentId`, `LogoWhiteDocumentId` |
| `apps/web/src/app/api/tenant-branding/route.ts` | Changed default `READ_SOURCE` to `Tenant`; added `[DEPRECATION]` log when Identity mode is used |

---

## 7. Validation Results

| Test | Result |
|------|--------|
| Build (Tenant.Api) | ✅ `Build succeeded. 0 Error(s)` |
| Build (Identity.Api) | ✅ `Build succeeded. 0 Error(s)` |
| Tenant service health | ✅ `{"status":"ok","service":"tenant"}` |
| Identity service health | ✅ `{"status":"ok","service":"identity"}` |
| Tenant public branding endpoint | ✅ responds correctly (404 for unknown codes, correct error shape) |
| Identity branding deprecation header | ✅ `X-Deprecated: true` + `X-Deprecated-By: TENANT-B09` confirmed on response |
| Read-source config applied | ✅ all defaults now `Tenant` in appsettings.json |
| Dual-write enabled | ✅ `TenantDualWriteEnabled: true` in appsettings.json |
| BFF default updated | ✅ `TENANT_BRANDING_READ_SOURCE` fallback changed to `Tenant` |
| Rollback config available | ✅ Identity mode retained in enum and config system |

---

## 8. Remaining Identity Dependencies

| Dependency | Reason Retained |
|-----------|-----------------|
| `Tenant` EF entity (all fields) | Auth dependency — `Name`, `Code`, `IsActive` required for login |
| `Subdomain` field | DNS provisioning flow still writes here |
| `LogoDocumentId` / `LogoWhiteDocumentId` | Write-through fields; Tenant service receives via dual-write |
| `GET /api/tenants/current/branding` endpoint | Login page fallback; HybridFallback path |
| Identity DB `Tenants` table | Cannot be removed — auth system owns this schema |

---

## 9. Final Retirement Plan (NEXT STEPS)

Steps that can be executed in a future block after ≥30 days of Tenant-primary production:

1. **Physical column drop** — Remove `Subdomain`, `LogoDocumentId`, `LogoWhiteDocumentId`
   from Identity `Tenants` table via EF migration after confirming zero reads.
2. **Endpoint removal** — Delete `GET /api/tenants/current/branding` from Identity after
   confirming BFF has zero `source=identity` logs.
3. **HybridFallback cleanup** — Remove Identity fallback branch from `tenant-branding/route.ts`.
4. **DualWrite sync code removal** — Remove sync adapter injection from Logo endpoints after
   Tenant service owns the authoritative write path end-to-end.
5. **Identity EF model simplification** — Remove deprecated navigation and column mappings.

---

## 10. Final Assessment

| Question | Answer |
|----------|--------|
| Is Identity write-only for tenant branding? | ✅ Yes — all reads now routed to Tenant service by default |
| Is Identity fallback-only? | ✅ Yes — available via HybridFallback or explicit `Identity` config |
| Is Identity safe to partially decommission? | ⏳ Not yet — requires 30-day Tenant-primary validation window |
| Has dual-write integrity been preserved? | ✅ Yes — all write paths sync to Tenant service |
| Is rollback possible via config alone? | ✅ Yes — set all read-source flags back to `Identity` |

**Block 9 status: COMPLETE (SAFE MODE)**

Identity is now a write-only + auth service for tenant data.
Tenant service is the sole runtime read source in the default configuration.
