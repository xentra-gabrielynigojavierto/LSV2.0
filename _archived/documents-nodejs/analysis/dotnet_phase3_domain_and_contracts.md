# .NET Documents Service — Phase 3: Domain and Contracts

**Date**: 2026-03-29

---

## 1. Domain Layer Overview

The Domain layer (`Documents.Domain`) is a pure C# project with zero external NuGet dependencies. It defines all business types, rules, and interface contracts.

---

## 2. Entities

### 2.1 `Document` (aggregate root)

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | PK, maps to `id UUID` |
| `TenantId` | `Guid` | Partition key — never nullable |
| `ProductId` | `string` | Product scope |
| `ReferenceId` | `string` | External entity reference |
| `ReferenceType` | `string` | e.g., "Case", "Claim" |
| `DocumentTypeId` | `Guid` | FK to document type registry |
| `Title` | `string` | max 500 |
| `Description` | `string?` | max 2000 |
| `Status` | `DocumentStatus` | enum-to-string via EF converter |
| `MimeType` | `string` | validated against allow-list |
| `FileSizeBytes` | `long` | |
| `StorageKey` | `string` | **never in API responses** |
| `StorageBucket` | `string` | **never in API responses** |
| `Checksum` | `string?` | optional SHA-256 |
| `ScanStatus` | `ScanStatus` | Pending / Clean / Infected / Failed / Skipped |
| `ScanThreats` | `List<string>` | stored as JSONB |
| `LegalHoldAt` | `DateTime?` | timestamp of hold imposition |
| `RetainUntil` | `DateTime?` | compliance retention deadline |
| `IsDeleted` | `bool` | soft-delete |

**Factory method**: `Document.Create(...)` sets all creation defaults. No public constructor parameter mutation.

**Domain invariant**: `doc.IsOnLegalHold => doc.LegalHoldAt.HasValue` — cannot be deleted while on legal hold.

### 2.2 `DocumentVersion`

Each version has its own `StorageKey`, `ScanStatus`, and `ScanThreats`. When a new version is uploaded, the parent `Document.CurrentVersionId` and `Document.VersionCount` are updated.

### 2.3 `DocumentAudit`

Append-only audit log. The `Detail` column is a free-form JSON string (JSONB in PostgreSQL), serialized from `object?` by `AuditService`. The `DocumentAudit` entity does not reference the full audit payload as a typed object — it stores raw JSON to avoid coupling.

---

## 3. Enums

### `DocumentStatus`
```
Draft | Active | Archived | Deleted | LegalHold
```
Stored as string in database (EF `EnumToStringConverter<DocumentStatus>`). TypeScript uses SCREAMING_SNAKE_CASE; .NET uses PascalCase internally and converts on the DTO boundary.

### `ScanStatus`
```
Pending | Clean | Infected | Failed | Skipped
```

### `AuditEvent` (static constants class)
Not an enum — strings are used as event names to match the TypeScript service's event catalog. 19 constants covering document lifecycle, scan lifecycle, token lifecycle, and security events.

---

## 4. Interfaces

### `IDocumentRepository`
- `FindByIdAsync(id, tenantId)` — always requires tenantId (L1 guard in implementation)
- `ListAsync(DocumentFilter)` — filter carries tenantId
- `CreateAsync / UpdateAsync`
- `SoftDeleteAsync` — sets `is_deleted=true`, `status=DELETED`
- `UpdateScanStatusAsync` — atomic partial update (no full entity reload required)

### `IDocumentVersionRepository`
- `ListByDocumentAsync(documentId, tenantId)`
- `CreateAsync`
- `UpdateScanStatusAsync`

### `IAuditRepository`
- `InsertAsync` — fire-and-forget, never throws to caller
- `ListForDocumentAsync` — for audit trail endpoint (future)

### `IStorageProvider`
- `UploadAsync(key, stream, mimeType)` — returns bucket name
- `GenerateSignedUrlAsync(key, ttlSeconds, disposition)` — returns opaque URL
- `DeleteAsync / ExistsAsync`

### `IFileScannerProvider`
- `ScanAsync(stream, fileName)` — returns `ScanResult { Status, Threats, DurationMs, EngineVersion }`

### `IAccessTokenStore`
- `StoreAsync / GetAsync / MarkUsedAsync / RevokeAsync`
- `MarkUsedAsync` returns `bool` — `true` if this call was the first (atomic one-time-use)

---

## 5. Value Objects

### `AccessToken`
Represents a stored opaque token. Properties: `Token` (64-char hex string), `DocumentId`, `TenantId`, `Type` (view|download), `ExpiresAt`, `IssuedToUserId`, `IsUsed`.

### `Principal`
Extracted from JWT: `UserId`, `TenantId`, `Email`, `Roles[]`. `IsPlatformAdmin` computed from `Roles.Contains("PlatformAdmin")`.

---

## 6. Domain Invariants Enforced

| Invariant | Enforcement point |
|-----------|------------------|
| `TenantId` never empty | `RequireTenantId()` in each repository method |
| Legal hold blocks delete | `if (doc.IsOnLegalHold) throw ForbiddenException` in DocumentService |
| Only allowed MIME types | `ValidateMimeType()` before upload |
| Scan gate before access | `ScanService.EnforceCleanScan()` in AccessTokenService and GetSignedUrlAsync |
| Storage keys never in responses | `DocumentResponse.From()` omits StorageKey/StorageBucket/Checksum |
| Token format: 64 lowercase hex | Validated in `AccessEndpoints` before calling `RedeemAsync` |

---

## Grade: Phase 3 complete. Proceed to Phase 4 (API and Application).
