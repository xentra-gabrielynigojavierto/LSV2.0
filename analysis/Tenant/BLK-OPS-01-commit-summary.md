# BLK-OPS-01 Commit Summary

**Block:** BLK-OPS-01 — Runtime & Environment Hardening  
**Window:** TENANT-STABILIZATION (2026-04-23 → 2026-05-07)  
**Service scope:** Gateway, CareConnect, Tenant, Identity, Web BFF, BuildingBlocks

---

## Commit ID

`87ea2f70384aa8f771b404f1bf73ea1ae0c3207f` — "Improve runtime configuration validation for production environments"

---

## Files Changed

```
shared/building-blocks/BuildingBlocks/RuntimeConfigValidator.cs         (new)
shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/RuntimeConfigValidatorTests.cs  (new)
apps/gateway/Gateway.Api/Program.cs
apps/services/careconnect/CareConnect.Api/Program.cs
apps/services/tenant/Tenant.Api/Program.cs
apps/services/identity/Identity.Api/Program.cs
apps/web/src/lib/env-validation.ts                                      (new)
apps/web/src/instrumentation.ts                                         (new)
analysis/BLK-OPS-01-report.md                                           (new)
analysis/BLK-OPS-01-commit.diff.txt                                     (new)
analysis/BLK-OPS-01-commit-summary.md                                   (new)
```

---

## Key Changes

### 1. Shared `RuntimeConfigValidator` (BuildingBlocks)

New fluent startup validation helper with four methods:
- `RequireNonEmpty(key)` — fails on null/empty/whitespace
- `RequireNotPlaceholder(key)` — fails on known placeholder strings (`REPLACE_VIA_SECRET`, `CHANGE_ME`, etc.)
- `RequireAbsoluteUrl(key)` — fails on non-absolute or non-HTTP/HTTPS URLs
- `RequireConnectionString(key)` — `RequireNonEmpty` + `RequireNotPlaceholder` combined

17 unit tests added to `BuildingBlocks.Tests`.

### 2. Gateway Hardening

- Added: `RequireNotPlaceholder("Jwt:SigningKey")`
- Added: `RequireNonEmpty` + `RequireNotPlaceholder` for `PublicTrustBoundary:InternalRequestSecret`
- Prior state: empty trust-boundary secret was silently ignored at startup

### 3. CareConnect Hardening

- Replaced BLK-SEC-01 inline checks with `RuntimeConfigValidator`
- Added: `RequireNotPlaceholder("Jwt:SigningKey")`
- Added: `RequireNonEmpty` + `RequireNotPlaceholder` for `PublicTrustBoundary:InternalRequestSecret`
- Added: `RequireAbsoluteUrl` for `TenantService:BaseUrl` and `IdentityService:BaseUrl`
- Added: `RequireConnectionString("ConnectionStrings:CareConnectDb")`

### 4. Tenant Hardening

- Replaced BLK-SEC-01 inline check with `RuntimeConfigValidator`
- Added: `RequireNotPlaceholder("Jwt:SigningKey")`
- Added: `RequireConnectionString("ConnectionStrings:TenantDb")`

### 5. Identity Hardening

- Replaced BLK-SEC-01 inline checks with `RuntimeConfigValidator`
- Added: `RequireNotPlaceholder("Jwt:SigningKey")`
- Changed: `NotificationsService:BaseUrl` validated as absolute URL (not just non-empty)
- Added: `RequireConnectionString("ConnectionStrings:IdentityDb")`

### 6. Web BFF Validation

- `apps/web/src/lib/env-validation.ts` — validates `GATEWAY_URL` and `INTERNAL_REQUEST_SECRET` at server startup
- `apps/web/src/instrumentation.ts` — Next.js startup hook calling `validateServerEnv()`
- Validation skipped in development (`NODE_ENV=development` or `NEXT_PUBLIC_ENV=development`)
