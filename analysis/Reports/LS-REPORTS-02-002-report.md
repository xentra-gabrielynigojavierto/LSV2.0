# LS-REPORTS-02-002 — Tenant Custom Report Override (Inheritance Model)

## Story ID
LS-REPORTS-02-002

## Objective
Introduce a tenant override model that allows a tenant to derive a tenant-specific report configuration from a published `ReportTemplate` without changing the global template itself.

## Scope
- Tenant override domain entity + EF configuration
- Migration for `rpt_TenantReportOverrides` table
- `ITenantReportOverrideRepository` + EF/mock implementations
- `ITenantReportOverrideService` + `TenantReportOverrideService`
- Request/response DTOs
- Override management endpoints (CRUD + deactivate)
- Effective tenant report resolution endpoint
- Validation, conflict handling, audit hooks

## Out of Scope
- Full drag-and-drop report builder UI
- Report execution / scheduling
- Pricing engine enforcement
- End-user UI / user-level saved views

---

## Execution Log

### Step 1 — Report created FIRST
- Created `/analysis/LS-REPORTS-02-002-report.md` before any code changes.

### Step 2 — Domain entity + EF configuration
- Created `TenantReportOverride` entity with all required fields
- Created `TenantReportOverrideConfiguration` (table: `rpt_TenantReportOverrides`)
- Added `DbSet<TenantReportOverride>` to `ReportsDbContext`
- Added `SaveChangesAsync` timestamp handling for `TenantReportOverride`

### Step 3 — Migration
- Generated migration `20260415082312_AddTenantReportOverrides`
- Applied to AWS RDS MySQL (`reports_db`) successfully
- Table `rpt_TenantReportOverrides` created with indexes

### Step 4 — Repository support
- Created `ITenantReportOverrideRepository` interface in `Reports.Contracts`
- Created `EfTenantReportOverrideRepository` with duplicate-entry conflict handling
- Created `MockTenantReportOverrideRepository` for fallback/testing
- Registered both in `DependencyInjection.cs` (EF when DB available, mock otherwise)

### Step 5 — Service layer
- Created `ITenantReportOverrideService` interface
- Created `TenantReportOverrideService` with:
  - Assignment dependency validation
  - Published version requirement
  - One-active-override-per-tenant-per-template enforcement
  - Base version anchoring (captures published version number)
  - Effective resolution with overlay logic
  - Soft deactivate (sets IsActive=false)
  - Audit hooks (4 events)

### Step 6 — DTOs
- `CreateTenantReportOverrideRequest`
- `UpdateTenantReportOverrideRequest`
- `TenantReportOverrideResponse`
- `TenantEffectiveReportResponse`

### Step 7 — Override management endpoints
- `POST /api/v1/tenant-templates/{templateId}/overrides` — create
- `PUT /api/v1/tenant-templates/{templateId}/overrides/{overrideId}` — update
- `GET /api/v1/tenant-templates/{templateId}/overrides/{overrideId}` — get by ID
- `GET /api/v1/tenant-templates/{templateId}/overrides?tenantId=` — list
- `DELETE /api/v1/tenant-templates/{templateId}/overrides/{overrideId}` — deactivate

### Step 8 — Effective resolution endpoint
- `GET /api/v1/tenant-templates/{templateId}/effective?tenantId=`

### Step 9 — Validation and conflict handling
- All validation rules implemented and verified

### Step 10 — Validation
- All 10 tests passed

---

## Files Created
- `reports/src/Reports.Domain/Entities/TenantReportOverride.cs`
- `reports/src/Reports.Infrastructure/Persistence/Configurations/TenantReportOverrideConfiguration.cs`
- `reports/src/Reports.Contracts/Persistence/ITenantReportOverrideRepository.cs`
- `reports/src/Reports.Infrastructure/Persistence/EfTenantReportOverrideRepository.cs`
- `reports/src/Reports.Infrastructure/Persistence/MockTenantReportOverrideRepository.cs`
- `reports/src/Reports.Application/Overrides/ITenantReportOverrideService.cs`
- `reports/src/Reports.Application/Overrides/TenantReportOverrideService.cs`
- `reports/src/Reports.Application/Overrides/DTOs/CreateTenantReportOverrideRequest.cs`
- `reports/src/Reports.Application/Overrides/DTOs/UpdateTenantReportOverrideRequest.cs`
- `reports/src/Reports.Application/Overrides/DTOs/TenantReportOverrideResponse.cs`
- `reports/src/Reports.Application/Overrides/DTOs/TenantEffectiveReportResponse.cs`
- `reports/src/Reports.Api/Endpoints/OverrideEndpoints.cs`
- `reports/src/Reports.Infrastructure/Migrations/20260415082312_AddTenantReportOverrides.cs`
- `reports/src/Reports.Infrastructure/Migrations/20260415082312_AddTenantReportOverrides.Designer.cs`

## Files Modified
- `reports/src/Reports.Infrastructure/Persistence/ReportsDbContext.cs` — added DbSet + SaveChangesAsync handler
- `reports/src/Reports.Application/DependencyInjection.cs` — registered override service
- `reports/src/Reports.Infrastructure/DependencyInjection.cs` — registered override repositories
- `reports/src/Reports.Api/Program.cs` — added `MapOverrideEndpoints()`

## Migration Output
```
Migration: 20260415082312_AddTenantReportOverrides
Applied: SUCCESS
Table: rpt_TenantReportOverrides
Indexes:
  - IX_rpt_TenantReportOverrides_TenantId
  - IX_rpt_TenantReportOverrides_ReportTemplateId
  - IX_rpt_TenantReportOverrides_TenantId_ReportTemplateId
  - IX_rpt_TenantReportOverrides_TenantId_ReportTemplateId_IsActive
FK: ReportTemplateId → rpt_ReportDefinitions(Id) CASCADE
```

## Database Schema Summary
```sql
rpt_TenantReportOverrides (
  Id                        CHAR(36) PK,
  TenantId                  VARCHAR(100) NOT NULL,
  ReportTemplateId          CHAR(36) NOT NULL FK,
  BaseTemplateVersionNumber INT NOT NULL,
  NameOverride              VARCHAR(200) NULL,
  DescriptionOverride       VARCHAR(2000) NULL,
  LayoutConfigJson          LONGTEXT NULL,
  ColumnConfigJson          LONGTEXT NULL,
  FilterConfigJson          LONGTEXT NULL,
  FormulaConfigJson         LONGTEXT NULL,
  HeaderConfigJson          LONGTEXT NULL,
  FooterConfigJson          LONGTEXT NULL,
  IsActive                  TINYINT(1) NOT NULL,
  RequiredFeatureCode       VARCHAR(100) NULL,
  MinimumTierCode           VARCHAR(50) NULL,
  CreatedAtUtc              DATETIME(6) NOT NULL,
  CreatedByUserId           VARCHAR(100) NOT NULL,
  UpdatedAtUtc              DATETIME(6) NOT NULL,
  UpdatedByUserId           VARCHAR(100) NULL
)
```

## API Validation Results
| # | Test | Expected | Actual | Result |
|---|------|----------|--------|--------|
| 1 | Create override for assigned template with published version | 201 | 201 | PASS |
| 2 | Reject duplicate active override | 409 | 409 | PASS |
| 3 | Reject override for unassigned template | 400 | 400 | PASS |
| 4 | Get override by ID | 200 | 200 | PASS |
| 5 | List overrides by tenant/template | 200 (count=1) | 200 | PASS |
| 6 | Effective resolution with override (hasOverride=true, effectiveName="Custom Name") | 200 | 200 | PASS |
| 7 | Deactivate override (isActive=false) | 200 | 200 | PASS |
| 8 | Effective resolution without override (hasOverride=false, effectiveName=base) | 200 | 200 | PASS |
| 9 | Health endpoint | 200 | 200 | PASS |
| 10 | Ready endpoint | 200 | 200 | PASS |

**10/10 passed**

## Build / Run / Validation Status
- **Build:** 0 errors, 0 warnings
- **Startup:** Service starts and listens on port 5029
- **Health:** Returns 200 `{"status":"healthy"}`
- **Ready:** Returns 200 with all adapter checks passing
- **Existing APIs:** Template, version, assignment APIs all working (used in test setup)

## Issues Encountered
- None

## Decisions Made
1. Used `longtext` MySQL column type for JSON override fields (layout, column, filter, formula, header, footer) to accommodate future complex configurations
2. Soft-deactivate via `DELETE` endpoint sets `IsActive=false` rather than physical delete
3. Assignment dependency check uses existing `HasActiveGlobalAssignmentAsync` + `HasActiveTenantAssignmentAsync` from `ITemplateAssignmentRepository`
4. Effective resolution overlays override fields on top of base template metadata — returns null for JSON fields when no override exists (base template doesn't have JSON configs at this stage)
5. `BaseTemplateVersionNumber` is captured at override creation time from the currently published version
6. Audit hooks use warning-only pattern consistent with LS-REPORTS-02-001

## Known Gaps / Not Yet Implemented
- No UI for override management (out of scope per story)
- No version drift detection (when base template publishes new version vs. override's BaseTemplateVersionNumber)
- No JSON validation for config override fields (acceptable at this foundation stage)
- No tenant permission/entitlement checks (deferred to future stories)

## Final Summary
LS-REPORTS-02-002 is **COMPLETE**. All 15 acceptance criteria are met:
1. ✅ Tenant override persistence entity exists
2. ✅ EF configuration exists for tenant overrides
3. ✅ Migration exists and applied for tenant override table
4. ✅ Repository support exists (EF + mock)
5. ✅ Service layer exists with full business logic
6. ✅ Override CRUD APIs exist
7. ✅ Effective tenant report resolution API exists
8. ✅ Override only created for assigned templates with published versions
9. ✅ One-active-override-per-tenant-per-template enforced
10. ✅ Effective resolution returns base + override-derived output
11. ✅ DTOs isolate API from persistence/domain entities
12. ✅ Service builds successfully (0 errors, 0 warnings)
13. ✅ Service starts successfully
14. ✅ `/api/v1/health` works
15. ✅ `/api/v1/ready` works
