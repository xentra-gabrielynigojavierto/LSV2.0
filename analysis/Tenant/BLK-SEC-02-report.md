# BLK-SEC-02 Report

## 1. Summary

**Block:** Cross-Tenant Boundary Enforcement
**Status:** Complete
**Date:** 2026-04-23
**Window:** TENANT-STABILIZATION 2026-04-23 → 2026-05-07

Enforces strict tenant isolation across CareConnect admin endpoints. Five endpoints
that were previously unscoped (returning or accepting data from all tenants regardless
of the caller's role) have been tightened so TenantAdmin callers are confined to their
own tenant context. PlatformAdmin callers retain full platform-wide visibility.

---

## 2. Tenant Context Audit

### Where Tenant Context Originates (pre-block)

| Layer | Source | Mechanism |
|-------|--------|-----------|
| **Public routes** (`/api/public/network`, `/api/public/referrals`) | BFF server-side subdomain resolution | `X-Tenant-Id` header set by Next.js BFF after resolving `tenantCode` via Identity service |
| **Authenticated tenant routes** | JWT `tenant_id` claim | `ICurrentRequestContext.TenantId` — shared building block, all services |
| **Service-to-service** | Caller-derived tenant, forwarded via `X-Tenant-Id` header | Outbound HTTP clients in CareConnect, Liens, Flow propagate tenant from the active request |
| **Admin routes** (CareConnect) | JWT `tenant_id` claim via `ICurrentRequestContext` | Same JWT mechanism — but two dashboard handlers and three activation handlers were missing the filter |
| **Tenant resolution service** | Host / subdomain / code via query/route param | `Tenant.Application.Services.ResolutionService` — public unauthenticated resolution API only |

### What Was Corrected

Before this block, five CareConnect admin endpoints returned or accepted cross-tenant
data without scoping non-PlatformAdmin callers to their own tenant:

1. `GET /api/admin/dashboard` — aggregate metrics included all tenants' referrals and
   blocked-access logs regardless of caller role.
2. `GET /api/admin/providers/blocked` — blocked provider log returned entries from all
   tenants; TenantAdmin could inspect another tenant's blocked users.
3. `GET /api/admin/activations` — activation queue returned all pending requests
   platform-wide; TenantAdmin could see another tenant's activation backlog.
4. `GET /api/admin/activations/{id}` — detail fetch had no ownership guard; TenantAdmin
   could retrieve the detail of an activation request for a different tenant.
5. `POST /api/admin/activations/{id}/approve` — approval had no ownership guard;
   TenantAdmin could approve (and link organization IDs for) a provider belonging to
   another tenant.

---

## 3. Removed Client Trust

### Pre-existing trusted inputs (safe, no change needed)

- **`X-Tenant-Id` header on public routes**: Set server-side by the Next.js BFF after
  resolving the subdomain via the Identity branding endpoint. The Next.js BFF never
  reads this value from user input. The CareConnect service trusts it because it is an
  internal network path — the header is originated by the BFF server component, not the
  browser. This model is explicitly documented in `PublicNetworkEndpoints.cs`.
- **`?tenantId=` on `GET /api/admin/referrals`**: Accepted only from PlatformAdmin
  callers as an optional narrowing filter; TenantAdmin path ignores it and always uses
  `ctx.TenantId`. This pre-existing design is correct and was left unchanged.
- **All referral/provider/facility/network endpoints**: Already use `ctx.TenantId` from
  the JWT claim exclusively — no client-supplied tenant input trusted.

### Changes made in this block

No client-supplied tenant inputs were added or removed. The fix closes server-side
data-access gaps (missing query filters), not client-trust gaps.

---

## 4. Tenant-Scoped Query Enforcement

### Changes implemented

#### 4.1 `GET /api/admin/dashboard`
**File:** `CareConnect.Api/Endpoints/AdminDashboardEndpoints.cs`

Added `ICurrentRequestContext ctx` parameter. Derived `scopeTenantId`:
- `PlatformAdmin` → `null` (platform-wide aggregates, unchanged behavior)
- `TenantAdmin` → `ctx.TenantId` (JWT claim, throws if missing)

Applied scope to all EF queries before counting:
- `db.Referrals` filtered by `r.TenantId == scopeTenantId`
- `db.BlockedProviderAccessLogs` filtered by `l.TenantId == scopeTenantId`

#### 4.2 `GET /api/admin/providers/blocked`
**File:** `CareConnect.Api/Endpoints/AdminDashboardEndpoints.cs`

Added `ICurrentRequestContext ctx` parameter. Added `scopeTenantId` derivation (same
pattern as dashboard). Applied a pre-GroupBy filter `baseQuery.Where(l => l.TenantId == scopeTenantId)` so the grouping and paging only touch the caller's tenant data.

`BlockedProviderAccessLog.TenantId` is `Guid?` (nullable for legacy records); the EF
`== scopeTenantId` comparison correctly excludes null-tenant legacy rows when scoping
is active, which is the safe default.

#### 4.3 `GET /api/admin/activations`
**File:** `CareConnect.Api/Endpoints/ActivationAdminEndpoints.cs`

Added `ICurrentRequestContext ctx` parameter. After `service.GetPendingAsync()` returns
the full list, non-PlatformAdmin callers have the list filtered in-memory by
`i.TenantId == callerTenantId`. This required `TenantId` to be added to
`ActivationRequestSummary` (see §4.6).

#### 4.4 `GET /api/admin/activations/{id}`
**File:** `CareConnect.Api/Endpoints/ActivationAdminEndpoints.cs`

Added `ICurrentRequestContext ctx` parameter. After fetching `ActivationRequestDetail`
(which already carried `TenantId`), non-PlatformAdmin callers receive **HTTP 403** if
`detail.TenantId != callerTenantId`.

#### 4.5 `POST /api/admin/activations/{id}/approve`
**File:** `CareConnect.Api/Endpoints/ActivationAdminEndpoints.cs`

Added pre-approval ownership check for non-PlatformAdmin callers: fetches the request
detail and compares `TenantId` against `ctx.TenantId`. Returns **HTTP 403** if the
request belongs to a different tenant. Only then proceeds to `service.ApproveAsync()`.
PlatformAdmin bypasses this check (intended cross-tenant admin capability).

#### 4.6 `ActivationRequestSummary` DTO — `TenantId` added
**File:** `CareConnect.Application/DTOs/ActivationRequestDtos.cs`

Added `public Guid TenantId { get; set; }` so the endpoint-layer filter in §4.3 can
operate without a second round-trip to the database.

**File:** `CareConnect.Application/Services/ActivationRequestService.cs`

Added `TenantId = r.TenantId` to the mapping in `GetPendingAsync` so the new DTO
field is populated from the domain entity.

---

## 5. Subdomain / Host Resolution

### Public routes (`/api/public/network`, `/api/public/referrals`)

Tenant isolation relies on `X-Tenant-Id` header set by the Next.js BFF from the
request subdomain. The BFF performs:
1. Subdomain extraction from the `Host` (or `X-Forwarded-Host`) header
2. Call to `GET /api/identity/branding/resolve?code={subdomain}` to map code → GUID
3. Forward the GUID as `X-Tenant-Id` to CareConnect

The CareConnect `ResolveTenantId()` helper reads this header and rejects requests where
the header is absent or malformed with `400 Bad Request`. Network and referral queries
are scoped to the resolved `tenantId` — no cross-tenant data leakage is possible via
the public routes because all repository calls include `tenantId.Value` as a mandatory
parameter.

### Authenticated routes

Subdomain is not used post-login. Tenant context comes exclusively from the JWT
`tenant_id` claim, populated at login by Identity after verifying membership.

### Common Portal vs Tenant Portal

Common Portal (provider portal, `/provider/*`) operates on provider-level context
(`orgId`, `orgType` from JWT) and does not have access to tenant-portal admin endpoints.
The admin endpoints (`/api/admin/*`) require `PlatformOrTenantAdmin` role — providers
using the Common Portal carry neither role.

---

## 6. Public vs Common vs Tenant Portal Boundaries

| Boundary | Auth required | Tenant context source | Admin access |
|----------|--------------|----------------------|-------------|
| **Public** (`/api/public/*`) | None | `X-Tenant-Id` from BFF subdomain resolution | None |
| **Common Portal** (`/provider/*`) | `platform_session` or `portal_session` | JWT `orgId` / `orgType` | None |
| **Tenant Portal** (`/(platform)/*`) | `platform_session` with `TenantAdmin` or staff role | JWT `tenant_id` claim | Scoped to own tenant |
| **Control Center** | `platform_session` with `PlatformAdmin` | JWT `tenant_id` = platform tenant | Full platform scope |

No endpoint was found that straddles these boundaries in a dangerous way. The five
endpoints fixed in this block were not boundary straddles — they were missing tenant
filters on already-correct authorization requirements.

---

## 7. Validation Results

| Check | Result |
|-------|--------|
| `GET /api/admin/dashboard` — TenantAdmin sees only their tenant metrics | ENFORCED — scope filter applied |
| `GET /api/admin/providers/blocked` — TenantAdmin sees only their tenant | ENFORCED — pre-GroupBy filter applied |
| `GET /api/admin/activations` — TenantAdmin sees only their activation queue | ENFORCED — in-memory filter after service call |
| `GET /api/admin/activations/{id}` — TenantAdmin cannot read cross-tenant detail | ENFORCED — 403 on TenantId mismatch |
| `POST /api/admin/activations/{id}/approve` — TenantAdmin cannot approve cross-tenant | ENFORCED — pre-approval ownership check |
| Public `/network` only shows resolved tenant data | CONFIRMED — all queries use `tenantId.Value` from header |
| Public referral cannot be forced to another tenant via body | CONFIRMED — `tenantId` comes from header, body has no tenant field |
| Common Portal cannot access tenant admin endpoints | CONFIRMED — PlatformOrTenantAdmin policy blocks non-admin roles |
| PlatformAdmin retains platform-wide visibility | CONFIRMED — `ctx.IsPlatformAdmin` bypasses all scope filters |
| `dotnet build CareConnect` | PASS — 0 errors |
| `dotnet build Tenant` | PASS — 0 errors |
| `dotnet build Identity` | PASS — 0 errors |
| Onboarding flow unchanged | PASS — no onboarding code touched |
| Referral endpoints unchanged | PASS — already correctly scoped |
| Provider/Network/Facility endpoints unchanged | PASS — already correctly scoped |

---

## 8. Changed Files

| File | Change |
|------|--------|
| `apps/services/careconnect/CareConnect.Api/Endpoints/AdminDashboardEndpoints.cs` | Added tenant scope to `GetDashboardAsync` and `GetBlockedProvidersAsync` |
| `apps/services/careconnect/CareConnect.Api/Endpoints/ActivationAdminEndpoints.cs` | Added tenant ownership guards to `GetPendingAsync`, `GetByIdAsync`, `ApproveAsync` |
| `apps/services/careconnect/CareConnect.Application/DTOs/ActivationRequestDtos.cs` | Added `TenantId` property to `ActivationRequestSummary` |
| `apps/services/careconnect/CareConnect.Application/Services/ActivationRequestService.cs` | Populate `TenantId` in `GetPendingAsync` DTO mapping |

---

## 9. Methods / Endpoints Updated

| Endpoint | Handler | Change |
|----------|---------|--------|
| `GET /api/admin/dashboard` | `GetDashboardAsync` | Added `ICurrentRequestContext ctx`; referral + blocked queries scoped by tenant for TenantAdmin |
| `GET /api/admin/providers/blocked` | `GetBlockedProvidersAsync` | Added `ICurrentRequestContext ctx`; base query scoped by tenant for TenantAdmin |
| `GET /api/admin/activations` | `GetPendingAsync` | Added `ICurrentRequestContext ctx`; in-memory list filtered to caller's tenant for TenantAdmin |
| `GET /api/admin/activations/{id}` | `GetByIdAsync` | Added `ICurrentRequestContext ctx`; 403 on cross-tenant access for TenantAdmin |
| `POST /api/admin/activations/{id}/approve` | `ApproveAsync` | Added pre-approval ownership guard; 403 on cross-tenant approve for TenantAdmin |

---

## 10. GitHub Commits

*(Auto-committed by Replit checkpoint system at end of loop.)*

---

## 11. Issues / Gaps

### Residual architectural notes (not fixed in this block — out of scope)

1. **`X-Tenant-Id` header strippability**: Public routes trust `X-Tenant-Id` set by the
   BFF. If a caller can reach CareConnect directly (bypassing the BFF and YARP gateway),
   they could set an arbitrary `X-Tenant-Id`. The gateway configuration does not
   explicitly strip this header. In the production network topology this is acceptable
   (CareConnect is not directly reachable), but a future block could add an explicit
   `RequestHeaderRemove: X-Tenant-Id` YARP transform on the public route cluster to
   harden the defense-in-depth chain.

2. **`GetDashboardAsync` does not provide `tenantId` in response for PlatformAdmin**:
   PlatformAdmin currently sees aggregates without knowing which tenant they belong to
   (they are platform-wide counts). No change needed — this is intended.

3. **`ActivationRequestSummary.TenantId` now exposed in API response**: The new field
   is returned in the JSON response of `GET /api/admin/activations`. This is informational
   and harmless (TenantId is not secret), and it aligns the summary with the detail DTO.

4. **`BlockedProviderAccessLog.TenantId` is `Guid?`**: Legacy rows with null TenantId
   are excluded when a TenantAdmin scope filter is active. This is the correct behavior
   (legacy rows belong to no tenant; TenantAdmin should not see them).

---

## 12. GitHub Diff Reference

- **Diff file:** `analysis/BLK-SEC-02-commit.diff.txt`
- **Summary file:** `analysis/BLK-SEC-02-commit-summary.md`
- **Files changed:** 4 source files
