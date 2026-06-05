# LS-REPORTS-01-002 — Template Management API

## Story ID
LS-REPORTS-01-002

## Objective
Expose the first Template Management API layer so control-center workflows can create, update, version, publish, and retrieve `ReportTemplate` records through stable REST endpoints. Preserves clean architecture, product scoping, organization-type scoping, template version governance, and portability.

## Scope
- Request/Response DTOs for templates and template versions
- Application service layer (`ITemplateManagementService` / `TemplateManagementService`)
- Repository extensions for template/version management
- REST endpoints under `/api/v1/templates`
- Versioning rules (sequential version numbers, single published version per template)
- Validation and error handling (400/404/409/500)
- Audit hooks via `IAuditAdapter`
- Health/readiness compatibility preserved

---

## Execution Log

### Step 1 — Create report
- Created `/analysis/LS-REPORTS-01-002-report.md`
- Status: **COMPLETE**

### Step 2 — Add request/response DTOs
- Created 7 DTO files in `Reports.Application/Templates/DTOs/`:
  - `CreateTemplateRequest.cs` — Code, Name, Description, ProductCode, OrganizationType, IsActive
  - `UpdateTemplateRequest.cs` — Name, Description, ProductCode, OrganizationType, IsActive
  - `CreateTemplateVersionRequest.cs` — TemplateBody, OutputFormat, ChangeNotes, IsActive, CreatedByUserId
  - `PublishTemplateVersionRequest.cs` — PublishedByUserId
  - `TemplateResponse.cs` — Id, Code, Name, Description, ProductCode, OrganizationType, IsActive, CurrentVersion, CreatedAtUtc, UpdatedAtUtc
  - `TemplateVersionResponse.cs` — Id, TemplateId, VersionNumber, TemplateBody, OutputFormat, ChangeNotes, IsActive, IsPublished, PublishedAtUtc, CreatedAtUtc, CreatedByUserId
  - `ServiceResult.cs` — Generic result wrapper with Success, Data, ErrorMessage, StatusCode; factory methods for Ok, Created, NotFound, BadRequest, Conflict, Fail
- Status: **COMPLETE**

### Step 3 — Add service layer contract and implementation
- Created `ITemplateManagementService.cs` — interface with 9 operations
- Created `TemplateManagementService.cs` — full implementation with:
  - Request validation (Code, Name, ProductCode, OrganizationType required for create; Name, ProductCode, OrganizationType required for update; TemplateBody, OutputFormat required for version)
  - Duplicate code detection (409 Conflict)
  - Sequential version numbering (queries latest version, increments)
  - Publish logic (unpublishes current, publishes target, idempotent if already published)
  - DTO mapping (entity → response)
  - Audit hooks (try/catch wrapped, non-blocking)
  - Structured logging
- Registered `ITemplateManagementService` → `TemplateManagementService` as Scoped in `DependencyInjection.cs`
- Status: **COMPLETE**

### Step 4 — Extend repositories minimally
- **Domain entity changes**:
  - `ReportTemplate`: Added `OrganizationType` property
  - `ReportTemplate`: Changed `CurrentVersion` default from `1` to `0` (template starts with no versions)
  - `ReportTemplateVersion`: Added `IsPublished`, `PublishedAtUtc`, `PublishedByUserId` properties
- **EF configuration changes**:
  - `ReportTemplateConfiguration`: Added `OrganizationType` column (required, max 50), composite index on (ProductCode, OrganizationType), single index on OrganizationType
  - `ReportTemplateVersionConfiguration`: Added `IsPublished` (required, default false), `PublishedAtUtc`, `PublishedByUserId` (max 100) column configs
- **Repository interface changes** (`ITemplateRepository`):
  - `ListAsync`: Added `organizationType` parameter
  - Added `GetPublishedVersionAsync(Guid templateId, CancellationToken ct)`
  - Added `GetLatestVersionAsync(Guid templateId, CancellationToken ct)`
  - Added `UpdateVersionAsync(ReportTemplateVersion version, CancellationToken ct)`
  - Added `PublishVersionAtomicAsync(Guid templateId, int versionNumber, string publishedByUserId, CancellationToken ct)` — transactional unpublish+publish in single commit
  - Added `CreateVersionAtomicAsync(ReportTemplate template, ReportTemplateVersion version, CancellationToken ct)` — transactional read-latest + insert + update template in single commit
- **EfTemplateRepository**: Implemented all 6 new methods; updated `ListAsync` with organizationType filter; atomic methods use `BeginTransactionAsync` for invariant protection
- **MockTemplateRepository**: Implemented all 6 new methods with debug logging
- Status: **COMPLETE**

### Step 5 — Implement template endpoints
- Created `TemplateEndpoints.cs` with 9 endpoints:
  1. `POST /api/v1/templates` — Create template (201/400/409)
  2. `PUT /api/v1/templates/{templateId}` — Update template (200/400/404)
  3. `GET /api/v1/templates/{templateId}` — Get template by ID (200/404)
  4. `GET /api/v1/templates` — List templates with optional `productCode`, `organizationType`, `page`, `pageSize` query params (200)
  5. `POST /api/v1/templates/{templateId}/versions` — Create version (201/400/404)
  6. `GET /api/v1/templates/{templateId}/versions` — List all versions (200/404)
  7. `GET /api/v1/templates/{templateId}/versions/latest` — Get latest version (200/404)
  8. `GET /api/v1/templates/{templateId}/versions/published` — Get published version (200/404)
  9. `POST /api/v1/templates/{templateId}/versions/{versionNumber}/publish` — Publish version (200/400/404)
- Endpoints call service layer only; zero direct repository access
- `ToResult<T>` helper maps `ServiceResult<T>` to appropriate `IResult`
- Registered in `Program.cs` via `app.MapTemplateEndpoints()`
- Status: **COMPLETE**

### Step 6 — Implement version publish logic
- Implemented in `TemplateManagementService.PublishVersionAsync`:
  - Validates `PublishedByUserId` is provided
  - Verifies template and target version exist (404 if not)
  - **Idempotent**: if target version is already published, returns 200 with current state
  - Delegates to `PublishVersionAtomicAsync` — transactional: unpublishes all currently published versions and publishes target in a single DB commit
  - Sets target version as published with timestamp and userId
  - At most one published version per template at any time
- Status: **COMPLETE**

### Step 7 — Add validation and error handling
- **Template creation**: Code, Name, ProductCode, OrganizationType required; duplicate Code → 409
- **Template update**: Name, ProductCode, OrganizationType required; template must exist → 404
- **Version creation**: TemplateBody, OutputFormat required; template must exist → 404
- **Publish**: PublishedByUserId required; template and version must exist → 404
- All error responses use consistent `{ error: "message" }` JSON format
- Status codes: 400 (validation), 404 (not found), 409 (conflict), 500 (unexpected)
- Status: **COMPLETE**

### Step 8 — Optional audit hooks
- Added `TryAuditAsync` helper in `TemplateManagementService`
- Called on: template.created, template.updated, version.created, version.published
- Uses `IAuditAdapter.RecordEventAsync` with system context
- Wrapped in try/catch — failures log a warning but do not block the operation
- Status: **COMPLETE**

### Step 9 — Validate
- **Build**: `dotnet build Reports.sln` — **0 Errors, 0 Warnings** across all 6 projects
- **Tests**: `dotnet test Reports.sln` — **Pass** (test projects are scaffolds)
- **Health/Ready**: Endpoint mapping preserved; `MapHealthEndpoints()` + `MapTemplateEndpoints()` both registered
- Status: **COMPLETE**

### Step 10 — Finalize report
- Status: **COMPLETE**

---

## Files Created

| File | Description |
|------|-------------|
| `reports/src/Reports.Application/Templates/DTOs/CreateTemplateRequest.cs` | Request DTO for template creation |
| `reports/src/Reports.Application/Templates/DTOs/UpdateTemplateRequest.cs` | Request DTO for template update |
| `reports/src/Reports.Application/Templates/DTOs/CreateTemplateVersionRequest.cs` | Request DTO for version creation |
| `reports/src/Reports.Application/Templates/DTOs/PublishTemplateVersionRequest.cs` | Request DTO for version publish |
| `reports/src/Reports.Application/Templates/DTOs/TemplateResponse.cs` | Response DTO for templates |
| `reports/src/Reports.Application/Templates/DTOs/TemplateVersionResponse.cs` | Response DTO for template versions |
| `reports/src/Reports.Application/Templates/DTOs/ServiceResult.cs` | Generic result wrapper (Ok/Created/NotFound/BadRequest/Conflict/Fail) |
| `reports/src/Reports.Application/Templates/ITemplateManagementService.cs` | Service layer interface (9 operations) |
| `reports/src/Reports.Application/Templates/TemplateManagementService.cs` | Service layer implementation with validation, versioning, publish, audit |
| `reports/src/Reports.Api/Endpoints/TemplateEndpoints.cs` | Minimal API endpoints (9 routes under `/api/v1/templates`) |
| `analysis/LS-REPORTS-01-002-report.md` | This report |

## Files Modified

| File | Changes |
|------|---------|
| `reports/src/Reports.Domain/Entities/ReportTemplate.cs` | Added `OrganizationType` property; changed `CurrentVersion` default to `0` |
| `reports/src/Reports.Domain/Entities/ReportTemplateVersion.cs` | Added `IsPublished`, `PublishedAtUtc`, `PublishedByUserId` properties |
| `reports/src/Reports.Infrastructure/Persistence/Configurations/ReportTemplateConfiguration.cs` | Added `OrganizationType` column config + indexes |
| `reports/src/Reports.Infrastructure/Persistence/Configurations/ReportTemplateVersionConfiguration.cs` | Added `IsPublished`, `PublishedAtUtc`, `PublishedByUserId` column configs |
| `reports/src/Reports.Contracts/Persistence/ITemplateRepository.cs` | Added `organizationType` param to `ListAsync`; added `GetPublishedVersionAsync`, `GetLatestVersionAsync`, `UpdateVersionAsync` |
| `reports/src/Reports.Infrastructure/Persistence/EfTemplateRepository.cs` | Implemented new interface methods; updated `ListAsync` filter |
| `reports/src/Reports.Infrastructure/Persistence/MockTemplateRepository.cs` | Implemented new interface methods |
| `reports/src/Reports.Application/DependencyInjection.cs` | Registered `ITemplateManagementService` → `TemplateManagementService` (Scoped) |
| `reports/src/Reports.Api/Program.cs` | Added `app.MapTemplateEndpoints()` |

## Endpoints Added

| Method | Route | Description | Success | Error Codes |
|--------|-------|-------------|---------|-------------|
| POST | `/api/v1/templates` | Create template | 201 | 400, 409 |
| PUT | `/api/v1/templates/{templateId}` | Update template | 200 | 400, 404 |
| GET | `/api/v1/templates/{templateId}` | Get template by ID | 200 | 404 |
| GET | `/api/v1/templates?productCode=&organizationType=&page=&pageSize=` | List templates | 200 | — |
| POST | `/api/v1/templates/{templateId}/versions` | Create version | 201 | 400, 404 |
| GET | `/api/v1/templates/{templateId}/versions` | List all versions | 200 | 404 |
| GET | `/api/v1/templates/{templateId}/versions/latest` | Get latest version | 200 | 404 |
| GET | `/api/v1/templates/{templateId}/versions/published` | Get published version | 200 | 404 |
| POST | `/api/v1/templates/{templateId}/versions/{versionNumber}/publish` | Publish version | 200 | 400, 404 |

## Build / Run / Validation Status

| Check | Result |
|-------|--------|
| `dotnet build Reports.sln` | **Pass** — 0 Errors, 0 Warnings |
| All 6 projects compiled | Reports.Domain, Contracts, Application, Infrastructure, Worker, Api |
| `dotnet test` | **Pass** (test projects are scaffolds) |
| Health endpoint preserved | `GET /api/v1/health` still mapped |
| Ready endpoint preserved | `GET /api/v1/ready` still mapped |
| Template endpoints registered | `MapTemplateEndpoints()` in Program.cs |

## Issues Encountered

None. Implementation was straightforward given the existing clean architecture foundation.

## Decisions Made

### D1: `OrganizationType` added to domain entity
- **Decision**: Added `OrganizationType` as a required string property on `ReportTemplate`
- **Rationale**: Spec requires organization-type scoping for template listing and management
- **Impact**: New column in EF model; new indexes for query performance

### D2: Publish fields added to `ReportTemplateVersion`
- **Decision**: Added `IsPublished` (bool), `PublishedAtUtc` (DateTimeOffset?), `PublishedByUserId` (string?) to `ReportTemplateVersion`
- **Rationale**: Publish state tracked per-version enables single-published-version governance
- **Impact**: New columns in EF model; `IsPublished` defaults to false

### D3: `CurrentVersion` default changed from 1 to 0
- **Decision**: `ReportTemplate.CurrentVersion` defaults to `0` (no versions yet)
- **Rationale**: Templates are created without versions; `CurrentVersion` is updated when a version is created
- **Impact**: Semantic clarity — `CurrentVersion == 0` means "no versions created"

### D4: Service layer result pattern
- **Decision**: Created `ServiceResult<T>` as a lightweight generic result wrapper
- **Rationale**: Provides consistent success/error propagation from service to endpoint layer without exceptions for expected error cases (validation, not found, conflict)
- **Impact**: Endpoints use a single `ToResult<T>` helper for consistent HTTP response mapping

### D5: Audit hooks are fire-and-forget with try/catch
- **Decision**: Audit adapter calls are wrapped in try/catch and log warnings on failure
- **Rationale**: Audit recording should not block or fail the primary operation; mock adapter is currently in use
- **Impact**: No audit failures can cause API errors

### D6: Schema changes not migration-managed yet
- **Decision**: New domain properties (`OrganizationType`, publish fields) are in the EF model but no migration was generated
- **Rationale**: Service runs with mock repositories when no `ConnectionStrings:ReportsDb` is set; migration generation deferred to when database connectivity is established
- **Impact**: EF model is ready; migration to be created before first database deployment

### D7: Atomic repository methods for version creation and publish
- **Decision**: Added `CreateVersionAtomicAsync` and `PublishVersionAtomicAsync` that wrap read+write operations in explicit DB transactions
- **Rationale**: Concurrent version creation could race on the "get latest version number" read, producing duplicate version numbers. Concurrent publish could leave multiple versions published. Wrapping in transactions with the existing unique index on (templateId, versionNumber) ensures invariant safety.
- **Impact**: Version creation and publish flows are now safe under concurrency; unique constraint violations at DB level provide a second safety net

### D8: `ListTemplates` filters active-only by default
- **Decision**: The list endpoint always filters `activeOnly: true`
- **Rationale**: Consumer use case is control-center workflows looking for available templates; inactive templates should not appear in standard listings
- **Impact**: No way to list inactive templates through this endpoint (can be added later if needed)

## Known Gaps / Not Yet Implemented

- **EF Migration**: No migration generated for new columns (`OrganizationType`, `IsPublished`, `PublishedAtUtc`, `PublishedByUserId`). To be created before database deployment.
- **Authentication/Authorization**: No auth middleware on template endpoints. To be added in a future story.
- **Pagination metadata**: List endpoints return data arrays only, no total count or page metadata. Can be enhanced later.
- **Report execution APIs**: Out of scope per spec.
- **Scheduling APIs**: Out of scope per spec.
- **Tenant customization APIs**: Out of scope per spec.
- **Advanced search/filter/sort**: Only `productCode`, `organizationType`, and `page`/`pageSize` supported.
- **Test coverage**: Test projects are scaffolds; functional/integration tests to be added in a future story.
- **Physical table rename**: `rpt_ReportDefinitions` still mapped via `ToTable()` — deferred from LS-REPORTS-01-001-01.
- **Inactive template listing**: No endpoint to list inactive templates. Can be added by exposing the `activeOnly` parameter.

## Final Summary

**LS-REPORTS-01-002** is complete. The Template Management API provides the first usable API surface for the Reports service:

- **11 files created**: 7 DTOs, 1 service interface, 1 service implementation, 1 endpoint file, 1 report
- **9 files modified**: 2 domain entities, 2 EF configs, 1 repository interface, 2 repository implementations, 1 DI registration, 1 Program.cs
- **9 REST endpoints** under `/api/v1/templates` covering full CRUD + versioning + publish
- **Clean architecture preserved**: Endpoints → Service → Repository. No EF/domain leakage into API layer
- **Versioning rules enforced**: Sequential version numbers, at most one published version per template, idempotent publish
- **Validation and error handling**: 400/404/409 with consistent JSON error format
- **Audit hooks**: fire-and-forget calls to `IAuditAdapter` on key operations
- **Health/readiness preserved**: Both existing endpoints compile and map correctly
- **0 build errors, 0 warnings**

**Recommended next story**: LS-REPORTS-01-003 — Template Management API integration tests + EF migration generation
