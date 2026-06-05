# BLK-GOV-02 — Commit Summary: Governance Enforcement & Least-Privilege Model

**Block:** BLK-GOV-02  
**Commit:** `91122b779a3cd567ca3a69b934ed46cc561351ff`  
**Window:** TENANT-STABILIZATION (2026-04-23 → 2026-05-07)  
**Date:** 2026-04-23  
**Projects affected:** BuildingBlocks · BuildingBlocks.Tests · CareConnect.Api

---

## What Changed

### New: `AdminTenantScope` centralized guard helper

**File:** `shared/building-blocks/BuildingBlocks/Authorization/AdminTenantScope.cs`

Introduces a reusable, testable helper that replaces fragile inline tenant-scope ternaries scattered across admin endpoint handlers. Three resolution modes:

| Mode | PlatformAdmin | TenantAdmin |
|------|--------------|-------------|
| `PlatformWide(ctx)` | `TenantId = null` (platform-wide) | `TenantId = ctx.TenantId` |
| `SingleTenant(ctx, explicitId)` | `TenantId = explicitId` (400 if missing) | `TenantId = ctx.TenantId` (ignores explicit) |
| `CheckOwnership(ctx, resourceTenantId)` | `null` (allow all) | `null` or `Forbid()` |

All modes emit structured `Warning`-level log entries on governance denials via the `ILoggerFactory` service-locator pattern from `HttpContext.RequestServices`.

### New: `AdminTenantScopeTests` unit tests

**File:** `shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/AdminTenantScopeTests.cs`

18 xunit tests covering:
- `PlatformWide` — PlatformAdmin platform-wide, PlatformAdmin with tenantId, TenantAdmin scoped, TenantAdmin missing claim throws
- `SingleTenant` — PlatformAdmin with/without explicit tenantId, TenantAdmin ignores explicit, TenantAdmin missing claim throws
- `CheckOwnership` — PlatformAdmin always allowed, TenantAdmin same-tenant allows, TenantAdmin cross-tenant forbids, TenantAdmin missing claim throws
- Standard user cannot escape tenant scope (regression guard)

All 256 tests pass (0 failures).

### Updated: CareConnect.Api endpoint files (6 files)

All 6 admin endpoint handlers migrated from inline tenant-scope patterns to `AdminTenantScope`:

| File | Mode(s) Used | Fix |
|------|-------------|-----|
| `AnalyticsEndpoints.cs` | `PlatformWide` | Replaces inline ternary |
| `PerformanceEndpoints.cs` | `PlatformWide` | Replaces inline ternary |
| `AdminDashboardEndpoints.cs` | `PlatformWide` (×3) | Replaces inline ternaries; preserves optional `?tenantId` narrow for PlatformAdmin in referral endpoint |
| `ActivationAdminEndpoints.cs` | `PlatformWide` + `CheckOwnership` (×2) | Replaces inline IsPlatformAdmin branches |
| `ProviderAdminEndpoints.cs` | `SingleTenant` (×3) + `CheckOwnership` (×1) | **Fixes PlatformAdmin 500** on link-org / get-unlinked / bulk-link (missing `?tenantId` now returns 400 instead of 500); adds `[FromQuery] Guid? targetTenantId` to all 3 single-tenant endpoints |
| `AdminBackfillEndpoints.cs` | `SingleTenant` | Replaces 6-line if/else branching |

---

## Behaviour Changes

### Breaking (intentional security fixes)

1. **`PUT /api/admin/providers/{id}/link-organization`** — PlatformAdmin callers that omit `?targetTenantId` now receive `400 Bad Request` instead of an unhandled `InvalidOperationException` (500).
2. **`GET /api/admin/providers/unlinked`** — Same fix; was previously throwing 500 for PlatformAdmin.
3. **`POST /api/admin/providers/bulk-link-organization`** — Same fix; was previously throwing 500 for PlatformAdmin.

### Non-breaking improvements

4. `GET /api/admin/analytics/funnel` — Governance denial now emits a Warning log (previously silent).
5. `GET /api/admin/performance` — Same observability improvement.
6. `GET /api/admin/dashboard`, `/providers/blocked`, `/referrals` — Same observability improvement.
7. `GET /api/admin/activations` — TenantAdmin now filtered via `scope.TenantId` (same behaviour, explicit code).
8. `GET /api/admin/activations/{id}` — Cross-tenant denial now logs `GovernanceDenial` Warning.
9. `POST /api/admin/activations/{id}/approve` — Same.

---

## Tests

```
Passed!  - Failed: 0, Passed: 256, Skipped: 0, Total: 256
```

18 new `AdminTenantScopeTests` cases in the 256 total.

---

## Files Changed

```
shared/building-blocks/BuildingBlocks/Authorization/AdminTenantScope.cs          [NEW]
shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/AdminTenantScopeTests.cs  [NEW]
apps/services/careconnect/CareConnect.Api/Endpoints/AnalyticsEndpoints.cs        [UPDATED]
apps/services/careconnect/CareConnect.Api/Endpoints/PerformanceEndpoints.cs      [UPDATED]
apps/services/careconnect/CareConnect.Api/Endpoints/AdminDashboardEndpoints.cs   [UPDATED]
apps/services/careconnect/CareConnect.Api/Endpoints/ActivationAdminEndpoints.cs  [UPDATED]
apps/services/careconnect/CareConnect.Api/Endpoints/ProviderAdminEndpoints.cs    [UPDATED]
apps/services/careconnect/CareConnect.Api/Endpoints/AdminBackfillEndpoints.cs    [UPDATED]
```

Diff: `analysis/BLK-GOV-02-commit.diff.txt` (539 lines)
