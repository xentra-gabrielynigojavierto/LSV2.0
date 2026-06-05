# .NET Documents Service — Phase 4: API and Application Layer

**Date**: 2026-03-29

---

## 1. Application Layer

### 1.1 `RequestContext`

The `RequestContext` record is the application-layer equivalent of the TypeScript `RequestContext` / `EffectiveTenantId` resolver:

```csharp
public Guid EffectiveTenantId =>
    Principal.IsPlatformAdmin && TargetTenantId.HasValue
        ? TargetTenantId.Value
        : Principal.TenantId;
```

Platform admins supply the target tenant via `X-Admin-Target-Tenant` header. All other callers always receive their own tenant ID. This is an immutable property — not settable after construction.

### 1.2 `DocumentService`

Core orchestration. Implements all 9 document operations:

| Method | Auth guard | Scan gate | Tenant isolation |
|--------|-----------|-----------|-----------------|
| `CreateAsync` | write | scan before store | L1 TenantId match + ABAC |
| `ListAsync` | read | none | L2 WHERE clause |
| `GetByIdAsync` | read | none | ABAC |
| `UpdateAsync` | write | none | ABAC |
| `DeleteAsync` | delete | none | ABAC + legal hold check |
| `CreateVersionAsync` | write | scan before store | ABAC |
| `ListVersionsAsync` | read | none | ABAC |
| `GetSignedUrlAsync` | read | enforce clean | ABAC |
| `GetContentRedirectAsync` | read | enforce clean | ABAC + audit |

### 1.3 RBAC Permissions Matrix

```csharp
private static readonly Dictionary<string, string[]> Permissions = new()
{
    ["DocReader"]     = new[] { "read" },
    ["DocUploader"]   = new[] { "read", "write" },
    ["DocManager"]    = new[] { "read", "write", "delete" },
    ["TenantAdmin"]   = new[] { "read", "write", "delete" },
    ["PlatformAdmin"] = new[] { "read", "write", "delete", "admin" },
};
```

This matches the TypeScript service's permission map exactly. Multiple roles are unioned (any matching role grants access).

### 1.4 `AccessTokenService`

Issues and redeems opaque 64-char hex tokens:

```csharp
var tokenBytes = new byte[32];
RandomNumberGenerator.Fill(tokenBytes);
var tokenString = Convert.ToHexString(tokenBytes).ToLowerInvariant();
```

One-time-use enforced atomically via `MarkUsedAsync` (Redis Lua / in-memory CAS). Token TTL: configurable via `AccessToken:TtlSeconds` (default 300s). Redirect TTL: `AccessToken:RedirectTtlSeconds` (default 30s).

### 1.5 `ScanService`

- `ScanAsync(stream, fileName)` — calls provider, throws `InfectedFileException` on `ScanStatus.Infected`.
- `EnforceCleanScan(doc/version, requireClean)` — throws `ScanBlockedException` if infected; optionally blocks Pending/Failed if `requireClean=true`.
- After scanning, stream position is reset if seekable.

### 1.6 `AuditService`

Fire-and-forget: catches all exceptions, logs them, never throws to caller. This matches the TypeScript service's "audit must not break happy path" principle.

### 1.7 Exception Hierarchy

```
DocumentsException (abstract)
├── NotFoundException (404, NOT_FOUND)
├── ForbiddenException (403, ACCESS_DENIED)
├── ScanBlockedException (403, SCAN_BLOCKED)
├── InfectedFileException (422, INFECTED_FILE)
├── UnsupportedFileTypeException (422, UNSUPPORTED_FILE_TYPE)
├── TenantIsolationException (403, TENANT_ISOLATION_VIOLATION)
├── TokenExpiredException (401, TOKEN_EXPIRED)
├── TokenInvalidException (401, TOKEN_INVALID)
└── ValidationException (400, VALIDATION_ERROR) — carries Details dict
```

All exceptions carry `StatusCode` (int) and `ErrorCode` (string) for consistent serialization by `ExceptionHandlingMiddleware`.

---

## 2. API Layer

### 2.1 Program.cs Pipeline

```
CorrelationIdMiddleware
→ ExceptionHandlingMiddleware
→ SerilogRequestLogging
→ [Swagger in Development]
→ CORS
→ RateLimiter
→ Authentication (JwtBearer)
→ Authorization
→ Endpoints
```

### 2.2 Minimal API Routing

All document routes live under `MapGroup("/documents").RequireAuthorization()`.  
The `/access/{token}` route uses `.AllowAnonymous()`.  
Health routes use `.AllowAnonymous()`.

### 2.3 Multipart File Handling

File upload endpoints read `IFormFileCollection` via `ctx.Request.ReadFormAsync()`. The `DisableAntiforgery()` call is required for stateless API usage. File streams are passed directly to `DocumentService` — no temp file buffering.

### 2.4 Rate Limiting

Three named policies registered in `AddRateLimiter`:

| Policy | Window | Limit |
|--------|--------|-------|
| `general` | 1 min | 100 req |
| `upload` | 1 min | 10 req |
| `signed-url` | 1 min | 30 req |

On rejection: HTTP 429 with `Retry-After: 60` header and JSON body `{ error: "RATE_LIMIT_EXCEEDED" }`.

### 2.5 JWT Configuration

Supports two modes:

| Mode | Config key | Algorithm | Use case |
|------|-----------|-----------|---------|
| Symmetric | `Jwt:SigningKey` | HS256 | Dev/test only |
| Asymmetric JWKS | `Jwt:JwksUri` | RS256/ES256 | Production |

Startup throws `InvalidOperationException` if neither is configured — no silent fallback to insecure defaults.

### 2.6 Principal Extraction

`JwtPrincipalExtractor.Extract(ClaimsPrincipal)`:
- Reads `sub` / `userId` / `NameIdentifier` for user ID
- Reads `tenantId` / `tenant_id` for tenant ID (throws if missing or non-UUID)
- Reads `roles` / `role` / `ClaimTypes.Role` claims (multiple supported)
- Throws `UnauthorizedAccessException` for missing required claims

### 2.7 Error Response Format

```json
{
  "error": "NOT_FOUND",
  "message": "Document not found: 3fa85f64...",
  "correlationId": "abc123"
}
```

Validation errors add `"details"` with per-field arrays.

### 2.8 Swagger / OpenAPI

Available at `/docs` in Development. Bearer JWT security scheme registered. `IFormFile` mapped as `string/binary` for upload endpoints.

---

## 3. Configuration Reference

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:DocsDb` | (required) | PostgreSQL connection string |
| `Jwt:SigningKey` | dev default | HS256 key (dev only) |
| `Jwt:JwksUri` | empty | JWKS endpoint (production) |
| `Jwt:Issuer` | `legalsynq-identity` | Token issuer |
| `Jwt:Audience` | `legalsynq-platform` | Token audience |
| `Storage:Provider` | `local` | `local` or `s3` |
| `Storage:Local:BasePath` | `/tmp/docs-local` | Local storage root |
| `Storage:S3:BucketName` | `docs-prod` | S3 bucket |
| `Storage:S3:Region` | `us-east-1` | AWS region |
| `Scanner:Provider` | `none` | `none` or `mock` |
| `Scanner:Mock:MockResult` | `clean` | `clean`, `infected`, `failed` |
| `AccessToken:Store` | `memory` | `memory` or `redis` |
| `AccessToken:TtlSeconds` | 300 | Token TTL |
| `AccessToken:OneTimeUse` | true | One-time-use enforcement |
| `Documents:RequireCleanScanForAccess` | false | Block pending/failed scans |
| `Documents:SignedUrlTtlSeconds` | 30 | Redirect URL lifetime |
| `Redis:Url` | (required if redis) | Redis connection URL |
| `Cors:Origins` | `*` | Comma-separated allowed origins |

---

## Grade: Phase 4 complete. Proceed to Phase 5 (Infrastructure).
