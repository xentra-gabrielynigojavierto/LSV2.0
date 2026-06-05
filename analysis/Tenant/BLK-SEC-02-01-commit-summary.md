# Commit Summary — BLK-SEC-02-01

## Commit ID
`8a0aaad34602bf45a769e9666fa49383cbf723e9`

## Commit Message
`[BLK-SEC-02-01] Cross-Tenant Boundary Enforcement Continuation — analytics, performance, provider activation`

## Diff File
`analysis/BLK-SEC-02-01-commit.diff.txt`

---

## Files Changed: 7

| # | File | Change |
|---|------|--------|
| 1 | `apps/services/careconnect/CareConnect.Application/Interfaces/IActivationFunnelAnalyticsService.cs` | Added `Guid? tenantId = null` to `GetMetricsAsync` |
| 2 | `apps/services/careconnect/CareConnect.Application/Interfaces/IReferralPerformanceService.cs` | Added `Guid? tenantId = null` to `GetPerformanceAsync` |
| 3 | `apps/services/careconnect/CareConnect.Infrastructure/Services/ActivationFunnelAnalyticsService.cs` | `ComputeCountsAsync` now accepts `tenantId`; applies `WHERE TenantId = @tenantId` base filters to `Referrals` and `ActivationRequests` |
| 4 | `apps/services/careconnect/CareConnect.Infrastructure/Services/ReferralPerformanceService.cs` | Adds `referralsBase` scoped to `tenantId`; applies to window query, accepted-history join, and aging distribution |
| 5 | `apps/services/careconnect/CareConnect.Api/Endpoints/AnalyticsEndpoints.cs` | Adds `ICurrentRequestContext ctx`; derives `scopeTenantId`; passes to service |
| 6 | `apps/services/careconnect/CareConnect.Api/Endpoints/PerformanceEndpoints.cs` | Adds `ICurrentRequestContext ctx`; derives `scopeTenantId`; passes to service |
| 7 | `apps/services/careconnect/CareConnect.Api/Endpoints/ProviderAdminEndpoints.cs` | `ActivateForCareConnectAsync`: TenantAdmin pre-check via `GetByIdAsync(callerTenantId, id)` |

---

## Key Changes

### Client-Trust Removal
No client-supplied tenant inputs were removed — none existed in these endpoints. The gaps were missing DB query filters, not client-trust issues.

### Public Route Hardening
No changes — confirmed already secure. `X-Tenant-Id` resolved server-side from host; no user input trusted.

### Common Portal Boundary Fixes
No changes — confirmed already correct. All onboarding flows use `ctx.UserId` exclusively; no tenant input accepted.

### Tenant Portal Non-Admin Scoping
No changes — confirmed already correct across all 10 endpoint groups. All use `ctx.TenantId ?? throw` before any DB call; detail/write endpoints add participant-level row checks.

### Host/JWT Tenant Alignment
No code changes — confirmed safe by design. Authenticated routes use JWT-only for tenant context; public routes use host-derived header. No cross-tenant leakage path exists.

### Analytics Funnel Scoping (`GET /api/admin/analytics/funnel`)
- **Before:** `GetMetricsAsync(from, to, ct)` — no tenant filter; TenantAdmin saw platform-wide counts
- **After:** `GetMetricsAsync(from, to, scopeTenantId, ct)` — `Referrals` and `ActivationRequests` filtered by tenant for TenantAdmin
- **Impact:** TenantAdmin funnel metrics now show only their tenant's activation funnel

### Performance Metrics Scoping (`GET /api/admin/performance`)
- **Before:** `GetPerformanceAsync(windowFrom, ct)` — no tenant filter; TenantAdmin saw all referrals platform-wide
- **After:** `GetPerformanceAsync(windowFrom, scopeTenantId, ct)` — referral window query, accepted-history query, and aging distribution all scoped to tenant for TenantAdmin
- **Impact:** TenantAdmin performance metrics now show only their tenant's referral data

### Provider Activation Ownership (`POST /api/admin/providers/{id}/activate-for-careconnect`)
- **Before:** No ownership check — TenantAdmin could activate any provider on the platform
- **After:** TenantAdmin path performs `GetByIdAsync(callerTenantId, id)` before activation; `NotFoundException` propagates as 404 (same as referral detail convention) if provider is not in caller's tenant
- **Impact:** TenantAdmin can only activate providers within their own tenant

---

## Build Verification

| Service | Result |
|---------|--------|
| Identity | PASS — 0 errors, 0 warnings |
| Tenant | PASS — 0 errors, 0 warnings |
| CareConnect | PASS — 0 errors, 0 warnings |

---

## Audit Surfaces Confirmed Secure (No Changes)

| Surface | Verdict |
|---------|---------|
| Public `/network` + `/referrals` — host-derived `X-Tenant-Id` | Secure |
| Next.js BFF public proxy — strips client `X-Tenant-Id`, resolves from host | Secure |
| Common Portal onboarding — uses `ctx.UserId` only | Secure |
| Tenant Portal referral endpoints — `ctx.TenantId` + participant check | Secure |
| Tenant Portal network endpoints — `ctx.TenantId` + scoped detail lookup | Secure |
| Tenant Portal provider/facility/appointment/note/offering endpoints | Secure |
| Admin referral monitor — TenantAdmin scope was already implemented in BLK-SEC-02 | Secure |
| Admin dashboard metrics — fixed in BLK-SEC-02 | Secure |
| Admin blocked providers — fixed in BLK-SEC-02 | Secure |
| Admin activation queue — fixed in BLK-SEC-02 | Secure |
| Flow service — has `TenantValidationMiddleware` rejecting cross-tenant body/query params | Secure |
