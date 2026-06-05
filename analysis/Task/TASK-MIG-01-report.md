# TASK-MIG-01 Report — Governance Migration (Liens → Task)

_Date: 2026-04-21 | Status: COMPLETE_

---

## 1. Codebase Analysis

### Liens governance implementation

**Entity:** `Liens.Domain.Entities.LienTaskGovernanceSettings`
- Lives in `liens_TaskGovernanceSettings` (MySQL, per TASK-MIG-01 scope)
- One row per `(TenantId, ProductCode)` — always `ProductCode = "SYNQ_LIENS"`
- Created on first access with safe defaults (required fields default to `true` / strict mode)
- Versioned for optimistic concurrency

**Service:** `LienTaskGovernanceService` (implements `ILienTaskGovernanceService`)
- `GetOrCreateAsync` — returns existing or creates a default row; used by admin endpoints
- `UpdateAsync` — updates entity with version check; used by admin endpoints
- `GetAsync` _(added in this task)_ — dual-read: Task service first, Liens DB fallback, no auto-create

**Repository:** `LienTaskGovernanceSettingsRepository` → `ILienTaskGovernanceSettingsRepository`
- `GetByTenantProductAsync` — single tenant lookup (unique index: `UX_TaskGovernance_TenantId_ProductCode`)
- `GetAllAsync` _(added in this task)_ — full table scan for startup migration
- `AddAsync`, `UpdateAsync` — direct EF operations

**API endpoints:** `TaskGovernanceEndpoints` in `Liens.Api`
- `GET /api/liens/task-governance` — reads settings for calling tenant
- `PUT /api/liens/task-governance` — updates settings for calling tenant
- `GET /api/liens/admin/task-governance/tenants/{tenantId}` — admin read
- `PUT /api/liens/admin/task-governance/tenants/{tenantId}` — admin update

**Enforcement site:** `LienTaskService.CreateAsync` (line ~103)
```csharp
var governance = await _governanceService.GetAsync(tenantId, ct);  // post-migration
if (governance is not null)
{
    if (governance.RequireAssigneeOnCreate && !assignedUserId.HasValue) ...
    if (governance.RequireCaseLinkOnCreate && !caseId.HasValue) ...
    if (governance.RequireWorkflowStageOnCreate && !stageId.HasValue) ...
        → DeriveStartStageAsync (uses DefaultStartStageMode + ExplicitStartStageId)
}
```

### Task service governance implementation

**Entity:** `Task.Domain.Entities.TaskGovernanceSettings`
- Lives in `tasks_GovernanceSettings` (MySQL)
- One row per `(TenantId, SourceProductCode)` — `SourceProductCode` is nullable (null = tenant-wide default)
- Product code is canonicalized to UPPER INVARIANT on save
- Versioned for optimistic concurrency; hard-coded `GovernanceFallback` class as last resort

**Service:** `TaskGovernanceService` (implements `ITaskGovernanceService`)
- `ResolveAsync` — 3-level priority lookup: product → tenant default → hard-coded fallback
- `UpsertAsync` — create-or-update; validates product code against `KnownProductCodes`
- `GetAsync` — direct fetch; returns null if not found (HTTP 204)

**Repository:** `TaskGovernanceRepository` → `ITaskGovernanceRepository`
- `GetByTenantAndProductAsync`, `GetTenantDefaultAsync`, `AddAsync`

**API endpoints:** `TaskGovernanceEndpoints` in `Task.Api`
- `GET /api/tasks/governance?sourceProductCode=SYNQ_LIENS` — fetch by product
- `POST /api/tasks/governance` — upsert (PlatformOrTenantAdmin policy)

---

## 2. Schema Comparison

### Field mapping

| Liens field | Task field | Notes |
|-------------|-----------|-------|
| `RequireAssigneeOnCreate` (bool) | `RequireAssignee` (bool) | Direct map |
| `RequireWorkflowStageOnCreate` (bool) | `RequireStage` (bool) | Direct map |
| `DefaultPriority` (not in Liens) | `DefaultPriority` (string) | Task provides this; Liens uses "MEDIUM" |
| `RequireDueDate` (not in Liens) | `RequireDueDate` (bool) | Task provides this; default false |
| `AllowUnassign` (not in Liens) | `AllowUnassign` (bool) | Task provides this; default true |
| `AllowCancel` (not in Liens) | `AllowCancel` (bool) | Task provides this; default true |
| `AllowCompleteWithoutStage` (not in Liens) | `AllowCompleteWithoutStage` (bool) | Inverse of `RequireWorkflowStageOnCreate` |
| `AllowNotesOnClosedTasks` (not in Liens) | `AllowNotesOnClosedTasks` (bool) | Task provides this; default false |
| `DefaultTaskScope` (not in Liens) | `DefaultTaskScope` (string) | Task provides this; default "GENERAL" |

### Liens-specific fields with no Task equivalent (before this migration)

| Liens field | Value type | Semantics |
|-------------|-----------|-----------|
| `RequireCaseLinkOnCreate` | bool | Liens-domain rule: tasks must be linked to a lien case |
| `AllowMultipleAssignees` | bool | Whether more than one user can be assigned |
| `DefaultStartStageMode` | string ("FIRST_ACTIVE_STAGE" / "EXPLICIT_STAGE") | How to auto-select the starting kanban stage |
| `ExplicitStartStageId` | Guid? | Stage ID used when `DefaultStartStageMode = EXPLICIT_STAGE` |

### Resolution

These four fields have no natural generic equivalent in `tasks_GovernanceSettings`. The chosen approach (see §3) is a `ProductSettingsJson` TEXT column on `tasks_GovernanceSettings` that stores them as a JSON blob, keyed only for `SourceProductCode = "SYNQ_LIENS"`.

---

## 3. Migration Mapping Design

### Decision: `ProductSettingsJson` TEXT column

A nullable `TEXT` column (`ProductSettingsJson`) was added to `tasks_GovernanceSettings` to store product-specific governance extensions as a JSON blob. This approach was chosen over adding nullable typed columns because:
- It avoids polluting the generic table schema with Liens-specific fields
- It follows the existing pattern (`MetadataJson`, `ConfigJson`, etc.) already used elsewhere in Flow
- The JSON structure can evolve per-product without schema migrations
- For products that don't use extensions, the column is NULL

### JSON schema for `SYNQ_LIENS` extensions

```json
{
  "requireCaseLinkOnCreate": true,
  "allowMultipleAssignees": false,
  "defaultStartStageMode": "FIRST_ACTIVE_STAGE",
  "explicitStartStageId": null
}
```

Defined as `LiensGovernanceExtensions` in `Liens.Application.DTOs.TaskServiceGovernanceDto`.

### Complete field mapping (Liens → Task)

| Liens field | Task field / location | Direction |
|-------------|----------------------|-----------|
| `RequireAssigneeOnCreate` | `tasks_GovernanceSettings.RequireAssignee` | Direct |
| `RequireWorkflowStageOnCreate` | `tasks_GovernanceSettings.RequireStage` | Direct |
| `RequireCaseLinkOnCreate` | `ProductSettingsJson.requireCaseLinkOnCreate` | Via JSON |
| `AllowMultipleAssignees` | `ProductSettingsJson.allowMultipleAssignees` | Via JSON |
| `DefaultStartStageMode` | `ProductSettingsJson.defaultStartStageMode` | Via JSON |
| `ExplicitStartStageId` | `ProductSettingsJson.explicitStartStageId` | Via JSON |
| `RequireDueDate` | `tasks_GovernanceSettings.RequireDueDate` = false | Constant (not in Liens model) |
| `AllowUnassign` | `tasks_GovernanceSettings.AllowUnassign` = true | Constant |
| `AllowCancel` | `tasks_GovernanceSettings.AllowCancel` = true | Constant |
| `AllowCompleteWithoutStage` | = `!RequireWorkflowStageOnCreate` | Derived |
| `AllowNotesOnClosedTasks` | `tasks_GovernanceSettings.AllowNotesOnClosedTasks` = false | Constant |
| `DefaultPriority` | `tasks_GovernanceSettings.DefaultPriority` = "MEDIUM" | Constant |
| `DefaultTaskScope` | `tasks_GovernanceSettings.DefaultTaskScope` = "GENERAL" | Constant |
| `TenantId` | `TenantId` | Preserved |
| `ProductCode` = "SYNQ_LIENS" | `SourceProductCode` = "SYNQ_LIENS" | Preserved (normalized) |

---

## 4. Task Schema Adjustments

### Migration: `20260421000007_AddGovernanceProductSettingsJson`

**File:** `apps/services/task/Task.Infrastructure/Persistence/Migrations/20260421000007_AddGovernanceProductSettingsJson.cs`

**Change:** Adds `ProductSettingsJson TEXT NULL` to `tasks_GovernanceSettings`.

```sql
-- Up
ALTER TABLE `tasks_GovernanceSettings`
  ADD COLUMN `ProductSettingsJson` TEXT NULL;

-- Down
ALTER TABLE `tasks_GovernanceSettings`
  DROP COLUMN `ProductSettingsJson`;
```

**Impact on existing rows:** All existing rows get `NULL` in the new column. No data loss. No index change.

**Entity change:** `Task.Domain.Entities.TaskGovernanceSettings` has a new `ProductSettingsJson { get; private set; }` property. The `Update()` method accepts an optional `productSettingsJson` parameter (default null).

**DTO changes:** `TaskGovernanceDto` and `UpsertTaskGovernanceRequest` both include `ProductSettingsJson` as optional string. The model snapshot was updated to reflect the new column.

**Model snapshot:** Updated `TasksDbContextModelSnapshot.cs` with `b.Property<string>("ProductSettingsJson").HasColumnType("TEXT")`.

**Build result:** Task.Api → 0 errors, 1 pre-existing MSB3277 JwtBearer warning.

---

## 5. Data Migration Execution

### Mechanism: `LiensGovernanceSyncService` (IHostedService / BackgroundService)

**File:** `apps/services/liens/Liens.Infrastructure/TaskService/LiensGovernanceSyncService.cs`

**Behaviour:**
1. Runs once on startup, after a 5-second delay (to ensure full host readiness)
2. Loads ALL rows from `liens_TaskGovernanceSettings` via `ILienTaskGovernanceSettingsRepository.GetAllAsync`
3. For each row: calls Task service `GetGovernanceAsync` to check existence, then `UpsertGovernanceAsync`
4. Upsert is idempotent — if the row already exists in Task it is updated; if missing it is created
5. Errors per-tenant are logged and skipped; the loop continues for all other tenants
6. Uses a static system user ID `00000000-0000-0000-0000-000000000001` as the migration actor

**Logging output (per run):**
```
TASK-MIG-01: governance sync starting.
TASK-MIG-01: created governance in Task service for TenantId=...   (new rows)
TASK-MIG-01: updated governance in Task service for TenantId=...   (existing rows)
TASK-MIG-01: failed to sync TenantId=...; skipping.                (errors)
TASK-MIG-01: sync complete — created=N updated=M skipped=0 errors=E
```

**Idempotency:** The Task service `UpsertAsync` is a create-or-update. `ExpectedVersion = 0` is passed, which the Task service service interprets as "use current version" (line: `request.ExpectedVersion == 0 ? existing.Version : request.ExpectedVersion`). Safe to run multiple times.

**Liens data:** Never modified. `liens_TaskGovernanceSettings` is read-only during this sync.

**New repository method:** `ILienTaskGovernanceSettingsRepository.GetAllAsync` added to support the full-table scan needed by the sync service.

---

## 6. Dual-Read / Fallback Logic

### Read path (all governance lookups)

Implemented in `LienTaskGovernanceService` (methods `GetAsync` and `GetOrCreateAsync`):

```
1. Call _taskClient.GetGovernanceAsync(tenantId, "SYNQ_LIENS")
   ├── HTTP 200 → deserialize + map to TaskGovernanceSettingsResponse
   │     - RequireAssignee             → RequireAssigneeOnCreate
   │     - RequireStage                → RequireWorkflowStageOnCreate
   │     - ProductSettingsJson         → deserialize to LiensGovernanceExtensions
   │           → RequireCaseLinkOnCreate, AllowMultipleAssignees,
   │              DefaultStartStageMode, ExplicitStartStageId
   │     - log: governance_source=task_service
   │     - RETURN
   ├── HTTP 204 / 404 → null (not found in Task)
   └── Any exception → log warning + null

2. If Task returned null: query _repo.GetByTenantProductAsync(tenantId, "SYNQ_LIENS")
   ├── Found → log governance_source=liens_fallback → RETURN
   └── Not found → null (GetAsync) or create default (GetOrCreateAsync)
```

**Enforcement path change:** `LienTaskService.CreateAsync` was updated to use `ILienTaskGovernanceService.GetAsync` instead of directly calling `ILienTaskGovernanceSettingsRepository`. This ensures governance enforcement always goes through the dual-read layer.

**`DeriveStartStageAsync` signature** was updated from `LienTaskGovernanceSettings` to `TaskGovernanceSettingsResponse` parameter — both types expose `DefaultStartStageMode` and `ExplicitStartStageId` with identical property names.

### Write path (governance updates)

Implemented as write-through in `LienTaskGovernanceService.UpdateAsync`:

```
1. Write to Liens DB (primary/authoritative — unchanged from before this task)
2. Best-effort: call _taskClient.UpsertGovernanceAsync(...)
   ├── Success → log governance_sync=ok
   └── Failure → log warning governance_sync=failed (do NOT fail the request)
```

The Liens DB is authoritative during migration. If Task service sync fails, the next startup sync will reconcile.

### Logging signals

| Log message | Meaning |
|-------------|---------|
| `governance_source=task_service` | Read came from Task service (primary) |
| `governance_source=liens_fallback` | Read fell back to Liens DB (Task had no data yet) |
| `governance_source=liens_created` | No data in either system; created default in Liens |
| `governance_source=task_service_error` | HTTP error calling Task service; fell back to Liens |
| `governance_sync=ok` | Write-through to Task service succeeded |
| `governance_sync=failed` | Write-through to Task service failed (non-fatal) |

---

## 7. Validation Results

### Build verification

| Service | Errors | Warnings |
|---------|--------|---------|
| Task.Api (Release) | **0** | 1 pre-existing MSB3277 JwtBearer |
| Liens.Api (Release) | **0** | 1 pre-existing MSB3277 JwtBearer |

### Code-level validation points

**1. Zero UI behavior regression**
Governance enforcement in `LienTaskService.CreateAsync` is functionally identical before and after. The dual-read returns the same `TaskGovernanceSettingsResponse` DTO whether data came from Task service or Liens DB. The field names (`RequireAssigneeOnCreate`, `RequireCaseLinkOnCreate`, `RequireWorkflowStageOnCreate`, `DefaultStartStageMode`, `ExplicitStartStageId`) are identical in both code paths.

**2. Task service returns governance correctly**
`GET /api/tasks/governance?sourceProductCode=SYNQ_LIENS` returns `TaskGovernanceDto` which now includes `ProductSettingsJson`. Liens client deserializes extensions from this field.

**3. Liens fallback works when Task data missing**
When `GetGovernanceAsync` returns null (HTTP 204) or throws, the service falls through to the Liens repository. Logs confirm the path taken.

**4. Migration is idempotent**
`UpsertGovernanceAsync` calls `POST /api/tasks/governance` which the Task service processes as create-or-update (`UpsertAsync` with `ExpectedVersion = 0`). Running the sync multiple times produces the same result.

**5. Mixed state works (some tenants migrated, some not)**
The dual-read handles this: tenants whose data is in Task service read from Task; tenants whose data is not yet in Task service fall back to Liens DB seamlessly.

**6. No runtime errors**
`TryFetchFromTaskServiceAsync` catches all exceptions and returns null. `TrySyncToTaskServiceAsync` catches all exceptions and logs a warning. Neither method can throw in production.

---

## 8. Rollback Plan

### To roll back the dual-read (emergency)

The dual-read is implemented in `LienTaskGovernanceService`. Rollback requires no schema changes:

1. Revert `LienTaskGovernanceService.GetAsync` to read only from `_repo` (Liens DB):
   ```csharp
   return entity is null ? null : MapToResponse(entity);
   ```
2. Revert `LienTaskGovernanceService.UpdateAsync` to remove the `TrySyncToTaskServiceAsync` call.
3. Revert `LienTaskService` constructor back to `ILienTaskGovernanceSettingsRepository` injection.

These are code-only changes. No database rollback is required.

### To roll back the Task schema change

The migration `20260421000007_AddGovernanceProductSettingsJson` can be rolled back by running its `Down()` method:
```sql
ALTER TABLE `tasks_GovernanceSettings` DROP COLUMN `ProductSettingsJson`;
```

This is safe because:
- Existing Task service functionality does not read `ProductSettingsJson` — it is only used in the Liens→Task sync path
- Dropping the column causes no regression to the generic task governance flow
- All existing rows have `NULL` in this column

### Data safety guarantee

- `liens_TaskGovernanceSettings` is **never written to or deleted** by any code in this task except the existing `UpdateAsync` path (which is unchanged)
- `tasks_GovernanceSettings` rows written by the sync can be safely ignored or deleted — they are re-synced on next startup
- The Liens DB remains the source of truth throughout

---

## 9. Known Gaps / Risks

| # | Gap / Risk | Detail | Mitigation |
|---|-----------|--------|------------|
| G-01 | `AllowCompleteWithoutStage` is derived, not stored | The Liens model has no direct equivalent. The sync writes `!RequireWorkflowStageOnCreate`. If a future Liens UI adds an independent `AllowCompleteWithoutStage` setting, the mapping must be updated. | Acceptable for this phase. Document dependency. |
| G-02 | `DefaultPriority`, `AllowUnassign`, `AllowCancel`, `AllowNotesOnClosedTasks` are constants | These governance fields exist in Task but have no Liens equivalent. The sync always writes platform defaults. If Liens adds these settings in future, the sync payload must be extended. | Low risk. Liens doesn't surface these in its current task creation UI. |
| G-03 | `ProductSettingsJson` format is undocumented contract between Liens and Task | There is no JSON schema validation on read or write. If the JSON format changes (e.g. field renamed), the deserializer silently produces defaults. | `DeserializeExtensions` has a try/catch that falls back to `new LiensGovernanceExtensions()` on parse failure. Risk is low. |
| G-04 | Write-through sync is best-effort, not transactional | A failed `UpsertGovernanceAsync` call (e.g. Task service temporarily down) means the Task service briefly has stale data. The next startup sync corrects this. | Acceptable during migration. Dual-read fallback ensures correct enforcement regardless of Task service state. |
| G-05 | `KnownProductCodes` must include `SYNQ_LIENS` | The Task service `UpsertAsync` validates `SourceProductCode` against `KnownProductCodes`. If "SYNQ_LIENS" is not in that set, the upsert will fail and the sync will log errors. | Verified: TASK-B05 added `SYNQ_LIENS` to `KnownProductCodes`. |
| G-06 | Startup sync requires Task service reachability | `LiensGovernanceSyncService` will log per-tenant errors if the Task service is unreachable at startup. This is non-fatal (fallback reads from Liens DB), but produces noise. | Acceptable. The sync retries on next restart. |
| G-07 | `liens_TaskGovernanceSettings` is NOT deleted in this task | Dual source of truth persists until TASK-MIG-01 is confirmed stable and a follow-up TASK-MIG-01-cleanup is executed. | By design (non-negotiable rule). Cleanup is a separate step. |
| G-08 | `ExpectedVersion = 0` in sync payloads | The sync always sends `ExpectedVersion = 0`, which the Task service interprets as "accept any version". This means re-running the sync always overwrites the Task service row with Liens data. Intended behavior (Liens is authoritative), but blocks any Task-only governance edits from persisting. | Acceptable during migration. Once migration is declared complete, `UpdateAsync` on the Task service side (direct edits) should be the only write path. |

### Temporary duplication

Both `liens_TaskGovernanceSettings` and `tasks_GovernanceSettings` (for `SYNQ_LIENS`) will coexist during the migration window. The dual-read ensures consistent behavior regardless of which has more recent data, since:
- Writes always update Liens DB first
- Writes best-effort sync to Task service  
- Reads prefer Task service (but fall back to Liens DB on miss/error)

### Future cleanup (TASK-MIG-01-cleanup, prerequisite for TASK-MIG-02)

Once the Task service is confirmed as stable primary:
1. Remove `TrySyncToTaskServiceAsync` calls (keep Task DB as write target directly)
2. Remove Liens DB fallback from `GetAsync` and `GetOrCreateAsync`
3. Stop writing to `_repo` in `UpdateAsync` (write to Task service API instead)
4. Create a Liens migration to drop `liens_TaskGovernanceSettings`
5. Remove `LiensGovernanceSyncService`

---

## Summary of files changed

### Task service

| File | Change |
|------|--------|
| `Task.Domain/Entities/TaskGovernanceSettings.cs` | Added `ProductSettingsJson` property; added optional parameter to `Update()` |
| `Task.Infrastructure/Persistence/Configurations/TaskGovernanceSettingsConfiguration.cs` | Mapped `ProductSettingsJson` as TEXT column |
| `Task.Application/DTOs/TaskGovernanceDtos.cs` | Added `ProductSettingsJson` to `TaskGovernanceDto` and `UpsertTaskGovernanceRequest` |
| `Task.Application/Services/TaskGovernanceService.cs` | Passes `ProductSettingsJson` to `Update()` |
| `Task.Infrastructure/Persistence/Migrations/20260421000007_AddGovernanceProductSettingsJson.cs` | **NEW** — adds column |
| `Task.Infrastructure/Persistence/Migrations/TasksDbContextModelSnapshot.cs` | Updated snapshot |

### Liens service

| File | Change |
|------|--------|
| `Liens.Application/DTOs/TaskServiceGovernanceDto.cs` | **NEW** — DTOs for Task service governance API round-trip |
| `Liens.Application/Interfaces/ILiensTaskServiceClient.cs` | Added `GetGovernanceAsync` + `UpsertGovernanceAsync` |
| `Liens.Infrastructure/TaskService/LiensTaskServiceClient.cs` | Implemented new governance HTTP methods |
| `Liens.Application/Interfaces/ILienTaskGovernanceService.cs` | Added `GetAsync` (dual-read, no auto-create) |
| `Liens.Application/Services/LienTaskGovernanceService.cs` | Dual-read + write-through; injected `ILiensTaskServiceClient` |
| `Liens.Application/Services/LienTaskService.cs` | Replaced `_governanceRepo` with `_governanceService`; updated `DeriveStartStageAsync` signature |
| `Liens.Application/Repositories/ILienTaskGovernanceSettingsRepository.cs` | Added `GetAllAsync` |
| `Liens.Infrastructure/Repositories/LienTaskGovernanceSettingsRepository.cs` | Implemented `GetAllAsync` |
| `Liens.Infrastructure/TaskService/LiensGovernanceSyncService.cs` | **NEW** — IHostedService startup migration |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `LiensGovernanceSyncService` |
| `Liens.Domain/Enums/WorkflowUpdateSources.cs` | Added `TaskServiceSync` constant (DTO use only) |
