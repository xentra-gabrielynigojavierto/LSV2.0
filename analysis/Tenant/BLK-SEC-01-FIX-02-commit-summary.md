# BLK-SEC-01-FIX-02 Commit Summary

**Commit message:** `[BLK-SEC-01-FIX-02] Fix build errors + secure admin integrity endpoint`
**Parent commit:** `33a316685a988cf00151da86b060cdd8e0f04998` (BLK-SEC-01-FIX)
**Date:** 2026-04-23
**Diff file:** `analysis/Tenant/BLK-SEC-01-FIX-02-commit.diff.txt`

---

## Files Changed: 2

| # | File | Change type | Description |
|---|------|-------------|-------------|
| 1 | `apps/services/careconnect/CareConnect.Api/Endpoints/CareConnectIntegrityEndpoints.cs` | Security fix | Replaced named-policy `RequireAuthorization(Policies.PlatformOrTenantAdmin)` with inline `RequireAuthorization(policy => policy.RequireRole(Roles.PlatformAdmin))` for proper 401/403 split |
| 2 | `analysis/Tenant/BLK-SEC-01-report.md` | Documentation | Appended §13 BLK-SEC-01-FIX-02 Corrections |

---

## Fixes Applied

### Fix 1 — Identity build error (`ITenantSyncAdapter` not found)

- **Resolution:** No source change required.
- **Root cause:** The error was produced only when building with `--no-dependencies`,
  which bypasses compilation of `Identity.Infrastructure` (where `ITenantSyncAdapter`
  lives in `Identity.Infrastructure.Services`). Both the `using` directive
  (`using Identity.Infrastructure.Services;` at `AdminEndpoints.cs` line 9) and the
  `<ProjectReference>` to `Identity.Infrastructure` are already correctly in place.
  DI registration (NoOp + Http adapter paths) is present in
  `Identity.Infrastructure/DependencyInjection.cs`.
- **Build result:** `dotnet build Identity.Api/Identity.Api.csproj --no-restore -c Release`
  → **0 errors, 0 warnings**

### Fix 2 — Tenant build error (`TenantProvisioningStatus` not found)

- **Resolution (applied in BLK-SEC-01-FIX):** Added `using Tenant.Domain;` to
  `Tenant.Infrastructure/Data/Configurations/TenantConfiguration.cs`.
- **Root cause:** `HasDefaultValue(TenantProvisioningStatus.Unknown)` resolved the enum at
  compile time. Without `using Tenant.Domain;`, CS0246 was raised even though the runtime
  projection was valid.
- **Build result:** `dotnet build Tenant.Api/Tenant.Api.csproj --no-restore -c Release`
  → **0 errors, 0 warnings**

### Fix 3 — `GET /api/admin/integrity` inline role auth (upgrade from BLK-SEC-01-FIX)

- **Before (BLK-SEC-01-FIX):**
  ```csharp
  routes.MapGet("/api/admin/integrity", GetIntegrityReport)
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);
  ```
- **After (BLK-SEC-01-FIX-02):**
  ```csharp
  routes.MapGet("/api/admin/integrity", GetIntegrityReport)
        .RequireAuthorization(policy => policy.RequireRole(Roles.PlatformAdmin));
  ```
- **Why:** The inline `RequireRole` builder produces the correct HTTP status split:
  - No token → JWT bearer middleware → **401 Unauthorized**
  - Valid token, wrong role → `ForbidAsync` → **403 Forbidden**
  - Valid token, `PlatformAdmin` role → **200 OK** + integrity payload
- **`Roles.PlatformAdmin`** = `"PlatformAdmin"` constant from `BuildingBlocks.Authorization.Roles`
- **`using BuildingBlocks.Authorization;`** retained (needed for both `Roles` and the previously
  used `Policies` — compiler confirms no unused-using warning since `Roles` is referenced)

---

## Build Verification

| Service | Command | Result |
|---------|---------|--------|
| Identity | `dotnet build Identity.Api/Identity.Api.csproj --no-restore -c Release --verbosity quiet` | PASS — 0 errors |
| Tenant | `dotnet build Tenant.Api/Tenant.Api.csproj --no-restore -c Release --verbosity quiet` | PASS — 0 errors |
| CareConnect | `dotnet build CareConnect.Api/CareConnect.Api.csproj --no-restore -c Release --verbosity quiet` | PASS — 0 errors |

## Authorization Behavior

| Request | Auth state | HTTP response |
|---------|-----------|---------------|
| No token | Unauthenticated | **401** |
| Valid token, `TenantAdmin` role only | Authenticated, wrong role | **403** |
| Valid token, `PlatformAdmin` role | Authenticated, correct role | **200** |

---

## No Regressions

- No business logic modified across any service.
- No onboarding flow code touched.
- No provisioning flow code touched.
- Only changed: one `RequireAuthorization()` call (integrity endpoint) + report appendix.
