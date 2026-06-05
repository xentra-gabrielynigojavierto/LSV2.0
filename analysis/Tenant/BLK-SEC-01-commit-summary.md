# BLK-SEC-01 Commit Summary

## Commit ID

`3ddf2ae4c9e7e82ead668aa8789206e382306023`

## Commit Message

> Add secure provisioning tokens for inter-service communication
>
> Update service configurations and client logic to use explicit provisioning tokens
> for secure inter-service communication, with fail-fast checks in production.

## Author / Date

- **Author:** Agent
- **Date:** 2026-04-23 19:44:16 UTC

## Stats

| Metric | Value |
|--------|-------|
| Files changed | 11 |
| Insertions | 589 |
| Deletions | 17 |
| Net lines | +572 |

---

## Files Changed

| File | Change |
|------|--------|
| `analysis/BLK-SEC-01-report.md` | New — block report (219 lines) |
| `analysis/Tenant/BLK-CC-02-commit-summary.md` | Renamed (moved into Tenant subfolder) |
| `analysis/Tenant/BLK-CC-02-commit.diff.txt` | Renamed (moved into Tenant subfolder) |
| `apps/services/careconnect/CareConnect.Api/Program.cs` | Updated — production fail-fast guards for both `TenantService:ProvisioningToken` and `IdentityService:ProvisioningToken` |
| `apps/services/careconnect/CareConnect.Api/appsettings.Development.json` | Updated — `IdentityService` section: replaced `AuthHeaderName`/`AuthHeaderValue` with `ProvisioningToken` |
| `apps/services/careconnect/CareConnect.Api/appsettings.json` | Updated — same config change |
| `apps/services/careconnect/CareConnect.Infrastructure/Services/HttpIdentityMembershipClient.cs` | Updated — `BuildClient()` uses `ProvisioningToken` to inject `X-Provisioning-Token`; legacy fallback retained |
| `apps/services/careconnect/CareConnect.Infrastructure/Services/IdentityServiceOptions.cs` | Updated — added `ProvisioningToken` property; `AuthHeaderName`/`AuthHeaderValue` kept for backward compatibility |
| `apps/services/identity/Identity.Api/Program.cs` | Updated — production fail-fast for `TenantService:ProvisioningSecret` alongside existing NotificationsService checks |
| `apps/services/tenant/Tenant.Api/Program.cs` | Updated — production fail-fast for `TenantService:ProvisioningSecret` |
| `attached_assets/BLK-SEC-01-Tenant-Internal-API-Sec_1776972843472.txt` | Source brief (instructions) |

---

## Key Changes

### Token enforcement (Tenant Service)

`Tenant.Api/Program.cs` — startup guard added:
```
if (!IsDevelopment && string.IsNullOrWhiteSpace("TenantService:ProvisioningSecret"))
    throw InvalidOperationException(...)
```
Existing `IsAuthorized()` token guard in `ProvisionEndpoints.cs` unchanged (already enforced).

### Token enforcement (Identity Service)

`Identity.Api/Program.cs` — startup guard added alongside existing NotificationsService checks:
```
if (!IsDevelopment && string.IsNullOrWhiteSpace("TenantService:ProvisioningSecret"))
    throw InvalidOperationException(...)
```
Existing `ValidateProvisioningToken()` guards on `/assign-tenant`, `/assign-roles`, and
`/api/internal/tenant-provisioning/provision` unchanged (already enforced).

### Fail-fast startup validation

Three services now fail immediately on startup in non-Development environments
if required secrets/tokens are missing:

| Service | Config key checked |
|---|---|
| Tenant service | `TenantService:ProvisioningSecret` |
| Identity service | `TenantService:ProvisioningSecret` |
| CareConnect | `TenantService:ProvisioningToken` AND `IdentityService:ProvisioningToken` |

### CareConnect client update (ProvisioningToken)

`IdentityServiceOptions` — new `ProvisioningToken` field:
- Mirrors `TenantServiceOptions.ProvisioningToken` pattern
- Sent as `X-Provisioning-Token` header in all internal Identity calls
- Legacy `AuthHeaderName`/`AuthHeaderValue` retained as fallback

`HttpIdentityMembershipClient.BuildClient()` — updated priority:
1. Use `ProvisioningToken` (explicit, standardised)
2. Fall back to `AuthHeaderName`/`AuthHeaderValue` if `ProvisioningToken` empty

### Config updates (appsettings)

CareConnect `appsettings.json` and `appsettings.Development.json`:
- `IdentityService.AuthHeaderName` removed
- `IdentityService.AuthHeaderValue` removed
- `IdentityService.ProvisioningToken: ""` added

### Anonymous access audit

No `AllowAnonymous` decorators were removed. All reviewed:
- `GET /api/v1/tenants/check-code` — intentionally public (pre-signup UX)
- `GET /health`, `GET /info` — liveness probes
- Auth endpoints — public by definition
- Resolution/branding — public read for frontend tenant lookup

---

## Diff File

`analysis/BLK-SEC-01-commit.diff.txt` — 30,441 bytes / 749 lines
