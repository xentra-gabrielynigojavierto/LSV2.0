# BLK-SEC-02-01 Report

## 1. Summary

**Block:** Cross-Tenant Boundary Enforcement Continuation
**Status:** Complete
**Date:** 2026-04-23
**Window:** TENANT-STABILIZATION 2026-04-23 → 2026-05-07
**Continues:** BLK-SEC-02 (commit `67bdfe5`)

Full audit of all remaining CareConnect tenant boundary surfaces beyond the admin
dashboard and activation endpoints fixed in BLK-SEC-02. Three additional missing
tenant-scope guards were found and fixed. All public, Common Portal, and Tenant Portal
non-admin surfaces were confirmed already correctly scoped — no changes required there.

---

## 2. Remaining Tenant Boundary Audit

### 2.1 Public Routes

| Endpoint | Tenant source | Client input trusted? | Cross-tenant possible? |
|----------|--------------|----------------------|----------------------|
| `GET /api/public/network` | `X-Tenant-Id` header set by Next.js BFF from subdomain resolution (Tenant service `/by-code` → `/by-subdomain`) | No — BFF never reads header from user input | No — header rejected if missing/malformed |
| `GET /api/public/network/{id}/providers` | Same | No | No — network fetched by `(tenantId, id)`; wrong tenant → 404 |
| `GET /api/public/network/{id}/providers/markers` | Same | No | No |
| `GET /api/public/network/{id}/detail` | Same | No | No |
| `POST /api/public/referrals` | Same | No | No — referral created as `tenantId.Value` from header; no override in body |

**The Next.js BFF route** (`/api/public/careconnect/[...path]/route.ts`) explicitly strips any client-supplied `X-Tenant-Id` header and resolves from the request `Host` header via the Tenant service. The pattern:
1. Extract subdomain from `x-forwarded-host` / `host`
2. Resolve via `GET /tenant/api/v1/public/resolve/by-code/{subdomain}`
3. Fallback: `by-subdomain/{subdomain}` (handles code ≠ subdomain)
4. If neither resolves: returns HTTP 400

The `public-network-api.ts` used by Server Components is identical — resolves tenant from code before calling the backend with `X-Tenant-Id: <resolvedGuid>`.

**Conclusion: No code changes required for public routes.**

### 2.2 Common Portal Flows

| Endpoint | Tenant source | Client input trusted? | Note |
|----------|--------------|----------------------|------|
| `GET /api/provider/onboarding/status` | `ctx.UserId` (JWT sub claim) — cross-tenant provider lookup by identity user | N/A — no tenant used | Correct; provider record scoped by identity user ID |
| `GET /api/provider/onboarding/check-code` | None — code availability is tenant-independent | N/A | Correct; only checks if code string is free |
| `POST /api/provider/onboarding/provision-tenant` | `ctx.UserId` only — creates tenant for this user; no tenant ID input | No | Correct; provider identity drives the whole flow |

Common Portal providers carry no `tenant_id` JWT claim when acting as a COMMON_PORTAL
provider. All onboarding flows derive context exclusively from `ctx.UserId`. No tenant
input is accepted or honored. The provisioning flow creates a tenant — it does not
accept a tenant to operate within.

**Conclusion: No code changes required for Common Portal.**

### 2.3 Tenant Portal Non-Admin Routes

All tenant portal routes already use `ctx.TenantId ?? throw` as the first statement:

| Endpoint group | Scoped by | Cross-tenant possible? |
|---------------|-----------|----------------------|
| `/api/referrals` (list, detail, create, update) | `ctx.TenantId` from JWT + participant check for detail/update | No |
| `/api/referrals/{id}/history`, `/notifications`, `/audit` | `ctx.TenantId` + participant check | No |
| `/api/referrals/{id}/reassign-provider` | `ctx.TenantId` | No — PlatformOrTenantAdmin only |
| `/api/networks` (all CRUD, add/remove providers, markers, search) | `ctx.TenantId` | No |
| `/api/providers` (list, map, detail, create, update, availability) | `ctx.TenantId` | No |
| `/api/facilities` (list, create, update) | `ctx.TenantId` | No |
| `/api/referrals/{referralId}/notes` | `ctx.TenantId` | No |
| `/api/service-offerings` | `ctx.TenantId` | No |
| `/api/appointments` | `ctx.TenantId` | No |
| `/api/categories` | No tenant needed (global category list) | N/A — read-only lookup |

**Conclusion: No code changes required for Tenant Portal non-admin routes.**

### 2.4 Admin Routes — Gaps Found

Three admin endpoints were missing tenant scoping for TenantAdmin callers:

| Endpoint | Gap | Fix |
|----------|-----|-----|
| `GET /api/admin/analytics/funnel` | No tenant filter — TenantAdmin saw platform-wide funnel metrics | Added `ICurrentRequestContext ctx`; pass `scopeTenantId` to service |
| `GET /api/admin/performance` | No tenant filter — TenantAdmin saw platform-wide performance metrics | Added `ICurrentRequestContext ctx`; pass `scopeTenantId` to service |
| `POST /api/admin/providers/{id}/activate-for-careconnect` | No ownership check — TenantAdmin could activate any provider | Added scoped `GetByIdAsync(callerTenantId, id)` pre-check for TenantAdmin |

---

## 3. Removed Client Trust

### No client-supplied tenant inputs were found to remove

The audit confirmed no remaining client-supplied tenant inputs across public, Common
Portal, or Tenant Portal non-admin routes. All surfaces that could accept a `tenantId`
from the caller already either:
- Ignore it entirely (using `ctx.TenantId` from JWT exclusively), or
- Require `PlatformAdmin` role before accepting it as an optional filter (e.g. `GET /api/admin/referrals`)

The three fixed admin endpoints did not accept a client-supplied tenant ID — they simply
lacked tenant filters on DB queries. No client trust was removed; data-scope filters
were added.

---

## 4. Public Route Hardening

### Confirmed secure — no changes made

All public CareConnect routes (`/api/public/network/*`, `/api/public/referrals`) resolve
tenant context exclusively from the server-side `X-Tenant-Id` header, which is set by
the Next.js BFF after resolving the tenant from the incoming request's `Host` header
(subdomain extraction → Tenant service lookup). The header is never read from user input.

**Trust boundary for `X-Tenant-Id` on public routes:**
- The header is consumed only by CareConnect public endpoints
- It is set only by the Next.js BFF server (not by the browser)
- In the production network topology, CareConnect is not directly reachable from the
  internet — it sits behind YARP and the Next.js server
- The YARP gateway does not strip this header on public routes, relying on network
  isolation instead. This is documented as an acceptable architectural choice; adding
  an explicit `RequestHeaderRemove` YARP transform would further harden defense-in-depth
  but is not required for correctness (documented in BLK-SEC-02 Issues §11)

No changes to public routes in this block.

---

## 5. Common Portal Boundary Enforcement

### Confirmed correct — no changes made

Common Portal endpoints (`/api/provider/onboarding/*`) operate entirely on provider
identity context (`ctx.UserId` from JWT sub claim). They:
- Do not accept a `tenantId` from caller input
- Do not expose tenant portal admin data
- Do not derive tenant context from the request host

Provider users authenticated via the Common Portal carry `COMMON_PORTAL` in their role
claims. They cannot call admin endpoints (`/api/admin/*`), which require
`PlatformOrTenantAdmin`. This role boundary is enforced at the authorization policy
level, not by individual handler logic.

---

## 6. Tenant Portal Non-Admin Scoping

### Confirmed correct across all 10 endpoint groups — no additional changes made

All Tenant Portal non-admin endpoint groups already enforce `ctx.TenantId ?? throw` as
the first statement before any DB query. Detail and write endpoints additionally enforce
participant-level row checks (referrals: `ReferringOrganizationId / ReceivingOrganizationId`;
networks: scoped `GetByIdAsync(tenantId, id)` which throws `NotFoundException` cross-tenant).

Verified manually against `ReferralEndpoints.cs`, `NetworkEndpoints.cs`,
`ProviderEndpoints.cs`, `FacilityEndpoints.cs`, `ReferralNoteEndpoints.cs`,
`ServiceOfferingEndpoints.cs`, `AppointmentEndpoints.cs`.

---

## 7. Host / JWT Tenant Alignment

### Design analysis — no code changes required

**Authenticated routes (Tenant Portal / Control Center):**
The backend uses **JWT `tenant_id` claim exclusively** for all authenticated route
tenant context. The request host/subdomain is not consulted post-login. This means:
- A user logged into Tenant A visiting Tenant B's subdomain still receives Tenant A's
  data — because the JWT claim determines scope, not the host
- There is no cross-tenant data leakage: the user's JWT limits data access to Tenant A
- There is no host/JWT conflict: the backend does not read host for auth decisions

This is the **correct and safe** architecture. The JWT is the authoritative trust
anchor. The host determines which tenant's login page a user sees, but once
authenticated, the JWT claim governs all data access.

**Common Portal:**
The Common Portal is intentionally host-agnostic. Provider users log in at a shared
portal URL and carry no `tenant_id` claim. They cannot impersonate tenant-bound context
because admin endpoints require `PlatformOrTenantAdmin` role (which COMMON_PORTAL users
never carry).

**Host/subdomain mismatch handling:**
A user with a Tenant A JWT visiting Tenant B's subdomain:
- Gets their Tenant A JWT-scoped data from authenticated API calls (no leakage)
- Would see Tenant A's branding/data inside Tenant B's shell (UX issue, not security issue)
- The Next.js middleware does not check host vs JWT tenant alignment — this is intentional;
  the middleware is a cookie-gate only and defers auth decisions to Server Components

**Conclusion:** No server-side mismatch detection is required for correctness. A future
UX improvement could redirect users to their correct subdomain on login, but this is not
a security gap and is out of scope for this block.

---

## 8. Validation Results

| Check | Result |
|-------|--------|
| Public `/network` cannot be forced into another tenant via query/body | CONFIRMED — `X-Tenant-Id` resolved server-side from host; body/query has no tenant field |
| Public referral cannot create records in another tenant | CONFIRMED — `tenantId` from header only; body has no tenant override |
| Tenant portal non-admin user cannot access another tenant's referrals | CONFIRMED — all referral handlers: `ctx.TenantId ?? throw` + participant check |
| Tenant portal non-admin user cannot access another tenant's networks | CONFIRMED — all network handlers: `ctx.TenantId ?? throw`; detail throws 404 cross-tenant |
| Tenant portal non-admin user cannot access another tenant's providers | CONFIRMED — all provider handlers: `ctx.TenantId ?? throw` |
| Common Portal provider cannot access tenant portal admin data | CONFIRMED — `PlatformOrTenantAdmin` policy blocks COMMON_PORTAL users |
| Host/subdomain mismatch is handled safely | CONFIRMED — authenticated routes use JWT only; no cross-tenant leakage via host |
| Missing tenant claim on tenant-bound route fails cleanly | CONFIRMED — all handlers: `ctx.TenantId ?? throw new InvalidOperationException(...)` → 500 logged |
| `GET /api/admin/analytics/funnel` scoped for TenantAdmin | ENFORCED — `scopeTenantId` passed to `IActivationFunnelAnalyticsService.GetMetricsAsync` |
| `GET /api/admin/performance` scoped for TenantAdmin | ENFORCED — `scopeTenantId` passed to `IReferralPerformanceService.GetPerformanceAsync` |
| `POST /api/admin/providers/{id}/activate-for-careconnect` ownership check | ENFORCED — TenantAdmin: `GetByIdAsync(callerTenantId, id)` pre-check; 404 on cross-tenant |
| Admin endpoint protections from BLK-SEC-02 remain intact | CONFIRMED — no regressions; dashboard + activation + blocked-provider scoping unchanged |
| No regression in public provider/network flows | CONFIRMED — no public endpoint code touched |
| No regression in onboarding flow | CONFIRMED — no onboarding endpoint code touched |
| `dotnet build CareConnect` | PASS — 0 errors, 0 warnings |
| `dotnet build Tenant` | PASS — 0 errors, 0 warnings |
| `dotnet build Identity` | PASS — 0 errors, 0 warnings |
| TypeScript/Next.js build | No frontend files changed — no build required |

---

## 9. Changed Files

| File | Change |
|------|--------|
| `apps/services/careconnect/CareConnect.Application/Interfaces/IActivationFunnelAnalyticsService.cs` | Added `Guid? tenantId = null` parameter to `GetMetricsAsync` |
| `apps/services/careconnect/CareConnect.Application/Interfaces/IReferralPerformanceService.cs` | Added `Guid? tenantId = null` parameter to `GetPerformanceAsync` |
| `apps/services/careconnect/CareConnect.Infrastructure/Services/ActivationFunnelAnalyticsService.cs` | Added tenant-scoped base queries; `ComputeCountsAsync` now accepts and applies `tenantId` filter |
| `apps/services/careconnect/CareConnect.Infrastructure/Services/ReferralPerformanceService.cs` | Added tenant-scoped `referralsBase` applied across all three DB queries (window, accepted-history, aging) |
| `apps/services/careconnect/CareConnect.Api/Endpoints/AnalyticsEndpoints.cs` | Added `ICurrentRequestContext ctx`; derives `scopeTenantId`; passes to service |
| `apps/services/careconnect/CareConnect.Api/Endpoints/PerformanceEndpoints.cs` | Added `ICurrentRequestContext ctx`; derives `scopeTenantId`; passes to service |
| `apps/services/careconnect/CareConnect.Api/Endpoints/ProviderAdminEndpoints.cs` | Added TenantAdmin ownership pre-check in `ActivateForCareConnectAsync` |

---

## 10. Methods / Endpoints Updated

| Endpoint | Handler / Method | Change |
|----------|-----------------|--------|
| `GET /api/admin/analytics/funnel` | `AnalyticsEndpoints.MapAnalyticsEndpoints` | Added `ICurrentRequestContext ctx`; scopeTenantId → `GetMetricsAsync` |
| `IActivationFunnelAnalyticsService.GetMetricsAsync` | Interface + implementation | Added `Guid? tenantId` parameter |
| `ActivationFunnelAnalyticsService.ComputeCountsAsync` | Infrastructure service | Added `tenantId` param; base-query filter on `Referrals` and `ActivationRequests` |
| `GET /api/admin/performance` | `PerformanceEndpoints.GetPerformanceAsync` | Added `ICurrentRequestContext ctx`; scopeTenantId → `GetPerformanceAsync` |
| `IReferralPerformanceService.GetPerformanceAsync` | Interface + implementation | Added `Guid? tenantId` parameter |
| `ReferralPerformanceService.GetPerformanceAsync` | Infrastructure service | Added `tenantId` param; `referralsBase` filter applied to all 3 queries (window, new-aging) |
| `POST /api/admin/providers/{id}/activate-for-careconnect` | `ProviderAdminEndpoints.ActivateForCareConnectAsync` | Added pre-activation scoped lookup for TenantAdmin: 404 on cross-tenant |

---

## 11. GitHub Commits

- `8a0aaad34602bf45a769e9666fa49383cbf723e9` — BLK-SEC-02-01: Enforce tenant boundaries for analytics, performance, and provider activation

---

## 12. Issues / Gaps

### Residual architectural notes (not fixed in this block — out of scope or by design)

1. **YARP `X-Tenant-Id` stripping on public routes**: Not stripping the header on
   anonymous public routes means a direct caller (bypassing the BFF) could set an
   arbitrary `X-Tenant-Id`. In production, CareConnect is not directly reachable. A
   future hardening could add explicit YARP `RequestHeaderRemove: X-Tenant-Id` transforms
   on the public cluster.

2. **Host/JWT mismatch UX redirect**: Authenticated users with a Tenant A JWT visiting
   Tenant B's subdomain get Tenant A data inside Tenant B's UI shell. This is not a
   security issue (JWT is authoritative), but a UX issue. A future improvement could
   add a server-side redirect in the Next.js layout if the resolved tenant from the
   host doesn't match the JWT `tenant_id` claim.

3. **`ActivateForCareConnect` cross-tenant 404 vs 403**: TenantAdmin attempting to
   activate a provider from another tenant receives 404 (provider not found in their
   tenant), not 403. This is intentional — returning 403 would confirm the provider
   exists in another tenant. 404 is the safer and more consistent behavior with the
   platform convention used in referral detail endpoints.

4. **`GET /api/admin/analytics/funnel` — no `tenantId` in response**: The response
   does not indicate which tenant the metrics belong to. This is acceptable for both
   PlatformAdmin (platform-wide) and TenantAdmin (their tenant is known from their JWT).
   No change needed.

5. **Common Portal `check-code` endpoint**: Allows probing whether a tenant code string
   is free, but only for authenticated users (`RequireAuthorization()`). Unauthenticated
   enumeration of all tenant subdomain codes is not possible. No change needed.

---

## 13. GitHub Diff Reference

- **Commit ID:** `8a0aaad34602bf45a769e9666fa49383cbf723e9`
- **Diff file:** `analysis/BLK-SEC-02-01-commit.diff.txt`
- **Summary file:** `analysis/BLK-SEC-02-01-commit-summary.md`
- **Files changed:** 7 source files
