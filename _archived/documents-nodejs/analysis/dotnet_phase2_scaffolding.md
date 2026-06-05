# .NET Documents Service — Phase 2: Scaffolding

**Date**: 2026-03-29

---

## 1. Project Structure Created

```
apps/services/documents-dotnet/
├── Documents.Domain/
│   ├── Documents.Domain.csproj
│   ├── Entities/
│   │   ├── Document.cs
│   │   ├── DocumentVersion.cs
│   │   └── DocumentAudit.cs
│   ├── Enums/
│   │   ├── DocumentStatus.cs
│   │   ├── ScanStatus.cs
│   │   └── AuditEvent.cs           # static constants, not an enum
│   ├── Interfaces/
│   │   ├── IDocumentRepository.cs
│   │   ├── IDocumentVersionRepository.cs
│   │   ├── IAuditRepository.cs
│   │   ├── IStorageProvider.cs
│   │   ├── IFileScannerProvider.cs
│   │   └── IAccessTokenStore.cs
│   └── ValueObjects/
│       ├── AccessToken.cs
│       └── Principal.cs
│
├── Documents.Application/
│   ├── Documents.Application.csproj
│   ├── DTOs/
│   │   ├── CreateDocumentRequest.cs    (+ FluentValidation validator)
│   │   ├── UpdateDocumentRequest.cs    (+ FluentValidation validator)
│   │   ├── ListDocumentsRequest.cs
│   │   ├── DocumentResponse.cs         (+ DocumentListResponse)
│   │   ├── DocumentVersionResponse.cs
│   │   ├── UploadDocumentVersionRequest.cs
│   │   └── IssuedTokenResponse.cs
│   ├── Exceptions/
│   │   └── DocumentsExceptions.cs      (9 typed exception classes)
│   ├── Models/
│   │   └── RequestContext.cs           (with EffectiveTenantId)
│   └── Services/
│       ├── DocumentService.cs          (core orchestration)
│       ├── AccessTokenService.cs
│       ├── AuditService.cs
│       └── ScanService.cs
│
├── Documents.Infrastructure/
│   ├── Documents.Infrastructure.csproj
│   ├── Database/
│   │   ├── DocsDbContext.cs
│   │   ├── DocumentRepository.cs
│   │   ├── DocumentVersionRepository.cs
│   │   ├── AuditRepository.cs
│   │   ├── schema.sql
│   │   └── Migrations/               # EF Core migration placeholder
│   ├── Storage/
│   │   ├── LocalStorageProvider.cs
│   │   ├── S3StorageProvider.cs
│   │   └── StorageProviderFactory.cs
│   ├── Scanner/
│   │   ├── NullScannerProvider.cs
│   │   └── MockScannerProvider.cs
│   ├── AccessToken/
│   │   ├── InMemoryAccessTokenStore.cs
│   │   └── RedisAccessTokenStore.cs
│   ├── Auth/
│   │   └── JwtPrincipalExtractor.cs
│   └── DependencyInjection.cs
│
└── Documents.Api/
    ├── Documents.Api.csproj
    ├── Program.cs                       (Minimal API bootstrap)
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── Endpoints/
    │   ├── DocumentEndpoints.cs         (9 routes)
    │   ├── AccessEndpoints.cs           (1 route)
    │   └── HealthEndpoints.cs           (2 routes)
    └── Middleware/
        ├── CorrelationIdMiddleware.cs
        └── ExceptionHandlingMiddleware.cs
```

**Total files**: 36 source files

---

## 2. Project Reference Graph

```
Documents.Api
    └── Documents.Infrastructure
        └── Documents.Application
            └── Documents.Domain
```

- `Domain` has zero external dependencies (pure C# / BCL only).
- `Application` depends only on `Domain` and `FluentValidation`.
- `Infrastructure` depends on `Domain`, `Application`, plus all external packages.
- `Api` depends on `Application` (service types) and `Infrastructure` (DI wiring).

---

## 3. Package Summary

| Package | Used in | Purpose |
|---------|---------|---------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.4 | Infrastructure | PostgreSQL ORM |
| `AWSSDK.S3` 3.7.x | Infrastructure | S3 storage |
| `StackExchange.Redis` 2.7.x | Infrastructure | Redis token store |
| `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.4 | Infrastructure / Api | JWT auth |
| `Serilog.AspNetCore` 8.0.1 | Api | Structured logging |
| `Swashbuckle.AspNetCore` 6.6.2 | Api | OpenAPI / Swagger |
| `FluentValidation` 11.9.2 | Application | Request validation |
| `Microsoft.AspNetCore.RateLimiting` 8.0.4 | Api | Rate limiting |

---

## 4. Solution Registration

All 4 projects added to `LegalSynq.sln`:
- Nested under existing `services` solution folder.
- Build configurations: `Debug|Any CPU` and `Release|Any CPU`.

---

## 5. Port Assignment

| Service | Port |
|---------|------|
| TypeScript Docs Service | 5005 |
| **.NET Documents Service** | **5006** |

---

## Grade: Phase 2 complete. Proceed to Phase 3 (Domain & Contracts).
