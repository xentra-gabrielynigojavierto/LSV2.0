# BLK-CC-01 Report

## 1. Summary

**Block:** CareConnect Onboarding Switch — Phase 1
**Status:** COMPLETE
**Date:** 2026-04-23
**Window:** TENANT-STABILIZATION 2026-04-23 → 2026-05-07

Rewires CareConnect provider onboarding away from the retired Identity tenant endpoints
(HTTP 410) and toward the new architecture:

```
CareConnect → Tenant service  (check-code, provision)
            → Identity service (assign-tenant / assign-roles via BLK-ID-02)
```

Provider lifecycle is unchanged: URL → COMMON_PORTAL → TENANT.

---

## 2. Old Identity Coupling Found

### `HttpIdentityOrganizationService.cs` (Infrastructure)

| Method | Identity Endpoint | Status |
|---|---|---|
| `CheckTenantCodeAvailableAsync` | `GET /api/admin/tenants/check-code` | **Retired by BLK-ID-01 (410)** |
| `SelfProvisionProviderTenantAsync` | `POST /api/admin/tenants/self-provision` | **Retired by BLK-ID-01 (410)** |

### `IIdentityOrganizationService.cs` (Application interface)

Both retired methods were declared here — `CheckTenantCodeAvailableAsync` and
`SelfProvisionProviderTenantAsync` — along with their result types
`TenantCodeCheckResult` and `SelfProvisionTenantResult`.

### `ProviderOnboardingService.cs` (Application service)

- `CheckCodeAvailableAsync` delegated entirely to `_identityService.CheckTenantCodeAvailableAsync`
- `ProvisionToTenantAsync` delegated entirely to `_identityService.SelfProvisionProviderTenantAsync`

No Identity membership (BLK-ID-02) APIs were called — the old Identity self-provision
endpoint created the tenant AND handled user membership internally.

---

## 3. Tenant Service Integration

### New interface: `ITenantServiceClient` (Application layer)

Two methods:
- `CheckCodeAsync(code)` → `GET /api/v1/tenants/check-code?code={code}`
- `ProvisionAsync(tenantName, tenantCode)` → `POST /api/v1/tenants/provision`

### New implementation: `HttpTenantServiceClient` (Infrastructure layer)

Matches the style of `HttpIdentityOrganizationService`:
- Options-bound (`TenantServiceOptions`, section `TenantService`)
- Named HTTP client `TenantService`
- Optional `X-Provisioning-Token` auth header
- Returns null / typed failure on all error paths (never throws infrastructure exceptions)
- 409 Conflict → typed `CODE_TAKEN` failure

### Tenant service provision endpoint auth

`POST /api/v1/tenants/provision` was `AdminOnly` (RequireRole PlatformAdmin).
Changed to `AllowAnonymous` + `X-Provisioning-Token` guard (same pattern as Identity's
provisioning endpoints). In dev mode (no secret configured) the check is skipped.
`GET /api/v1/tenants/check-code` was already `.AllowAnonymous()` — no change needed.

---

## 4. Identity Membership Integration

### New interface: `IIdentityMembershipClient` (Application layer)

One method:
- `AssignTenantAsync(userId, tenantId, roles)` → `POST /api/internal/users/assign-tenant`

Passes `X-Provisioning-Token` from `IdentityServiceOptions` auth header config.

### New implementation: `HttpIdentityMembershipClient` (Infrastructure layer)

- Reuses `IdentityServiceOptions` (same base URL, timeout, auth header)
- Returns null on network/5xx failure
- Returns typed result with `AlreadyInTenant` flag on 200

### Provisioning flow in `ProviderOnboardingService.ProvisionToTenantAsync`

Old:
```
_identityService.SelfProvisionProviderTenantAsync(userId, name, code)
→ returns tenant + sets user membership internally
```

New:
```
1. _tenantClient.ProvisionAsync(name, code)           → tenantId, code, subdomain
2. _identityMembership.AssignTenantAsync(userId, tenantId, ["TenantAdmin"])
3. provider.MarkTenantProvisioned(tenantId)
4. _providerRepo.UpdateAsync(provider)
```

Step 3 is only reached after BOTH steps 1 and 2 succeed. Partial failure
(Tenant provisioned, Identity assignment failed) → exception thrown, provider NOT
marked as TENANT.

---

## 5. Frontend / UX Impact

No frontend changes required. All endpoint routes and response shapes are preserved:
- `GET /api/provider/onboarding/check-code` → same response shape
- `POST /api/provider/onboarding/provision-tenant` → same response shape
- Error codes / HTTP statuses unchanged:
  - 409 → tenant code taken
  - 503 → service unavailable
  - 422 → wrong stage / invalid input

---

## 6. Data Consistency / Failure Handling

| Scenario | Behavior |
|---|---|
| Tenant code already taken (409 from Tenant svc) | `CODE_TAKEN` failure → `ProviderOnboardingErrorCode.TenantCodeUnavailable` → HTTP 409 to caller |
| Tenant service unavailable | `_tenantClient.ProvisionAsync` returns null → `IdentityServiceFailed` → HTTP 503 |
| Identity membership failure after tenant created | `_identityMembership.AssignTenantAsync` returns null → `IdentityServiceFailed` → HTTP 503. Provider NOT marked TENANT. **Partial failure window documented below.** |
| Provider not at COMMON_PORTAL | Rejected before any service call — HTTP 422 |
| Missing IdentityUserId | Rejected — provider not found or `identityUserId` comes from validated JWT |
| Duplicate call (idempotent?) | Tenant provision is NOT idempotent (409 on duplicate code). Recommend callers use check-code first. |

**Partial failure window:** If Tenant service provisions the tenant but Identity assignment
fails, the tenant record exists in Tenant service but the CareConnect provider remains at
COMMON_PORTAL. Retry of the provision step will get a 409 CODE_TAKEN. This is the known
Phase 1 gap — Phase 2 should add a rollback / recovery path (e.g. store tenantId on
provider before identity assignment, allow resume).

---

## 7. Changed Files

**New files:**
- `apps/services/careconnect/CareConnect.Application/Interfaces/ITenantServiceClient.cs`
- `apps/services/careconnect/CareConnect.Application/Interfaces/IIdentityMembershipClient.cs`
- `apps/services/careconnect/CareConnect.Infrastructure/Services/TenantServiceOptions.cs`
- `apps/services/careconnect/CareConnect.Infrastructure/Services/HttpTenantServiceClient.cs`
- `apps/services/careconnect/CareConnect.Infrastructure/Services/HttpIdentityMembershipClient.cs`

**Modified files:**
- `apps/services/careconnect/CareConnect.Application/Interfaces/IIdentityOrganizationService.cs` — removed retired methods + result types
- `apps/services/careconnect/CareConnect.Infrastructure/Services/HttpIdentityOrganizationService.cs` — removed retired method implementations
- `apps/services/careconnect/CareConnect.Application/Services/ProviderOnboardingService.cs` — refactored to use new clients
- `apps/services/careconnect/CareConnect.Infrastructure/DependencyInjection.cs` — registered new services
- `apps/services/careconnect/CareConnect.Api/appsettings.json` — added `TenantService` section
- `apps/services/careconnect/CareConnect.Api/appsettings.Development.json` — added `TenantService` dev config
- `apps/services/tenant/Tenant.Api/Endpoints/ProvisionEndpoints.cs` — provision endpoint now accepts `X-Provisioning-Token` (AllowAnonymous + token guard)

---

## 8. Methods / Endpoints Updated

### Service methods

| Old | New |
|---|---|
| `IIdentityOrganizationService.CheckTenantCodeAvailableAsync` | `ITenantServiceClient.CheckCodeAsync` |
| `IIdentityOrganizationService.SelfProvisionProviderTenantAsync` | `ITenantServiceClient.ProvisionAsync` + `IIdentityMembershipClient.AssignTenantAsync` |

### HTTP calls

| Old | New |
|---|---|
| `GET /api/admin/tenants/check-code` (Identity, **retired**) | `GET /api/v1/tenants/check-code` (Tenant service) |
| `POST /api/admin/tenants/self-provision` (Identity, **retired**) | `POST /api/v1/tenants/provision` (Tenant service) + `POST /api/internal/users/assign-tenant` (Identity) |

---

## 9. GitHub Commits (MANDATORY)

- `9ecc1c9` — BLK-CC-01: Update provider onboarding to use new Tenant and Identity membership services

---

## 10. Validation Results

- [x] `GET /api/provider/onboarding/check-code` now calls `ITenantServiceClient.CheckCodeAsync` → `GET /api/v1/tenants/check-code` (Tenant service) ✓
- [x] `POST /api/provider/onboarding/provision-tenant` now calls Tenant service → Identity membership (no retired endpoints) ✓
- [x] `GET /api/admin/tenants/check-code` (retired Identity endpoint) no longer referenced anywhere in CareConnect ✓
- [x] `POST /api/admin/tenants/self-provision` (retired Identity endpoint) no longer referenced anywhere in CareConnect ✓
- [x] Provider transitions to TENANT stage only after both `ProvisionAsync` AND `AssignTenantAsync` succeed ✓
- [x] Partial failure (Tenant success + Identity failure) → exception thrown, provider remains COMMON_PORTAL, logged with clear warning ✓
- [x] CODE_TAKEN (409 from Tenant service) → mapped to `TenantCodeUnavailable` → HTTP 409 to caller ✓
- [x] No duplicate provider record — `MarkTenantProvisioned` + `UpdateAsync` in single transaction ✓
- [x] No duplicate Identity user — `AssignTenantAsync` reuses existing `provider.IdentityUserId` ✓
- [x] Portal URL derived from `Subdomain` returned by Tenant service ✓
- [x] `dotnet build CareConnect.Api` — 0 errors ✓
- [x] `dotnet build Tenant.Api` — 0 errors ✓
- [x] `dotnet build CareConnect.Tests` — 0 errors ✓
- [x] Application startup clean — no runtime errors ✓

---

## 11. Issues / Gaps

**Phase 1 partial-failure gap:** If tenant is provisioned but Identity membership fails,
the tenant record exists in Tenant DB but CareConnect provider remains at COMMON_PORTAL.
Retry of provision will get CODE_TAKEN (409). Recovery requires Phase 2 work:
store `pendingTenantId` on provider to allow resume without re-provisioning.

**Tenant provision endpoint auth:** Changed from `AdminOnly` (JWT PlatformAdmin role)
to `AllowAnonymous + X-Provisioning-Token`. In production, configure
`TenantService:ProvisioningSecret` in both Tenant service and CareConnect so the
shared token is validated. Do NOT leave both empty in production.
