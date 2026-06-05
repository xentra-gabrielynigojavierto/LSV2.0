# LS-REPORTS-01-001 — Template Data Model & Persistence Foundation

## Status: COMPLETE

## Objective
Introduce MySQL + EF Core persistence foundation and implement the core data model for report templates and versioning.

## Steps

### Step A: Add Dependencies — COMPLETE
- **Files Modified**: `Reports.Infrastructure.csproj`, `Reports.Api.csproj`
- **Packages Added**:
  - `Microsoft.EntityFrameworkCore` 8.0.7 → Infrastructure
  - `Pomelo.EntityFrameworkCore.MySql` 8.0.2 → Infrastructure
  - `Microsoft.Extensions.Configuration.Abstractions` 8.0.* → Infrastructure
  - `Microsoft.EntityFrameworkCore.Design` 8.0.7 → Api (design-time only)
- **Notes**: Versions aligned with platform standard (Liens, CareConnect, etc. all use Pomelo 8.0.2)

### Step B: Configuration — COMPLETE
- **Files Modified**: `appsettings.json`
- **Notes**: Added `ConnectionStrings:ReportsDb` section. Existing `MySql` settings section preserved for backward compat. Connection string left empty by default — system falls back to mock repositories when empty.

### Step C: Domain Entities — COMPLETE
- **Files Modified**: `ReportDefinition.cs`, `ReportExecution.cs`
- **Files Created**: `ReportTemplateVersion.cs`
- **Implementation Notes**:
  - `ReportDefinition` — upgraded from bootstrap placeholder: added `CurrentVersion`, `UpdatedAtUtc`, navigation to `Versions` collection. Changed from `init`-only to `set` for EF compatibility.
  - `ReportExecution` — upgraded: replaced `ReportDefinitionCode` (string) with `ReportDefinitionId` (Guid FK), added `TemplateVersionNumber`, navigation to `ReportDefinition`. Changed from `init`-only to `set`.
  - `ReportTemplateVersion` — new entity for versioned templates: stores `TemplateBody` (longtext), `OutputFormat`, `ChangeNotes`, `IsActive`, `CreatedByUserId`, FK to `ReportDefinition`.
  - Domain layer remains EF-free (no attributes, no EF dependencies)

### Step D: EF Core DbContext — COMPLETE
- **Files Created**: `Reports.Infrastructure/Persistence/ReportsDbContext.cs`
- **Notes**: Exposes `DbSet<ReportDefinition>`, `DbSet<ReportTemplateVersion>`, `DbSet<ReportExecution>`. Auto-sets `CreatedAtUtc`/`UpdatedAtUtc` timestamps on save. Uses `ApplyConfigurationsFromAssembly` for fluent config.

### Step E: Entity Configurations — COMPLETE
- **Files Created**:
  - `Persistence/Configurations/ReportDefinitionConfiguration.cs`
  - `Persistence/Configurations/ReportTemplateVersionConfiguration.cs`
  - `Persistence/Configurations/ReportExecutionConfiguration.cs`
- **Table Naming**: `rpt_ReportDefinitions`, `rpt_ReportTemplateVersions`, `rpt_ReportExecutions`
- **Indexes**:
  - ReportDefinition: unique on `Code`, non-unique on `ProductCode`, `IsActive`
  - ReportTemplateVersion: unique composite on `(ReportDefinitionId, VersionNumber)`, non-unique on `IsActive`
  - ReportExecution: non-unique on `TenantId`, `Status`, composite on `(TenantId, CreatedAtUtc)`
- **Relationships**:
  - Definition → Versions: one-to-many, cascade delete
  - Execution → Definition: many-to-one, restrict delete

### Step F: Persistence Contracts — COMPLETE
- **Files Modified**: `IReportRepository.cs`
- **Files Created**: `ITemplateRepository.cs`
- **Notes**: Replaced bootstrap `object`-based signatures with strongly-typed domain entity contracts.
  - `IReportRepository`: `SaveAsync(ReportExecution)`, `GetByIdAsync(Guid)`, `ListByTenantAsync(tenantId)`, `UpdateAsync(ReportExecution)`
  - `ITemplateRepository`: Full CRUD for `ReportDefinition` + version management methods (`GetVersionAsync`, `GetActiveVersionAsync`, `ListVersionsAsync`, `CreateVersionAsync`)
  - Added `Reports.Domain` project reference to `Reports.Contracts.csproj`

### Step G: EF Repository Implementations — COMPLETE
- **Files Created**:
  - `Persistence/EfReportRepository.cs`
  - `Persistence/EfTemplateRepository.cs`
- **Files Modified**:
  - `Persistence/MockReportRepository.cs` — updated to match new `IReportRepository` signature
- **Files Created**:
  - `Persistence/MockTemplateRepository.cs` — mock implementation of `ITemplateRepository`
- **Notes**: EF repos use `Include()` for navigation properties, proper pagination with `Skip/Take`, `OrderByDescending` on timestamps.

### Step H: DI Registration & Program.cs — COMPLETE
- **Files Modified**: `DependencyInjection.cs`, `Program.cs`
- **Notes**:
  - `AddReportsInfrastructure` now accepts `IConfiguration` parameter
  - When `ConnectionStrings:ReportsDb` is non-empty: registers `ReportsDbContext`, `EfReportRepository`, `EfTemplateRepository` (scoped lifetime)
  - When empty: falls back to `MockReportRepository`, `MockTemplateRepository` (singleton lifetime)
  - Program.cs updated to pass `builder.Configuration` to `AddReportsInfrastructure`

### Step I: Health Probe Update — COMPLETE
- **Files Modified**: `HealthEndpoints.cs`
- **Notes**: Added database connectivity check via `IServiceProvider.GetService<ReportsDbContext>()`:
  - If DbContext registered: calls `CanConnectAsync()` → "ok" or "fail"
  - If not registered (mock mode): reports "mock"
  - "mock" treated as passing for overall readiness

### Step J: Design-Time Factory — COMPLETE
- **Files Created**: `Reports.Api/DesignTimeDbContextFactory.cs`
- **Notes**: Implements `IDesignTimeDbContextFactory<ReportsDbContext>` for `dotnet ef migrations` tooling. Reads connection string from `appsettings.json` / env vars.

### Step K: Build Validation — COMPLETE
- **Build Result**: 0 errors, 0 warnings across all 10 projects (6 src + 4 tests/infra)
- **Restore**: All NuGet packages restored successfully

---

## Issues Encountered
- None

## Decisions Made
1. **Graceful fallback**: When `ConnectionStrings:ReportsDb` is empty, the service continues to work with mock repositories — no crash, no forced MySQL dependency for dev/testing.
2. **Table prefix `rpt_`**: Consistent with platform convention (e.g., `idt_` for identity, `cc_` for CareConnect).
3. **Pomelo 8.0.2**: Matched version used by Liens, Documents, Notifications, CareConnect, Fund services.
4. **EF Core 8.0.7**: Matched version used across the platform for consistency.
5. **`set` over `init`**: Domain entity properties changed from `init` to `set` — EF Core change tracking requires mutable setters.
6. **`ReportDefinitionCode` → `ReportDefinitionId`**: ReportExecution now uses proper FK (Guid) instead of string code reference — enables EF navigation and referential integrity.
7. **Contracts references Domain**: Added `Reports.Domain` project reference to `Reports.Contracts` so repository interfaces can use strongly-typed domain entities. Domain remains dependency-free.

## Files Summary

### Created
| File | Layer |
|------|-------|
| `Reports.Domain/Entities/ReportTemplateVersion.cs` | Domain |
| `Reports.Infrastructure/Persistence/ReportsDbContext.cs` | Infrastructure |
| `Reports.Infrastructure/Persistence/Configurations/ReportDefinitionConfiguration.cs` | Infrastructure |
| `Reports.Infrastructure/Persistence/Configurations/ReportTemplateVersionConfiguration.cs` | Infrastructure |
| `Reports.Infrastructure/Persistence/Configurations/ReportExecutionConfiguration.cs` | Infrastructure |
| `Reports.Infrastructure/Persistence/EfReportRepository.cs` | Infrastructure |
| `Reports.Infrastructure/Persistence/EfTemplateRepository.cs` | Infrastructure |
| `Reports.Infrastructure/Persistence/MockTemplateRepository.cs` | Infrastructure |
| `Reports.Contracts/Persistence/ITemplateRepository.cs` | Contracts |
| `Reports.Api/DesignTimeDbContextFactory.cs` | API |

### Modified
| File | Layer | Changes |
|------|-------|---------|
| `Reports.Domain/Entities/ReportDefinition.cs` | Domain | Added `CurrentVersion`, `UpdatedAtUtc`, `Versions` nav; `init` → `set` |
| `Reports.Domain/Entities/ReportExecution.cs` | Domain | `ReportDefinitionCode` → `ReportDefinitionId` (Guid FK); added `TemplateVersionNumber`, nav; `init` → `set` |
| `Reports.Contracts/Persistence/IReportRepository.cs` | Contracts | Typed signatures replacing `object`-based bootstrap |
| `Reports.Contracts/Reports.Contracts.csproj` | Contracts | Added `Reports.Domain` project reference |
| `Reports.Infrastructure/Reports.Infrastructure.csproj` | Infrastructure | Added EF Core, Pomelo, Configuration.Abstractions packages; Domain reference |
| `Reports.Infrastructure/DependencyInjection.cs` | Infrastructure | Accepts `IConfiguration`; conditional EF vs mock registration |
| `Reports.Infrastructure/Persistence/MockReportRepository.cs` | Infrastructure | Updated to match new typed `IReportRepository` |
| `Reports.Api/Reports.Api.csproj` | API | Added `Microsoft.EntityFrameworkCore.Design` |
| `Reports.Api/Program.cs` | API | Passes `Configuration` to `AddReportsInfrastructure` |
| `Reports.Api/appsettings.json` | API | Added `ConnectionStrings:ReportsDb` |
| `Reports.Api/Endpoints/HealthEndpoints.cs` | API | Added database connectivity check |
