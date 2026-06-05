# .NET Documents Service — Phase 5: Infrastructure Layer

**Date**: 2026-03-29

---

## 1. Database Infrastructure

### 1.1 `DocsDbContext`

EF Core 8 + Npgsql. Key configuration decisions:

**Enum-to-string conversion**: Both `DocumentStatus` and `ScanStatus` are stored as VARCHAR strings (matching the TypeScript schema's string columns). `EnumToStringConverter<T>` is registered per property.

**JSONB columns**: `scan_threats` (both `documents` and `document_versions`) and `detail` (document_audits) use PostgreSQL's native JSONB type. Value converters serialize `List<string>` ↔ JSON string. EF Core marks the column type as `"jsonb"` to ensure the Npgsql driver uses binary JSONB protocol.

**Global query filter**: Documents have `.HasQueryFilter(d => !d.IsDeleted)` — soft-deleted records are never returned by standard queries. This is consistent with the TypeScript repository's `WHERE is_deleted = false` predicate.

**Indexes**:
- `idx_documents_tenant` — tenant scan
- `idx_documents_product` — tenant + product lookup
- `idx_documents_reference` — tenant + reference ID lookup
- `idx_versions_document` — version listing per document
- `idx_audits_document` / `idx_audits_tenant` — audit queries

### 1.2 Repositories — Three-Layer Tenant Isolation

Every repository method receives an explicit `tenantId` parameter. The three layers are:

```
L1: RequireTenantId(tenantId) — throws TenantIsolationException if Guid.Empty
L2: .Where(d => d.TenantId == tenantId) — SQL predicate
L3: AssertDocumentTenantScopeAsync() — cross-tenant ABAC (in DocumentService)
```

`ExecuteUpdateAsync` (EF Core 8 bulk update) is used for `SoftDeleteAsync` and `UpdateScanStatusAsync` to avoid loading the full entity into memory for status-only updates. This is important for scan webhook callbacks that may run at high volume.

### 1.3 Schema

`schema.sql` provides the complete DDL as an alternative to EF Core migrations:
- `pgcrypto` extension for `gen_random_uuid()`
- `JSONB` columns with `NOT NULL DEFAULT '[]'`
- `CHECK` constraints for enum values
- Partial indexes with `WHERE NOT is_deleted`
- `UNIQUE(document_id, version_number)` constraint on versions

---

## 2. Storage Infrastructure

### 2.1 `LocalStorageProvider`

Development-only. Stores files in `Storage:Local:BasePath` (default `/tmp/docs-local`). Generates redirect tokens (32-char random UUID) stored in an in-memory dictionary — not suitable for multi-replica deployments.

Includes a `ResolveToken(token)` method for the `/internal/files?token=...` dev endpoint in `Program.cs`. Expired tokens are cleaned up on resolution.

### 2.2 `S3StorageProvider`

Production storage. Key features:
- Uses `AmazonS3Client` with instance role credentials if `AccessKeyId` is not set.
- Server-side encryption: `ServerSideEncryptionMethod.AES256` on all uploads.
- Pre-signed URLs: configures `Content-Disposition` based on `disposition` parameter (`"inline"` for view, `"attachment; filename=..."` for download).
- Implements `IAsyncDisposable` to properly dispose the `IAmazonS3` client.

### 2.3 `StorageProviderFactory`

Simple factory function registered in `DependencyInjection.cs`:

```csharp
services.AddSingleton<IStorageProvider>(sp =>
    StorageProviderFactory.Create(storageProvider, sp));
```

Both providers are registered as singletons — the factory resolves the correct one based on `Storage:Provider` config.

---

## 3. File Scanner Infrastructure

### 3.1 `NullScannerProvider` (`Scanner:Provider=none`)

All files pass with `ScanStatus.Skipped`. Used in environments without an AV solution. Appropriate for development and for tenants using client-side virus scanning.

### 3.2 `MockScannerProvider` (`Scanner:Provider=mock`)

Configurable result via `Scanner:Mock:MockResult` (`clean` | `infected` | `failed`). Used in integration testing to simulate infected file scenarios without a real AV engine.

> **Extension point**: A production ClamAV provider would implement `IFileScannerProvider` and be registered as `Scanner:Provider=clamav`. This is the expected path for HIPAA-aligned environments requiring active malware scanning.

---

## 4. Access Token Store Infrastructure

### 4.1 `InMemoryAccessTokenStore` (`AccessToken:Store=memory`)

`ConcurrentDictionary<string, AccessToken>`. Suitable for single-replica development. The `MarkUsedAsync` implementation is not perfectly atomic under high concurrency (not lock-free CAS) — this is an accepted trade-off for the in-memory case.

### 4.2 `RedisAccessTokenStore` (`AccessToken:Store=redis`)

Uses `StackExchange.Redis`. Key features:
- Keys are prefixed `access_token:` with TTL set to `token.ExpiresAt - now`.
- `MarkUsedAsync` uses a **Lua script** evaluated server-side to achieve atomic read-modify-write without TOCTOU race conditions. The script:
  1. GETs the token JSON.
  2. If `IsUsed` is already true → returns 0 (already used).
  3. Sets `IsUsed = true`, re-serializes, and re-SETs with the original TTL.
  4. Returns 1 (success).
- Returns -1 if key not found (expired or never existed).

This is the critical guarantee for one-time-use tokens in production.

---

## 5. Auth Infrastructure

### 5.1 `JwtPrincipalExtractor`

Stateless utility class (not a service). Called in each endpoint handler:

```csharp
var principal = JwtPrincipalExtractor.Extract(ctx.User);
```

Claim name fallback order:
- User ID: `sub` → `userId` → `NameIdentifier`
- Tenant ID: `tenantId` → `tenant_id`
- Roles: `roles` + `role` + `ClaimTypes.Role` (union of all)

Throws `UnauthorizedAccessException` (→ HTTP 401) for:
- Missing `sub` claim
- Missing or non-UUID `tenantId` claim

---

## 6. Dependency Injection

`DependencyInjection.AddInfrastructure()` wires:

| Service | Lifetime | Implementation |
|---------|----------|---------------|
| `DocsDbContext` | Scoped | Npgsql + EF Core |
| `IDocumentRepository` | Scoped | `DocumentRepository` |
| `IDocumentVersionRepository` | Scoped | `DocumentVersionRepository` |
| `IAuditRepository` | Scoped | `AuditRepository` |
| `IStorageProvider` | Singleton | `LocalStorageProvider` or `S3StorageProvider` |
| `IFileScannerProvider` | Singleton | `NullScannerProvider` or `MockScannerProvider` |
| `IAccessTokenStore` | Singleton | `InMemoryAccessTokenStore` or `RedisAccessTokenStore` |
| `IConnectionMultiplexer` | Singleton | Redis connection (when `redis` store) |
| `ScanService` | Scoped | Application service |
| `AuditService` | Scoped | Application service |
| `DocumentService` | Scoped | Application service |
| `AccessTokenService` | Scoped | Application service |

---

## Grade: Phase 5 complete. Proceed to Phase 6 (Security and Tenancy).
