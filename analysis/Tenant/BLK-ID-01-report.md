# BLK-ID-01 Report

**Block:** Identity Tenant Decoupling (Remove Ownership)
**Status:** COMPLETE
**Date:** 2026-04-23
**Window:** TENANT-STABILIZATION 2026-04-23 → 2026-05-07

---

## 1. Summary

Removing tenant creation and tenant code-check ownership from the Identity service.
After this block, Identity does not create tenants and does not validate tenant codes —
those responsibilities now belong exclusively to the Tenant service.

The internal provisioning endpoint (`POST /api/internal/tenant-provisioning/provision`)
called by the Tenant service's `IIdentityProvisioningAdapter` is **preserved** — it handles
Identity-side user/org/role setup after the Tenant service creates the canonical record.

---

## 2. Removed Endpoints

| Endpoint | Method | Action |
|----------|--------|--------|
| `GET /api/admin/tenants/check-code` | Replaced | Returns **410 Gone** with redirect hint to Tenant service |
| `POST /api/admin/tenants/self-provision` | Replaced | Returns **410 Gone** with redirect hint to Tenant service |

The endpoints remain registered (routes still exist) so callers receive a 410 with a
clear message rather than a 404 or connection error. This prevents silent failures during
the stabilization window while the CareConnect onboarding flow migration is pending.

---

## 3. Removed Logic

- `SelfProvisionTenant` handler body: tenant creation, org creation, DNS provisioning,
  product provisioning, dual-write sync — all replaced with 410 response.
- `CheckTenantCode` handler body: code normalization, DB lookup — replaced with 410 response.
- Dead code cleanup: `SelfProvisionTenantRequest` record retained only because it is still
  referenced by the (now stub) method signature. Marked obsolete.

---

## 4. Preserved Behavior

- `POST /api/internal/tenant-provisioning/provision` — unchanged; called by Tenant service adapter.
- User login (`POST /api/auth/login`) — unchanged.
- JWT issuance — unchanged.
- `User.TenantId` assignment — unchanged (performed during internal provisioning).
- Role assignment (`TenantAdmin` scoped role) — unchanged.
- `UserOrganizationMembership` creation — unchanged.
- All JWT tenant claims — unchanged.
- `CreateTenant` (`POST /api/admin/tenants`) — retained, already marked DEPRECATED in TENANT-B12.

---

## 5. Safeguards Added

Both deprecated endpoints now return:
```
HTTP 410 Gone
{
  "error": "This endpoint has been retired.",
  "reason": "Tenant creation and code validation have moved to Tenant service.",
  "tenantServiceEndpoints": {
    "checkCode": "GET /tenant/api/v1/tenants/check-code?code={code}",
    "createTenant": "POST /tenant/api/v1/admin/tenants"
  }
}
```

This ensures any caller that has not yet migrated gets a clear, actionable error.

---

## 6. Validation Results

- [x] `GET /api/admin/tenants/check-code` — returns 410 Gone ✓
- [x] `POST /api/admin/tenants/self-provision` — returns 410 Gone ✓
- [x] `POST /api/internal/tenant-provisioning/provision` — preserved, unchanged ✓
- [x] User login flow — unchanged (AuthEndpoints, IAuthService untouched) ✓
- [x] JWT issuance — unchanged ✓
- [x] `User.TenantId` assignment — handled by internal provisioning endpoint ✓
- [x] Role assignment (TenantAdmin scoped role) — handled by internal provisioning endpoint ✓
- [x] `dotnet build Identity.Api` — 0 errors, 0 warnings ✓
- [x] Application startup — clean, no runtime errors ✓

---

## 7. Issues / Gaps

- **CareConnect dependency:** `HttpIdentityOrganizationService` in CareConnect still calls
  both retired endpoints (`check-code` and `self-provision`). These callers will now receive
  410 responses. Migration of the CareConnect onboarding flow to call Tenant service directly
  is a future block (not part of BLK-ID-01 per spec: "DO NOT: move onboarding flow yet").

- **CreateTenant not removed:** `POST /api/admin/tenants` (the original admin tenant creation
  endpoint) was already marked DEPRECATED in TENANT-B12 and is retained per stabilization
  policy. Removal is a future block.
