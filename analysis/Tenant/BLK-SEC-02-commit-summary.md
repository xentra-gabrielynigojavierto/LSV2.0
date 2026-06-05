# Commit Summary — BLK-SEC-02

## Commit Message
`[BLK-SEC-02] Cross-Tenant Boundary Enforcement — scope admin dashboard and activation endpoints`

## Diff File
`analysis/BLK-SEC-02-commit.diff.txt`

---

## Files Changed: 4

| # | File | Change |
|---|------|--------|
| 1 | `apps/services/careconnect/CareConnect.Api/Endpoints/AdminDashboardEndpoints.cs` | `GetDashboardAsync` + `GetBlockedProvidersAsync` — added `ICurrentRequestContext ctx`; tenant-scoped queries for TenantAdmin |
| 2 | `apps/services/careconnect/CareConnect.Api/Endpoints/ActivationAdminEndpoints.cs` | `GetPendingAsync` / `GetByIdAsync` / `ApproveAsync` — added ICurrentRequestContext ctx; tenant ownership guards for TenantAdmin |
| 3 | `apps/services/careconnect/CareConnect.Application/DTOs/ActivationRequestDtos.cs` | `ActivationRequestSummary` — added `TenantId` property |
| 4 | `apps/services/careconnect/CareConnect.Application/Services/ActivationRequestService.cs` | `GetPendingAsync` mapping — populated `TenantId` from domain entity |

---

## Key Changes

### Tenant-Context Validation
- Added `ICurrentRequestContext ctx` dependency injection to five endpoint handlers that previously did not read the caller's tenant claim.
- All non-PlatformAdmin callers are now required to have a valid `tenant_id` JWT claim; missing claim throws `InvalidOperationException` (caught by global error handling → 500, visible in logs, not silently swallowed).

### Tenant-Scoped Query Enforcement

#### Dashboard metrics (`GET /api/admin/dashboard`)
- EF queries against `db.Referrals` and `db.BlockedProviderAccessLogs` are now pre-filtered by `TenantId` for TenantAdmin callers.
- PlatformAdmin still receives platform-wide aggregates (null scope = no filter).

#### Blocked-provider log (`GET /api/admin/providers/blocked`)
- A pre-GroupBy base-query filter `WHERE TenantId = @callerTenantId` applied for TenantAdmin.
- `BlockedProviderAccessLog.TenantId` is `Guid?`; null-tenant legacy rows are excluded from TenantAdmin view (correct behavior).

#### Activation queue list (`GET /api/admin/activations`)
- `ActivationRequestSummary` now carries `TenantId` (propagated from domain entity in service mapping).
- Post-fetch in-memory filter by `TenantId` for TenantAdmin callers.

#### Activation detail (`GET /api/admin/activations/{id}`)
- `ActivationRequestDetail.TenantId` (already present) checked against caller's `TenantId`.
- HTTP 403 returned if mismatch (TenantAdmin requesting another tenant's detail).

#### Activation approve (`POST /api/admin/activations/{id}/approve`)
- Pre-approval ownership check for TenantAdmin: `GetByIdAsync()` → compare `TenantId` → 403 on mismatch.
- Approval only proceeds if the activation request belongs to the caller's tenant (or caller is PlatformAdmin).

### Host/Subdomain Hardening
- No code changes required. Public routes correctly use `X-Tenant-Id` header set server-side by the Next.js BFF from subdomain resolution. All public network/referral repository queries are parameterized by `tenantId.Value`.

### Boundary Fixes (Public / Common / Tenant Portal)
- No boundary crossings found requiring code change. All five fixed endpoints were already behind `PlatformOrTenantAdmin` auth — the gap was missing data-scope filters, not missing auth.

---

## Build Verification

| Service | Result |
|---------|--------|
| Identity | PASS — 0 errors, 0 warnings |
| Tenant | PASS — 0 errors, 0 warnings |
| CareConnect | PASS — 0 errors, 0 warnings |
