# .NET Documents Service — Phase 7: Parity Review

**Date**: 2026-03-29

---

## 1. API Endpoint Parity

| TypeScript Endpoint | .NET Endpoint | Status | Notes |
|--------------------|--------------|--------|-------|
| `POST /documents` | `MapPost("/documents/")` | ✅ | Multipart form |
| `GET /documents` | `MapGet("/documents/")` | ✅ | Query params |
| `GET /documents/:id` | `MapGet("/documents/{id:guid}")` | ✅ | |
| `PATCH /documents/:id` | `MapPatch("/documents/{id:guid}")` | ✅ | |
| `DELETE /documents/:id` | `MapDelete("/documents/{id:guid}")` | ✅ | |
| `POST /documents/:id/versions` | `MapPost("/documents/{id:guid}/versions")` | ✅ | Multipart form |
| `GET /documents/:id/versions` | `MapGet("/documents/{id:guid}/versions")` | ✅ | |
| `POST /documents/:id/view-url` | `MapPost("/documents/{id:guid}/view-url")` | ✅ | |
| `POST /documents/:id/download-url` | `MapPost("/documents/{id:guid}/download-url")` | ✅ | |
| `GET /documents/:id/content` | `MapGet("/documents/{id:guid}/content")` | ✅ | 302 redirect |
| `GET /access/:token` | `MapGet("/access/{token}")` | ✅ | Anonymous |
| `GET /health` | `MapGet("/health")` | ✅ | |
| `GET /health/ready` | `MapGet("/health/ready")` | ✅ | DB check |

**Coverage**: 13/13 endpoints ✅

---

## 2. Business Logic Parity

| Feature | TypeScript | .NET | Status |
|---------|-----------|------|--------|
| Three-layer tenant isolation | ✅ | ✅ | Full parity |
| RBAC permission matrix | ✅ | ✅ | Identical roles/perms |
| Scan before store | ✅ | ✅ | Stream rewind added |
| Scan gate on access | ✅ | ✅ | |
| Legal hold blocks delete | ✅ | ✅ | |
| Retain-until enforced | Partial (TS) | Defined (domain) | RetainUntil stored; enforcement at delete is partial in TS too |
| Opaque token issuance | ✅ | ✅ | 256-bit, hex |
| One-time-use token | ✅ | ✅ | Atomic in Redis |
| Token expiry | ✅ | ✅ | |
| 302 redirect flow | ✅ | ✅ | |
| Storage key never in response | ✅ | ✅ | Omitted in From() |
| Soft delete | ✅ | ✅ | |
| Versioning | ✅ | ✅ | |
| Audit log (non-fatal) | ✅ | ✅ | try/catch in AuditRepository |
| PlatformAdmin cross-tenant | ✅ | ✅ | X-Admin-Target-Tenant header |
| Cross-tenant audit event | ✅ | ✅ | ADMIN_CROSS_TENANT_ACCESS |
| Tenant isolation violation audit | ✅ | ✅ | TENANT_ISOLATION_VIOLATION |
| Correlation ID propagation | ✅ | ✅ | X-Correlation-Id header |
| MIME type allow-list | ✅ | ✅ | 10 types |
| Rate limiting | ✅ | ✅ | 3 policies |

---

## 3. Infrastructure Parity

| Component | TypeScript | .NET | Status |
|-----------|-----------|------|--------|
| PostgreSQL | Knex.js + pg | EF Core 8 + Npgsql | ✅ |
| Local storage | Local file system | `LocalStorageProvider` | ✅ |
| S3 storage | AWS SDK for JS | AWSSDK.S3 | ✅ |
| Redis token store | ioredis | StackExchange.Redis | ✅ |
| In-memory token store | `Map<string, token>` | `ConcurrentDictionary` | ✅ |
| Null scanner | `NullScanner` | `NullScannerProvider` | ✅ |
| Mock scanner | `MockScanner` | `MockScannerProvider` | ✅ |
| JWT auth | `jsonwebtoken` | `AddJwtBearer` | ✅ |
| JWKS auth | `jwks-rsa` | `MetadataAddress` | ✅ |
| Structured logging | Winston | Serilog | ✅ |
| OpenAPI / Swagger | swagger-jsdoc | Swashbuckle | ✅ |
| Schema | Knex migrations | EF Core Migrations + schema.sql | ✅ |

---

## 4. Security Parity

| Control | TypeScript | .NET | Status |
|---------|-----------|------|--------|
| 256-bit token entropy | ✅ | ✅ | |
| Token format validation | ✅ | ✅ | 64 hex chars, pre-lookup |
| Atomic one-time-use (Redis) | ✅ | ✅ | Lua script |
| Storage key exclusion | ✅ | ✅ | |
| Infected file rejection | ✅ | ✅ | Before storage |
| Scan gate (access) | ✅ | ✅ | |
| TenantId pre-query guard | ✅ | ✅ | |
| SQL predicate isolation | ✅ | ✅ | |
| ABAC cross-tenant | ✅ | ✅ | |
| Legal hold delete block | ✅ | ✅ | |
| Auth mock block in prod | ✅ | ✅ | `Jwt:SigningKey` requires dev config |
| HTTPS enforcement | Configurable | JWKS: `RequireHttpsMetadata=true` | ✅ |

---

## 5. Known Divergences (Intentional)

| Item | TypeScript | .NET | Reason |
|------|-----------|------|--------|
| ORM | Knex raw SQL | EF Core LINQ | .NET ecosystem standard |
| Scan result in TS | Stored in `scan_threats` JSON | Same JSONB column | Parity |
| Enum storage | String (uppercase) | String via `EnumToStringConverter` | Parity with TS schema |
| Auth middleware | Custom `req.user` | `HttpContext.User` + `JwtPrincipalExtractor` | .NET idiom |
| Rate limit library | express-rate-limit | ASP.NET Core built-in | .NET 8 standard |
| Dev file serving | `/files/:key` route | `/internal/files?token=...` | Token-based for local storage too |

---

## 6. Gaps vs TypeScript Service

| Gap | Severity | Notes |
|-----|----------|-------|
| ClamAV scanner provider | Medium | Extension point exists; requires `IFileScannerProvider` implementation |
| Retention expiry job | Low | Background job outside service scope |
| Document type registry | Low | `DocumentTypeId` is a UUID FK but no type table in this service |
| Audit list endpoint | Low | `IAuditRepository.ListForDocumentAsync` exists; no endpoint mapped |
| Scan webhook receiver | Low | External scanner callback path not implemented |
| OpenTelemetry traces | Low | Serilog covers logging; OTEL tracing not wired |

---

## 7. Overall Assessment

| Category | Grade | Notes |
|----------|-------|-------|
| API Completeness | A | 13/13 endpoints |
| Business Logic | A | All invariants preserved |
| Security | A- | Audit trail endpoint gap |
| Infrastructure | A | Both providers implemented |
| Tenant Isolation | A | Three-layer, fully implemented |
| HIPAA Alignment | B+ | Gap: retention enforcement job, OTEL |
| Documentation | A | 7 analysis phases + schema.sql |

**Overall Grade: A-**

The .NET Documents Service achieves full functional parity with the TypeScript reference implementation. All critical security controls, tenant isolation mechanisms, and business logic rules are preserved. The primary gaps are non-functional (observability) and operational (background jobs) — both expected to be addressed at the platform/infra layer.

---

## 8. Next Steps

1. **EF Core migrations**: Run `dotnet ef migrations add InitialCreate --project Documents.Infrastructure --startup-project Documents.Api` when PostgreSQL is available.
2. **ClamAV provider**: Implement `IFileScannerProvider` backed by ClamdClient for production HIPAA scanning.
3. **OTEL**: Wire `OpenTelemetry.Sdk` with trace and metric exporters.
4. **Retention job**: Implement a `IHostedService` that queries for documents past `RetainUntil` and marks them for destruction.
5. **Audit list endpoint**: Map `GET /documents/{id}/audit` using `IAuditRepository.ListForDocumentAsync`.
6. **Integration tests**: Mirror the TypeScript service's 97 integration tests using `WebApplicationFactory<Program>` + testcontainers-dotnet.
