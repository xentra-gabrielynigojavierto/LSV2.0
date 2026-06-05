# BLK-OPS-01 Report — Runtime & Environment Hardening

**Block:** BLK-OPS-01  
**Window:** TENANT-STABILIZATION (2026-04-23 → 2026-05-07)  
**Preceded by:** BLK-PERF-01 (commit `fee961e5fef3c3e0b75a98adb15bd4c7416c22ae`)  
**Status:** COMPLETE

---

## 1. Summary

BLK-OPS-01 hardened the runtime configuration across all primary services so that the platform fails fast with clear, actionable error messages when deployed with missing, empty, or placeholder secrets.

| Area | Prior State | After BLK-OPS-01 |
|---|---|---|
| Gateway trust-boundary secret | Silent: empty secret → no header injected, trust boundary silently broken | Fail fast: non-empty + not placeholder required in non-dev |
| Gateway JWT signing key | Non-null only (no placeholder check) | Non-null + not placeholder in non-dev |
| CareConnect trust-boundary secret | Not validated at startup | Fail fast: non-empty + not placeholder required in non-dev |
| CareConnect service URLs | Empty string accepted silently | Fail fast: absolute URL required in non-dev |
| CareConnect connection string | Placeholder accepted silently | Fail fast: not placeholder in non-dev |
| Tenant JWT signing key | Non-null only | Non-null + not placeholder in non-dev |
| Tenant connection string | Not validated | Fail fast: not placeholder in non-dev |
| Identity JWT signing key | Non-null only | Non-null + not placeholder in non-dev |
| Identity connection string | Not validated | Fail fast: not placeholder in non-dev |
| Web BFF env vars | Never validated | Server-side validation at startup via `instrumentation.ts` |
| Shared validation helper | None — every service had its own inline pattern | `RuntimeConfigValidator` added to BuildingBlocks |

**Test gate:** 29 BuildingBlocks tests passed (17 new `RuntimeConfigValidatorTests`, 12 pre-existing). Zero regressions. All four .NET services build with 0 errors. TypeScript compiles clean.

---

## 2. Runtime Config Audit

### 2.1 Gateway (`apps/gateway/Gateway.Api`)

| Key | Required in Production | Dev Default | Base Default | Validation before BLK-OPS-01 |
|---|---|---|---|---|
| `Jwt:SigningKey` | Yes | `dev-only-signing-key-minimum-32-chars-long!` | `REPLACE_VIA_SECRET_minimum_32_characters_long` | Non-null (threw on null) — no placeholder check |
| `Jwt:Issuer` | Yes | `legalsynq-identity` | `legalsynq-identity` | None |
| `Jwt:Audience` | Yes | `legalsynq-platform` | `legalsynq-platform` | None |
| `PublicTrustBoundary:InternalRequestSecret` | Yes | `dev-internal-request-secret-minimum-32-chars!!` | `REPLACE_VIA_SECRET` | **None** — silent skip if empty |
| YARP `ReverseProxy:*` | Yes (loaded from config) | (inline routes) | (inline routes) | None |

**Key gap:** `PublicTrustBoundary:InternalRequestSecret` — the trust-boundary pipeline middleware checked `!string.IsNullOrWhiteSpace(secret)` and skipped header injection rather than failing. In production this would silently break the public network directory without any startup error.

### 2.2 CareConnect (`apps/services/careconnect/CareConnect.Api`)

| Key | Required in Production | Dev Default | Base Default | Validation before BLK-OPS-01 |
|---|---|---|---|---|
| `Jwt:SigningKey` | Yes | `dev-only-signing-key-minimum-32-chars-long!` | `REPLACE_VIA_SECRET_minimum_32_characters_long` | Non-null — no placeholder check |
| `PublicTrustBoundary:InternalRequestSecret` | Yes | `dev-internal-request-secret-minimum-32-chars!!` | `REPLACE_VIA_SECRET` | **None** |
| `TenantService:BaseUrl` | Yes | `http://localhost:5005` | `""` (empty) | Validated non-empty via BLK-SEC-01 inline check |
| `TenantService:ProvisioningToken` | Yes | `""` (empty; dev-bypass) | `""` (empty) | Validated non-empty (BLK-SEC-01) — no URL format check |
| `IdentityService:BaseUrl` | Yes | `http://localhost:5001` | `""` (empty) | Validated non-empty via BLK-SEC-01 inline check |
| `IdentityService:ProvisioningToken` | Yes | `""` (empty; dev-bypass) | `""` (empty) | Validated non-empty (BLK-SEC-01) |
| `ConnectionStrings:CareConnectDb` | Yes | (overridden via env) | `…password=REPLACE_VIA_SECRET` | **None** — placeholder accepted |
| `AuditClient:BaseUrl` | Soft-required | `http://localhost:5007` | `http://localhost:5007` | None |

### 2.3 Tenant (`apps/services/tenant/Tenant.Api`)

| Key | Required in Production | Dev Default | Base Default | Validation before BLK-OPS-01 |
|---|---|---|---|---|
| `Jwt:SigningKey` | Yes | `dev-only-signing-key-minimum-32-chars-long!` | `REPLACE_VIA_SECRET_minimum_32_characters_long` | Non-null — no placeholder check |
| `TenantService:ProvisioningSecret` | Yes | `""` (empty; dev-bypass) | `""` (empty) | Validated non-empty (BLK-SEC-01) |
| `ConnectionStrings:TenantDb` | Yes | (see appsettings.Development.json) | `…password=REPLACE_VIA_SECRET` | **None** — placeholder accepted |
| `IdentityService:ProvisioningSecret` | Yes | `""` | `""` | **None** |

### 2.4 Identity (`apps/services/identity/Identity.Api`)

| Key | Required in Production | Dev Default | Base Default | Validation before BLK-OPS-01 |
|---|---|---|---|---|
| `Jwt:SigningKey` | Yes | `dev-only-signing-key-minimum-32-chars-long!` | `REPLACE_VIA_SECRET_minimum_32_characters_long` | Non-null — no placeholder check |
| `TenantService:ProvisioningSecret` | Yes | `""` (empty; dev-bypass) | `""` (empty) | Validated non-empty (BLK-SEC-01) |
| `NotificationsService:BaseUrl` | Yes | `""` | `""` | Validated non-empty (BLK-SEC-01) — no URL format check |
| `NotificationsService:PortalBaseUrl` | Yes | `http://localhost:3050` | `""` | Validated non-empty (BLK-SEC-01) |
| `ConnectionStrings:IdentityDb` | Yes | (overridden via env) | `…password=REPLACE_VIA_SECRET` | **None** — placeholder accepted |

### 2.5 Web / BFF (`apps/web`)

| Variable | Required in Production | Dev Default (`.env.local`) | Validation before BLK-OPS-01 |
|---|---|---|---|
| `GATEWAY_URL` | Yes | `http://127.0.0.1:5010` | `?? 'http://127.0.0.1:5000'` fallback in `server-api-client.ts` — no error if unset |
| `INTERNAL_REQUEST_SECRET` | Yes | `dev-internal-request-secret-minimum-32-chars!!` | **None** — no validation |
| `NEXT_PUBLIC_ENV` | Dev-only | `development` | None |
| `NEXT_PUBLIC_TENANT_CODE` | Dev-only | `LEGALSYNQ` | None |

---

## 3. Config Validation Implementation

### 3.1 `RuntimeConfigValidator` — New BuildingBlocks Helper

**File:** `shared/building-blocks/BuildingBlocks/RuntimeConfigValidator.cs`

A minimal, fluent startup validation helper with four methods:

```csharp
var v = new RuntimeConfigValidator(builder.Configuration, "service-name");
v.RequireNonEmpty("Jwt:SigningKey")
 .RequireNotPlaceholder("Jwt:SigningKey")
 .RequireAbsoluteUrl("TenantService:BaseUrl")
 .RequireConnectionString("ConnectionStrings:MyDb");
```

| Method | Behaviour |
|---|---|
| `RequireNonEmpty(key)` | Throws if value is null, empty, or whitespace |
| `RequireNotPlaceholder(key)` | Throws if value contains any of the known placeholder strings (case-insensitive, substring match) |
| `RequireAbsoluteUrl(key)` | Throws if value is empty or not a valid absolute HTTP/HTTPS URL |
| `RequireConnectionString(key)` | `RequireNonEmpty` + `RequireNotPlaceholder` combined |

Known placeholders: `REPLACE_VIA_SECRET`, `CHANGE_ME`, `YOUR_SECRET_HERE`, `INSERT_SECRET_HERE`, `TODO`, `FIXME`.

All errors include: the service name, the config key, the reason, and the ASP.NET Core double-underscore environment variable convention (`Jwt:SigningKey` → `Jwt__SigningKey`).

Returns `this` for fluent chaining. All methods are idempotent and throw on first failure.

### 3.2 Pattern Consistency

All pre-existing BLK-SEC-01 inline checks in CareConnect, Tenant, and Identity were replaced with equivalent `RuntimeConfigValidator` calls. Semantic behaviour is identical — the same conditions cause startup failure. The refactor standardises error message format and placeholder detection.

---

## 4. Production Secret Fail-Fast Rules

### 4.1 Gateway

```csharp
if (!builder.Environment.IsDevelopment())
{
    var v = new RuntimeConfigValidator(builder.Configuration, "gateway");
    v.RequireNotPlaceholder("Jwt:SigningKey")
     .RequireNonEmpty("PublicTrustBoundary:InternalRequestSecret")
     .RequireNotPlaceholder("PublicTrustBoundary:InternalRequestSecret");
}
```

`Jwt:SigningKey` non-null check (`?? throw`) remains as the first gate (catches null before the validator runs).

### 4.2 CareConnect

```csharp
if (!builder.Environment.IsDevelopment())
{
    var v = new RuntimeConfigValidator(builder.Configuration, "careconnect");
    v.RequireNotPlaceholder("Jwt:SigningKey")
     .RequireNonEmpty("PublicTrustBoundary:InternalRequestSecret")
     .RequireNotPlaceholder("PublicTrustBoundary:InternalRequestSecret")
     .RequireAbsoluteUrl("TenantService:BaseUrl")
     .RequireAbsoluteUrl("IdentityService:BaseUrl")
     .RequireNonEmpty("TenantService:ProvisioningToken")
     .RequireNonEmpty("IdentityService:ProvisioningToken")
     .RequireConnectionString("ConnectionStrings:CareConnectDb");
}
```

### 4.3 Tenant

```csharp
if (!builder.Environment.IsDevelopment())
{
    var v = new RuntimeConfigValidator(builder.Configuration, "tenant");
    v.RequireNotPlaceholder("Jwt:SigningKey")
     .RequireNonEmpty("TenantService:ProvisioningSecret")
     .RequireConnectionString("ConnectionStrings:TenantDb");
}
```

### 4.4 Identity

```csharp
if (!builder.Environment.IsDevelopment())
{
    var v = new RuntimeConfigValidator(builder.Configuration, "identity");
    v.RequireNotPlaceholder("Jwt:SigningKey")
     .RequireNonEmpty("TenantService:ProvisioningSecret")
     .RequireAbsoluteUrl("NotificationsService:BaseUrl")
     .RequireNonEmpty("NotificationsService:PortalBaseUrl")
     .RequireConnectionString("ConnectionStrings:IdentityDb");
}
```

### 4.5 Web / BFF

```typescript
// apps/web/src/lib/env-validation.ts — called from instrumentation.ts
if (!isDev) {
  requireAbsoluteUrl('GATEWAY_URL', process.env.GATEWAY_URL);
  requireNonEmpty('INTERNAL_REQUEST_SECRET', process.env.INTERNAL_REQUEST_SECRET);
  requireNotPlaceholder('INTERNAL_REQUEST_SECRET', process.env.INTERNAL_REQUEST_SECRET);
}
```

Executed once at server startup via `apps/web/src/instrumentation.ts` (Next.js `register()` hook). The `nodejs` runtime guard ensures it only runs server-side.

---

## 5. Trust Boundary Config Consistency

The public trust boundary uses a shared secret across three locations. All three **must** match in production.

| Location | Config Key / Variable | Validation |
|---|---|---|
| Gateway | `PublicTrustBoundary:InternalRequestSecret` | Required non-empty + not placeholder in non-dev |
| CareConnect | `PublicTrustBoundary:InternalRequestSecret` | Required non-empty + not placeholder in non-dev |
| Web BFF | `INTERNAL_REQUEST_SECRET` (env var) | Required non-empty + not placeholder in non-dev |

**Cross-service equality cannot be validated at startup** (services do not share config at runtime). The three values are compared only through their shared deployment secrets system — the same secret must be injected at all three mount points.

If any of the three is wrong:
- **Gateway wrong:** Secret injected by YARP middleware is incorrect → CareConnect rejects all public requests with 403.
- **CareConnect wrong:** HMAC validation fails → all public network API calls return 403.
- **BFF wrong:** BFF sends incorrect secret → Gateway injects gateway's value, but BFF-originated requests fail.

Production deployment MUST inject the same value for all three.

---

## 6. Service URL Validation

### Production URL Rules

| Service | Key | Rule |
|---|---|---|
| CareConnect | `TenantService:BaseUrl` | Absolute HTTP/HTTPS URL required |
| CareConnect | `IdentityService:BaseUrl` | Absolute HTTP/HTTPS URL required |
| Identity | `NotificationsService:BaseUrl` | Absolute HTTP/HTTPS URL required |
| Web BFF | `GATEWAY_URL` | Absolute HTTP/HTTPS URL required |

**Localhost URLs in production:** Explicitly allowed by `RequireAbsoluteUrl` — a `http://` URL is valid regardless of host. Services deployed in a shared network (e.g. Kubernetes pod co-location) may legitimately use `http://localhost:PORT`. The validator enforces format, not host.

**Empty-string silently treating as unavailable:** Before this block, CareConnect's `TenantService:BaseUrl = ""` in the base config caused `HttpTenantServiceClient` to attempt requests to an empty base URL at the OS level, producing confusing connection-refused errors with no trace to the misconfiguration. The `RequireAbsoluteUrl` check surfaces this as a clear startup error.

---

## 7. Gateway Route Hardening Review

The gateway YARP route configuration was fully audited. No code changes were required — the existing posture is correct.

### Route Posture

| Route | Authorization Policy | Path | Assessment |
|---|---|---|---|
| `identity-login` | `Anonymous` | `/identity/api/auth/{**}` | Correct — login/token endpoints must be public |
| `identity-service-health` | `Anonymous` | `/identity/health` | Correct — health probes must be public |
| `identity-service-info` | `Anonymous` | `/identity/info` | Correct — non-sensitive service info |
| `identity-branding` | `Anonymous` | `/identity/api/tenants/current/branding` | Correct — public branding for login page |
| `identity-protected` | (default — requires auth via `.RequireAuthorization()`) | `/identity/{**}` | Correct — all other identity routes authenticated |
| `careconnect-service-health` | `Anonymous` | `/careconnect/health` | Correct |
| `careconnect-service-info` | `Anonymous` | `/careconnect/info` | Correct |
| `careconnect-internal-block` | **`Deny`** | `/careconnect/internal/{**}` | Correct — internal routes explicitly rejected at gateway |
| `careconnect-public-network` | `Anonymous` | `/careconnect/api/public/{**}` | Correct — trust boundary enforced by YARP middleware (BLK-SEC-02-02) |
| `careconnect-protected` | (default) | `/careconnect/{**}` | Correct |
| `fund-service-health` / `fund-service-info` | `Anonymous` | `/fund/health`, `/fund/info` | Correct |
| `fund-protected` | (default) | `/fund/{**}` | Correct |

**Global catch-all:** `MapReverseProxy().RequireAuthorization()` — any proxied request not matching an explicit anonymous route falls through to require authentication. No accidental anonymous admin/internal route exists.

**`Deny` policy:** The `Deny` policy (`RequireAssertion(_ => false)`) is correctly registered and applied to `/careconnect/internal/**`. A `Deny` policy request responds 403 regardless of the caller's token.

**Public CareConnect trust-boundary flow:** The YARP middleware for `/careconnect/api/public/*` strips client-supplied `X-Internal-Gateway-Secret` headers before injecting the configured secret. This prevents client forgery of the trust credential.

---

## 8. Production Readiness Checklist

### 8.1 Required Secrets (must be injected via environment or secrets manager)

| Secret / Variable | Required By | Must Match |
|---|---|---|
| `Jwt__SigningKey` | Gateway, CareConnect, Tenant, Identity, Notifications | **All must be identical** — the same HS256 signing key used by Identity to mint tokens validated by all others |
| `PublicTrustBoundary__InternalRequestSecret` | Gateway, CareConnect | Must match `INTERNAL_REQUEST_SECRET` in Web BFF |
| `INTERNAL_REQUEST_SECRET` | Web BFF | Must match `PublicTrustBoundary__InternalRequestSecret` in Gateway and CareConnect |
| `TenantService__ProvisioningSecret` | Tenant, Identity | **Same value** — Identity sends it as `X-Provisioning-Token`, Tenant validates it |
| `TenantService__ProvisioningToken` (CareConnect) | CareConnect | Must equal `TenantService__ProvisioningSecret` on Tenant |
| `IdentityService__ProvisioningToken` (CareConnect) | CareConnect | Must equal `TenantService__ProvisioningSecret` on Identity |
| `ConnectionStrings__CareConnectDb` | CareConnect | MySQL connection string with real password |
| `ConnectionStrings__TenantDb` | Tenant | MySQL connection string with real password |
| `ConnectionStrings__IdentityDb` | Identity | MySQL connection string with real password |
| `FLOW_SERVICE_TOKEN_SECRET` | CareConnect, Notifications | Same value across all services that validate Flow M2M tokens |

### 8.2 Required Environment Variables (Web BFF)

| Variable | Required | Description |
|---|---|---|
| `GATEWAY_URL` | Yes | Absolute URL to the .NET API gateway |
| `INTERNAL_REQUEST_SECRET` | Yes | Trust boundary secret — must match gateway and CareConnect |
| `NEXT_PUBLIC_ENV` | Dev-only | Set to `development` in `.env.local`; omit in production |
| `NEXT_PUBLIC_TENANT_CODE` | Dev-only | Pre-fills login form; omit in production |

### 8.3 Services That Must Share the Same Value

| Value | Services |
|---|---|
| `Jwt__SigningKey` | Gateway, CareConnect, Tenant, Identity, Notifications, Fund, Liens, Documents, Monitoring, Task, Reports |
| `PublicTrustBoundary:InternalRequestSecret` + `INTERNAL_REQUEST_SECRET` | Gateway, CareConnect, Web BFF |
| `TenantService:ProvisioningSecret` ↔ `ProvisioningToken` | Tenant service (receives) + Identity (sends as X-Provisioning-Token) + CareConnect (sends as X-Provisioning-Token to Tenant) |

### 8.4 Database Migrations

CareConnect, Tenant, and Identity all run EF Core `db.Database.Migrate()` at startup. The pending migration for CareConnect is `BLK_PERF_01_PerformanceIndexes` (adds 4 composite indexes, ProviderNetworks tables). This runs automatically on startup — no manual `dotnet ef database update` is needed.

### 8.5 Gateway Route Assumptions

- The `Deny` policy on `/careconnect/internal/**` blocks all external callers regardless of token.
- The `/careconnect/api/public/**` route is intentionally anonymous — the trust boundary is enforced by the YARP middleware injecting `X-Internal-Gateway-Secret` (not by token auth).
- Any new CareConnect route added to YARP must explicitly set `AuthorizationPolicy: "Anonymous"` or it will require a valid platform JWT.

### 8.6 Public Endpoint Trust-Boundary Assumptions

Callers of `/api/public/network/*` endpoints are anonymous (no JWT required). Tenant identity is derived from the `X-Tenant-Id` header validated against an HMAC signed by `INTERNAL_REQUEST_SECRET`. Spoofing `X-Tenant-Id` requires knowing `INTERNAL_REQUEST_SECRET`. Keep this secret out of browser code and client bundles.

### 8.7 Rollback Notes

If a deployment fails:
1. The service startup throws `InvalidOperationException` with a descriptive message in the host log. Read the log before assuming a code bug.
2. The most likely startup failure causes: missing `Jwt__SigningKey`, missing `PublicTrustBoundary__InternalRequestSecret`, or connection strings still containing `REPLACE_VIA_SECRET`.
3. Rollback by re-deploying the previous image — no DDL rollback is required for BLK-OPS-01 (no schema changes in this block).

### 8.8 Smoke Tests After Deploy

1. `GET /health` on Gateway, CareConnect, Tenant, Identity → all return `200 {"status": "ok"}`
2. `GET /careconnect/api/public/network/` via Gateway → returns network list (proves trust boundary is configured correctly)
3. `POST /identity/api/auth/login` → returns JWT (proves Jwt:SigningKey matches between Identity and Gateway)
4. Admin login → dashboard loads → proves full auth chain (Identity → Gateway → CareConnect → Tenant)
5. Public network directory loads on the Web BFF → proves `GATEWAY_URL` and `INTERNAL_REQUEST_SECRET` are correct

---

## 9. Validation Results

| Check | Result |
|---|---|
| `dotnet build Gateway.Api -c Release` | ✅ Succeeded: 0 errors (pre-existing MSB3277 version warnings) |
| `dotnet build CareConnect.Api -c Release` | ✅ Succeeded: 0 errors, 0 warnings |
| `dotnet build Tenant.Api -c Release` | ✅ Succeeded: 0 errors (pre-existing MSB3277) |
| `dotnet build Identity.Api -c Release` | ✅ Succeeded: 0 errors (pre-existing CA2017 warning) |
| `dotnet build BuildingBlocks -c Release` | ✅ Succeeded: 0 errors, 0 warnings |
| `npx tsc --noEmit` (web) | ✅ Passed cleanly |
| `dotnet test BuildingBlocks.Tests --filter RuntimeConfigValidator` | ✅ 29 passed, 0 failed (17 new + 12 pre-existing) |
| Development startup unaffected | ✅ All validation blocks gated on `!IsDevelopment()` |
| Production startup fails on `REPLACE_VIA_SECRET` placeholder | ✅ `RequireNotPlaceholder` detects all known placeholders |
| Production startup fails on empty values | ✅ `RequireNonEmpty` detects null/empty/whitespace |
| Production startup fails on non-URL service address | ✅ `RequireAbsoluteUrl` rejects relative and non-HTTP URLs |
| Trust boundary flow unaffected | ✅ No changes to trust boundary logic; only startup validation added |
| Existing security controls intact | ✅ BLK-SEC-01 and BLK-SEC-02-* controls unchanged |

---

## 10. Changed Files

| File | Change |
|---|---|
| `shared/building-blocks/BuildingBlocks/RuntimeConfigValidator.cs` | New — shared validation helper |
| `shared/building-blocks/BuildingBlocks.Tests/BuildingBlocks.Tests/RuntimeConfigValidatorTests.cs` | New — 17 unit tests |
| `apps/gateway/Gateway.Api/Program.cs` | Added `using BuildingBlocks;` + BLK-OPS-01 production validation block |
| `apps/services/careconnect/CareConnect.Api/Program.cs` | Added `using BuildingBlocks;` + replaced BLK-SEC-01 inline checks with `RuntimeConfigValidator` + added trust-boundary, URL, and connection string checks |
| `apps/services/tenant/Tenant.Api/Program.cs` | Added `using BuildingBlocks;` + replaced BLK-SEC-01 inline checks with `RuntimeConfigValidator` + added connection string check |
| `apps/services/identity/Identity.Api/Program.cs` | Added `using BuildingBlocks;` + replaced BLK-SEC-01 inline checks with `RuntimeConfigValidator` + added connection string check and URL format check for notifications |
| `apps/web/src/lib/env-validation.ts` | New — server-side Next.js BFF env validation with placeholder detection |
| `apps/web/src/instrumentation.ts` | New — Next.js startup hook that calls `validateServerEnv()` |

---

## 11. Methods / Endpoints Updated

No new endpoints. No existing endpoint signatures changed.

**Startup code changed:** `Program.cs` in Gateway, CareConnect, Tenant, and Identity now include production fail-fast validation blocks that run before `builder.Build()`. This is pure startup guard code — no request handling paths were modified.

---

## 12. GitHub Commits

`87ea2f70384aa8f771b404f1bf73ea1ae0c3207f` — "Improve runtime configuration validation for production environments"

---

## 13. Issues / Gaps

| Item | Severity | Disposition |
|---|---|---|
| Comms, Fund, Liens, Monitoring, Task, Reports services not validated | Low | These services have limited external exposure; no `PublicTrustBoundary` or cross-service provisioning. Their `Jwt:SigningKey` uses `?? throw` (non-null). Placeholder check deferred to a future ops pass. |
| Notifications service not validated | Low | `Jwt:SigningKey` uses `?? throw`; service token key optional. Deferred. |
| `TenantService:SyncSecret` in Identity not validated | Low | Used for Identity → Tenant sync; currently empty in all known configs. Deferred — requires further investigation into whether sync is active in production. |
| `NEXT_PUBLIC_*` variables cannot be validated at runtime | By design | NEXT_PUBLIC env vars are inlined at build time; only `GATEWAY_URL` and `INTERNAL_REQUEST_SECRET` (server-side) are runtime-injectable. |
| Cross-service secret equality not verified at startup | By design | Services cannot read each other's config at startup. Equality is enforced via deployment secrets management (same secret injected to all three trust-boundary locations). |
| MSB3277 JwtBearer version mismatch (Gateway, Tenant) | Low | Pre-existing since before BLK-OPS-01; build succeeds. Requires package version alignment in a future dependency-management pass. |


---

## 14. GitHub Diff Reference

- Commit ID: `87ea2f70384aa8f771b404f1bf73ea1ae0c3207f`
- Diff file: `analysis/BLK-OPS-01-commit.diff.txt`
- Summary: `analysis/BLK-OPS-01-commit-summary.md`
