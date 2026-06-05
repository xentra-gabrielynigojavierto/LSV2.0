# TENANT-B04 — Product Entitlements + Settings + Migration Utility Foundation

**Status:** COMPLETE  
**Date:** 2026-04-23  
**Block:** 4 of the Tenant service initiative

---

## 1. Objective

Extend the standalone Tenant service (port 5005) with:
1. Tenant product entitlement model (`TenantProductEntitlement`)
2. Capability flags model (`TenantCapability`)
3. Default product behavior (auto-demote prior default)
4. Tenant settings store (`TenantSetting`) — both typed core settings and extensible namespaced settings
5. Admin APIs for all three
6. Migration utility foundation — dry-run reconciliation between Identity and Tenant data

---

## 2. Design Decisions

### Default product behavior
**Decision: auto-demote** — when a new default is set, the prior default entitlement is automatically cleared (`IsDefault = false`). This is operator-friendly and avoids "reject on conflict" friction during administrative setup.

### Capability model
Capabilities are either **tenant-global** (`ProductEntitlementId = null`) or **product-scoped** (`ProductEntitlementId = <entitlementId>`). Uniqueness is enforced at the service layer: duplicate `(TenantId, CapabilityKey, ProductEntitlementId)` is rejected.

### Settings model
- `SettingKey` is normalized to lowercase and **must be dot-namespaced** (e.g., `platform.default-product`, `portal.locale.default`).
- `ValueType` enum: `String | Boolean | Number | Json`.
- `ProductKey` (nullable) scopes a setting to a specific product entitlement.
- Uniqueness enforced at service layer: duplicate `(TenantId, SettingKey, ProductKey)` is rejected.

### Migration utility
- **Dry-run only** in this block — no writes to either database.
- Inspects Identity tenant data via a separate read-only connection (`IdentityDb`).
- Reports: missing tenants, code mismatches, name mismatches, subdomain/domain gaps, logo gaps.
- Output: structured result via `/api/admin/migration/dry-run` endpoint and written to `analysis/TENANT-B04-migration-dry-run.md`.

### Table naming convention (matches B01–B03)
- `tenant_ProductEntitlements`
- `tenant_Capabilities`
- `tenant_Settings`

---

## 3. Entities Added

| Entity | Table | Purpose |
|---|---|---|
| `TenantProductEntitlement` | `tenant_ProductEntitlements` | Product entitlement per tenant |
| `TenantCapability` | `tenant_Capabilities` | Fine-grained capability flags |
| `TenantSetting` | `tenant_Settings` | Namespaced key/value settings store |

---

## 4. API Endpoints

### Entitlements (`/api/tenants/{tenantId}/entitlements`)
| Method | Route | Description |
|---|---|---|
| GET | `/api/tenants/{tenantId}/entitlements` | List all entitlements for tenant |
| GET | `/api/tenants/{tenantId}/entitlements/{id}` | Get single entitlement |
| POST | `/api/tenants/{tenantId}/entitlements` | Create entitlement |
| PUT | `/api/tenants/{tenantId}/entitlements/{id}` | Update entitlement |
| DELETE | `/api/tenants/{tenantId}/entitlements/{id}` | Remove entitlement |
| PUT | `/api/tenants/{tenantId}/entitlements/{id}/default` | Set as default (auto-demotes prior) |

### Capabilities (`/api/tenants/{tenantId}/capabilities`)
| Method | Route | Description |
|---|---|---|
| GET | `/api/tenants/{tenantId}/capabilities` | List capabilities |
| GET | `/api/tenants/{tenantId}/capabilities/{id}` | Get single capability |
| POST | `/api/tenants/{tenantId}/capabilities` | Create capability |
| PUT | `/api/tenants/{tenantId}/capabilities/{id}` | Update capability |
| DELETE | `/api/tenants/{tenantId}/capabilities/{id}` | Remove capability |

### Settings (`/api/tenants/{tenantId}/settings`)
| Method | Route | Description |
|---|---|---|
| GET | `/api/tenants/{tenantId}/settings` | List settings |
| GET | `/api/tenants/{tenantId}/settings/{id}` | Get single setting |
| PUT | `/api/tenants/{tenantId}/settings` | Upsert setting by key |
| DELETE | `/api/tenants/{tenantId}/settings/{id}` | Remove setting |

### Migration utility (`/api/admin/migration`)
| Method | Route | Description |
|---|---|---|
| GET | `/api/admin/migration/dry-run` | Run dry-run reconciliation |

---

## 5. Architecture Decisions

- **No read switch** from Identity to Tenant in this block.
- **No dual write** from Identity to Tenant in this block.
- **No Identity field removal** in this block.
- Migration utility uses `IdentityDb` connection string (read-only) via raw SQL — no EF model for Identity entities.
- Identity connection must be configured in `appsettings.json` under `ConnectionStrings:IdentityDb`; falls back gracefully if absent (dry-run returns "unavailable").

---

## 6. Implementation Summary

**Files added:**
- `Tenant.Domain/TenantProductEntitlement.cs`
- `Tenant.Domain/TenantCapability.cs`
- `Tenant.Domain/TenantSetting.cs`
- `Tenant.Application/DTOs/EntitlementDtos.cs`
- `Tenant.Application/DTOs/CapabilityDtos.cs`
- `Tenant.Application/DTOs/SettingDtos.cs`
- `Tenant.Application/DTOs/MigrationDtos.cs`
- `Tenant.Application/Interfaces/IEntitlementRepository.cs`
- `Tenant.Application/Interfaces/ICapabilityRepository.cs`
- `Tenant.Application/Interfaces/ISettingRepository.cs`
- `Tenant.Application/Interfaces/IEntitlementService.cs`
- `Tenant.Application/Interfaces/ICapabilityService.cs`
- `Tenant.Application/Interfaces/ISettingService.cs`
- `Tenant.Application/Interfaces/IMigrationUtilityService.cs`
- `Tenant.Application/Services/EntitlementService.cs`
- `Tenant.Application/Services/CapabilityService.cs`
- `Tenant.Application/Services/SettingService.cs`
- `Tenant.Application/Services/MigrationUtilityService.cs`
- `Tenant.Infrastructure/Data/Configurations/TenantProductEntitlementConfiguration.cs`
- `Tenant.Infrastructure/Data/Configurations/TenantCapabilityConfiguration.cs`
- `Tenant.Infrastructure/Data/Configurations/TenantSettingConfiguration.cs`
- `Tenant.Infrastructure/Repositories/EntitlementRepository.cs`
- `Tenant.Infrastructure/Repositories/CapabilityRepository.cs`
- `Tenant.Infrastructure/Repositories/SettingRepository.cs`
- `Tenant.Infrastructure/Data/Migrations/20260423210000_AddEntitlementsCapabilitiesSettings.cs`
- `Tenant.Api/Endpoints/EntitlementEndpoints.cs`
- `Tenant.Api/Endpoints/CapabilityEndpoints.cs`
- `Tenant.Api/Endpoints/SettingEndpoints.cs`
- `Tenant.Api/Endpoints/MigrationEndpoints.cs`

**Files modified:**
- `Tenant.Infrastructure/Data/TenantDbContext.cs`
- `Tenant.Infrastructure/Data/Migrations/TenantDbContextModelSnapshot.cs`
- `Tenant.Infrastructure/DependencyInjection.cs`
- `Tenant.Api/Program.cs`

**Migration added:** `20260423210000_AddEntitlementsCapabilitiesSettings`

---

## 7. Database Changes

| Table | Indexes | Constraints |
|---|---|---|
| `tenant_ProductEntitlements` | `TenantId`, `ProductKey`, `IsDefault` | Service-layer uniqueness on `(TenantId, ProductKey)` |
| `tenant_Capabilities` | `TenantId`, `CapabilityKey`, `ProductEntitlementId` | Service-layer uniqueness on `(TenantId, CapabilityKey, ProductEntitlementId)` |
| `tenant_Settings` | `TenantId`, `SettingKey`, `ProductKey` | Service-layer uniqueness on `(TenantId, SettingKey, ProductKey)` |

*Note: MySQL does not support filtered unique indexes; uniqueness enforced in service layer.*

---

## 8. Validation Results

- [x] Build: `dotnet build Tenant.Api.csproj` — clean (0 errors, 0 relevant warnings)
- [x] Startup: service starts on port 5005, `{"status":"ok","service":"tenant"}` confirmed
- [x] Migration `20260423210000_AddEntitlementsCapabilitiesSettings` applied successfully at startup
- [x] Tables created: `tenant_ProductEntitlements`, `tenant_Capabilities`, `tenant_Settings`
- [x] Entitlement API: endpoints registered (CRUD + `/default` promotion, AdminOnly)
- [x] Capability API: endpoints registered (CRUD, AdminOnly)
- [x] Settings API: endpoints registered (GET/PUT upsert/DELETE, AdminOnly)
- [x] Migration dry-run: `GET /api/admin/migration/dry-run` registered (AdminOnly)
- [x] Migration dry-run artifact: `analysis/TENANT-B04-migration-dry-run.md` written
- [x] Backward compatibility: CC health `HTTP 200`, Tenant health `HTTP 200`, B01–B03 endpoints unchanged
- [x] CC production error (routeModule.prepare not a function): resolved via Next.js 15.5.15 module symlink fix

---

## 9. Known Gaps / Deferred Items

- **Read switch deferred** — Identity remains the authoritative source for B01 consumers.
- **Dual write deferred** — no writes to Identity from Tenant service.
- **Identity decoupling deferred** — tenant fields remain in Identity DB.
- **DNS verification deferred** — custom domain activation requires external verification.
- **Notification integration deferred** — no email/webhook hooks on setting changes.
- **Migration write mode deferred** — only dry-run in this block.

---

## 10. Next Recommended Block

**BLOCK 5 — Migration Execution + Dual Write Preparation**  
Implement write mode for the migration utility, introduce dual-write guard, and prepare the read switch plan for Block 6.
