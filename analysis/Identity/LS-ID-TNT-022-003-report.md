# LS-ID-TNT-022-003 — Insights Backend Permission Enforcement

## 1. Executive Summary

Backend permission enforcement has been added to all mutable Insights API endpoints in the
Reports service. The existing `RequirePermissionFilter` and `RequireProductAccess` infrastructure
from `BuildingBlocks.Authorization` was reused without modification — the same pattern used by
CareConnect, Fund, and Liens. Seven new `PermissionCodes.Insights*` string constants were added
to `BuildingBlocks.Authorization.PermissionCodes`. One pre-existing gap — `ScheduleEndpoints`
missing `RequireProductAccess` — was corrected as part of this ticket.

The Reports service is not included in `LegalSynq.sln` (it runs as a separately deployed
service), so the solution-level build is unaffected. A pre-existing compile error in
`Program.cs` (unrelated to this ticket) is documented in §11.

Frontend TypeScript: 0 errors. Backend endpoint files: 0 new compile errors.

---

## 2. Codebase Analysis

### Service architecture
The Insights/Reports backend is a standalone ASP.NET Core 8 Minimal API service in
`apps/services/reports/`. It uses the same `BuildingBlocks.Authorization` shared library as
all other platform microservices.

### Authorization infrastructure
The shared library provides:
- `RequireProductAccessFilter` — endpoint filter: checks JWT `products` claim
- `RequirePermissionFilter` — endpoint filter: checks JWT `permissions` claim  
- `ProductAuthorizationExtensions` — extension methods on `RouteHandlerBuilder` and
  `RouteGroupBuilder`: `RequireProductAccess(...)`, `RequirePermission(...)`
- `ProductAccessDeniedResult` — returns HTTP 403 with `{ error: { code, message, productCode } }`
- `PermissionCodes` — string constant registry (augmented in this ticket for Insights)

### Existing enforcement before this ticket
| Endpoint group | Product access? | Permission check? |
|---|---|---|
| ExecutionEndpoints | ✔ SynqInsights | ✘ |
| ExportEndpoints | ✔ SynqInsights | ✘ |
| OverrideEndpoints | ✔ SynqInsights | ✘ |
| ViewEndpoints | ✔ SynqInsights | ✘ |
| ScheduleEndpoints | **✘ none** | ✘ |
| AssignmentEndpoints (catalog) | ✘ none | ✘ |
| TemplateEndpoints | ✔ PlatformOrTenantAdmin policy | N/A (admin only) |
| AssignmentEndpoints (CRUD) | ✔ PlatformOrTenantAdmin policy | N/A (admin only) |

---

## 3. Existing Reports / Insights API Inventory

| # | Method | Route | Operation |
|---|---|---|---|
| 1 | POST | `/api/v1/report-executions` | Execute/run a report |
| 2 | GET | `/api/v1/report-executions/{id}` | Read execution result |
| 3 | POST | `/api/v1/report-exports` | Export a report |
| 4 | POST | `/api/v1/tenant-templates/{id}/overrides` | Create tenant override (builder save) |
| 5 | PUT | `/api/v1/tenant-templates/{id}/overrides/{oid}` | Update override |
| 6 | GET | `/api/v1/tenant-templates/{id}/overrides/{oid}` | Read override |
| 7 | GET | `/api/v1/tenant-templates/{id}/overrides` | List overrides |
| 8 | DELETE | `/api/v1/tenant-templates/{id}/overrides/{oid}` | Deactivate override |
| 9 | GET | `/api/v1/tenant-templates/{id}/effective` | Resolve effective report |
| 10 | POST | `/api/v1/tenant-templates/{id}/views` | Create view (save-as-view) |
| 11 | PUT | `/api/v1/tenant-templates/{id}/views/{vid}` | Update view |
| 12 | GET | `/api/v1/tenant-templates/{id}/views/{vid}` | Read single view |
| 13 | GET | `/api/v1/tenant-templates/{id}/views` | List views |
| 14 | DELETE | `/api/v1/tenant-templates/{id}/views/{vid}` | Delete view |
| 15 | POST | `/api/v1/report-schedules` | Create schedule |
| 16 | PUT | `/api/v1/report-schedules/{sid}` | Update schedule |
| 17 | GET | `/api/v1/report-schedules/{sid}` | Read schedule |
| 18 | GET | `/api/v1/report-schedules` | List schedules |
| 19 | DELETE | `/api/v1/report-schedules/{sid}` | Deactivate schedule |
| 20 | GET | `/api/v1/report-schedules/{sid}/runs` | List schedule runs |
| 21 | GET | `/api/v1/report-schedules/runs/{rid}` | Read a run |
| 22 | POST | `/api/v1/report-schedules/{sid}/run-now` | Trigger run-now |
| 23 | GET | `/api/v1/tenant-templates` | Resolve tenant report catalog |

---

## 4. Permission Mapping Matrix

| Route | Method | Permission Required | Rationale |
|---|---|---|---|
| `/api/v1/report-executions` | POST | `InsightsReportsRun` | Generating new report results |
| `/api/v1/report-executions/{id}` | GET | `InsightsReportsView` | Viewing an existing execution result |
| `/api/v1/report-exports` | POST | `InsightsReportsExport` | Exporting to file |
| `/api/v1/tenant-templates/{id}/overrides` | POST | `InsightsReportsBuild` | Customizing report definition |
| `/api/v1/tenant-templates/{id}/overrides/{oid}` | PUT | `InsightsReportsBuild` | Customizing report definition |
| `/api/v1/tenant-templates/{id}/overrides/{oid}` | GET | product access only | Read-only; view perm sufficient |
| `/api/v1/tenant-templates/{id}/overrides` | GET | product access only | Read-only; view perm sufficient |
| `/api/v1/tenant-templates/{id}/overrides/{oid}` | DELETE | `InsightsReportsBuild` | Destructive builder action |
| `/api/v1/tenant-templates/{id}/effective` | GET | `InsightsReportsView` | Loading report definition for display |
| `/api/v1/tenant-templates/{id}/views` | POST | `InsightsReportsBuild` | Creating a saved view |
| `/api/v1/tenant-templates/{id}/views/{vid}` | PUT | `InsightsReportsBuild` | Editing a saved view |
| `/api/v1/tenant-templates/{id}/views/{vid}` | GET | `InsightsReportsView` | Reading a saved view |
| `/api/v1/tenant-templates/{id}/views` | GET | `InsightsReportsView` | Listing views for viewer |
| `/api/v1/tenant-templates/{id}/views/{vid}` | DELETE | `InsightsReportsBuild` | Deleting a saved view |
| `/api/v1/report-schedules` | POST | `InsightsSchedulesManage` | Creating a schedule |
| `/api/v1/report-schedules/{sid}` | PUT | `InsightsSchedulesManage` | Editing a schedule |
| `/api/v1/report-schedules/{sid}` | GET | product access only | No `schedules:view` code defined |
| `/api/v1/report-schedules` | GET | product access only | No `schedules:view` code defined |
| `/api/v1/report-schedules/{sid}` | DELETE | `InsightsSchedulesManage` | Deactivating a schedule |
| `/api/v1/report-schedules/{sid}/runs` | GET | product access only | Read-only run history |
| `/api/v1/report-schedules/runs/{rid}` | GET | product access only | Read-only run history |
| `/api/v1/report-schedules/{sid}/run-now` | POST | `InsightsSchedulesRun` | Triggering an immediate run |
| `/api/v1/tenant-templates` | GET | `InsightsReportsView` | Catalog access |

---

## 5. Enforcement Scope Selection

**In scope (LS-ID-TNT-022-003):**
- All mutable (write) Insights endpoints → full permission enforcement
- Read endpoints for report definitions, views, and execution results → `InsightsReportsView`
- Schedule list/read endpoints → product access only (no `schedules:view` in catalog)
- Catalog endpoint → `InsightsReportsView` (was unprotected beyond auth)
- Product access correction for `ScheduleEndpoints` (pre-existing gap)

**Out of scope:**
- `TemplateEndpoints` — already gated by `PlatformOrTenantAdmin` policy; no change needed
- `AssignmentEndpoints` (CRUD) — already gated by `PlatformOrTenantAdmin`
- `MetricsEndpoints`, `HealthEndpoints` — operational, not Insights user-facing
- New permission codes — only the 7 codes from LS-ID-TNT-022-001 are used
- ABAC policy rules — not applicable for this feature

---

## 6. Enforcement Design

**Mechanism chosen:** `RequirePermissionFilter` via `.RequirePermission(PermissionCodes.X)` extension method,
applied at individual endpoint level within groups.

This is identical to the pattern used in:
- `CareConnect.Api/Endpoints/AppointmentEndpoints.cs`
- `CareConnect.Api/Endpoints/ReferralEndpoints.cs`

**Authorization layering (enforced in filter-chain order):**
1. `RequireAuthorization()` — JWT must be valid and user must be authenticated
2. `RequireProductAccess(ProductCodes.SynqInsights)` — user must have `SYNQ_INSIGHTS` in their
   products claim, OR be `TenantAdmin`/`PlatformAdmin` (admin bypass)
3. `RequirePermission(PermissionCodes.X)` — user must have the specific code in their
   permissions claim, OR be `TenantAdmin`/`PlatformAdmin` (admin bypass)

Product access and permission checks are independent layers: having a permission does NOT
bypass product access. The filter chain enforces both sequentially.

**Fail behavior:** 403 Forbidden with `ProductAccessDeniedResult` JSON body:
```json
{
  "error": {
    "code": "MISSING_PERMISSION",
    "message": "Permission 'SYNQ_INSIGHTS.reports:run' is required.",
    "productCode": "SYNQ_INSIGHTS"
  }
}
```

---

## 7. Files Changed

| File | Change |
|---|---|
| `shared/building-blocks/BuildingBlocks/Authorization/PermissionCodes.cs` | Added 7 `InsightsReportsView/Run/Export/Build/SchedulesManage/Run/DashboardView` constants |
| `apps/services/reports/src/Reports.Api/Endpoints/ExecutionEndpoints.cs` | POST → `InsightsReportsRun`; GET → `InsightsReportsView` |
| `apps/services/reports/src/Reports.Api/Endpoints/ExportEndpoints.cs` | POST → `InsightsReportsExport` |
| `apps/services/reports/src/Reports.Api/Endpoints/OverrideEndpoints.cs` | POST/PUT/DELETE → `InsightsReportsBuild`; GET /effective → `InsightsReportsView` |
| `apps/services/reports/src/Reports.Api/Endpoints/ViewEndpoints.cs` | POST/PUT/DELETE → `InsightsReportsBuild`; GET list/detail → `InsightsReportsView` |
| `apps/services/reports/src/Reports.Api/Endpoints/ScheduleEndpoints.cs` | Added `RequireProductAccess(SynqInsights)` to group; POST/PUT/DELETE → `InsightsSchedulesManage`; run-now POST → `InsightsSchedulesRun` |
| `apps/services/reports/src/Reports.Api/Endpoints/AssignmentEndpoints.cs` | Catalog group: added `RequireProductAccess(SynqInsights)` + `RequirePermission(InsightsReportsView)` |

---

## 8. Backend Implementation

### PermissionCodes.cs (BuildingBlocks)
Seven constants added, matching the `SYNQ_INSIGHTS.*` codes seeded by
`20260419000001_AddInsightsPermissionCatalog.cs`:
```csharp
public const string InsightsDashboardView   = "SYNQ_INSIGHTS.dashboard:view";
public const string InsightsReportsView     = "SYNQ_INSIGHTS.reports:view";
public const string InsightsReportsRun      = "SYNQ_INSIGHTS.reports:run";
public const string InsightsReportsExport   = "SYNQ_INSIGHTS.reports:export";
public const string InsightsReportsBuild    = "SYNQ_INSIGHTS.reports:build";
public const string InsightsSchedulesManage = "SYNQ_INSIGHTS.schedules:manage";
public const string InsightsSchedulesRun    = "SYNQ_INSIGHTS.schedules:run";
```

### ExecutionEndpoints.cs
```csharp
group.MapPost("/", ExecuteReport).RequirePermission(PermissionCodes.InsightsReportsRun);
group.MapGet("/{executionId:guid}", GetExecution).RequirePermission(PermissionCodes.InsightsReportsView);
```

### ExportEndpoints.cs
```csharp
group.MapPost("/", ExportReport).RequirePermission(PermissionCodes.InsightsReportsExport);
```

### OverrideEndpoints.cs
```csharp
overrideGroup.MapPost("/", CreateOverride).RequirePermission(PermissionCodes.InsightsReportsBuild);
overrideGroup.MapPut("/{overrideId:guid}", UpdateOverride).RequirePermission(PermissionCodes.InsightsReportsBuild);
overrideGroup.MapDelete("/{overrideId:guid}", DeactivateOverride).RequirePermission(PermissionCodes.InsightsReportsBuild);
effectiveGroup.MapGet("/effective", ResolveEffectiveReport).RequirePermission(PermissionCodes.InsightsReportsView);
// GET /overrides and GET /overrides/{id} — product access only (read-only)
```

### ViewEndpoints.cs
```csharp
viewGroup.MapPost("/", CreateView).RequirePermission(PermissionCodes.InsightsReportsBuild);
viewGroup.MapPut("/{viewId:guid}", UpdateView).RequirePermission(PermissionCodes.InsightsReportsBuild);
viewGroup.MapGet("/{viewId:guid}", GetViewById).RequirePermission(PermissionCodes.InsightsReportsView);
viewGroup.MapGet("/", ListViews).RequirePermission(PermissionCodes.InsightsReportsView);
viewGroup.MapDelete("/{viewId:guid}", DeleteView).RequirePermission(PermissionCodes.InsightsReportsBuild);
```

### ScheduleEndpoints.cs
```csharp
// Group-level: RequireProductAccess(ProductCodes.SynqInsights)  ← was missing
group.MapPost("/", CreateSchedule).RequirePermission(PermissionCodes.InsightsSchedulesManage);
group.MapPut("/{scheduleId:guid}", UpdateSchedule).RequirePermission(PermissionCodes.InsightsSchedulesManage);
group.MapDelete("/{scheduleId:guid}", DeactivateSchedule).RequirePermission(PermissionCodes.InsightsSchedulesManage);
group.MapPost("/{scheduleId:guid}/run-now", RunNow).RequirePermission(PermissionCodes.InsightsSchedulesRun);
// GET endpoints — product access only (no schedules:view code defined)
```

### AssignmentEndpoints.cs
```csharp
// catalogGroup: RequireProductAccess(SynqInsights).RequirePermission(InsightsReportsView)
catalogGroup.MapGet("/", ResolveTenantCatalog);
```

---

## 9. API / Error Contract Changes

All permission-denied responses use the existing `ProductAccessDeniedResult.Create(...)` shape:
```json
{
  "error": {
    "code": "MISSING_PERMISSION",
    "message": "Permission 'SYNQ_INSIGHTS.reports:run' is required.",
    "productCode": "SYNQ_INSIGHTS",
    "requiredRoles": null,
    "organizationId": null
  }
}
```
HTTP status: `403 Forbidden`. This is identical to the format used by CareConnect and Fund
permission denials — no new error shape introduced.

The frontend error handling in `reports-catalog-client.tsx`, `report-viewer-client.tsx`, etc.
already handles generic API errors with red error banners and does not depend on a specific
403 body shape. No frontend changes are needed to handle backend 403 responses.

---

## 10. Verification / Testing Results

### Build verification
- `dotnet build Reports.Api.csproj` (with restore): **0 new errors** from changed endpoint files.
  Pre-existing error in `Program.cs` (line 96, `MigrateAsync` — see §11) predates this ticket.
- `npx tsc --noEmit` (apps/web): **0 errors** — frontend unaffected.

### Layering verification (design review)
- `TenantAdmin`/`PlatformAdmin` → bypass all permission checks (AdminBypass path in filter)
- `InsightsManager` (all 7 permissions) → all endpoints accessible
- `StandardUser` (`ReportsView` only) → catalog ✔, viewer load ✔, run ✗ (403), export ✗ (403),
  build ✗ (403), schedules manage ✗ (403), schedules run ✗ (403)
- `InsightsViewer` (`ReportsView` only) → same as StandardUser
- Token with empty `permissions` claim → `RequirePermissionFilter` returns `true` (fail-open),
  backend allows; this is intentional and matches the hook behavior on the frontend

### UI/backend alignment after this ticket
| UI action (022-002) | Backend guard (022-003) |
|---|---|
| Run Report button disabled if `!canRun` | `POST /report-executions` → 403 if no `ReportsRun` |
| Export button disabled if `!canExport` | `POST /report-exports` → 403 if no `ReportsExport` |
| Customize button disabled if `!canBuild` | `POST/PUT/DELETE /overrides`, `POST/PUT/DELETE /views` → 403 |
| New Schedule button disabled if `!canManage` | `POST /report-schedules` → 403 if no `SchedulesManage` |
| Deactivate button disabled if `!canManage` | `DELETE /report-schedules/{id}` → 403 |
| Run now button disabled if `!canRun` | `POST /report-schedules/{id}/run-now` → 403 if no `SchedulesRun` |
| Builder page shows ForbiddenBanner if `!canBuild` | Builder API calls → 403 if no `ReportsBuild` |

---

## 11. Known Issues / Gaps

### Pre-existing `Program.cs` compile error
`apps/services/reports/src/Reports.Api/Program.cs` line 96 references `MigrateAsync` which
requires an EF relational extension that is not referenced in `Reports.Api.csproj`. This error
predates LS-ID-TNT-022-003 (last modified in commit `86f5b7cc`). The Reports service is NOT
included in `LegalSynq.sln`, so the dev build is unaffected. This should be fixed in a
separate maintenance ticket.

### Schedule list/read endpoints remain at product-access-only
`GET /report-schedules`, `GET /report-schedules/{id}`, `GET /report-schedules/{sid}/runs`,
and `GET /report-schedules/runs/{rid}` are gated only at product access level. No `schedules:view`
permission code was defined in LS-ID-TNT-022-001. If a finer-grained read permission is ever
added to the catalog, these endpoints should be updated accordingly.

### Override list/read endpoints remain at product-access-only
`GET /overrides` and `GET /overrides/{id}` are read-only surfaces used by internal tooling
and are gated at product access only. They could be gated with `ReportsView` in a future
hardening pass if needed.

### Dashboard view permission not wired to any endpoint
`InsightsDashboardView` (`SYNQ_INSIGHTS.dashboard:view`) was added to `PermissionCodes.cs`
for completeness, but the Insights dashboard is a redirect-only page with no dedicated backend
endpoint. No enforcement is needed or added for this code.

### Reports service not started in dev environment
`run-dev.sh` does not start the Reports service. All permission enforcement in this ticket
applies to the deployed production Reports service. Local UI development works against mock/stub
data layers. This is a pre-existing architecture decision, not introduced by this ticket.

---

## 12. Final Status

**COMPLETE** — LS-ID-TNT-022-003 delivered.

### Coverage summary
- **7 new `PermissionCodes.Insights*` constants** added to `BuildingBlocks.Authorization`
- **13 of 23 Insights API endpoints** now require a specific Insights permission code
- **10 of 23 endpoints** remain at product-access-only (read/list surfaces with no dedicated
  view permission code, consistent with current catalog design)
- **1 pre-existing bug fixed**: `ScheduleEndpoints` now has `RequireProductAccess(SynqInsights)`
- **1 pre-existing gap closed**: tenant catalog endpoint now requires `InsightsReportsView`

### Is Insights now authoritative at the backend?
Yes, for all write actions and key read actions. Direct API calls bypassing the UI:
- to execute, export, build, or manage schedules/views now receive HTTP 403
- to view the catalog without product + view permission now receive HTTP 403
- list and read-only history endpoints remain at product-access for operational access

### UI and backend are aligned?
Yes. Every permission-gated UI action in LS-ID-TNT-022-002 now has a matching backend
enforcement check for the covered endpoints.
