# BLK-SEC-01-FIX Commit Summary

**Commit message:** `[BLK-SEC-01-FIX] Resolve build errors + secure integrity endpoint`
**Parent commit:** `3ddf2ae4c9e7e82ead668aa8789206e382306023` (BLK-SEC-01)
**Date:** 2026-04-23
**Diff file:** `analysis/Tenant/BLK-SEC-01-FIX-commit.diff.txt`

---

## Files Changed: 3

| # | File | Change type | Description |
|---|------|-------------|-------------|
| 1 | `apps/services/tenant/Tenant.Infrastructure/Data/Configurations/TenantConfiguration.cs` | Fix | Added `using Tenant.Domain;` — resolves CS0246 on `TenantProvisioningStatus` |
| 2 | `apps/services/careconnect/CareConnect.Api/Endpoints/CareConnectIntegrityEndpoints.cs` | Security fix | Removed `AllowAnonymous`, added `RequireAuthorization(Policies.PlatformOrTenantAdmin)` and `using BuildingBlocks.Authorization;` |
| 3 | `analysis/Tenant/BLK-SEC-01-report.md` | Documentation | Appended §12 BLK-SEC-01-FIX Corrections |

---

## Fixes Applied

### Fix 1 — Tenant build error (CS0246: TenantProvisioningStatus not found)

- **File:** `Tenant.Infrastructure/Data/Configurations/TenantConfiguration.cs`
- **Change:** Added `using Tenant.Domain;`
- **Why:** `TenantConfiguration.cs` called `HasDefaultValue(TenantProvisioningStatus.Unknown)`
  but had no import for the `Tenant.Domain` namespace where the enum lives.
  EF Core resolves default-value literals at compile time, making this a build blocker.

### Fix 2 — Identity build error (ITenantSyncAdapter not found)

- **File:** No source change required.
- **Why:** The error appeared only when the project was built with `--no-dependencies`,
  skipping compilation of `Identity.Infrastructure` (which contains `ITenantSyncAdapter`
  in `Identity.Infrastructure.Services`). Both the `using` directive and the
  `<ProjectReference>` were already correctly in place. The full project build
  (`dotnet build Identity.Api/Identity.Api.csproj`) succeeds without modification.

### Fix 3 — CareConnect `/api/admin/integrity` was AllowAnonymous

- **File:** `CareConnect.Api/Endpoints/CareConnectIntegrityEndpoints.cs`
- **Before:** `routes.MapGet("/api/admin/integrity", GetIntegrityReport).AllowAnonymous();`
- **After:**
  ```csharp
  routes.MapGet("/api/admin/integrity", GetIntegrityReport)
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);
  ```
- **Auth policy:** `PlatformOrTenantAdmin` (role: `PlatformAdmin` or `TenantAdmin`)
  already registered in CareConnect `Program.cs`.
- **Import added:** `using BuildingBlocks.Authorization;`
- **Unauthorized behavior:** HTTP 401 — ASP.NET Core rejects unauthenticated requests
  before the handler is invoked. No environment bypass exists.

---

## Build Verification

| Service | Build result |
|---------|-------------|
| Identity | PASS — zero errors, zero warnings |
| Tenant | PASS — zero errors, zero warnings |
| CareConnect | PASS — zero errors, zero warnings |

Command used: `dotnet build <Project>.csproj --no-restore -c Release --verbosity quiet`

---

## No Regressions

- No business logic modified.
- No onboarding flow code touched.
- No provisioning flow code touched.
- Only files: one `using` directive, one `.AllowAnonymous()` → `.RequireAuthorization()`,
  and the report appendix.
