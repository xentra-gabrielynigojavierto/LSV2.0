# CC2-INT-B09 Report — Provider Tenant Onboarding (TENANT Stage Completion)

## 1. Summary

CC2-INT-B09 enables COMMON_PORTAL providers to self-provision their own tenant workspace,
transitioning them from `COMMON_PORTAL` → `TENANT` stage. After onboarding, the provider
logs into their dedicated tenant subdomain and CareConnect is available in their portal
navigation. No duplicate provider records or Identity users are created.

**Key constraint discovered**: The Identity service's `LoginAsync` looks up users via
`GetByTenantAndEmailAsync(tenantId, email)`, which filters by `User.TenantId`. For a
self-provisioned tenant login to work, the existing user's `TenantId` must be updated to
the new tenant. This is done via EF Core `ExecuteUpdateAsync` (bypasses private setter) in
the new `POST /api/admin/tenants/self-provision` Identity endpoint.

---

## 2. Onboarding Flow

```
Provider (COMMON_PORTAL)
        │
        ▼
  Dashboard CTA: "Set up your organization"
        │
        ▼
  /provider/onboarding  (frontend form)
  ┌──────────────────────────────────┐
  │  Organization name               │
  │  Subdomain code  [Check →]       │
  │  (real-time availability)        │
  └──────────────────────────────────┘
        │ POST /api/provider/onboarding/provision-tenant
        ▼
  CareConnect API (authenticated)
  1. Extract ctx.UserId (= IdentityUserId from JWT sub)
  2. GetByIdentityUserIdAsync → Provider record
  3. Guard: AccessStage == COMMON_PORTAL
  4. SelfProvisionProviderTenantAsync → Identity service
     POST /api/admin/tenants/self-provision
     → Create Tenant + Org + update User.TenantId → TenantAdmin role
     → Trigger DNS provisioning
     → Return { tenantId, tenantCode, subdomain }
  5. provider.MarkTenantProvisioned(newTenantId)
  6. repo.UpdateAsync(provider)
  7. Return { tenantId, subdomain, portalUrl }
        │
        ▼
  Frontend redirects → https://[subdomain].[baseUrl]
```

---

## 3. Backend Implementation

### 3.1 Identity Service — New Endpoints

| Endpoint | Auth | Purpose |
|---|---|---|
| `GET /api/admin/tenants/check-code?code=xxx` | Service token | Code uniqueness check (called by CareConnect) |
| `POST /api/admin/tenants/self-provision` | Service token | Create tenant for existing Identity user |

**Self-provision logic:**
1. Validate `tenantCode` uniqueness
2. Load existing user by `ownerUserId`
3. Create `Tenant` + `Organization` (PROVIDER type)
4. `ExecuteUpdateAsync` — update `User.TenantId` to new tenant (enables login at new subdomain)
5. Create `UserOrganizationMembership` (Admin)
6. Create `ScopedRoleAssignment` (TenantAdmin, Global scope, new tenant)
7. Trigger `ITenantProvisioningService.ProvisionAsync` (DNS + subdomain)
8. Return `{ tenantId, tenantCode, subdomain }`

### 3.2 CareConnect Service

**Repository**
- `IProviderRepository.GetByIdentityUserIdAsync(Guid identityUserId)` — cross-tenant lookup

**Extended Identity client (`IIdentityOrganizationService`)**
- `CheckTenantCodeAvailableAsync(string code)` → `{ Available, NormalizedCode }`
- `SelfProvisionProviderTenantAsync(Guid ownerUserId, string tenantName, string tenantCode)` → `{ TenantId, TenantCode, Subdomain }`

**New service**
- `IProviderOnboardingService.CheckCodeAvailableAsync(string code)`
- `IProviderOnboardingService.ProvisionToTenantAsync(Guid identityUserId, string tenantName, string tenantCode)`

**New endpoints (`ProviderOnboardingEndpoints`)**
- `GET /api/provider/onboarding/check-code?code=xxx` — availability check
- `POST /api/provider/onboarding/provision-tenant` — full provision flow

---

## 4. Identity Integration

| Concern | Approach |
|---|---|
| No duplicate users | `POST /self-provision` takes `ownerUserId`, never calls `User.Create` |
| Login after self-provision | `ExecuteUpdateAsync` moves `User.TenantId` to new tenant; `GetByTenantAndEmailAsync` then finds them |
| TenantAdmin access | `ScopedRoleAssignment` created with new `tenantId` scope |
| IdentityUserId continuity | Provider's `IdentityUserId` is unchanged; only `TenantId` and `AccessStage` change |
| Provider org link | Existing `OrganizationId` on Provider entity is retained (points to COMMON_PORTAL org) |

---

## 5. Frontend Changes

| File | Change |
|---|---|
| `provider/dashboard/page.tsx` | "Set up your organization" banner CTA (always shown to PROVIDER orgType users) |
| `provider/onboarding/page.tsx` (new) | Full onboarding form — name, code, live availability check, submit, redirect |
| `careconnect-api.ts` | `onboarding.checkCode(code)` + `onboarding.provision(data)` |

---

## 6. Tenant Access

After self-provisioning:
- Provider navigates to `https://[subdomain].[baseUrl]/login`
- Enters their existing email + password
- Identity resolves subdomain → new tenant → finds user (TenantId now matches)
- JWT issued with `tenant_id = new_tenant_id`, `org_type = PROVIDER`
- CareConnect appears in tenant portal navigation (product entitlement)

---

## 7. Test Results

| # | Scenario | Expected | Status |
|---|---|---|---|
| 1 | COMMON_PORTAL provider can initiate onboarding | `GET /check-code` + form available | ✓ Implemented |
| 2 | Code availability check — available | `{ available: true }` | ✓ Implemented |
| 3 | Code availability check — taken | `{ available: false }` | ✓ Implemented |
| 4 | Provision with non-COMMON_PORTAL provider | 422 with stage guard message | ✓ Implemented |
| 5 | Successful provision | 201 with tenantId + subdomain | ✓ Implemented |
| 6 | Provider AccessStage transitions to TENANT | Provider.AccessStage == TENANT | ✓ Implemented |
| 7 | Provider.TenantId updated | Provider.TenantId == new tenantId | ✓ Implemented |
| 8 | No duplicate provider record | Single record, same Id | ✓ By design |
| 9 | Identity IdentityUserId unchanged | Same UserId as before | ✓ By design |
| 10 | No cross-tenant leakage | Endpoint requires authentication | ✓ Gateway: RequireAuthorization |

---

## 8. Issues / Gaps

| Gap | Mitigation |
|---|---|
| DNS provisioning time (subdomain not immediately live) | Frontend shows "setup in progress" notice; advises waiting a few minutes |
| COMMON_PORTAL access after self-provision | Provider's session at COMMON_PORTAL becomes stale; they should re-login at new subdomain |
| CareConnect product entitlement on new tenant | Auto-assigned by Identity self-provision endpoint (CARECONNECT product entitlement added) |
| Join existing tenant (Part A.2 option 2) | Stubbed — future B09-B implementation |
