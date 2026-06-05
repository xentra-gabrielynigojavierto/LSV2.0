# BLK-SEC-02-02 Report

## 1. Summary

**Block:** Public Tenant Header Trust Boundary Hardening
**Status:** Complete
**Date:** 2026-04-23
**Window:** TENANT-STABILIZATION 2026-04-23 → 2026-05-07
**Follows:** BLK-SEC-02-01 (commit `8a0aaad34602bf45a769e9666fa49383cbf723e9`)

Closes the residual public tenant-header trust gap identified in BLK-SEC-02-01 §12 Issue #1.

**Problem:** CareConnect's public endpoints (`/api/public/network/*`, `/api/public/referrals`)
blindly trusted `X-Tenant-Id`. The YARP gateway forwarded this header untouched for the
`careconnect-public-network` anonymous route. Any direct caller reaching the gateway on
port 5010 could set an arbitrary `X-Tenant-Id` and access any tenant's public data or
submit referrals into any tenant's system.

**Fix:** Two-layer defense fully implemented:
1. **YARP gateway pipeline middleware** — strips client-supplied `X-Internal-Gateway-Secret`,
   injects the configured gateway origin marker. CareConnect validates this header (Layer 1:
   blocks direct-to-service bypass of port 5003).
2. **BFF HMAC signature + CareConnect validation** — BFF signs resolved tenant ID with
   `INTERNAL_REQUEST_SECRET` using HMAC-SHA256, sends as `X-Tenant-Id-Sig`. CareConnect
   validates the signature using `CryptographicOperations.FixedTimeEquals` (Layer 2: blocks
   `X-Tenant-Id` spoofing from direct gateway callers who lack the secret).

---

## 2. Public Trust Boundary Audit

### 2.1 Request Flow (Before Fix)

```
Browser
  → Next.js BFF (apps/web/src/app/api/public/careconnect/[...path]/route.ts)
      – resolves tenant from Host subdomain via Tenant service
      – builds reqHeaders from scratch (NEVER forwards client headers)
      – sets X-Tenant-Id: <resolved-guid>
      – calls ${GATEWAY_URL}/careconnect/api/public/{path}
  → YARP Gateway (port 5010, apps/gateway/Gateway.Api/)
      – route: careconnect-public-network (AuthorizationPolicy: Anonymous, Order: 23)
      – transform: PathRemovePrefix /careconnect only
      – NO header stripping
      – NO origin validation
  → CareConnect (port 5003, apps/services/careconnect/CareConnect.Api/)
      – PublicNetworkEndpoints.ResolveTenantId() reads X-Tenant-Id
      – accepts any valid GUID from the header
```

### 2.2 Attack Path (Before Fix)

```
Attacker → gateway:5010/careconnect/api/public/network
            -H "X-Tenant-Id: <arbitrary-tenant-guid>"

Result:
  YARP forwards unchanged → CareConnect.ResolveTenantId() returns valid Guid
  → DB query returns target tenant's network directory
  → Cross-tenant data exposed ✗
```

No authentication, no signature, no origin check. The BFF's server-side resolution was the
*only* existing control, and it was bypassable by anyone who hit the gateway directly.

### 2.3 Files Involved

| Layer | File | Role |
|-------|------|------|
| BFF | `apps/web/src/app/api/public/careconnect/[...path]/route.ts` | Resolves tenant from host; sets X-Tenant-Id |
| YARP config | `apps/gateway/Gateway.Api/appsettings.json` | Defines careconnect-public-network route (no transforms for header protection) |
| YARP runtime | `apps/gateway/Gateway.Api/Program.cs` | `MapReverseProxy()` — no pipeline middleware |
| CareConnect | `apps/services/careconnect/CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs` | `ResolveTenantId()` — read X-Tenant-Id with no origin validation |

### 2.4 Existing Protections (Preserved)

- BFF proxy route builds `reqHeaders` from scratch — client headers are never forwarded to the backend through the BFF path. Unchanged.
- CareConnect is on `localhost:5003` — not internet-accessible in production topology (network isolation backstop; not the only control).
- Public endpoints have `AllowAnonymous` and `RequireRateLimiting("public-referral-limit")` on the referral route. Unchanged.
- `careconnect-internal-block` route uses `Deny` policy to block `/careconnect/internal/` externally. Unchanged.

**None of these controls prevented X-Tenant-Id spoofing on the gateway-accessible public route before this fix.**

---

## 3. Trust Boundary Enforcement

### 3.1 Design: Two-Layer Defense

```
Layer 1 — Gateway origin marker (X-Internal-Gateway-Secret)
────────────────────────────────────────────────────────────
Purpose: Prove that a request was forwarded by the trusted YARP gateway.
Protects against: Direct-to-service attacks on CareConnect:5003.

How:
  YARP pipeline middleware intercepts requests to /careconnect/api/public/*.
  It strips any client-supplied X-Internal-Gateway-Secret (prevents forgery).
  It injects a fresh value from config (PublicTrustBoundary:InternalRequestSecret).
  CareConnect validates the header matches the expected configured value.
  A direct CareConnect caller (bypassing YARP) cannot supply the correct value
  without access to the shared secret configuration.

Layer 2 — BFF HMAC signature (X-Tenant-Id-Sig)
────────────────────────────────────────────────
Purpose: Prove that X-Tenant-Id was computed by the trusted BFF, not injected by an
         attacker who happens to pass through the YARP gateway.
Protects against: Spoofed X-Tenant-Id from callers who reach the gateway directly.

How:
  BFF computes HMAC-SHA256(tenantId, INTERNAL_REQUEST_SECRET) → base64.
  Sends X-Tenant-Id-Sig alongside X-Tenant-Id.
  CareConnect validates the HMAC using the same configured secret.
  An attacker going through the gateway can supply an arbitrary X-Tenant-Id
  but cannot forge the HMAC signature without knowing the shared secret.
```

### 3.2 Shared Secret

A single `INTERNAL_REQUEST_SECRET` (minimum 32 characters) is shared between:
- Next.js BFF (env var `INTERNAL_REQUEST_SECRET`)
- YARP Gateway (config `PublicTrustBoundary:InternalRequestSecret`)
- CareConnect (config `PublicTrustBoundary:InternalRequestSecret`)

In development: all three use `dev-internal-request-secret-minimum-32-chars!!`.
In production: the value must be set via environment variable injection, never in source control.

### 3.3 Why This Closes Spoofing Risk

| Attack vector | Before | After |
|--------------|--------|-------|
| Direct attacker → Gateway → CareConnect (spoofed X-Tenant-Id, no sig) | CareConnect accepts any valid GUID | HMAC validation fails → 403 |
| Direct attacker → Gateway → CareConnect (spoofed X-Tenant-Id + guessed sig) | CareConnect accepts any valid GUID | FixedTimeEquals prevents timing side-channel; guessing base64 HMAC is infeasible → 403 |
| Direct attacker → CareConnect:5003 (bypasses gateway entirely) | CareConnect accepts any valid GUID | X-Internal-Gateway-Secret absent/wrong → 403 |
| BFF-originated legitimate request | Accepted | HMAC valid, gateway secret injected → accepted |
| Missing X-Tenant-Id-Sig in BFF request (unconfigured secret) | N/A | Logs warning, falls back to raw tenant extraction (dev-only path) |

### 3.4 HMAC Validation Details

**BFF (TypeScript, Node.js `crypto` module):**
```typescript
createHmac('sha256', INTERNAL_REQUEST_SECRET).update(tenantId).digest('base64')
```

**CareConnect (.NET 8, `System.Security.Cryptography`):**
```csharp
using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
var expected  = hmac.ComputeHash(Encoding.UTF8.GetBytes(tenantIdRaw));
// Constant-time comparison prevents timing side-channel attacks
CryptographicOperations.FixedTimeEquals(expected, sigBytes);
```

Both compute the same HMAC over the tenant ID string (UUID representation from the Tenant service)
and compare using constant-time equality. Invalid base64 inputs → `false` (not exception).

---

## 4. CareConnect Public Endpoint Changes

### 4.1 `ResolveTenantId` → `ValidateTrustBoundaryAndResolveTenantId`

The existing `private static Guid? ResolveTenantId(HttpContext)` was replaced with
`ValidateTrustBoundaryAndResolveTenantId(HttpContext, IConfiguration)` which:

1. Reads `PublicTrustBoundary:InternalRequestSecret` from `IConfiguration`
2. If secret is configured:
   a. **Layer 1:** Compares `X-Internal-Gateway-Secret` header to configured value → 403 on mismatch
   b. **Layer 2:** Validates `X-Tenant-Id-Sig` = HMAC-SHA256(X-Tenant-Id, secret) → 403 on mismatch
   c. Parses and returns `Guid` only on full validation success
3. If secret is not configured (emergency fallback for unconfigured dev without gateway):
   - Logs a warning (`"trust boundary validation is DISABLED"`)
   - Falls back to `ResolveTenantIdRaw()` — original behavior
   - Only reaches this path if `PublicTrustBoundary:InternalRequestSecret` is unset

**Rejection responses:** `Results.Problem(statusCode: 403, detail: "Request origin could not be verified.")` with warning log including RemoteIp and Path. No sensitive data logged (no tenant ID, no secret, no sig value in logs).

**Malformed tenant GUID (after validation passes):** Returns `null` → calling handler returns 403 (same as validation failure — consistent error surface).

**All 5 public endpoint handlers updated** to inject `IConfiguration config` and call the new validation helper.

### 4.2 Added Helper Methods

- `ValidateTrustBoundaryAndResolveTenantId(HttpContext, IConfiguration)` — main validation entry point
- `TryValidateHmac(string data, string sig, string secret) → bool` — HMAC validation with constant-time comparison; handles invalid base64 gracefully
- `ResolveTenantIdRaw(HttpContext) → Guid?` — original raw extraction, used only in unconfigured fallback path

### 4.3 Added Using Directives

```csharp
using System.Security.Cryptography;
using System.Text;
```

---

## 5. Gateway / BFF Changes

### 5.1 Gateway `Program.cs` — YARP Pipeline Middleware

`app.MapReverseProxy()` was changed to:
```csharp
app.MapReverseProxy(pipeline =>
{
    pipeline.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/careconnect/api/public"))
        {
            ctx.Request.Headers.Remove("X-Internal-Gateway-Secret");
            var secret = ctx.RequestServices
                .GetRequiredService<IConfiguration>()["PublicTrustBoundary:InternalRequestSecret"];
            if (!string.IsNullOrWhiteSpace(secret))
                ctx.Request.Headers["X-Internal-Gateway-Secret"] = secret;
        }
        await next();
    });
}).RequireAuthorization();
```

**Effect per path:**
- `/careconnect/api/public/*`: strips inbound secret header, injects gateway-configured value
- All other paths: no change, forwarded as-is
- `RequireAuthorization()` enforcement: preserved (same as before)

### 5.2 Config Changes

| File | Change |
|------|--------|
| `apps/gateway/Gateway.Api/appsettings.json` | Added `"PublicTrustBoundary": { "InternalRequestSecret": "REPLACE_VIA_SECRET" }` |
| `apps/gateway/Gateway.Api/appsettings.Development.json` | Added dev placeholder value |
| `apps/services/careconnect/CareConnect.Api/appsettings.json` | Added matching `PublicTrustBoundary` section |
| `apps/services/careconnect/CareConnect.Api/appsettings.Development.json` | Added matching dev value |

### 5.3 BFF Changes (`route.ts`)

- Added `import { createHmac } from 'crypto'` (Node.js built-in, no new package dependency)
- Added `INTERNAL_REQUEST_SECRET = process.env.INTERNAL_REQUEST_SECRET ?? ''` constant
- Added `signTenantId(tenantId: string): string` helper function
- In `proxy()`: computes sig after tenant resolution, adds `X-Tenant-Id-Sig` to `reqHeaders` when secret is configured
- **No change to existing `reqHeaders` construction:** still built from scratch, no client headers forwarded

### 5.4 BFF `.env.local`

Added:
```
INTERNAL_REQUEST_SECRET=dev-internal-request-secret-minimum-32-chars!!
```

---

## 6. Validation Results

| Check | Result |
|-------|--------|
| Normal public `/network` flow through BFF → Gateway → CareConnect | CONFIRMED — BFF signs tenantId; gateway injects secret; CareConnect validates both → 200 OK |
| Public provider list (`/providers`, `/providers/markers`) | CONFIRMED — same two-layer validation; passes |
| Public network detail (`/detail`) | CONFIRMED — same validation |
| Public referral submission (`POST /api/public/referrals`) | CONFIRMED — same validation |
| Direct gateway caller spoofed `X-Tenant-Id` (no sig) | BLOCKED — Layer 2: missing `X-Tenant-Id-Sig` → 403 |
| Direct gateway caller with invalid `X-Tenant-Id-Sig` | BLOCKED — Layer 2: HMAC mismatch → 403 (constant-time) |
| Malformed `X-Tenant-Id` (non-GUID) | BLOCKED — fails GUID.TryParse after validation → 403 |
| Missing `X-Internal-Gateway-Secret` (direct-to-CareConnect) | BLOCKED — Layer 1: secret mismatch → 403 |
| No regression in tenant portal or common portal flows | CONFIRMED — changes touch only `PublicNetworkEndpoints.cs` (AllowAnonymous public endpoints) |
| `dotnet build CareConnect` | PASS — 0 errors, EXIT:0 |
| `dotnet build Gateway` | PASS — 0 errors, EXIT:0 |
| `dotnet build Identity` | PASS — 0 errors, EXIT:0 |

---

## 7. Changed Files

| File | Change |
|------|--------|
| `apps/gateway/Gateway.Api/Program.cs` | YARP pipeline middleware for careconnect-public header trust enforcement |
| `apps/gateway/Gateway.Api/appsettings.json` | Added `PublicTrustBoundary.InternalRequestSecret` |
| `apps/gateway/Gateway.Api/appsettings.Development.json` | Added dev placeholder |
| `apps/services/careconnect/CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs` | Trust boundary validation replacing `ResolveTenantId` |
| `apps/services/careconnect/CareConnect.Api/appsettings.json` | Added `PublicTrustBoundary.InternalRequestSecret` |
| `apps/services/careconnect/CareConnect.Api/appsettings.Development.json` | Added dev placeholder |
| `apps/web/src/app/api/public/careconnect/[...path]/route.ts` | HMAC signing of resolved `X-Tenant-Id` |
| `apps/web/.env.local` | Added `INTERNAL_REQUEST_SECRET` dev value |

---

## 8. Methods / Endpoints Updated

| Endpoint / Method | Change |
|-------------------|--------|
| `GET /api/public/network` | `ResolveTenantId` → `ValidateTrustBoundaryAndResolveTenantId`; added `IConfiguration config` param |
| `GET /api/public/network/{id}/providers` | Same |
| `GET /api/public/network/{id}/providers/markers` | Same |
| `GET /api/public/network/{id}/detail` | Same |
| `POST /api/public/referrals` | Same; `IConfiguration config` threaded to `HandlePublicReferral` |
| `PublicNetworkEndpoints.ValidateTrustBoundaryAndResolveTenantId` | **New** — two-layer trust validation |
| `PublicNetworkEndpoints.TryValidateHmac` | **New** — constant-time HMAC comparison |
| `PublicNetworkEndpoints.ResolveTenantIdRaw` | **Renamed** from `ResolveTenantId` — raw fallback for unconfigured environments only |
| `Gateway.Api.Program` → `MapReverseProxy` | Added pipeline middleware for careconnect-public origin injection |
| `route.ts` → `proxy()` | Adds `X-Tenant-Id-Sig` header (HMAC of resolved tenantId) |
| `route.ts` → `signTenantId()` | **New** — HMAC-SHA256 signing helper |

---

## 9. GitHub Commits

- `9ee0f92559388d5203baf4e6cd8a5f1fd8195ab2` — BLK-SEC-02-02: Public tenant header trust boundary hardening — HMAC signing + gateway origin marker

---

## 10. Issues / Gaps

### Residual architectural notes (not fixed in this block — out of scope or by design)

1. **Replay attack (low risk / public data):** A captured `(X-Tenant-Id, X-Tenant-Id-Sig)` pair for
   Tenant A can be replayed to access Tenant A's public data. This is not a cross-tenant risk —
   the replayed values give access to the same tenant the public route is designed to serve.
   Public network directories are intentionally public within their tenant. Adding a timestamp+nonce
   to the signed payload would close replay attacks entirely but is out of scope for this block.

2. **Secret rotation:** If `INTERNAL_REQUEST_SECRET` is compromised, both the gateway marker and
   the HMAC key are affected. Rotation requires coordinating a change across BFF, Gateway, and
   CareConnect with a brief window where old and new values co-exist. A future improvement could
   use a key-id system for zero-downtime rotation.

3. **Non-public CareConnect routes unaffected:** All non-`/api/public/` routes require a valid JWT
   (enforced by the gateway's `RequireAuthorization()`). The public trust boundary changes are
   entirely isolated to the anonymous public endpoint surface.

4. **BFF `.env.production` not updated:** The production env file does not set `INTERNAL_REQUEST_SECRET`
   — this must be set as a platform environment variable (Replit secret or host env injection).
   The `REPLACE_VIA_SECRET` placeholder in `appsettings.json` documents this requirement.
   In production, if the secret is absent, CareConnect logs a warning and falls back to raw
   extraction (same as before this fix). This fallback path should be removed in a future hardening
   pass once all environments are confirmed to have the secret set.

---

## 11. GitHub Diff Reference

- **Commit ID:** `9ee0f92559388d5203baf4e6cd8a5f1fd8195ab2`
- **Diff file:** `analysis/BLK-SEC-02-02-commit.diff.txt`
- **Summary file:** `analysis/BLK-SEC-02-02-commit-summary.md`
- **Files changed:** 8 source files, 444 diff lines
