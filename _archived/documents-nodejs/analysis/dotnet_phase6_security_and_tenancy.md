# .NET Documents Service — Phase 6: Security and Tenancy

**Date**: 2026-03-29

---

## 1. Threat Model Summary

The Documents Service manages potentially PHI-containing files. The key threat categories addressed are:

| Threat | Control |
|--------|---------|
| Unauthorized access across tenants | Three-layer tenant isolation |
| Privilege escalation within tenant | RBAC permission matrix |
| Malware delivery via upload | File scan gate |
| File access without authentication | Opaque token mediation |
| Token replay / sharing | One-time-use enforcement |
| Storage key disclosure | Storage keys excluded from all responses |
| Brute-force token guessing | 256-bit entropy (64 hex chars from 32 random bytes) |
| Retention bypass | `RetainUntil` and `LegalHoldAt` enforced in delete path |
| Information leakage via errors | Generic error messages for auth failures |
| Cross-site request forgery | Stateless Bearer auth, no cookies |
| Rate abuse | Fixed-window rate limiting per policy |

---

## 2. Three-Layer Tenant Isolation (Detail)

### Layer 1 — Pre-query Guard

```csharp
private static void RequireTenantId(Guid tenantId)
{
    if (tenantId == Guid.Empty)
        throw new TenantIsolationException();
}
```

Called at the top of every repository method. Prevents any database query if the tenant context was not properly extracted from the JWT.

### Layer 2 — SQL Predicate

Every EF LINQ query includes `WHERE tenant_id = :tenantId`:

```csharp
_db.Documents.Where(d => d.Id == id && d.TenantId == tenantId)
```

Even if Layer 1 were bypassed (e.g., `Guid.Empty` scenario), the SQL predicate would return zero rows for an empty tenant ID because no document has `tenant_id = '00000000-...'`.

EF Core global query filters (`HasQueryFilter(d => !d.IsDeleted)`) provide a third SQL-level guard for soft-deleted documents.

### Layer 3 — ABAC

```csharp
private async Task AssertDocumentTenantScopeAsync(RequestContext ctx, Document doc)
{
    if (doc.TenantId == ctx.Principal.TenantId) return;  // same tenant — OK

    if (!ctx.Principal.IsPlatformAdmin)
    {
        await _audit.LogAsync(AuditEvent.TenantIsolationViolation, ctx, doc.Id, outcome: "DENIED");
        throw new TenantIsolationException();
    }

    // PlatformAdmin cross-tenant — allow + audit
    await _audit.LogAsync(AuditEvent.AdminCrossTenantAccess, ctx, doc.Id);
}
```

This catches scenarios where a tenant-A JWT somehow reaches a tenant-B document (e.g., ID enumeration after a Layer 2 failure). Every cross-tenant access attempt — both violations and legitimate PlatformAdmin access — is audited.

---

## 3. RBAC Enforcement

Permissions are checked **before** any database call or file operation. The check uses `ctx.Principal.Roles` (multi-value, union semantics):

```csharp
var hasPermission = principal.Roles.Any(role =>
    Permissions.TryGetValue(role, out var perms) && perms.Contains(action));
```

Unknown roles grant no permissions (implicit deny). This avoids role-permission drift from future role additions.

---

## 4. File Scan Security

### Upload flow
1. File stream is passed to `ScanService.ScanAsync()` before being uploaded to storage.
2. If `ScanStatus.Infected` → `InfectedFileException` (HTTP 422) — file is never stored.
3. Stream is rewound (if seekable) before storage upload.
4. Scan result is persisted on both the `Document` and `DocumentVersion`.

### Access flow
1. `ScanService.EnforceCleanScan(doc)` checks for `INFECTED` status — throws `ScanBlockedException` (HTTP 403).
2. If `Documents:RequireCleanScanForAccess=true`, also blocks `PENDING` and `FAILED` status.
3. Audit event `SCAN_ACCESS_DENIED` is logged on blocked access (via TypeScript service convention — surfaced in the token service).

### Configuration hygiene
- `Scanner:Provider=none` means scans are skipped (status becomes `SKIPPED`, not `CLEAN`).
- Files with `SKIPPED` scan status pass the `EnforceCleanScan` check (not considered infected).
- For HIPAA environments, `RequireCleanScanForAccess=true` + a real scanner provider should be configured.

---

## 5. Access Token Security

### Token generation
```csharp
var tokenBytes = new byte[32];
RandomNumberGenerator.Fill(tokenBytes);  // Cryptographically secure
var tokenString = Convert.ToHexString(tokenBytes).ToLowerInvariant(); // 64 chars
```

256-bit entropy. Guessing probability: 1/2^256 ≈ 8.6 × 10^-78.

### Token format validation
Before any database lookup, the `AccessEndpoints` handler validates:
- Length exactly 64 characters
- All characters are lowercase hex (0-9, a-f)

This prevents timing oracle attacks from malformed inputs and provides fast rejection.

### One-time-use
`IAccessTokenStore.MarkUsedAsync()` is atomic:
- **Redis**: Server-side Lua script (no TOCTOU race)
- **In-memory**: Not fully atomic under concurrent access (documented limitation for dev use)

### TTL enforcement
Both `ExpiresAt` (application-level) and Redis key TTL (infrastructure-level) enforce expiry. The application check happens first:

```csharp
if (token is null || token.IsExpired)
    throw new TokenExpiredException("Access token has expired");
```

---

## 6. Storage Key Protection

The `DocumentResponse.From(doc)` factory method explicitly **omits**:
- `StorageKey`
- `StorageBucket`
- `Checksum`

These fields exist on the `Document` entity but are not mapped to the DTO. This is enforced at the application layer, not the infrastructure layer — the storage URL is only accessible via the token redemption flow (`/access/{token}` → 302 → signed URL).

---

## 7. HIPAA Alignment Notes

| HIPAA Safeguard | Implementation |
|----------------|---------------|
| Access controls | JWT + RBAC (roles from identity provider) |
| Audit controls | Append-only audit log, all access events recorded |
| Transmission security | HTTPS enforced in production (`RequireHttpsMetadata=true` when using JWKS) |
| Encryption at rest | S3 server-side encryption (AES-256) |
| Automatic logoff | Access tokens expire (TTL configurable) |
| Tenant isolation | Three-layer isolation prevents cross-tenant PHI access |
| Legal hold | `LegalHoldAt` timestamp, delete blocked while set |
| Retention | `RetainUntil` field — enforcement at delete is in domain |

**Gap**: Document destruction (purge after retention expiry) is not implemented — requires a background job outside this service scope.

---

## 8. Security Headers

Not explicitly configured in this service — expected to be handled by the API Gateway (port 5000). Gateway should add:
- `Strict-Transport-Security`
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Content-Security-Policy`

---

## Grade: Phase 6 complete. Proceed to Phase 7 (Parity Review).
