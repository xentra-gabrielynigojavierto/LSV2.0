# TENANT-B11 Report — Complete Tenant Write Ownership (Controlled)

**Status:** IN PROGRESS

---

## 1. Objective

Move tenant-management read and write ownership further from Identity into the Tenant service in a controlled, reversible way. Complete the Tenant service as the canonical source for tenant master data, branding, domains, entitlements, capabilities, and settings. Introduce controlled Tenant → Identity orchestration for auth/provisioning concerns. Fix known B10 sync gaps.

---

## 2. Codebase Analysis

### What B10 left incomplete

| Gap | Description |
|-----|-------------|
| Control Center list/detail reads | Still calling `/identity/api/admin/tenants` because Identity returns richer fields (type, userCount, sessionTimeoutMinutes, productEntitlements via Identity DB) |
| ClearTenantLogo sync | Identity `ClearTenantLogo` clears logo in Identity DB but does NOT call `syncAdapter.SyncAsync` — Tenant DB remains out of sync |
| ClearTenantLogoWhite sync | Same gap — `ClearTenantLogoWhite` has no sync call |
| TenantDualWriteEnabled=false | Identity-side dual-write was disabled, so new tenant creates did NOT propagate to Tenant DB |
| PATCH /api/v1/admin/tenants/{id}/status | No dedicated status-update endpoint in Tenant service |

### Which reads/writes still depended on Identity

- `GET /identity/api/admin/tenants` — list view (Control Center BFF)
- `GET /identity/api/admin/tenants/{id}` — detail view (Control Center BFF)
- `POST /identity/api/admin/tenants` — tenant create with admin user provisioning
- `PATCH /identity/api/admin/tenants/{id}/session-settings` — session timeout (Identity-owned)
- `POST /identity/api/admin/tenants/{id}/entitlements/{productCode}` — entitlement toggle (Identity-owned, syncs to Identity DB products)

### Why this block is needed

- Control Center cannot use Tenant as primary data source for tenant management screens without richer read API
- B10 sync gaps allow logo divergence between Identity and Tenant DBs
- Dual-write disabled means new tenants created via Identity don't appear in Tenant DB (breaking future reads)

---

## 3. Ownership Transition Design

### What writes moved to Tenant

| Write | New Owner | Notes |
|-------|-----------|-------|
| Tenant profile update (PUT) | Tenant | Already existed — TenantEndpoints.MapPut |
| Tenant deactivation (DELETE) | Tenant | Already existed |
| Logo set/clear (PATCH/DELETE) | Tenant | Moved in B10 |
| Status update (PATCH) | Tenant | New endpoint — TenantAdminEndpoints |

### What stayed in Identity

| Concern | Owner | Reason |
|---------|-------|--------|
| Admin user creation | Identity | Creates users, roles, org membership atomically |
| Tenant auth provisioning (DNS, products) | Identity | `ITenantProvisioningService` + `IProductProvisioningService` are Identity-owned |
| Session timeout settings | Identity | Auth policy enforcement |
| Entitlement toggle | Identity | Also updates Identity's TenantProducts table used for product access checks |
| Retry provisioning / verification | Identity | DNS-specific provisioning state machine |

### Orchestration design

- `IIdentityCompatAdapter` (Tenant → Identity, read-through): reads `sessionTimeoutMinutes` from Identity's admin endpoint. Graceful degradation — returns `null` on timeout or error.
- TenantDualWriteEnabled enabled in Identity: existing `HttpTenantSyncAdapter` propagates new tenant creates to Tenant DB automatically.

### Transactional model for CreateTenant

CreateTenant remains in Identity for B11. The path is:
1. Control Center → POST /identity/api/admin/tenants (Identity creates + provisions)
2. Identity `syncAdapter.SyncAsync` (now enabled via `TenantDualWriteEnabled=true`) syncs to Tenant DB
3. Reads go to Tenant service (the newly synced record appears on next read)

This is a **staged with eventual consistency** model. The sync is fire-and-forget (non-blocking). A brief window exists where the new tenant is in Identity DB but not yet in Tenant DB. This is acceptable and documented.

---

## 4. API Contract

### New / updated Tenant service admin endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/admin/tenants` | Admin tenant list (enriched, compatible with mapTenantSummary) |
| GET | `/api/v1/admin/tenants/{id}` | Admin tenant detail (enriched, compatible with mapTenantDetail) |
| PATCH | `/api/v1/admin/tenants/{id}/status` | Targeted status update |

All require `PlatformAdmin` policy.

### Admin list response shape

```json
{
  "items": [
    {
      "id": "...",
      "code": "...",
      "displayName": "...",
      "type": "LawFirm",
      "status": "Active",
      "isActive": true,
      "primaryContactName": "",
      "userCount": 0,
      "orgCount": 0,
      "createdAtUtc": "...",
      "subdomain": "..."
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

### Admin detail response shape

```json
{
  "id": "...",
  "code": "...",
  "displayName": "...",
  "type": "LawFirm",
  "status": "Active",
  "isActive": true,
  "primaryContactName": "",
  "userCount": 0,
  "orgCount": 0,
  "activeUserCount": 0,
  "linkedOrgCount": 0,
  "createdAtUtc": "...",
  "updatedAtUtc": "...",
  "subdomain": "...",
  "email": null,
  "sessionTimeoutMinutes": 30,
  "logoDocumentId": "...",
  "logoWhiteDocumentId": "...",
  "productEntitlements": [
    {
      "productCode": "CareConnect",
      "productName": "CareConnect",
      "enabled": true,
      "status": "Active",
      "enabledAtUtc": null
    }
  ],
  "identityCompatibility": {
    "sessionTimeoutMinutes": 30,
    "source": "IdentityCompat"
  }
}
```

### BFF route changes

| Method | Old Target | New Target |
|--------|-----------|------------|
| GET tenants.list | `/identity/api/admin/tenants` | `/tenant/api/v1/admin/tenants` |
| GET tenants.getById | `/identity/api/admin/tenants/{id}` | `/tenant/api/v1/admin/tenants/{id}` |
| POST tenants.create | `/identity/api/admin/tenants` | **unchanged** (Identity still owns provisioning) |
| POST tenants.updateEntitlement | `/identity/api/admin/tenants/{id}/entitlements/...` | **unchanged** |
| PATCH tenants.updateSessionSettings | `/identity/api/admin/tenants/{id}/session-settings` | **unchanged** |

### Identity compatibility changes

- `ClearTenantLogo`: add `ITenantSyncAdapter syncAdapter` parameter + non-blocking sync call after clearing
- `ClearTenantLogoWhite`: same fix
- `TenantDualWriteEnabled`: set to `true` in Identity appsettings

---

## 5. Identity Integration

### Adapter interfaces / implementations

| Interface | File | Purpose |
|-----------|------|---------|
| `IIdentityCompatAdapter` | Tenant.Application/Interfaces | Read-through for sessionTimeoutMinutes |
| `HttpIdentityCompatAdapter` | Tenant.Infrastructure/Services | HTTP GET to Identity admin endpoint |

### Hook points added

- Identity `ClearTenantLogo`: syncAdapter.SyncAsync added (non-blocking)
- Identity `ClearTenantLogoWhite`: syncAdapter.SyncAsync added (non-blocking)
- Identity `TenantDualWriteEnabled=true`: enables CreateTenant → Tenant sync

### Sync gap fixes

| Gap | Fix |
|----|-----|
| ClearTenantLogo missing sync | Added `ITenantSyncAdapter syncAdapter` parameter + `_ = syncAdapter.SyncAsync(...)` |
| ClearTenantLogoWhite missing sync | Same fix |
| TenantDualWriteEnabled=false | Enabled in Identity appsettings.json |

### What remains deferred

- Entitlement toggle still calls Identity (syncing to Tenant entitlements table is a separate concern)
- CreateTenant orchestration (Pattern A with Tenant-first + ID coordination) deferred to B12
- Session timeout write path still at Identity
- Provisioning/verification retry still at Identity

---

## 6. Implementation Summary

### Files added

| File | Description |
|------|-------------|
| `Tenant.Application/Interfaces/IIdentityCompatAdapter.cs` | Read-through interface for Identity compat data |
| `Tenant.Application/DTOs/TenantAdminDtos.cs` | Admin-specific response shapes compatible with control-center mappers |
| `Tenant.Application/Interfaces/ITenantAdminService.cs` | Admin read/write service interface |
| `Tenant.Application/Services/TenantAdminService.cs` | Aggregates tenant + branding + entitlements + compat |
| `Tenant.Infrastructure/Services/HttpIdentityCompatAdapter.cs` | HTTP read-through to Identity admin endpoint |
| `Tenant.Api/Endpoints/TenantAdminEndpoints.cs` | GET list, GET detail, PATCH status |

### Files modified

| File | Change |
|------|--------|
| `Tenant.Infrastructure/DependencyInjection.cs` | Register IIdentityCompatAdapter, ITenantAdminService |
| `Tenant.Api/Program.cs` | Map TenantAdminEndpoints |
| `Tenant.Api/appsettings.json` | Add IdentityService:InternalUrl |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Fix ClearTenantLogo + ClearTenantLogoWhite sync gap |
| `Identity.Api/appsettings.json` | TenantDualWriteEnabled=true |
| `control-center/src/lib/control-center-api.ts` | Switch tenants.list/getById to Tenant service |

---

## 7. Validation Results

*To be filled in after implementation.*

| Check | Result | Notes |
|-------|--------|-------|
| Solution build | PENDING | |
| Tenant service startup | PENDING | |
| Identity service startup | PENDING | |
| Gateway startup | PENDING | |
| GET admin tenants (Tenant service) → 401 | PENDING | |
| GET admin tenants/{id} (Tenant service) → 401 | PENDING | |
| PATCH admin tenants/{id}/status → 401 | PENDING | |
| Control Center reads from Tenant | PENDING | |
| ClearLogo sync fixed | PENDING | |

---

## 8. Remaining Identity Dependencies

| Concern | Stays in Identity | Retirement Path |
|---------|-------------------|-----------------|
| CreateTenant + admin user provisioning | Identity | B12: Tenant-first Pattern A with explicit ID coordination |
| Session timeout auth enforcement | Identity | Permanent (auth concern) |
| Entitlement toggle (Identity products) | Identity | B12: sync entitlements to Tenant DB on toggle |
| DNS provisioning / retry / verification | Identity | Long-term: extract to separate provisioning service |

---

## 9. Final Assessment

After B11:
- **Tenant** is the effective primary owner of tenant master-data reads for Control Center
- **Identity** remains the provisioning and auth authority
- The platform is moving toward full ownership separation; physical retirement planning for Identity tenant tables can begin in B13+ once entitlement sync and CreateTenant ownership are complete
