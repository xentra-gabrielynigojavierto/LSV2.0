# LS-REPORTS-01-001-01 — Persistence Model Alignment

## Iteration ID
LS-REPORTS-01-001-01

## Objective
Align the persistence model with the correct domain language by refactoring `ReportDefinition` → `ReportTemplate` and updating all related code artifacts for consistent product terminology.

## Scope
- Rename domain entity `ReportDefinition` → `ReportTemplate`
- Update FK/navigation references in `ReportExecution` and `ReportTemplateVersion`
- Update DbContext, EF configurations, repository layer
- Update guardrail validator interface/implementation method name
- Sweep all remaining references
- Keep database table names and column names stable
- Build and startup validation

---

## Execution Log

### Step 1 — Create report
- Created `/analysis/LS-REPORTS-01-001-01-results.md`
- Status: **COMPLETE**

### Step 2 — Rename entity
- Created `reports/src/Reports.Domain/Entities/ReportTemplate.cs` (new file, class `ReportTemplate`)
- Deleted `reports/src/Reports.Domain/Entities/ReportDefinition.cs`
- All properties preserved exactly (Id, Code, Name, Description, ProductCode, IsActive, CurrentVersion, timestamps, Versions collection)
- Status: **COMPLETE**

### Step 3 — Update related entity references
- `ReportExecution.cs`: `ReportDefinitionId` → `ReportTemplateId`, navigation `ReportDefinition?` → `ReportTemplate?`
- `ReportTemplateVersion.cs`: `ReportDefinitionId` → `ReportTemplateId`, navigation `ReportDefinition?` → `ReportTemplate?`
- Status: **COMPLETE**

### Step 4 — Update DbContext and configurations
- `ReportsDbContext.cs`: `DbSet<ReportDefinition> ReportDefinitions` → `DbSet<ReportTemplate> ReportTemplates`; `ChangeTracker.Entries<ReportDefinition>()` → `ChangeTracker.Entries<ReportTemplate>()`
- Created `ReportTemplateConfiguration.cs` (renamed from `ReportDefinitionConfiguration.cs`); class name `ReportDefinitionConfiguration` → `ReportTemplateConfiguration`; `IEntityTypeConfiguration<ReportDefinition>` → `IEntityTypeConfiguration<ReportTemplate>`
- **Table name kept stable**: `builder.ToTable("rpt_ReportDefinitions")` — no physical schema change
- `ReportExecutionConfiguration.cs`: FK reference updated from `ReportDefinitionId` → `ReportTemplateId` with `.HasColumnName("ReportDefinitionId")` to keep physical column stable; navigation from `.HasOne(e => e.ReportDefinition)` → `.HasOne(e => e.ReportTemplate)`
- `ReportTemplateVersionConfiguration.cs`: index reference updated from `ReportDefinitionId` → `ReportTemplateId` with `.HasColumnName("ReportDefinitionId")` to keep physical column stable
- Deleted `ReportDefinitionConfiguration.cs`
- Status: **COMPLETE**

### Step 5 — Update repository layer
- `ITemplateRepository.cs`: All return types and parameters changed from `ReportDefinition` → `ReportTemplate`; parameter names `definition` → `template`, `definitionId` → `templateId`
- `EfTemplateRepository.cs`: All code updated — `_db.ReportDefinitions` → `_db.ReportTemplates`; all variable/parameter names aligned; lambda variables `d` → `t`
- `MockTemplateRepository.cs`: All return types, parameters, and `Array.Empty<>` types updated to `ReportTemplate`; parameter names aligned
- `EfReportRepository.cs`: `.Include(e => e.ReportDefinition)` → `.Include(e => e.ReportTemplate)`
- `MockReportRepository.cs`: No changes needed (does not reference `ReportDefinition`)
- Status: **COMPLETE**

### Step 6 — Sweep remaining references
- `IGuardrailValidator.cs`: `ValidateReportDefinition()` → `ValidateReportTemplate()`
- `GuardrailValidator.cs`: Method implementation renamed to match interface
- `HealthEndpoints.cs`: Updated guardrail probe to call `ValidateReportTemplate()` instead of `ValidateReportDefinition()`
- Full-solution grep for `ReportDefinition` confirmed: only intentional database column/table mapping references remain (3 occurrences, all in `HasColumnName()`/`ToTable()` configuration calls)
- No stale references in: Api, Application, Contracts, Domain, Infrastructure, Worker, Shared, or tests
- Status: **COMPLETE**

### Step 7 — Validate
- **Build**: `dotnet build` — **0 Errors, 0 Warnings** across all 6 projects (Domain, Contracts, Application, Infrastructure, Worker, Api)
- **Tests**: `dotnet test` — passed (test projects are empty scaffolds at this stage)
- **Health/Ready**: Service startup depends on `ConnectionStrings:ReportsDb` — with empty connection string, falls back to mock repositories (design behavior). Health and ready endpoints compile correctly and are structurally unchanged.
- Status: **COMPLETE**

### Step 8 — Finalize report
- Status: **COMPLETE**

---

## Files Created
| File | Description |
|------|-------------|
| `reports/src/Reports.Domain/Entities/ReportTemplate.cs` | Renamed entity (was `ReportDefinition.cs`) |
| `reports/src/Reports.Infrastructure/Persistence/Configurations/ReportTemplateConfiguration.cs` | Renamed EF config (was `ReportDefinitionConfiguration.cs`) |
| `analysis/LS-REPORTS-01-001-01-results.md` | This report |

## Files Modified
| File | Changes |
|------|---------|
| `reports/src/Reports.Domain/Entities/ReportExecution.cs` | `ReportDefinitionId` → `ReportTemplateId`, navigation → `ReportTemplate` |
| `reports/src/Reports.Domain/Entities/ReportTemplateVersion.cs` | `ReportDefinitionId` → `ReportTemplateId`, navigation → `ReportTemplate` |
| `reports/src/Reports.Infrastructure/Persistence/ReportsDbContext.cs` | DbSet and ChangeTracker references updated |
| `reports/src/Reports.Infrastructure/Persistence/Configurations/ReportExecutionConfiguration.cs` | FK/nav alignment + `HasColumnName("ReportDefinitionId")` |
| `reports/src/Reports.Infrastructure/Persistence/Configurations/ReportTemplateVersionConfiguration.cs` | Index alignment + `HasColumnName("ReportDefinitionId")` |
| `reports/src/Reports.Contracts/Persistence/ITemplateRepository.cs` | All types/params → `ReportTemplate` |
| `reports/src/Reports.Infrastructure/Persistence/EfTemplateRepository.cs` | Implementation aligned |
| `reports/src/Reports.Infrastructure/Persistence/MockTemplateRepository.cs` | Implementation aligned |
| `reports/src/Reports.Infrastructure/Persistence/EfReportRepository.cs` | `.Include(e => e.ReportTemplate)` |
| `reports/src/Reports.Contracts/Guardrails/IGuardrailValidator.cs` | `ValidateReportDefinition` → `ValidateReportTemplate` |
| `reports/src/Reports.Application/Guardrails/GuardrailValidator.cs` | Method rename aligned |
| `reports/src/Reports.Api/Endpoints/HealthEndpoints.cs` | Guardrail probe call updated |

## Files Deleted
| File | Reason |
|------|--------|
| `reports/src/Reports.Domain/Entities/ReportDefinition.cs` | Replaced by `ReportTemplate.cs` |
| `reports/src/Reports.Infrastructure/Persistence/Configurations/ReportDefinitionConfiguration.cs` | Replaced by `ReportTemplateConfiguration.cs` |

---

## Build / Run / Validation Status

| Check | Result |
|-------|--------|
| `dotnet build Reports.sln` | **Pass** — 0 Errors, 0 Warnings |
| All 6 projects compiled | Reports.Domain, Contracts, Application, Infrastructure, Worker, Api |
| `dotnet test` | **Pass** (test projects are scaffolds) |
| Stale reference sweep | **Clean** — only intentional DB mapping references |

---

## Decisions Made

### D1: Keep physical table name `rpt_ReportDefinitions` stable
- **Decision**: The `ReportTemplate` entity maps to the existing `rpt_ReportDefinitions` table via `builder.ToTable("rpt_ReportDefinitions")`
- **Rationale**: Avoids unnecessary destructive migration. Table can be renamed in a future dedicated migration story with proper data safety checks
- **Impact**: Zero schema changes required for this iteration

### D2: Keep physical FK column names as `ReportDefinitionId` in the database
- **Decision**: Both `ReportExecution.ReportTemplateId` and `ReportTemplateVersion.ReportTemplateId` map to `HasColumnName("ReportDefinitionId")` in the database
- **Rationale**: Same as D1 — avoids FK column rename which would require migration and potential data risk
- **Impact**: Code uses `ReportTemplateId` consistently; only the physical column retains the old name

### D3: Rename guardrail method `ValidateReportDefinition` → `ValidateReportTemplate`
- **Decision**: Included in the naming alignment since it references the old `Definition` terminology
- **Rationale**: Consistent domain language across the entire codebase
- **Impact**: Interface, implementation, and health probe caller all updated

---

## Known Gaps / Not Yet Implemented
- Physical table rename (`rpt_ReportDefinitions` → `rpt_ReportTemplates`) deferred to a future migration story
- Physical FK column rename (`ReportDefinitionId` → `ReportTemplateId` in DB) deferred to same future story
- No EF migration generated for this iteration (no schema changes)
- Test coverage is scaffold-only; functional tests to be added with API implementation

---

## Final Summary

**LS-REPORTS-01-001-01** is complete. The `ReportDefinition` entity has been fully replaced by `ReportTemplate` across the entire Reports service codebase:

- 2 files created (renamed entity + renamed EF config)
- 12 files modified (entities, DbContext, configurations, repositories, contracts, guardrails, health endpoint)
- 2 files deleted (old `ReportDefinition.cs` and `ReportDefinitionConfiguration.cs`)
- **0 build errors, 0 warnings**
- **Zero schema changes** — database table and column names remain stable via explicit EF mappings
- All code references are consistent with `ReportTemplate` terminology

**Recommended next story**: LS-REPORTS-01-002 — Template Management API (CRUD endpoints for `ReportTemplate` with versioning support)
