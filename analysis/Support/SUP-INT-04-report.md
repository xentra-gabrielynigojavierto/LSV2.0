# SUP-INT-04 Report — Environment + Deployment Config

**Feature:** SUP-INT-04
**Date:** 2026-04-25
**Status:** COMPLETE

---

## 1. Codebase Analysis

### Support Service
- **Location:** `apps/services/support/Support.Api/`
- **Port:** `5017` (configured in `appsettings.json` via `"Urls": "http://0.0.0.0:5017"`)
- **Database:** MySQL 8.0+ via Pomelo EntityFrameworkCore
- **Connection string key:** `ConnectionStrings__Support`
- **Auth:** Symmetric JWT (HS256) via `Authentication:Jwt:SymmetricKey`; fails-closed at startup if key absent
- **Health endpoint:** `GET /support/api/health` (anonymous, mapped in Program.cs)
- **Metrics endpoint:** `GET /support/api/metrics` (Prometheus scrape, anonymous)
- **Integration modes:** Notifications, Audit, FileStorage — all default to `NoOp` in base config

### Gateway (YARP)
- **Location:** `apps/gateway/Gateway.Api/`
- **Port:** `5010`
- **JWT config path:** `Jwt:SigningKey` (different path from Support's `Authentication:Jwt:SymmetricKey`)
- **Support routes already registered:** `support-health` (Order 93, Anonymous), `support-metrics` (Order 94, Anonymous), `support-protected` (Order 193, default JWT policy)
- **No PathRemovePrefix** on support routes — correct, since the service itself serves under `/support/api/...`

### Scripts
- **`scripts/run-dev.sh`:** Builds and starts all .NET services in background. Support starts with `ASPNETCORE_ENVIRONMENT=Development` only — no JWT key env var passed.

### Docker / Compose
- No `docker-compose.yml` present in the repository.

---

## 2. Environment / Config Structure

### Convention across platform services

| Pattern | Example |
|---------|---------|
| Base config (non-secret defaults) | `appsettings.json` |
| Dev overrides (dev-only keys, not for prod) | `appsettings.Development.json` |
| Secrets injected at runtime | Environment variables (Replit secrets, Kubernetes secrets, etc.) |
| .NET env var override separator | `__` (double underscore) |

### Key env vars consumed by Support

| Env var | Config path | Required | Notes |
|---------|------------|----------|-------|
| `ConnectionStrings__Support` | `ConnectionStrings:Support` | Yes (runtime) | MySQL connection string |
| `Authentication__Jwt__SymmetricKey` | `Authentication:Jwt:SymmetricKey` | Yes | Must match `Jwt__SigningKey` |
| `Authentication__Jwt__Issuer` | `Authentication:Jwt:Issuer` | Yes | Default: `legalsynq-identity` |
| `Authentication__Jwt__Audience` | `Authentication:Jwt:Audience` | Yes | Default: `legalsynq-platform` |
| `ASPNETCORE_ENVIRONMENT` | — | Recommended | `Development` or `Production` |
| `ASPNETCORE_URLS` or `Urls` | `Urls` | No | Default: `http://0.0.0.0:5017` |
| `Support__Notifications__Mode` | `Support:Notifications:Mode` | No | Default: `NoOp` |
| `Support__Audit__Mode` | `Support:Audit:Mode` | No | Default: `NoOp` |
| `Support__FileStorage__Mode` | `Support:FileStorage:Mode` | No | Default: `NoOp` |

### Key env vars consumed by Gateway (for support cluster override)

| Env var | Config path | Notes |
|---------|------------|-------|
| `ReverseProxy__Clusters__support-cluster__Destinations__support-primary__Address` | YARP cluster destination | Override support service host in non-localhost deployments |

---

## 3. Support Runtime Configuration

### appsettings.json (base — committed, no secrets)

```json
{
  "Urls": "http://0.0.0.0:5017",
  "ConnectionStrings": {
    "Support": "Server=localhost;Port=3306;Database=support;User=root;Password=;"
  },
  "Authentication": {
    "Jwt": {
      "Issuer": "legalsynq-identity",
      "Audience": "legalsynq-platform",
      "RequireHttpsMetadata": false
    }
  },
  "Support": {
    "Notifications": { "Mode": "NoOp" },
    "Audit": { "Mode": "NoOp" },
    "FileStorage": { "Mode": "NoOp" }
  }
}
```

**Assessment:** Clean. No production secrets. Connection string is a dev placeholder (overridden at runtime via env var). `SymmetricKey` is absent from base config — correct, because it's a secret that must be injected.

### appsettings.Development.json (dev only — committed, dev placeholder key)

**Pre-fix:**
```json
{
  "Authentication": {
    "Jwt": {
      "SymmetricKey": "REPLACE_VIA_SECRET_minimum_32_characters_long"
    }
  }
}
```

**Post-fix:** Aligned to match gateway's dev signing key (`dev-only-signing-key-minimum-32-chars-long!`).

**Why this was broken:** Gateway `appsettings.Development.json` uses `dev-only-signing-key-minimum-32-chars-long!`; Support used a placeholder that doesn't match. Tokens minted by the gateway's identity service would fail validation in Support.

---

## 4. Gateway Cluster / Route Configuration

### Support routes in `gateway/Gateway.Api/appsettings.json`

| Route key | Order | Policy | Match |
|-----------|-------|--------|-------|
| `support-health` | 93 | Anonymous | `/support/api/health` |
| `support-metrics` | 94 | Anonymous | `/support/api/metrics` |
| `support-protected` | 193 | Default (JWT) | `/support/api/{**catch-all}` |

**No `PathRemovePrefix` transform** — correct. Support registers endpoints at `/support/api/...` paths directly; no rewriting needed.

### Support cluster

```json
"support-cluster": {
  "Destinations": {
    "support-primary": {
      "Address": "http://localhost:5017"
    }
  }
}
```

**Runtime override:** Set env var `ReverseProxy__Clusters__support-cluster__Destinations__support-primary__Address` to override the destination in non-localhost deployments (containerized, k8s, etc.).

**Assessment:** No changes required. Gateway configuration is complete and correct for Support.

---

## 5. Database Configuration

### Connection string

Support requires MySQL 8.0+. The connection string key is `ConnectionStrings__Support`.

**Dev placeholder** (in `appsettings.json`):
```
Server=localhost;Port=3306;Database=support;User=root;Password=;
```

**Production provisioning steps:**
1. Provision a MySQL 8.0+ database (RDS, PlanetScale, or self-managed).
2. Create database: `CREATE DATABASE support CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;`
3. Create a dedicated user with `SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, DROP` on the `support` database.
4. Set the env var at runtime:
   ```
   ConnectionStrings__Support=Server=<host>;Port=3306;Database=support;User=<user>;Password=<password>;SslMode=Required;
   ```
5. Run EF Core migrations on first deploy:
   ```bash
   dotnet ef database update \
     --project apps/services/support/Support.Api/Support.Api.csproj \
     --connection "Server=<host>;Port=3306;Database=support;..."
   ```

**Note:** No `ConnectionStrings__Support` secret is currently provisioned in the Replit environment. The Support service will start (health check passes) but DB operations will fail until a MySQL instance is available.

---

## 6. JWT / Auth Configuration

### Alignment requirement

| Config | Gateway | Support |
|--------|---------|---------|
| Config path | `Jwt:SigningKey` | `Authentication:Jwt:SymmetricKey` |
| Dev value | `dev-only-signing-key-minimum-32-chars-long!` | **Fixed to match** |
| Prod env var | `Jwt__SigningKey` | `Authentication__Jwt__SymmetricKey` |
| Issuer | `legalsynq-identity` | `legalsynq-identity` ✅ |
| Audience | `legalsynq-platform` | `legalsynq-platform` ✅ |

**Claim mapping:** Support disables .NET's legacy claim-type mapping (`JwtSecurityTokenHandler.DefaultMapInboundClaims = false`). Claims survive as issued:
- `sub` → user ID
- `role` → role (used for RBAC)
- `tenant_id` / `tenantId` / `tid` → tenant resolution (TenantResolutionMiddleware)

**Tenant from JWT:** Tenant ID comes from the JWT claim only — never from a request header. This is correct and safe.

**Production secret alignment:** Operators must ensure `Authentication__Jwt__SymmetricKey` == `Jwt__SigningKey`. Both are set via separate env vars (different config paths). This is a documented operational requirement.

---

## 7. Health Check Validation

### Expected behavior

| Endpoint | Auth required | Expected | Blocker |
|----------|--------------|----------|---------|
| `GET http://localhost:5017/support/api/health` | No | 200 OK | Requires Support service to start (needs MySQL reachable or graceful startup) |
| `GET http://localhost:5010/support/api/health` | No | Proxied 200 | Requires gateway + support both running |
| `GET http://localhost:5010/support/api/tickets` (no token) | Yes | 401 | Gateway enforces JWT on `support-protected` route |
| `GET http://localhost:5010/support/api/tickets` (valid JWT) | Yes | 200 or 503 | 200 if MySQL is up; 503/500 if DB not provisioned |

### Actual validation results

Support service startup is blocked by the missing `Authentication__Jwt__SymmetricKey` alignment (before fix) and the lack of MySQL (`ConnectionStrings__Support` not provisioned).

**Health check note:** `builder.Services.AddHealthChecks()` in Program.cs has no DB health check registered. The `/support/api/health` endpoint will respond `200 Healthy` even without a MySQL connection — the database error only surfaces on actual data operations.

**Blocker:** `ConnectionStrings__Support` — MySQL not provisioned in this environment. Service will crash at EF migration/connection on first DB operation but health endpoint itself is unaffected.

---

## 8. Deployment / Script Updates

### `scripts/run-dev.sh` changes

**Before:**
```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" &
```

**After:**
```bash
ASPNETCORE_ENVIRONMENT=Development \
  Authentication__Jwt__SymmetricKey="${Jwt__SigningKey:-dev-only-signing-key-minimum-32-chars-long!}" \
  dotnet run --no-build --project "$ROOT/apps/services/support/Support.Api/Support.Api.csproj" &
```

**Rationale:**
- In dev (no secrets set): falls back to the same dev key as gateway's `appsettings.Development.json`
- In production/staging (secret set): uses the real `Jwt__SigningKey` value
- Eliminates the mismatch between gateway and support JWT validation

### No docker-compose changes
No `docker-compose.yml` exists in the repository. Support follows the same pattern as all other services (direct process management via `run-dev.sh`).

---

## 9. Files Created / Changed

| File | Action | Change |
|------|--------|--------|
| `analysis/SUP-INT-04-report.md` | Created | This report |
| `apps/services/support/Support.Api/appsettings.Development.json` | Modified | Fixed `SymmetricKey` to match gateway's dev signing key |
| `scripts/run-dev.sh` | Modified | Pass `Authentication__Jwt__SymmetricKey` env var to Support startup |

---

## 10. Validation Results

| Check | Result | Notes |
|-------|--------|-------|
| Support runtime config is env-driven | ✅ | All secrets via env vars; base config has only non-secret defaults |
| No production secrets committed | ✅ | Dev key only in `appsettings.Development.json`; prod key injected at runtime |
| `appsettings.Development.json` SymmetricKey aligned to gateway | ✅ | Fixed from placeholder to matching dev key |
| `run-dev.sh` passes JWT key to Support | ✅ | Fixed to pass `Authentication__Jwt__SymmetricKey` |
| Gateway cluster destination overrideable | ✅ | `ReverseProxy__Clusters__support-cluster__Destinations__support-primary__Address` supported natively by YARP |
| Support DB connection documented | ✅ | See Section 5; no secret committed |
| Gateway routes correct (no PathRemovePrefix) | ✅ | No changes needed |
| Support standalone deployable | ✅ | `appsettings.json` has all defaults; Support.sln and standalone appsettings preserved |

---

## 11. Build Results

| Project | Result | Notes |
|---------|--------|-------|
| `Support.Api.csproj` | ✅ 0 errors, 0 warnings | `dotnet build --configuration Debug` |
| `Gateway.Api.csproj` | ✅ 0 errors, 1 pre-existing warning | MSB3277 on BuildingBlocks.dll — pre-existing, unrelated to this block |

### Health Check Validation (post-fix)

| Endpoint | Result | Detail |
|----------|--------|--------|
| `GET http://localhost:5017/support/api/health` (direct) | ✅ `200 Healthy` | Support service running; health check passes without DB probe |
| `GET http://localhost:5010/support/api/health` (via gateway) | ✅ `200 Healthy` | Gateway correctly proxies to support-cluster; no PathRemovePrefix needed |
| `GET http://localhost:5010/support/api/tickets` (no token) | ✅ `401 Unauthorized` | Gateway `support-protected` route enforces JWT correctly |

---

## 12. Known Gaps / Deferred Items

| ID | Item | Status | Notes |
|----|------|--------|-------|
| GAP-01 | MySQL DB not provisioned | Blocked (external) | `ConnectionStrings__Support` secret not set; requires MySQL instance |
| GAP-02 | DB health check not registered | Deferred | `AddHealthChecks()` has no DB probe; health returns 200 even if MySQL is down |
| GAP-03 | EF Core migrations not run | Blocked (external) | Requires provisioned DB; see Section 5 for steps |
| GAP-04 | Support service runtime validation | Blocked | Cannot fully validate `/support/api/tickets` 200 without DB |
| GAP-05 | `Authentication__Jwt__SymmetricKey` prod alignment | Operational | Operator must ensure this equals `Jwt__SigningKey` at deploy time |

---

## 13. Final Readiness Assessment

| Criterion | Status |
|-----------|--------|
| Support runtime config is environment-driven | ✅ |
| No production secrets are committed | ✅ |
| Support DB connection requirement clearly configured/documented | ✅ |
| Gateway cluster destination is overrideable via env var | ✅ |
| JWT config alignment documented and fixed in dev | ✅ |
| Health endpoints validated or exact blockers documented | ✅ (health OK; data ops blocked by DB) |
| Support remains standalone deployable | ✅ |
| Existing platform startup not broken | ✅ |
| Build results documented | See Section 11 |

**Overall:** Configuration and environment alignment is complete. The remaining gap is an external infrastructure dependency (MySQL provisioning) that cannot be resolved without a database instance.
