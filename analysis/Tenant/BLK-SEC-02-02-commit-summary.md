# Commit Summary — BLK-SEC-02-02

## Commit ID
`9ee0f92559388d5203baf4e6cd8a5f1fd8195ab2`

## Commit Message
`[BLK-SEC-02-02] Public tenant header trust boundary hardening — HMAC signing + gateway origin marker`

## Diff File
`analysis/BLK-SEC-02-02-commit.diff.txt` (444 lines)

---

## Files Changed: 8

| # | File | Change |
|---|------|--------|
| 1 | `apps/gateway/Gateway.Api/Program.cs` | YARP pipeline middleware: strips inbound `X-Internal-Gateway-Secret`; injects gateway-configured value for `careconnect/api/public/*` paths |
| 2 | `apps/gateway/Gateway.Api/appsettings.json` | Added `PublicTrustBoundary.InternalRequestSecret: REPLACE_VIA_SECRET` |
| 3 | `apps/gateway/Gateway.Api/appsettings.Development.json` | Added dev placeholder value |
| 4 | `apps/services/careconnect/CareConnect.Api/Endpoints/PublicNetworkEndpoints.cs` | `ResolveTenantId()` replaced with `ValidateTrustBoundaryAndResolveTenantId()` — validates `X-Internal-Gateway-Secret` (Layer 1) + HMAC signature of `X-Tenant-Id` (Layer 2) using `CryptographicOperations.FixedTimeEquals` |
| 5 | `apps/services/careconnect/CareConnect.Api/appsettings.json` | Added matching `PublicTrustBoundary.InternalRequestSecret: REPLACE_VIA_SECRET` |
| 6 | `apps/services/careconnect/CareConnect.Api/appsettings.Development.json` | Added matching dev placeholder |
| 7 | `apps/web/src/app/api/public/careconnect/[...path]/route.ts` | Added HMAC-SHA256 signing of resolved `X-Tenant-Id`; sends `X-Tenant-Id-Sig` alongside `X-Tenant-Id`; added `signTenantId()` helper |
| 8 | `apps/web/.env.local` | Added `INTERNAL_REQUEST_SECRET` dev value |

---

## Key Changes

### Trust Boundary Enforcement: Two-Layer Defense

**Layer 1 — Gateway origin marker:**
- YARP `Program.cs`: new pipeline middleware strips client-supplied `X-Internal-Gateway-Secret`, injects real value from `PublicTrustBoundary:InternalRequestSecret` config
- CareConnect validates `X-Internal-Gateway-Secret` before accepting any public request
- Blocks: direct-to-service bypass of CareConnect on port 5003

**Layer 2 — BFF HMAC signature:**
- BFF (`route.ts`) computes `HMAC-SHA256(tenantId, INTERNAL_REQUEST_SECRET)` and sends as `X-Tenant-Id-Sig`
- CareConnect validates HMAC using same configured secret with constant-time comparison
- Blocks: `X-Tenant-Id` spoofing by direct gateway callers who lack the HMAC secret

### No Client Header Trust Added
- BFF `reqHeaders` still built entirely from scratch (unchanged)
- No client headers forwarded at any layer
- `X-Tenant-Id` still derived server-side from host subdomain resolution (unchanged)

### Fallback Path (Unconfigured Secret)
If `PublicTrustBoundary:InternalRequestSecret` is not set:
- CareConnect logs a `LogWarning("trust boundary validation is DISABLED ...")`
- Falls back to raw `X-Tenant-Id` extraction (same as pre-fix behavior)
- This path should only be reached in local dev without the full gateway stack

---

## New Methods

| Method | File | Description |
|--------|------|-------------|
| `ValidateTrustBoundaryAndResolveTenantId(HttpContext, IConfiguration)` | `PublicNetworkEndpoints.cs` | Two-layer trust validation; returns `Guid?` on success, `null` on failure |
| `TryValidateHmac(string, string, string)` | `PublicNetworkEndpoints.cs` | HMAC-SHA256 constant-time comparison; handles invalid base64 gracefully |
| `ResolveTenantIdRaw(HttpContext)` | `PublicNetworkEndpoints.cs` | Renamed from `ResolveTenantId`; raw fallback for unconfigured dev only |
| `signTenantId(tenantId: string)` | `route.ts` | HMAC-SHA256 signing of tenantId using Node.js `crypto` |

---

## Build Verification

| Service | Result |
|---------|--------|
| CareConnect | PASS — 0 errors, EXIT:0 |
| Gateway | PASS — 0 errors, EXIT:0 |
| Identity | PASS — 0 errors, EXIT:0 |

---

## Attack Surfaces Closed

| Attack | Before | After |
|--------|--------|-------|
| Direct gateway caller with arbitrary `X-Tenant-Id` | Accepted (any valid GUID) | 403 — HMAC sig missing/invalid |
| Direct-to-CareConnect caller (bypasses gateway) | Accepted (any valid GUID) | 403 — gateway secret absent/wrong |
| Malformed `X-Tenant-Id` (non-GUID, after validation) | BadRequest | 403 — consistent with validation failure |
| Legitimate BFF request | Accepted | Accepted (HMAC valid, gateway secret injected by YARP) |
