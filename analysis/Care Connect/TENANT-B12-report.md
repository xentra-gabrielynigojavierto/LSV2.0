# TENANT-B12 Report — Full Lifecycle Ownership

**Status:** COMPLETE  
**Block:** TENANT-B12  
**Date:** 2026-04-23

---

## 1. Objective

Make the Tenant service the true lifecycle owner of tenant management:
- Canonical tenant creation moves from Identity → Tenant service
- Platform-level entitlement ownership moves to Tenant service
- Identity reduced to auth/session/provisioning-support role
- Tenant → Identity orchestration introduced via `IIdentityProvisioningAdapter`
- All changes rollback-safe; Identity compatibility endpoints preserved

---

## 2. Codebase Analysis

### What B11 left in Identity

| Concern | Owner after B11 |
|---|---|
| Tenant canonical record creation | Identity (`POST /api/admin/tenants`) |
| Entitlement write/toggle | Identity (`POST /api/admin/tenants/{id}/entitlements/{productCode}`) |
| Session timeout settings | Identity |
| DNS provisioning | Identity |
| Admin user creation | Identity |
| Role assignment | Identity |
| Read: tenant list/detail | **Tenant** (switched in B11) |
| Read: branding/logo | **Tenant** (switched in B09/B10) |
| Write: logo | **Tenant** (switched in B10) |

### Why B12 is the right next step

- Control Center read paths all use Tenant service (B11)
- Dual-write from Identity → Tenant is enabled (B11)
- Identity's `CreateTenant` still creates the canonical tenant data first; Tenant DB is populated via sync
- B12 inverts this: Tenant creates first, Identity does downstream auth work via adapter

---

## 3. Ownership Transition Design

### What becomes Tenant-owned after B12

| Concern | B12 Owner |
|---|---|
| Canonical tenant record creation | **Tenant** (`POST /api/v1/admin/tenants`) |
| Platform entitlement toggle | **Tenant** (`POST /api/v1/admin/tenants/{id}/entitlements/{productCode}`) |
| Status updates | Tenant (already B11) |
| Admin reads (list/detail) | Tenant (already B11) |
| Logo/branding | Tenant (already B09/B10) |

### What remains Identity-owned

| Concern | Reason |
|---|---|
| Session timeout enforcement | Auth-side, cannot move without auth rewrite |
| Admin user creation | Credential/password hash management |
| Role assignment (TenantAdmin) | RBAC model lives in Identity |
| DNS/subdomain provisioning | Retry state machine in Identity |
| Product provisioning (TenantProduct, OrgProduct) | Identity DB schema ownership |
| Provisioning retry/verification | State machine owned by Identity |
| Auth/login gating | Core Identity function |

### Adapter / Orchestration Design

**Pattern chosen:** Staged compensating model (NOT distributed transaction)

```
Control Center
    │
    ▼
POST /tenant/api/v1/admin/tenants          ← NEW canonical entry point
    │
    ├─ 1. Create Tenant record (Tenant DB, status=Active)
    │
    ├─ 2. Call IIdentityProvisioningAdapter.ProvisionAsync(...)
    │       → POST /api/internal/tenant-provisioning/provision (Identity)
    │             ├─ Create Identity.Tenant (with tenantId from Tenant service)
    │             ├─ Create Identity.Organization
    │             ├─ Create Identity.User + PasswordHash + TempPassword
    │             ├─ Create Identity.UserOrganizationMembership
    │             ├─ Create Identity.ScopedRoleAssignment (TenantAdmin)
    │             ├─ DNS provisioning (ITenantProvisioningService)
    │             └─ Product provisioning (IProductProvisioningService)
    │
    └─ 3. Return structured lifecycle response
           { tenantId, displayName, code, adminUserId, adminEmail,
             temporaryPassword, subdomain, hostname, provisioningStatus,
             identityProvisioned, tenantCreated, provisioningWarnings, errors }
```

**Failure model:**
- If Identity provisioning fails → Tenant record REMAINS (it is canonical)
- Response includes `identityProvisioned: false` + error details
- Admin can retry provisioning via existing Identity retry endpoints
- No silent partial success

### Entitlement toggle pattern

```
POST /tenant/api/v1/admin/tenants/{id}/entitlements/{productCode}
    │
    ├─ 1. Upsert TenantProductEntitlement in Tenant DB (authoritative)
    │
    └─ 2. Best-effort: call Identity entitlement sync to trigger
           TenantProduct + OrgProduct updates in Identity DB
           (fire-and-forget, failure does not affect response)
```

---

## 4. API Contract

### New Tenant endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/v1/admin/tenants` | **NEW** — canonical tenant create |
| `POST` | `/api/v1/admin/tenants/{id}/entitlements/{productCode}` | **NEW** — admin entitlement toggle |

### Updated BFF routes

| BFF method | Before | After |
|---|---|---|
| `tenants.create` | `/identity/api/admin/tenants` | `/tenant/api/v1/admin/tenants` |
| `tenants.updateEntitlement` | `/identity/api/admin/tenants/{id}/entitlements/{productCode}` | `/tenant/api/v1/admin/tenants/{id}/entitlements/{productCode}` |

### Identity compatibility changes

| Endpoint | Change |
|---|---|
| `POST /api/admin/tenants` | Added `X-Deprecated: true`, `X-Deprecated-By: TENANT-B12` |
| `POST /api/admin/tenants/{id}/entitlements/{productCode}` | Added `X-Deprecated: true`, `X-Deprecated-By: TENANT-B12` |

### Create request shape

```json
{
  "name": "Acme Corp",
  "code": "acme",
  "orgType": "LAW_FIRM",
  "adminEmail": "admin@acme.com",
  "adminFirstName": "Jane",
  "adminLastName": "Doe",
  "addressLine1": "...",
  "city": "...",
  "state": "...",
  "postalCode": "..."
}
```

### Create response shape (backward-compatible with existing BFF contract)

```json
{
  "tenantId": "...",
  "displayName": "Acme Corp",
  "code": "acme",
  "status": "Active",
  "adminUserId": "...",
  "adminEmail": "admin@acme.com",
  "temporaryPassword": "...",
  "subdomain": "acme",
  "provisioningStatus": "Provisioned",
  "hostname": "acme.demo.legalsynq.com",
  "tenantCreated": true,
  "identityProvisioned": true,
  "provisioningWarnings": [],
  "provisioningErrors": []
}
```

### Entitlement toggle request/response

**Request:** `POST /api/v1/admin/tenants/{id}/entitlements/{productCode}`
```json
{ "enabled": true }
```

**Response** (compatible with `mapEntitlementResponse`):
```json
{
  "productCode": "SynqFund",
  "productName": "SynqFund",
  "enabled": true,
  "status": "Active",
  "enabledAtUtc": "2026-04-23T..."
}
```

---

## 5. Identity Integration

### New internal provisioning endpoint

**Route:** `POST /api/internal/tenant-provisioning/provision`  
**Auth:** `X-Provisioning-Token` header matched against `TenantService:ProvisioningSecret` config.  
When `ProvisioningSecret` is empty (dev), token check is skipped.

**Operations performed:**
1. Create Identity.Tenant entity (using tenantId from request — the Tenant DB canonical ID)
2. Create Identity.Organization
3. Create Identity.User (generate temp password, bcrypt hash)
4. Create Identity.UserOrganizationMembership
5. Create Identity.ScopedRoleAssignment (TenantAdmin role)
6. Call ITenantProvisioningService.ProvisionAsync (DNS)
7. Call IProductProvisioningService.ProvisionAsync (products, if any)
8. Emit audit events

**NOT called:** Dual-write sync back to Tenant (Tenant is now canonical source)

### IIdentityProvisioningAdapter

```csharp
Task<IdentityProvisioningResult> ProvisionAsync(IdentityProvisioningRequest request, CancellationToken ct = default);
```

### Deferred items

- Entitlement sync from Tenant → Identity (B12 uses best-effort call to existing Identity endpoint)
- Session settings (remain Identity-owned)
- Provisioning retry (remains Identity-owned)

---

## 6. Implementation Summary

### Files added

| File | Description |
|---|---|
| `analysis/TENANT-B12-report.md` | This report |
| `Tenant.Application/Interfaces/IIdentityProvisioningAdapter.cs` | Interface for Identity lifecycle orchestration |
| `Tenant.Application/DTOs/TenantLifecycleDtos.cs` | Create request/response DTOs |
| `Tenant.Infrastructure/Services/HttpIdentityProvisioningAdapter.cs` | HTTP implementation of provisioning adapter |
| `Identity.Api/Endpoints/TenantProvisioningEndpoints.cs` | New Identity internal provisioning endpoint |

### Files modified

| File | Change |
|---|---|
| `Tenant.Application/Interfaces/ITenantAdminService.cs` | Added `CreateTenantAsync`, `ToggleEntitlementAsync` |
| `Tenant.Application/Services/TenantAdminService.cs` | Implemented new methods |
| `Tenant.Api/Endpoints/TenantAdminEndpoints.cs` | Added POST create + entitlement toggle endpoints |
| `Tenant.Infrastructure/DependencyInjection.cs` | Registered `IIdentityProvisioningAdapter` |
| `Tenant.Api/appsettings.json` | Added `IdentityService:ProvisioningSecret` |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Deprecated CreateTenant + UpdateEntitlement |
| `Identity.Api/Program.cs` | Registered `TenantProvisioningEndpoints` |
| `Identity.Api/appsettings.json` | Added `TenantService:ProvisioningSecret` |
| `control-center/src/lib/control-center-api.ts` | Switched `create` + `updateEntitlement` to Tenant service |

---

## 7. Validation Results

### Build / Startup

- Tenant.Api: Build succeeded (0 errors) ✅
- Identity.Api: Build succeeded (0 errors) ✅
- All services healthy (status:ok) ✅

### Endpoint registration

- `POST /tenant/api/v1/admin/tenants` → returns 401 (protected) ✅
- `POST /tenant/api/v1/admin/tenants/{id}/entitlements/{productCode}` → returns 401 ✅
- `POST /identity/api/internal/tenant-provisioning/provision` → returns 401 without token ✅
- Identity deprecated endpoints still callable ✅

### Ownership transfer

- `tenants.create` BFF → `/tenant/api/v1/admin/tenants` ✅
- `tenants.updateEntitlement` BFF → `/tenant/api/v1/admin/tenants/{id}/entitlements/{productCode}` ✅
- B11 read paths unchanged ✅
- B10 logo paths unchanged ✅

---

## 8. Remaining Identity Dependencies

| Dependency | Why stays in Identity | Future retirement requirement |
|---|---|---|
| Admin user creation | Password hashing, credential management | Move user provisioning to separate User service |
| Role assignment | RBAC model in Identity DB | Externalize RBAC |
| DNS provisioning | Retry state machine, Route53 integration | Move to Provisioning service |
| Product provisioning | TenantProduct + OrgProduct in Identity DB | Move to Tenant/Entitlement service |
| Session enforcement | Auth token validation | Cannot move without full auth redesign |
| Provisioning retry | State machine in Identity | Move to Provisioning service |

---

## 9. Final Assessment

- **Tenant is now the full lifecycle owner** of canonical tenant creation and platform-level entitlement state.
- **Identity is now primarily:** auth/session enforcement, admin-user creation mechanics, role assignment, DNS/product provisioning support services called via controlled adapter.
- **The platform is ready** for future physical Identity retirement planning for tenant management concerns.
- **Rollback path** preserved: Identity `CreateTenant` and `UpdateEntitlement` remain callable with deprecation headers.
