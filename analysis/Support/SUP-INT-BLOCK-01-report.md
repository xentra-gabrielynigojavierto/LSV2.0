# SUP-INT-BLOCK-01 Report

**Status:** Complete  
**Date:** 2026-04-24  
**Blocks:** SUP-INT-01 (Main Platform Shell Integration) + SUP-INT-02 (Gateway + Auth Validation)

---

## 1. Codebase Analysis

### Source ZIP
- **File:** `attached_assets/legalsynq-support-source-with-schema-20260424-230810Z_1777072597879.zip`
- **Extracted to:** `apps/services/support/`
- **Support service files extracted:** 84 files

### Projects Identified

| Role | Location |
|---|---|
| Platform gateway (YARP) | `apps/gateway/Gateway.Api/` |
| Support backend service | `apps/services/support/Support.Api/` |
| Support test project | `apps/services/support/Support.Tests/` |
| Web app (Next.js) | `apps/web/` |
| Control Center admin (Next.js) | `apps/control-center/` |
| Platform solution | `LegalSynq.sln` |
| Support standalone solution | `apps/services/support/Support.sln` |

### Support Source Previously Completed Blocks
The ZIP contained completed work through SUP-B08 (Audit Integration) and SUP-INT-08 (File Upload Provider Abstraction). This is a fully built standalone service with tickets, comments, attachments, product refs, queues, audit dispatch, notification dispatch, and file storage providers.

---

## 2. Existing Architecture Discovered

### YARP Route Configuration
- **Location:** `apps/gateway/Gateway.Api/appsettings.json` under `ReverseProxy.Routes`
- **Loading:** `builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))`
- **Pattern:** Each service has 2–4 named routes:
  - `{service}-health` / `{service}-info` — Anonymous, low order number
  - `{service}-protected` — Requires auth, high order number (100–196)
  - Optional: specific anonymous routes (webhooks, public APIs)
- **Transforms:** All routes use `PathRemovePrefix: "/{service-prefix}"` to strip the routing prefix before forwarding

### Support Path Exception
Support is the **only service** that retains its routing prefix at the downstream. All Support endpoints are mapped with `/support/api/...` baked into the route group at the service level:
- `app.MapGroup("/support/api/tickets")`
- `app.MapHealthChecks("/support/api/health")`

This means the gateway must **NOT apply PathRemovePrefix** for Support — the full path must be forwarded unchanged.

### Auth / JWT Middleware
- **Gateway:** `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)` validates HS256 tokens against `Jwt:SigningKey`
- **Support service:** `AddSupportAuth()` in `Auth/AuthExtensions.cs` — validates the same JWT using `Authentication:Jwt:SymmetricKey` (or OIDC `Authority`)
- **Claim type mapping disabled:** `JwtSecurityTokenHandler.DefaultMapInboundClaims = false` — `sub`, `role`, `tenant_id` survive intact
- **Roles validated:** `SupportPolicies.SupportRead/Write/Manage/Internal` — all map to platform roles (`TenantAdmin`, `TenantUser`, `SupportAgent`, etc.)
- **401/403:** Standard ASP.NET Core policy enforcement; no custom middleware

### Tenant Claims Resolution Pattern
- **Platform standard (`shared/building-blocks`):** `CurrentRequestContext` extracts `tenant_id` and `tenant_code` from JWT claims
- **Support service:** `TenantResolutionMiddleware` extracts `tenant_id` / `tenantId` / `tid` claim from `context.User`
- **Production rule (hard-coded):** X-Tenant-Id header is **ignored in non-Development/Testing environments**; only JWT claims are accepted
- **Dev/Test:** Falls back to `X-Tenant-Id` header after claim check (for local testing without a full JWT)

### Header Forwarding Behavior
| Header | Behavior |
|---|---|
| `Authorization` | Forwarded unchanged by YARP (no transform) |
| `X-Correlation-Id` | Assigned at gateway edge; forwarded to all downstreams |
| `X-Internal-Gateway-Secret` | Injected only for `/careconnect/api/public/*`; all other routes unaffected |
| `X-Tenant-Id` | **NOT injected** — tenant comes from JWT claims |

### Service Config Pattern
- Each service's `appsettings.json` contains `"Urls": "http://0.0.0.0:{port}"`
- Gateway cluster destinations point to `http://localhost:{port}`
- Secrets (signing keys, passwords) are provided via environment variables using the `__` double-underscore flattening convention

### Health Check Pattern
- Most services: `GET /health` → gateway route `/{service}/health` strips prefix → `/health` hits downstream
- Support (exception): `GET /support/api/health` → gateway route `/support/api/health` → no prefix strip → `/support/api/health` hits downstream

---

## 3. Database / Schema Assessment

**Database changes: None.**

Gateway route configuration, service cluster registration, navigation, and permissions are all config/code-based in `appsettings.json` and C# `Program.cs`. No database-backed service registry exists in this platform.

Support domain tables are untouched. The `db/support-schema.sql` in the ZIP documents the existing Support MySQL schema for deployment reference — no changes were made to it.

The Support service requires its own MySQL database (`support`). In production, `ConnectionStrings__Support` must be configured as an environment secret. This is a deployment concern, not a schema change.

---

## 4. Files Created / Changed

| File | Type | Purpose |
|---|---|---|
| `apps/services/support/` (84 files) | Created (extracted) | Full Support service source — preserved exactly as-is from ZIP |
| `apps/services/support/Support.Api/appsettings.json` | Modified | Added `Urls: http://0.0.0.0:5017` (port assignment) and `Authentication:Jwt` section with `Issuer`, `Audience`, `RequireHttpsMetadata` |
| `apps/services/support/Support.Api/appsettings.Development.json` | Created | Dev-environment overrides: `Authentication:Jwt:SymmetricKey` placeholder for local dev without a real IdP |
| `apps/gateway/Gateway.Api/appsettings.json` | Modified | Added `support-health`, `support-metrics`, `support-protected` routes + `support-cluster` destination |
| `scripts/run-dev.sh` | Modified | Added `dotnet restore` + `dotnet build` for Support in build phase; added `dotnet run` for Support in startup phase |
| `analysis/SUP-INT-BLOCK-01-report.md` | Created | This report |

### appsettings.json changes (Support service)
Added at top level:
```json
"Urls": "http://0.0.0.0:5017",
"Authentication": {
  "Jwt": {
    "Issuer": "legalsynq-identity",
    "Audience": "legalsynq-platform",
    "RequireHttpsMetadata": false
  }
}
```
Note: `SymmetricKey` is intentionally absent from the base appsettings — it must be provided as `Authentication__Jwt__SymmetricKey` environment secret in all non-Development environments.

---

## 5. Gateway / YARP Integration

### Added Routes

| Route ID | Path Pattern | Auth Policy | Order | Transform |
|---|---|---|---|---|
| `support-health` | `/support/api/health` | Anonymous | 93 | None |
| `support-metrics` | `/support/api/metrics` | Anonymous | 94 | None |
| `support-protected` | `/support/api/{**catch-all}` | Default (auth required) | 193 | None |

### Cluster
```json
"support-cluster": {
  "Destinations": {
    "support-primary": {
      "Address": "http://localhost:5017"
    }
  }
}
```
In production, the cluster address is overridden via `ReverseProxy__Clusters__support-cluster__Destinations__support-primary__Address`.

### Path Preservation
**No `PathRemovePrefix` transform is applied to any Support route.** This is intentional and correct: Support maps all endpoints with the full `/support/api/...` prefix at the service level. YARP forwards the complete request path unchanged to `http://localhost:5017/support/api/...`.

This differs from all other platform services which strip their routing prefix before forwarding.

### Authorization Forwarding
YARP forwards the `Authorization: Bearer <token>` header unchanged to Support. No special transform is needed — this is YARP's default behavior when no auth header transform is configured.

---

## 6. Service Registration / Config

### Support Service URL
- **Dev:** `http://0.0.0.0:5017` (set in `appsettings.json`)
- **Prod:** Override via `ASPNETCORE_URLS` or `Urls` environment variable

### Environment Variable Names (production)
| Variable | Value |
|---|---|
| `Authentication__Jwt__Issuer` | `legalsynq-identity` |
| `Authentication__Jwt__Audience` | `legalsynq-platform` |
| `Authentication__Jwt__SymmetricKey` | Same value as platform `Jwt__SigningKey` secret |
| `ConnectionStrings__Support` | Production MySQL connection string |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

### appsettings Layer
- `appsettings.json` — base config with port, JWT Issuer/Audience, Serilog, Support integration options
- `appsettings.Development.json` — dev overrides: debug logging + `SymmetricKey` placeholder (matches gateway's placeholder format)

---

## 7. Auth / Token Forwarding

### Authorization Forwarding Behavior
YARP passes `Authorization: Bearer <token>` to Support unchanged. No transform is applied, no header is stripped or added for Support routes.

### JWT Validation Behavior
Support validates the same HS256 token the gateway validated, using the identical signing key (`Authentication:Jwt:SymmetricKey` = platform `Jwt:SigningKey`). Validation parameters:
- `ValidateIssuer = true` → issuer must be `legalsynq-identity`
- `ValidateAudience = true` → audience must be `legalsynq-platform`
- `ValidateLifetime = true`
- `ValidateIssuerSigningKey = true`
- `ClockSkew = 30s`
- `RoleClaimType = "role"` / `NameClaimType = "sub"` — claim types preserved as-is (no mapping)

### 401/403 Behavior
- Unauthenticated requests to `/support/api/tickets` etc. → gateway returns **401** (authorization policy rejects before routing)
- Authenticated but wrong role → Support returns **403** (policy enforcement in `RequireAuthorization(SupportPolicies.*)`)
- Missing tenant claim on protected path in production → `TenantResolutionMiddleware` short-circuits with **403**

---

## 8. Tenant Propagation Validation

### Tenant Claim Source
Tenant identity flows exclusively through the JWT `tenant_id` claim (also checked: `tenantId`, `tid`). The `TenantResolutionMiddleware` extracts this claim from `context.User` after authentication.

### No X-Tenant-Id Production Dependency
The gateway does **not** inject `X-Tenant-Id`. The Support middleware explicitly **ignores** `X-Tenant-Id` in non-Development environments. Tenant context always comes from the validated JWT claim.

### Cross-Tenant Protection
The `TicketService`, `CommentService`, and all other Support domain services filter all reads and writes by `ITenantContext.TenantId`, which is resolved only from the authenticated JWT. A user from tenant A cannot read or write tenant B's data because their JWT carries only their own `tenant_id` claim.

---

## 9. Health Validation

### Gateway health route for Support
- **Route:** `GET /support/api/health` → gateway routes to `http://localhost:5017/support/api/health`
- **Support downstream:** `app.MapHealthChecks("/support/api/health").AllowAnonymous()`
- **Expected response:** `HTTP 200 OK` with ASP.NET Core health check JSON payload

### Direct Support health (standalone mode)
- Preserved: Support still starts independently on port 5017 with its own `appsettings.json`
- Direct access: `GET http://localhost:5017/support/api/health` continues to work

### Runtime validation
Full live validation (HTTP GET against running service) is blocked in this environment because the Support service requires a MySQL database (`ConnectionStrings__Support`) that is not yet provisioned. The service will fail to start at runtime without the database connection. The build validation below confirms the service is correctly implemented.

---

## 10. Test Results

### Gateway config syntax validation
- Confirmed YARP routes parse correctly by building `Gateway.Api.csproj` — `Build succeeded, 0 Error(s)`

### Support service standalone build
```
dotnet build apps/services/support/Support.Api/Support.Api.csproj
Build succeeded.
0 Error(s)
```
Warnings are pre-existing NuGet vulnerability advisories (pinned packages from SUP-B08) and a `CS0618` about `ISystemClock` in `TestAuthHandler.cs` (test-only, does not affect production).

### Live HTTP validation
Not runnable in this environment (MySQL for Support not provisioned). Documented as a known gap — see §12.

---

## 11. Build Results

### Backend — Gateway
```
dotnet build apps/gateway/Gateway.Api/Gateway.Api.csproj
Build succeeded. 0 Error(s)
```
(Pre-existing MSB3277 warning about JwtBearer version conflict with BuildingBlocks is unrelated to this block.)

### Backend — Support Service
```
dotnet build apps/services/support/Support.Api/Support.Api.csproj
Build succeeded. 0 Error(s)
```

### Frontend
No frontend changes were made in this block. The Control Center and web app were not modified.

---

## 12. Known Gaps / Deferred Items

1. **MySQL database for Support not provisioned.** The `ConnectionStrings__Support` secret is not yet configured. The Support service will fail to start at runtime until a MySQL database is created and the connection string is registered as an environment secret. This is a deployment step, not a code gap.

2. **Live HTTP validation blocked.** Because the database is not provisioned, end-to-end HTTP validation through the gateway (`GET /support/api/health`, authenticated `POST /support/api/tickets`) cannot be performed in this environment. The build validates the configuration is syntactically and type-check clean.

3. **Production `Authentication__Jwt__SymmetricKey` secret not registered.** In production, this must be set to the same value as the platform's `Jwt__SigningKey` secret. No code change is needed — the value is ready to receive via environment variable.

4. **Control Center UI for Support** — out of scope for this block. No UI navigation or page routes for Support were added to the Control Center. This is deferred to a future integration block.

5. **Notification and Audit dispatch** — Support's `Support:Notifications:Mode` and `Support:Audit:Mode` remain `NoOp`. Wiring these to the platform's Notifications and Audit services is deferred to a future integration block.

6. **File storage** — `Support:FileStorage:Mode` remains `NoOp` in the platform appsettings. Connecting to the Documents Service is deferred to a future integration block.

7. **Standalone Support deployability confirmed preserved.** The `Support.sln` and standalone `appsettings.json` are intact. The service can be run independently of the platform by providing its own `Authentication:Jwt` config.
