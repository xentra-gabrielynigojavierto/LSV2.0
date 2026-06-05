# LS-REPORTS-00-002 — Adapter Interface Hardening

## Story ID
LS-REPORTS-00-002

## Objective
Evolve the current adapter layer from mock-friendly scaffolding into production-ready integration contracts with strong typing, context models, standard result wrappers, error handling, and correlation propagation.

## Scope

### In Scope
- Adapter interface redesign with strong typing
- Shared context models (RequestContext, UserContext, TenantContext, ProductContext)
- Standard AdapterResult<T> wrapper
- Adapter error model with standard error codes
- Correlation propagation via RequestContext
- Updated mock adapter implementations
- Health/readiness endpoint updates
- Build validation across all 10 projects

### Out of Scope
- Real adapter integrations
- Database/MySQL integration
- Report templates / execution / scheduling
- UI / authentication middleware

## Execution Log

### Step 1 — Create report file
- **Status**: Complete

### Step 2 — Create context models
- **Status**: Complete
- **Files**: `RequestContext.cs`, `UserContext.cs`, `TenantContext.cs`, `ProductContext.cs` in `Reports.Contracts/Context/`

### Step 3 — Create AdapterResult<T> and AdapterErrors
- **Status**: Complete
- **Files**: `AdapterResult.cs`, `AdapterErrors.cs` in `Reports.Contracts/Adapters/`

### Step 4 — Refactor all 7 adapter interfaces
- **Status**: Complete
- **Changes**: All interfaces now accept `RequestContext` as first parameter, use typed context models instead of primitive strings, and return `AdapterResult<T>`

### Step 5 — Update all 7 mock adapter implementations
- **Status**: Complete
- **Changes**: All mocks updated to match new interfaces, log correlation IDs, and return `AdapterResult<T>` wrappers

### Step 6 — Update health/readiness probes
- **Status**: Complete
- **Changes**: `/api/v1/ready` now creates a `RequestContext` and typed probe objects for readiness checks

### Step 7 — Build validation
- **Status**: Complete
- **Result**: `dotnet build Reports.sln` — 0 warnings, 0 errors, all 10 projects (7 src + 3 test) compiled successfully

### Step 8 — Finalize report
- **Status**: Complete

## Files Created
| File | Purpose |
|------|---------|
| `reports/src/Reports.Contracts/Context/RequestContext.cs` | Correlation/request metadata context |
| `reports/src/Reports.Contracts/Context/UserContext.cs` | Authenticated user context |
| `reports/src/Reports.Contracts/Context/TenantContext.cs` | Resolved tenant context |
| `reports/src/Reports.Contracts/Context/ProductContext.cs` | Product identifier context |
| `reports/src/Reports.Contracts/Adapters/AdapterResult.cs` | Generic result wrapper with success/failure |
| `reports/src/Reports.Contracts/Adapters/AdapterErrors.cs` | Standard error codes and factory helpers |

## Files Modified
| File | Changes |
|------|---------|
| `reports/src/Reports.Contracts/Adapters/IIdentityAdapter.cs` | Added `RequestContext`, returns `AdapterResult<T>`, `GetUserIdFromTokenAsync` → `GetUserFromTokenAsync` returning `UserContext` |
| `reports/src/Reports.Contracts/Adapters/ITenantAdapter.cs` | Added `RequestContext`, `ResolveTenantIdAsync` → `ResolveTenantAsync` returning `TenantContext` |
| `reports/src/Reports.Contracts/Adapters/IEntitlementAdapter.cs` | Replaced string params with `TenantContext`/`UserContext`, returns `AdapterResult<bool>` |
| `reports/src/Reports.Contracts/Adapters/IAuditAdapter.cs` | Added `RequestContext`/`TenantContext`, returns `AdapterResult<bool>` |
| `reports/src/Reports.Contracts/Adapters/IDocumentAdapter.cs` | Added typed DTOs (`StoreReportRequest`, `StoredDocumentInfo`, `ReportContent`), returns `AdapterResult<T>` |
| `reports/src/Reports.Contracts/Adapters/INotificationAdapter.cs` | Added `ReportNotification` DTO, returns `AdapterResult<bool>` |
| `reports/src/Reports.Contracts/Adapters/IProductDataAdapter.cs` | Added `ProductDataQuery`/`ProductDataResult` DTOs, uses `ProductContext`, returns `AdapterResult<T>` |
| `reports/src/Reports.Infrastructure/Adapters/MockIdentityAdapter.cs` | Updated to new interface signatures |
| `reports/src/Reports.Infrastructure/Adapters/MockTenantAdapter.cs` | Updated to new interface signatures |
| `reports/src/Reports.Infrastructure/Adapters/MockEntitlementAdapter.cs` | Updated to new interface signatures |
| `reports/src/Reports.Infrastructure/Adapters/MockAuditAdapter.cs` | Updated to new interface signatures |
| `reports/src/Reports.Infrastructure/Adapters/MockDocumentAdapter.cs` | Updated to new interface signatures |
| `reports/src/Reports.Infrastructure/Adapters/MockNotificationAdapter.cs` | Updated to new interface signatures |
| `reports/src/Reports.Infrastructure/Adapters/MockProductDataAdapter.cs` | Updated to new interface signatures |
| `reports/src/Reports.Api/Endpoints/HealthEndpoints.cs` | Updated readiness probes for new adapter signatures |

## Endpoints Added or Changed
- `/api/v1/ready` — updated probe calls to use `RequestContext` and typed context objects (no signature change to the HTTP contract)

## Build / Run / Validation Status
- **Build**: `dotnet build Reports.sln` — **0 warnings, 0 errors** — all 10 projects pass
- **DI wiring**: No changes needed (same interface→implementation mapping, singletons)

## Decisions Made
1. `GetUserIdFromTokenAsync` renamed to `GetUserFromTokenAsync` returning full `UserContext` — avoids a second round-trip to get roles
2. `ResolveTenantIdAsync` renamed to `ResolveTenantAsync` returning full `TenantContext` — same rationale
3. Document adapter returns `AdapterErrors.NotFoundResult` for probe calls (expected mock behavior); readiness check uses `.Called` flag
4. `AdapterErrors` provides factory methods for standard HTTP-like error codes (`NOT_FOUND`, `UNAUTHORIZED`, `FORBIDDEN`, `UNAVAILABLE`, `TIMEOUT`, `INVALID_REQUEST`, `UNKNOWN`)
5. `IsRetryable` flag on `AdapterResult<T>` supports upstream retry/circuit-breaker logic

## Known Gaps / Not Yet Implemented
- Real adapter implementations (HTTP clients for Identity, Documents, etc.)
- Retry/circuit-breaker middleware wrapping adapters
- OpenTelemetry trace propagation through `RequestContext.Metadata`
- Integration tests against live service endpoints

## Final Summary
All 7 adapter interfaces have been hardened with:
- **Strong typing**: Primitive string parameters replaced with context models (`RequestContext`, `UserContext`, `TenantContext`, `ProductContext`)
- **Standard result wrapper**: `AdapterResult<T>` with success/failure semantics, error codes, retryability flag, and metadata
- **Correlation propagation**: Every adapter call receives a `RequestContext` with `CorrelationId` and `RequestId`
- **Typed DTOs**: `StoreReportRequest`, `StoredDocumentInfo`, `ReportContent`, `ReportNotification`, `ProductDataQuery`, `ProductDataResult`
- **Error taxonomy**: `AdapterErrors` static class with standard codes and factory methods

The adapter layer is now production-ready for real integrations while maintaining full backward compatibility with the mock implementations and health probes.
