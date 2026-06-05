# LS-REPORTS-03-001 — Report Execution Engine (Read Model + Query Layer)

## Story ID
LS-REPORTS-03-001

## Objective
Introduce the first report execution engine so the Reports Service can take a resolved report definition and actually run it against product data sources, returning a structured tabular result set suitable for on-screen viewing, export generation, and scheduled delivery.

## Scope
- Execution request/response DTOs
- Query adapter abstraction (`IReportDataQueryAdapter`) + mock implementation
- Execution service layer (`IReportExecutionService` / `ReportExecutionService`)
- Runtime resolution flow (template → assignment → published version → override → execute)
- Tabular result model (columns + rows)
- Execution persistence (status transitions: Pending → Running → Completed/Failed)
- Execution API endpoints (`POST /api/v1/report-executions`, `GET /api/v1/report-executions/{executionId}`)
- Row cap guardrail (500 rows)
- Audit hooks (started/completed/failed)

## Out of Scope
- PDF/CSV/XLSX export generation
- Scheduling / background execution
- Drag-and-drop builder UI
- Advanced analytics / charting
- Dapper optimization
- Caching optimization
- Advanced formula parsing

---

## Execution Log

### Step 1 — Report created FIRST
- Created `/analysis/LS-REPORTS-03-001-report.md` before any code changes.

### Step 2 — Execution DTOs
- Created `ExecuteReportRequest` — request DTO with TenantId, TemplateId, ProductCode, OrganizationType, ParametersJson, RequestedByUserId, UseOverride
- Created `ReportExecutionResponse` — full response with execution metadata, columns, rows, row count
- Created `ReportColumnResponse` — column metadata (Key, Label, DataType, Order)
- Created `ReportRowResponse` — row payload wrapper (Dictionary values)
- Created `ReportExecutionSummaryResponse` — lightweight execution status response for GET endpoint
- Created `ExecutionDefinition` — internal DTO for resolved effective execution config

### Step 3 — Query adapter contract and implementation
- Created `IReportDataQueryAdapter` contract in Reports.Contracts with:
  - `SupportsProduct(productCode)` — validates product support
  - `ExecuteQueryAsync(ReportQueryContext, ct)` — executes and returns `TabularResultSet`
- Created supporting contracts: `ReportQueryContext`, `TabularColumn`, `TabularResultSet`
- Created `MockReportDataQueryAdapter` with product-specific realistic mock data:
  - LIENS: lienId, claimantName, providerName, amount, status, filedDate
  - FUND: fundingId, applicantName, requestedAmount, approvedAmount, status, requestDate
  - CARECONNECT: referralId, patientName, referringProvider, receivingProvider, status, referralDate
  - Generic fallback for unknown products

### Step 4 — Execution service layer
- Created `IReportExecutionService` interface (ExecuteReportAsync, GetExecutionByIdAsync)
- Created `ReportExecutionService` with full runtime resolution flow:
  1. Request validation (5 required fields)
  2. Template existence check
  3. Template active check
  4. Product code alignment check
  5. Tenant assignment check (global + tenant-scoped)
  6. Published version check
  7. Product support check
  8. Tenant override resolution (when UseOverride=true)
  9. Execution definition derivation
  10. Execution record creation (Status=Pending)
  11. Status transition to Running
  12. Query adapter invocation
  13. Status transition to Completed (or Failed on error)
  14. Normalized response shaping

### Step 5 — Persistence alignment
- Existing `ReportExecution` entity already has all required fields (Id, TenantId, UserId, ReportTemplateId, TemplateVersionNumber, Status, FailureReason, CreatedAtUtc, CompletedAtUtc)
- No migration needed — schema already supports runtime execution tracking

### Step 6 — Execution endpoints
- `POST /api/v1/report-executions` — synchronous execute
- `GET /api/v1/report-executions/{executionId}` — get execution summary by ID
- Registered in Program.cs via `MapExecutionEndpoints()`

### Step 7 — Validation and guardrails
- Row cap: 500 rows (MaxRowCap constant)
- Required field validation: TenantId, TemplateId, ProductCode, OrganizationType, RequestedByUserId
- Assignment eligibility: global or tenant-scoped assignment required
- Published version required
- Product code alignment enforced
- Template active status checked
- Failure path: execution record updated to Failed with FailureReason

### Step 8 — Validation results
- All 12 tests passed (see below)

---

## Files Created
- `reports/src/Reports.Application/Execution/DTOs/ExecuteReportRequest.cs`
- `reports/src/Reports.Application/Execution/DTOs/ReportExecutionResponse.cs`
- `reports/src/Reports.Application/Execution/DTOs/ReportColumnResponse.cs`
- `reports/src/Reports.Application/Execution/DTOs/ReportRowResponse.cs`
- `reports/src/Reports.Application/Execution/DTOs/ReportExecutionSummaryResponse.cs`
- `reports/src/Reports.Application/Execution/DTOs/ExecutionDefinition.cs`
- `reports/src/Reports.Contracts/Adapters/IReportDataQueryAdapter.cs`
- `reports/src/Reports.Infrastructure/Adapters/MockReportDataQueryAdapter.cs`
- `reports/src/Reports.Application/Execution/IReportExecutionService.cs`
- `reports/src/Reports.Application/Execution/ReportExecutionService.cs`
- `reports/src/Reports.Api/Endpoints/ExecutionEndpoints.cs`

## Files Modified
- `reports/src/Reports.Application/DependencyInjection.cs` — registered `IReportExecutionService`
- `reports/src/Reports.Infrastructure/DependencyInjection.cs` — registered `IReportDataQueryAdapter`
- `reports/src/Reports.Api/Program.cs` — added `MapExecutionEndpoints()`

## API Validation Results

| # | Test | Expected | Actual | Result |
|---|------|----------|--------|--------|
| 1 | Execute report (assigned template, published version) | 200 | 200 | PASS |
| 2 | Get execution by ID | 200 | 200 | PASS |
| 3 | Missing TenantId | 400 | 400 | PASS |
| 4 | Template not found | 404 | 404 | PASS |
| 5 | Unassigned tenant | 400 | 400 | PASS |
| 6 | Product code mismatch | 400 | 400 | PASS |
| 7 | Execute with override (hasOverride=true, templateName=Custom) | 200 | 200 | PASS |
| 8 | Execute without override (hasOverride=false, templateName=base) | 200 | 200 | PASS |
| 9 | Execution not found | 404 | 404 | PASS |
| 10 | Missing required fields | 400 | 400 | PASS |
| 11 | Health endpoint | 200 | 200 | PASS |
| 12 | Ready endpoint | 200 | 200 | PASS |

**12/12 passed**

### Detailed Response Verification

#### T1 — Successful Execution
- executionId: valid GUID
- templateCode: correct
- templateName: "Execution E2E Test" (base)
- status: Completed
- rowCount: 25
- columns: 6 (lienId:string, claimantName:string, providerName:string, amount:decimal, status:string, filedDate:date)
- sample row: `{"lienId":"LN-t-exec-0001","claimantName":"Claimant 1","providerName":"Provider 2","amount":1250.5,"status":"Pending","filedDate":"2026-04-12"}`
- publishedVersionNumber: 1

#### T7 — Execution With Override
- templateName: "Custom Liens Report" (override applied)
- hasOverride: true
- baseTemplateVersionNumber: 1

#### T8 — Execution Without Override
- templateName: "Execution E2E Test" (base, override skipped)
- hasOverride: false

## Build / Run / Validation Status
- **Build:** 0 errors, 0 warnings
- **Startup:** Service starts and listens on port 5029
- **Health:** Returns 200 `{"status":"healthy"}`
- **Ready:** Returns 200 with all adapter checks passing
- **Existing APIs:** Template, version, assignment, override APIs all working (used in test setup)

## Issues Encountered
- None

## Decisions Made
1. **Row cap: 500 rows** — `MaxRowCap` constant in `ReportExecutionService`. Mock adapter respects it via `MaxRows` parameter. Safe truncation approach (mock generates at most min(MaxRows, 25) rows).
2. **Synchronous execution** — entire flow in one request (Pending → Running → Completed/Failed). Status transitions still persisted for auditability.
3. **Mock adapter generates 25 rows** — realistic but not excessive for development validation. Row cap at 500 allows headroom for real adapters.
4. **Product-specific column metadata** — mock adapter returns product-appropriate columns (LIENS: lienId/claimantName/amount etc., FUND: fundingId/applicantName/requestedAmount etc., CARECONNECT: referralId/patientName/referringProvider etc.)
5. **UseOverride flag** — defaults to `true`. When `false`, override resolution is skipped entirely.
6. **Product code alignment** — execution validates that request.ProductCode matches template.ProductCode to prevent cross-product execution.
7. **Existing ReportExecution entity** — no migration needed; existing schema supports all runtime fields.
8. **Adapter contract is real and portable** — `IReportDataQueryAdapter` accepts `ReportQueryContext` with tenant/product/template context and returns normalized `TabularResultSet`. Can be swapped for real product data sources without changing service layer.

## Known Gaps / Not Yet Implemented
- No real product data source integration (mock adapter only)
- No PDF/CSV/XLSX export generation
- No scheduling or background execution
- No row cap enforcement for real data sources (mock stays under cap naturally)
- No execution history pagination endpoint
- No execution cancellation
- No tenant permission/entitlement checks on execution
- No rate limiting on execution endpoint
- No execution result caching

## Endpoint Summary

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/report-executions` | Execute a report synchronously |
| GET | `/api/v1/report-executions/{executionId}` | Get execution status by ID |

## Execution Flow Summary
```
POST /api/v1/report-executions
  → validate required fields (TenantId, TemplateId, ProductCode, OrganizationType, RequestedByUserId)
  → resolve template (must exist, must be active)
  → validate product code alignment
  → validate tenant assignment (global or tenant-scoped)
  → resolve published version (must exist)
  → check product support via query adapter
  → resolve tenant override (if UseOverride=true)
  → build execution definition (merge template + override)
  → create execution record (Status=Pending)
  → transition to Running
  → invoke query adapter
  → on success: transition to Completed, return tabular response
  → on failure: transition to Failed, store FailureReason, return error
```

## Query Adapter Summary
- **Contract:** `IReportDataQueryAdapter` in `Reports.Contracts.Adapters`
- **Input:** `ReportQueryContext` (tenant, product, template, version, config JSONs, parameters, max rows)
- **Output:** `AdapterResult<TabularResultSet>` (columns + rows + total count + truncation flag)
- **Current implementation:** `MockReportDataQueryAdapter` — returns product-specific realistic mock data
- **Extensibility:** Swap implementation in DI for real product data queries

## Final Summary
LS-REPORTS-03-001 is **COMPLETE**. All 15 acceptance criteria are met:
1. ✅ Execution request/response DTOs exist
2. ✅ Query adapter abstraction exists (`IReportDataQueryAdapter`)
3. ✅ Execution service layer exists (`ReportExecutionService`)
4. ✅ Execution API endpoint exists (`POST /api/v1/report-executions`)
5. ✅ Execution validates template assignment and published version
6. ✅ Tenant override participates in execution resolution
7. ✅ `ReportExecution` records are created and updated (Pending → Running → Completed/Failed)
8. ✅ Synchronous execution returns normalized tabular results
9. ✅ Row count and column metadata are returned
10. ✅ Failure path updates execution status correctly
11. ✅ DTOs isolate API from persistence/domain entities
12. ✅ Service builds successfully (0 errors, 0 warnings)
13. ✅ Service starts successfully
14. ✅ `/api/v1/health` works
15. ✅ `/api/v1/ready` works

## Recommendation for Next Story
- **LS-REPORTS-03-002**: Swap mock query adapter for real product data source integration (connect to Liens/Fund/CareConnect read replicas)
- **LS-REPORTS-04-001**: Export generation (PDF/CSV/XLSX) from execution results
- **LS-REPORTS-05-001**: Scheduled execution with background worker
