# .NET Documents Service — Phase 1: Discovery and Mapping

**Date**: 2026-03-29  
**Service**: Documents.Api (.NET 8)  
**Port**: 5006  
**Reference implementation**: TypeScript Docs Service (port 5005)

---

## 1. Objective

Map the existing TypeScript Documents Service architecture to .NET 8 idioms before writing any code. Identify assumptions, risks, and language-level translation decisions.

---

## 2. TypeScript Service Inventory

| Layer | TS Module | .NET Equivalent |
|-------|-----------|-----------------|
| Entry | `src/index.ts` (Express) | `Program.cs` (Minimal APIs) |
| Config | `src/config.ts` | `appsettings.json` + `IConfiguration` |
| Routing | `express.Router()` | `MapGroup("/documents")` |
| Middleware | `src/middleware/*.ts` | `IMiddleware` / `UseMiddleware<T>()` |
| Services | `src/services/*.ts` | `*.Application/Services/*.cs` |
| Repositories | `src/repositories/*.ts` | `*.Infrastructure/Database/*Repository.cs` |
| ORM | Knex.js (raw SQL) | EF Core 8 (Npgsql) |
| Auth | custom JWT decode | `AddJwtBearer` + `JwtPrincipalExtractor` |
| Storage | `StorageProvider` factory | `IStorageProvider` + factory DI |
| Scanner | `FileScannerProvider` | `IFileScannerProvider` + factory DI |
| Token store | `AccessTokenStore` | `IAccessTokenStore` (memory / Redis) |
| Validation | Zod | FluentValidation |
| Audit | fire-and-forget | fire-and-forget (try/catch in AuditRepository) |
| Error types | Custom `AppError` subclasses | `DocumentsException` hierarchy |

---

## 3. Database Mapping

### Important Finding: PostgreSQL vs MySQL

Existing .NET services (Identity, Fund, CareConnect) use **MySQL** via Pomelo (`UseMySql`). The Documents service — both TS and .NET — must use **PostgreSQL** because:

- The TypeScript service's Knex migrations create PostgreSQL-specific types (`UUID`, `JSONB`, `TIMESTAMPTZ`).
- `JSONB` columns (`scan_threats`, `detail`) have no MySQL equivalent with the same semantics.
- The connection string secret `ConnectionStrings__CareConnectDb` / `ConnectionStrings__FundDb` use MySQL; the `.NET Documents` service uses `ConnectionStrings__DocsDb` (PostgreSQL).

**Decision**: Use `Npgsql.EntityFrameworkCore.PostgreSQL` throughout. Do NOT claim MySQL compatibility.

### Column name convention
TypeScript Knex uses `snake_case` columns. EF Core entity properties are `PascalCase` and mapped explicitly using `.HasColumnName("snake_case")` in `DocsDbContext.OnModelCreating`.

### JSONB columns
`scan_threats` and `detail` are stored as PostgreSQL JSONB. EF Core value converters serialize `List<string>` ↔ JSON string. The column type is declared `.HasColumnType("jsonb")`.

---

## 4. Auth Architecture Mapping

| TS approach | .NET approach |
|-------------|---------------|
| `req.user` populated by custom middleware | `HttpContext.User` (ClaimsPrincipal) |
| Manual JWT decode via `jsonwebtoken` | `AddJwtBearer` middleware |
| `AUTH_PROVIDER=mock` blocks in non-dev | Symmetric HS256 key only allowed in Development |
| JWKS support via `jwks-rsa` | `MetadataAddress` in JwtBearer options |
| Role extraction from `roles` claim | `JwtPrincipalExtractor.Extract(ClaimsPrincipal)` |

**Critical invariant preserved**: In production, `Jwt:SigningKey` must be empty and `Jwt:JwksUri` must be set. The `Program.cs` startup throws `InvalidOperationException` if neither is provided.

---

## 5. Three-Layer Tenant Isolation Mapping

| Layer | TS Implementation | .NET Implementation |
|-------|------------------|---------------------|
| L1 Pre-query guard | `assertTenantId(tenantId)` | `RequireTenantId(tenantId)` in each repository |
| L2 SQL predicate | `WHERE tenant_id = ?` | `.Where(d => d.TenantId == tenantId)` in EF LINQ |
| L3 ABAC | `assertDocumentTenantScope(actor, doc)` | `AssertDocumentTenantScopeAsync(ctx, doc)` in DocumentService |

---

## 6. Provider Strategy Mapping

| Provider type | TS env var | .NET config key | Implementations |
|--------------|------------|-----------------|-----------------|
| Storage | `STORAGE_PROVIDER` | `Storage:Provider` | `local`, `s3` |
| Scanner | `FILE_SCANNER_PROVIDER` | `Scanner:Provider` | `none`, `mock` |
| Token store | `ACCESS_TOKEN_STORE` | `AccessToken:Store` | `memory`, `redis` |

---

## 7. API Surface Mapping

| TS Route | .NET Minimal API | Auth |
|---------|-----------------|------|
| `POST /documents` | `MapPost("/documents/")` | JWT |
| `GET /documents` | `MapGet("/documents/")` | JWT |
| `GET /documents/:id` | `MapGet("/documents/{id:guid}")` | JWT |
| `PATCH /documents/:id` | `MapPatch("/documents/{id:guid}")` | JWT |
| `DELETE /documents/:id` | `MapDelete("/documents/{id:guid}")` | JWT |
| `POST /documents/:id/versions` | `MapPost("/documents/{id:guid}/versions")` | JWT |
| `GET /documents/:id/versions` | `MapGet("/documents/{id:guid}/versions")` | JWT |
| `POST /documents/:id/view-url` | `MapPost("/documents/{id:guid}/view-url")` | JWT |
| `POST /documents/:id/download-url` | `MapPost("/documents/{id:guid}/download-url")` | JWT |
| `GET /documents/:id/content` | `MapGet("/documents/{id:guid}/content")` | JWT |
| `GET /access/:token` | `MapGet("/access/{token}")` | None |
| `GET /health` | `MapGet("/health")` | None |
| `GET /health/ready` | `MapGet("/health/ready")` | None |

---

## 8. Risk Register

| Risk | Severity | Mitigation |
|------|----------|------------|
| EF Core JSONB serialization for `List<string>` | Medium | Value converter + `.HasColumnType("jsonb")` |
| Redis Lua atomicity for mark-used | Low | Tested Lua script in `RedisAccessTokenStore` |
| Multipart form parsing for file upload | Medium | `ctx.Request.ReadFormAsync()` + `IFormFile` |
| Stream position reset after scan | High | `if (fileStream.CanSeek) fileStream.Position = 0` after scan |
| EF Core `ExecuteUpdateAsync` vs Tracked entity | Low | Used for batch updates (scan status, soft delete) |
| `AllowAnonymous()` on access endpoint | Critical | Verified — `/access/{token}` is intentionally public |

---

## 9. Grade: Phase 1 complete. Proceed to Phase 2 (Scaffolding).
