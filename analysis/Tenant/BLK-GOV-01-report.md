# BLK-GOV-01 Report — Role & Policy Governance Audit

**Block:** BLK-GOV-01
**Window:** TENANT-STABILIZATION 2026-04-23 → 2026-05-07
**Status:** IN PROGRESS → COMPLETE

---

## 1. Summary

BLK-GOV-01 audits the role and policy model across the entire LegalSynq platform following
the security hardening work of BLK-SEC-01 / BLK-SEC-02 / BLK-SEC-02-01 / BLK-SEC-02-02 /
BLK-OBS-01.  The scope is access-control correctness: are the right principals reaching the
right endpoints, and are cross-tenant isolation checks in place everywhere they are needed?

The audit covered CareConnect, Identity, Tenant, Gateway, and shared BuildingBlocks.

Changes made in this block:

- **`AdminBackfillEndpoints.cs`** — Added `IsPlatformAdmin` branch with required `?tenantId`
  query parameter (GAP-1 fix).  Previously the handler called `ctx.TenantId ?? throw` without
  a PlatformAdmin path, producing a 500 `InvalidOperationException` when a PlatformAdmin JWT
  without a `tenant_id` claim called the endpoint.
- **`Policies.cs`** — Added REGISTRATION STATUS comments for every capability-based policy
  constant.  `CanReceiveCareConnect` and `CanManageCareConnectNetworks` were unregistered
  stubs; a developer using either with `RequireAuthorization()` would get a silent runtime
  failure.  The comments warn of this and redirect to `RequireProductRole(...)`.
- **`ProviderOnboardingEndpoints.cs`** — Added access-model rationale comment explaining why
  `RequireAuthorization()` (no product role) is intentional for COMMON_PORTAL provider
  self-onboarding (GAP-2, design expected — documentation only, no code change).

All builds confirmed clean (CareConnect 0 errors, 0 warnings) after implementation.

---

## 2. Role & Policy Model

### 2.1 System Roles

| Role constant | Value | Assigned to |
|---|---|---|
| `Roles.PlatformAdmin` | `"PlatformAdmin"` | LegalSynq platform operators |
| `Roles.TenantAdmin` | `"TenantAdmin"` | Per-tenant administrators |
| `Roles.StandardUser` | `"StandardUser"` | Regular authenticated users |

### 2.2 Core Policies (BuildingBlocks)

| Policy constant | Registered in | Semantics |
|---|---|---|
| `AuthenticatedUser` | CareConnect, all services | JWT required, any role |
| `AdminOnly` | Tenant service | `PlatformAdmin` role only |
| `PlatformOrTenantAdmin` | CareConnect | `PlatformAdmin` OR `TenantAdmin` |
| `ServiceSubmission` | Notification service | JWT or legacy X-Tenant-Id (backward-compat) |
| `CanReferCareConnect` | Flow.Api | Product claim gate for CareConnect referral workflows |
| `CanReceiveCareConnect` | **NOT YET REGISTERED** | Reserved; do not use with RequireAuthorization() |
| `CanManageCareConnectNetworks` | **NOT YET REGISTERED** | Networks use RequireProductRole() directly |
| `CanSellLien` | Flow.Api | Liens sell-side gate |
| `CanBuyLien` | Liens/Flow | Liens buy-side gate |
| `CanReferFund` | Flow.Api, Fund.Api | Fund referral gate |
| `CanFundApplications` | Fund.Api | Fund applications gate |

### 2.3 Product-Role Filter (`RequireProductRoleFilter`)

The `RequireProductRole(productCode, roles)` endpoint filter is the primary capability gate
for CareConnect operational endpoints (networks, referrals).  It:

1. Allows through any caller where `user.IsTenantAdminOrAbove()` returns `true`
   (`TenantAdmin` or `PlatformAdmin`) — logged as `source=AdminBypass`.
2. Denies callers without the declared product access — `ProductAccessDeniedException.NoProductAccess`.
3. Denies callers with product access but wrong product role — `ProductAccessDeniedException.InsufficientProductRole`.
4. Emits structured log entries (`AuthzDecision: result=ALLOW|DENY`) for every decision.

This means TenantAdmin and PlatformAdmin can bypass product-role checks and access any
tenant-scoped operation within their permitted scope.  This is by design.

### 2.4 ICurrentRequestContext

| Member | Type | Source |
|---|---|---|
| `UserId` | `Guid?` | `sub` JWT claim |
| `TenantId` | `Guid?` | `tenant_id` JWT claim |
| `IsPlatformAdmin` | `bool` | `PlatformAdmin` role in JWT |
| `Roles` | `IReadOnlyList<string>` | `role` claims |
| `ProductRoles` | `IReadOnlyList<ProductRole>` | `product_roles` JWT claim |

`TenantId` may be `null` for PlatformAdmin JWTs issued without a tenant context.
Every `PlatformOrTenantAdmin` handler must account for this.

---

## 3. Endpoint-by-Endpoint Audit

### 3.1 CareConnect — Admin Endpoints (`/api/admin/*`)

| Endpoint | Policy | Inline scope guard | Status |
|---|---|---|---|
| `GET /api/admin/integrity` | `Roles.PlatformAdmin` (inline) | Platform-only, no scoping needed | PASS ✓ (BLK-SEC-01) |
| `GET /api/admin/activations` | `PlatformOrTenantAdmin` | `!IsPlatformAdmin → filter to ctx.TenantId` | PASS ✓ (BLK-SEC-02) |
| `GET /api/admin/activations/{id}` | `PlatformOrTenantAdmin` | `!IsPlatformAdmin → Forbid if wrong tenant` | PASS ✓ (BLK-SEC-02) |
| `POST /api/admin/activations/{id}/approve` | `PlatformOrTenantAdmin` | `!IsPlatformAdmin → verify tenant before approve` | PASS ✓ (BLK-SEC-02) |
| `GET /api/admin/analytics/funnel` | `PlatformOrTenantAdmin` | `IsPlatformAdmin ? null : tenantId ?? throw` | PASS ✓ (BLK-SEC-02-01) |
| `GET /api/admin/performance` | `PlatformOrTenantAdmin` | `IsPlatformAdmin ? null : tenantId ?? throw` | PASS ✓ (BLK-SEC-02-01) |
| `GET /api/admin/dashboard/summary` | `PlatformOrTenantAdmin` | `IsPlatformAdmin ? null : tenantId ?? throw` | PASS ✓ (BLK-SEC-02-01) |
| `GET /api/admin/dashboard/blocked-logins` | `PlatformOrTenantAdmin` | `IsPlatformAdmin ? null : tenantId ?? throw` | PASS ✓ (BLK-SEC-02-01) |
| `GET /api/admin/dashboard/referrals` | `PlatformOrTenantAdmin` | `IsPlatformAdmin ? (opt tenantId filter) : force tenantId` | PASS ✓ (BLK-SEC-02-01) |
| `PUT /api/admin/providers/{id}/link-organization` | `PlatformOrTenantAdmin` | provider ownership check | PASS ✓ (BLK-SEC-02-02) |
| `POST /api/admin/providers/bulk-link-organization` | `PlatformOrTenantAdmin` | batch ownership check | PASS ✓ (BLK-SEC-02-02) |
| `POST /api/admin/providers/{id}/activate-for-careconnect` | `PlatformOrTenantAdmin` | activation ownership check | PASS ✓ (BLK-SEC-02-02) |
| `POST /api/admin/appointments/backfill-org-ids` | `PlatformOrTenantAdmin` | **FIXED BLK-GOV-01** — `IsPlatformAdmin → ?tenantId required; else ctx.TenantId` | FIXED ✓ |

### 3.2 CareConnect — Operational Endpoints

| Endpoint group | Policy | Filter | Status |
|---|---|---|---|
| `GET/POST/PUT/DELETE /api/networks/*` | `AuthenticatedUser` (group) + `RequireProductRole(NetworkManager)` per-route | TenantAdminOrAbove bypass | PASS ✓ |
| `GET/POST /api/referrals` | `AuthenticatedUser` | `tenantId = ctx.TenantId ?? throw` | PASS ✓ |
| `GET /api/referrals/{id}` | `AuthenticatedUser` | `tenantId` binding + provider-org bypass for cross-tenant referrals | PASS ✓ |
| `POST /api/referrals/{id}/reassign-provider` | `PlatformOrTenantAdmin` | `tenantId = ctx.TenantId ?? throw` | PASS ✓ |

### 3.3 CareConnect — Provider Onboarding (`/api/provider/onboarding/*`)

| Endpoint | Policy | Access model | Status |
|---|---|---|---|
| `GET /api/provider/onboarding/status` | `RequireAuthorization()` | Service-layer ownership: `identityUserId → provider lookup` | PASS — DESIGN ✓ |
| `GET /api/provider/onboarding/check-code` | `RequireAuthorization()` | Prevents anonymous probing of subdomain namespace | PASS — DESIGN ✓ |
| `POST /api/provider/onboarding/provision-tenant` | `RequireAuthorization()` | Service: `identityUserId` + `AccessStage == COMMON_PORTAL` gate | PASS — DESIGN ✓ |

COMMON_PORTAL providers hold a regular JWT with no product role.  Adding a product-role gate
would break the onboarding flow.  Service-layer ownership checks are the correct control.

### 3.4 Identity Service — Admin Endpoints (`/api/admin/*`)

| Category | Auth mechanism | Inline guard | Status |
|---|---|---|---|
| User lifecycle (deactivate/activate/invite/lock/unlock) | `RequirePermission("TENANT.users:manage")` | `IsCrossTenantAccess` check | PASS ✓ |
| Role assignment / revocation | `RequirePermission("TENANT.roles:assign")` | `IsCrossTenantAccess` check | PASS ✓ |
| Membership management | `RequirePermission("TENANT.users:manage")` | `IsCrossTenantAccess` check | PASS ✓ |
| Authorization simulation | Role check inline (`IsInRole`) | TenantAdmin scoped to own tenant | PASS ✓ |
| Read-only admin lists (tenants, roles, products) | Gateway `AdminOnly` (PlatformAdmin) | Platform-wide reads | PASS ✓ |

### 3.5 Identity Service — Internal Provisioning

| Endpoint | Auth mechanism | Status |
|---|---|---|
| `POST /api/internal/users/assign-tenant` | `X-Provisioning-Token` | PASS ✓ |
| `POST /api/internal/users/assign-roles` | `X-Provisioning-Token` | PASS ✓ |
| `POST /api/internal/tenant-provisioning/provision` | `X-Provisioning-Token` | PASS ✓ |
| `POST /api/v1/tenants/provision` | `PlatformAdmin` OR `X-Provisioning-Token` | PASS ✓ |

### 3.6 Tenant Service — Admin Endpoints (`/api/v1/admin/*`)

| Endpoint | Policy | Status |
|---|---|---|
| `POST /api/v1/admin/tenants` | `AdminOnly` (PlatformAdmin) | PASS ✓ |
| `GET /api/v1/admin/tenants` | `AdminOnly` (PlatformAdmin) | PASS ✓ |
| `PATCH /api/v1/admin/tenants/{id}/status` | `AdminOnly` (PlatformAdmin) | PASS ✓ |
| `POST /api/v1/admin/tenants/{id}/entitlements/{code}` | `AdminOnly` (PlatformAdmin) | PASS ✓ |
| `PATCH /api/v1/admin/tenants/{id}/logo` | `AdminOnly` (PlatformAdmin) | PASS ✓ |

### 3.7 Gateway Routes

| Route | AuthorizationPolicy | Status |
|---|---|---|
| `/careconnect/health` | `Anonymous` | PASS ✓ |
| `/careconnect/info` | `Anonymous` | PASS ✓ |
| `/careconnect/internal/**` | `Deny` | PASS ✓ (blocks internal bypass) |
| `/careconnect/api/public/**` | `Anonymous` | PASS ✓ (trust-boundary in service) |
| `/careconnect/**` (catch-all) | Default (RequireAuthorization) | PASS ✓ |

---

## 4. Gaps Found and Resolved

### GAP-1 — `AdminBackfillEndpoints`: PlatformAdmin path missing (FIXED)

**File:** `apps/services/careconnect/CareConnect.Api/Endpoints/AdminBackfillEndpoints.cs`

**Before:**
```csharp
var tenantId = ctx.TenantId
    ?? throw new InvalidOperationException("tenant_id claim is missing.");
```
A PlatformAdmin JWT without a `tenant_id` claim reached the `??throw` branch and produced an
unhandled `InvalidOperationException`, causing a 500 response.  There was no PlatformAdmin
path even though the endpoint was declared `PlatformOrTenantAdmin`.

**After (BLK-GOV-01 fix):**
```csharp
Guid scopeTenantId;
if (ctx.IsPlatformAdmin)
{
    if (!tenantId.HasValue)
        return Results.BadRequest(new
        {
            error = "PlatformAdmin must supply ?tenantId=<guid>. " +
                    "This endpoint operates on a single tenant at a time.",
        });
    scopeTenantId = tenantId.Value;
}
else
{
    scopeTenantId = ctx.TenantId
        ?? throw new InvalidOperationException("tenant_id claim is missing.");
}
```
This aligns with the `AdminDashboardEndpoints.GetReferralsAsync` pattern where PlatformAdmin
can supply an optional `?tenantId=` filter, but here it is **required** because backfill must
operate on exactly one tenant at a time (no meaningful platform-wide default exists).

**Severity before fix:** Medium — admin 500 with no cross-tenant data exposure.

---

### GAP-2 — `ProviderOnboardingEndpoints`: access model undocumented (DOCUMENTED)

**File:** `apps/services/careconnect/CareConnect.Api/Endpoints/ProviderOnboardingEndpoints.cs`

The three provider onboarding endpoints use generic `RequireAuthorization()`.  No code change
was needed — the service-layer ownership check (`identityUserId → provider + AccessStage`) is
the correct gate for COMMON_PORTAL providers, who by definition do not yet have a product role.
A governance comment was added to the file header explaining the design decision so future
developers do not add a product-role requirement that would break the onboarding flow.

**Severity:** Low (design correct; documentation only).

---

### GAP-3 — `Policies.cs`: unregistered policy constants (ANNOTATED)

**File:** `shared/building-blocks/BuildingBlocks/Authorization/Policies.cs`

`CanReceiveCareConnect` and `CanManageCareConnectNetworks` were defined but never registered
in any service's `AddAuthorization` call and never referenced by any endpoint.  Using either
with `RequireAuthorization(policyName)` would produce a runtime "policy not found" exception.

Registration-status comments were added to all capability-based policy constants so developers
know which are live and which are stubs.

**Severity:** Low (no active misuse; risk is future misuse by new developers).

---

## 5. Patterns Confirmed Consistent

The following platform-wide authorization patterns are in use consistently across all
audited services:

| Pattern | Where used | Standard implementation |
|---|---|---|
| `PlatformOrTenantAdmin` + inline scope | CareConnect admin endpoints | `IsPlatformAdmin ? null : ctx.TenantId ?? throw` |
| `PlatformAdmin` inline (no policy) | CareConnect integrity | `policy.RequireRole(Roles.PlatformAdmin)` |
| `RequirePermission` + `IsCrossTenantAccess` | Identity admin | Permission code + tenant boundary check |
| `RequireProductRole` + `TenantAdminOrAbove` bypass | CareConnect networks | `RequireProductRoleFilter` in BuildingBlocks |
| `X-Provisioning-Token` | Internal M2M provisioning | Header validation, fail-fast if empty in prod |
| `AdminOnly` | Tenant service admin | `PlatformAdmin` role only |

---

## 6. Commit Summary

**Commit:** `744b5b3635b7c482a539a70a7a4247ab9a66a2ba`
**Message:** `BLK-GOV-01: Role & Policy Governance Audit`

- `AdminBackfillEndpoints`: add PlatformAdmin path with required `?tenantId` query param —
  previously `ctx.TenantId ?? throw` would 500 for PlatformAdmin JWTs without a `tenant_id`
  claim (GAP-1 fix)
- `ProviderOnboardingEndpoints`: add access-model comment explaining why `RequireAuthorization()`
  without a product role is correct for COMMON_PORTAL provider self-onboarding (GAP-2 doc)
- `Policies.cs`: add registration-status comments for all capability-based policy constants;
  mark `CanReceiveCareConnect` and `CanManageCareConnectNetworks` as NOT YET REGISTERED stubs
  that must not be used with `RequireAuthorization()` (GAP-3 annotation)
- `analysis/BLK-GOV-01-report.md`: full audit report covering 12 endpoint categories across
  CareConnect, Identity, Tenant, and Gateway

**Build:** CareConnect 0 errors · 0 warnings

---

## 7. Changed Files

| File | Change | Block |
|---|---|---|
| `apps/services/careconnect/CareConnect.Api/Endpoints/AdminBackfillEndpoints.cs` | Add PlatformAdmin path with `?tenantId` requirement | BLK-GOV-01 GAP-1 fix |
| `apps/services/careconnect/CareConnect.Api/Endpoints/ProviderOnboardingEndpoints.cs` | Add governance access-model comment | BLK-GOV-01 GAP-2 doc |
| `shared/building-blocks/BuildingBlocks/Authorization/Policies.cs` | Add registration-status comments for capability policy stubs | BLK-GOV-01 GAP-3 doc |
