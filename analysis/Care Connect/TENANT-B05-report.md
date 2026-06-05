# TENANT-B05 — Migration Execution + Dual Write Preparation

**Status:** COMPLETE  
**Date:** 2026-04-23  
**Block:** 5 of the Tenant service initiative

---

## 1. Objective

Advance the migration utility from dry-run analysis to controlled, write-capable migration execution:

1. Write-capable Identity → Tenant migration execution (TENANT-E00-S03)
2. TenantId preservation enforcement (TENANT-E00-S04)
3. Dual write preparation scaffolding (TENANT-E00-S05)
4. Post-run reconciliation / verification (TENANT-E00-S07)
5. Rollback-safe migration run persistence (TENANT-E00-S08)

Identity remains the runtime source of truth. No read switch. No Identity ownership removal.

---

## 2. Design Decisions

### Execution API shape
`POST /api/admin/migration/execute` — explicit POST to distinguish from safe GET dry-run.  
Dry-run remains at `GET /api/admin/migration/dry-run`.  
Single-tenant scope: `POST /api/admin/migration/execute` with `{ "scope": "single", "tenantId": "..." }`.

### Idempotency strategy
- Tenant upsert: match by `Id` → update if exists; match by `Code` only if `Id` absent → detect conflict.
- Branding upsert: `GetOrCreate` by `TenantId`.
- Domain upsert: check for existing `Subdomain`-type record by `TenantId` + host before creating.
- Re-running returns the same converged state — never duplicates.

### TenantId preservation
The existing `Tenant.Rehydrate()` factory accepts an explicit `id` parameter. Combined with EF `Add` on new entities (tracked by Id), this guarantees the original Identity `Guid` is preserved exactly.

### Status normalization (Identity → Tenant)
| Identity state | TenantStatus |
|---|---|
| `IsActive=true` AND `ProvisioningStatus=Active` | `Active` |
| `IsActive=false` | `Inactive` |
| `ProvisioningStatus` ∈ {Pending, InProgress, Verifying} | `Pending` |
| `ProvisioningStatus=Failed` | `Inactive` |
| Anything else | `Active` |

### Subdomain domain record creation
Only creates a `TenantDomain` record if:
- Identity tenant has a non-empty `Subdomain`
- No existing `TenantDomain` of type `Subdomain` exists for this tenant with the same host
- Host is derived as `{subdomain}` (bare slug) — no base domain appended (base domain is environment-specific and unknown here)
- If a bare slug is not a valid hostname per `TenantDomain.IsValidHost()`, the domain record is skipped (logged as warning), and `Tenant.Subdomain` is still preserved

### Bug fixes from B04
B04 SQL queried `FROM tenants` (should be `idt_Tenants`) and selected `DisplayName` (should be `Name`). Both fixed in B05. These bugs were latent because `IdentityDb` was not configured — the dry-run always returned `IdentityAccessible: false`.

### Rollback-safe persistence
Two new tables added:
- `tenant_MigrationRuns` — one row per execute call (summary + metadata)
- `tenant_MigrationRunItems` — one row per Identity tenant processed per run

Provides a durable audit log of what changed without requiring destructive rollback.

### Dual write preparation (Option A + C hybrid)
- `ITenantSyncAdapter` interface (Application layer) defines the sync contract
- `TenantSyncRequest` DTO captures the event payload
- `NoOpTenantSyncAdapter` (Infrastructure) is registered and does nothing
- Wire-up (Identity → adapter) is deferred to Block 6
- Controlled by `Features:TenantDualWriteEnabled` config flag (default: `false`)

---

## 3. New Domain Entities

| Entity | Table | Purpose |
|---|---|---|
| `MigrationRun` | `tenant_MigrationRuns` | Per-execution audit record |
| `MigrationRunItem` | `tenant_MigrationRunItems` | Per-tenant action within a run |

---

## 4. New Application Layer

| Addition | Purpose |
|---|---|
| `MigrationExecuteRequest` DTO | Request shape for execute endpoint |
| `MigrationExecutionResult` DTO | Full structured result per run |
| `MigrationTenantResult` DTO | Per-tenant action + changes |
| `MigrationRunSummary` DTO | History list item |
| `TenantSyncRequest` DTO | Dual-write event contract |
| `ITenantSyncAdapter` interface | Dual-write abstraction |
| `IMigrationUtilityService` extended | `ExecuteAsync`, `GetHistoryAsync`, `GetRunAsync` |

---

## 5. New Infrastructure Layer

| Addition | Purpose |
|---|---|
| `TenantMigrationRunConfiguration` | EF config for `MigrationRun` |
| `TenantMigrationRunItemConfiguration` | EF config for `MigrationRunItem` |
| `MigrationUtilityService` extended | `ExecuteAsync` implementation + B04 SQL bug fixes |
| `NoOpTenantSyncAdapter` | Disabled-by-default dual-write stub |
| Migration `20260423220000_AddMigrationRunTracking` | Schema for audit tables |

---

## 6. Migration Scope

Per-tenant execute actions:

1. **Tenant core** — upsert Id, Code, Name→DisplayName, Status (normalized), Subdomain, LogoDocumentId, LogoWhiteDocumentId, CreatedAtUtc, UpdatedAtUtc
2. **Branding basics** — upsert LogoDocumentId + LogoWhiteDocumentId into `TenantBranding` row (create if absent)
3. **Domain compat** — if Subdomain exists and is a valid hostname, ensure `TenantDomain` Subdomain-type record exists
4. **Entitlements / Settings** — NOT migrated in this block (no direct Identity equivalent)

---

## 7. API Contract

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/admin/migration/dry-run` | B04 dry-run (unchanged) |
| `POST` | `/api/admin/migration/execute` | Execute migration |
| `GET` | `/api/admin/migration/history` | List recent runs (last 20) |
| `GET` | `/api/admin/migration/history/{runId}` | Get full run result |

### Execute request shape
```json
{
  "scope": "all",
  "tenantId": null,
  "tenantCode": null,
  "allowUpdates": true,
  "allowCreates": true
}
```

### Execute response shape
```json
{
  "runId": "...",
  "generatedAtUtc": "...",
  "mode": "Execute",
  "scope": "all",
  "identityAccessible": true,
  "tenantAccessible": true,
  "totalIdentityTenantsScanned": 5,
  "tenantsCreated": 3,
  "tenantsUpdated": 2,
  "tenantsSkipped": 0,
  "conflictsDetected": 0,
  "errorsDetected": 0,
  "durationMs": 220,
  "tenantResults": [...]
}
```

---

## 8. Validation Results

- [x] Build: `dotnet build Tenant.Api.csproj` — clean (0 errors)
- [x] Migration `20260423035637_AddMigrationRunTracking` applied at startup
- [x] Tables created: `tenant_MigrationRuns`, `tenant_MigrationRunItems` (with FK + indexes)
- [x] Migration schema fix: removed stray `AddColumn TimeZone` from auto-generated migration (column already existed in live DB from B02 entity — latent snapshot mismatch fixed)
- [x] `GET /api/admin/migration/dry-run` — HTTP 401 (registered + AdminOnly protected)
- [x] `POST /api/admin/migration/execute` — HTTP 401 (registered + AdminOnly protected)
- [x] `GET /api/admin/migration/history` — HTTP 401 (registered + AdminOnly protected)
- [x] `GET /api/admin/migration/history/{runId}` — HTTP 401 (registered + AdminOnly protected)
- [x] `NoOpTenantSyncAdapter` registered as `ITenantSyncAdapter` — dual-write disabled by default
- [x] Tenant service health: `{"status":"ok","service":"tenant"}` — HTTP 200
- [x] Control Center health: HTTP 200 — unchanged
- [x] B04 SQL bug fixes applied: `FROM tenants` → `FROM idt_Tenants`, `DisplayName` → `Name`
- [x] Identity `ProvisioningStatus`-aware status mapping implemented

---

## 9. Known Gaps / Deferred Items

| Item | Reason Deferred |
|---|---|
| Final read switch | Block 6 |
| Identity ownership removal | Block 7+ |
| Dual write wire-up (Identity → adapter) | Block 6 |
| Custom domain migration | Cannot safely derive custom domain host from Identity data |
| Entitlement / Setting migration | No direct Identity equivalent exists |
| Automated dry-run scheduling | Out of scope |
| Destructive rollback | Intentionally not implemented — audit log + re-run provides convergence |
