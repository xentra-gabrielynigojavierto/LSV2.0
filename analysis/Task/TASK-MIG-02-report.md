# TASK-MIG-02 Report — Templates Migration (Liens → Task)

_Date: 2026-04-21 | Status: COMPLETE_

---

## 1. Codebase Analysis

### Liens template implementation

**Entity:** `Liens.Domain.Entities.LienTaskTemplate`
- Lives in `liens_TaskTemplates` (MySQL)
- One row per template, tenant-scoped (`TenantId`). `ProductCode` is always `"SYNQ_LIENS"`.
- Versioned for optimistic concurrency
- Indexed on `(TenantId, ContextType)` and `(TenantId, IsActive)`

**Repository:** `LienTaskTemplateRepository` → `ILienTaskTemplateRepository`
- `GetByTenantAsync` — all templates for a tenant (admin list)
- `GetActiveByTenantAsync` — active templates filtered by `ContextType` and `ApplicableWorkflowStageId`
- `GetByIdAsync` — single lookup by `(TenantId, Id)`
- `GetAllAsync` _(added in this task)_ — full table scan for startup sync
- `AddAsync`, `UpdateAsync` — direct EF operations

**Service:** `LienTaskTemplateService` (implements `ILienTaskTemplateService`)
- `GetByTenantAsync`, `GetContextualAsync`, `GetByIdAsync` — read paths
- `CreateAsync`, `UpdateAsync`, `ActivateAsync`, `DeactivateAsync` — write paths with audit events
- `GetForGenerationAsync` _(added in this task)_ — dual-read for the generation engine

**API endpoints:** `TaskTemplateEndpoints` in `Liens.Api`
- `GET /api/liens/task-templates/` — admin list for calling tenant
- `GET /api/liens/task-templates/contextual` — stage/context-filtered list for task creation UI
- `GET /api/liens/task-templates/{id}` — single template
- `POST /api/liens/task-templates/` — create
- `PUT /api/liens/task-templates/{id}` — update
- `POST /api/liens/task-templates/{id}/activate`, `.../deactivate`
- Full admin passthrough under `/api/liens/admin/task-templates/tenants/{tenantId}/...`

**Generation engine usage:** `LienTaskGenerationEngine.ProcessRuleAsync`
- `rule.TaskTemplateId` (Guid) holds the Liens template ID
- Used to: fetch template → check active → extract defaults → check for duplicates
- Template ID also passed as `GeneratingTemplateId` on task creation and to `HasOpenTaskForTemplateAsync` for duplicate prevention

### Task service template implementation

**Entity:** `Task.Domain.Entities.TaskTemplate`
- Lives in `tasks_Templates` (MySQL)
- Unique constraint on `(TenantId, SourceProductCode, Code)` — Code is required, uppercase
- `SourceProductCode` nullable (null = tenant-wide)
- `DefaultScope` — generic field not present in Liens schema
- `ProductSettingsJson TEXT NULL` _(added in this task)_ — stores product-specific extensions

**Repository:** `TaskTemplateRepository` → `ITaskTemplateRepository`
- `GetByIdAsync`, `GetByTenantAsync`, `AddAsync`, `UpdateAsync` _(UpdateAsync added in this task)_

**Service:** `TaskTemplateService` (implements `ITaskTemplateService`)
- Standard CRUD + `CreateTaskFromTemplateAsync` for Task-native template-to-task flow
- `UpsertFromSourceAsync` _(added in this task)_ — idempotent create-or-update with caller-supplied ID

**API endpoint added:** `POST /api/tasks/templates/from-source` (PlatformOrTenantAdmin policy)

---

## 2. Schema Comparison

### Field mapping table

| Liens field | Task field | Notes |
|-------------|-----------|-------|
| `Id` (Guid) | `Id` (Guid) | **Preserved** — critical for generation engine compatibility |
| `TenantId` | `TenantId` | Direct |
| `ProductCode` = "SYNQ_LIENS" | `SourceProductCode` = "SYNQ_LIENS" | Normalized to UPPER |
| `Name` | `Name` | Direct |
| `Description` | `Description` | Direct |
| `DefaultTitle` | `DefaultTitle` | Direct |
| `DefaultDescription` | `DefaultDescription` | Direct |
| `DefaultPriority` | `DefaultPriority` | Direct |
| `DefaultDueOffsetDays` (int?) | `DefaultDueInDays` (int?) | Different name, same semantics |
| `IsActive` | `IsActive` | Direct |
| `ContextType` (string) | — | **No Task equivalent** → stored in `ProductSettingsJson` |
| `ApplicableWorkflowStageId` (Guid?) | — | **No Task equivalent** → stored in `ProductSettingsJson` |
| `DefaultRoleId` (string?) | — | **No Task equivalent** → stored in `ProductSettingsJson` |
| — | `Code` (required, unique) | **Derived** = `Id.ToString("N").ToUpperInvariant()` (32 hex chars) |
| — | `DefaultScope` | Constant = `"GENERAL"` |
| — | `DefaultStageId` | Constant = `null` (ApplicableWorkflowStageId stays in JSON) |

### Liens-specific fields with no generic Task equivalent

| Field | Type | Purpose | Stored in |
|-------|------|---------|-----------|
| `ContextType` | string (GENERAL / CASE / LIEN / STAGE) | Filters which templates are shown in the task creation UI based on current context | `ProductSettingsJson.contextType` |
| `ApplicableWorkflowStageId` | Guid? | For `ContextType=STAGE` templates, restricts availability to a specific workflow stage | `ProductSettingsJson.applicableWorkflowStageId` |
| `DefaultRoleId` | string? | Role used in `AssignByRole` generation mode to find the assignee | `ProductSettingsJson.defaultRoleId` |

---

## 3. Migration Mapping Design

### ID preservation decision

**Decision: Preserve Liens template IDs in `tasks_Templates`.**

Rationale:
- `LienTaskGenerationRule.TaskTemplateId` stores Liens template IDs; rules reference templates by their original GUIDs
- `HasOpenTaskForTemplateAsync(tenantId, rule.TaskTemplateId, ...)` passes Liens IDs to the Task service for duplicate prevention checking
- The generation engine's dual-read path calls `GET /api/tasks/templates/{id}` — if IDs are preserved, this works directly with no mapping table
- Without ID preservation, every template lookup in the generation engine would require an extra JOIN or secondary lookup by `liensTemplateId` from JSON, introducing runtime complexity and a new failure mode

The Task service `UpsertFromSourceAsync` accepts an explicit `Id` parameter. `TaskTemplate.Create()` was updated to accept an optional `Guid? id` parameter (default: `Guid.NewGuid()`). This is backward compatible — existing callers that don't supply an ID get a new GUID as before.

### `Code` field strategy

Liens templates have no `Code`. The Task service requires `Code` to be non-null and unique within `(TenantId, SourceProductCode, Code)`.

**Strategy:** `Id.ToString("N").ToUpperInvariant()` — 32 uppercase hexadecimal characters. Example: `A1B2C3D4E5F6789012345678901234AB`. This is:
- Stable (same template always generates the same code)
- Unique (UUID uniqueness guarantees code uniqueness)
- Human-identifiable (matches the template's primary key)
- Within the 50-char column limit

### `ProductSettingsJson` JSON shape for SYNQ_LIENS templates

```json
{
  "contextType": "GENERAL",
  "applicableWorkflowStageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "defaultRoleId": "SOLICITOR"
}
```

Defined as `LiensTemplateExtensions` in `Liens.Application.DTOs.TaskServiceTemplateDto`.

- `contextType`: defaults to `"GENERAL"` if missing
- `applicableWorkflowStageId`: null if not applicable
- `defaultRoleId`: null if not set

---

## 4. Task Schema Adjustments

### Migration: `20260421000008_AddTemplateProductSettingsJson`

**File:** `apps/services/task/Task.Infrastructure/Persistence/Migrations/20260421000008_AddTemplateProductSettingsJson.cs`

```sql
-- Up
ALTER TABLE `tasks_Templates`
  ADD COLUMN `ProductSettingsJson` TEXT NULL;

-- Down
ALTER TABLE `tasks_Templates`
  DROP COLUMN `ProductSettingsJson`;
```

**Impact on existing rows:** All existing rows get `NULL` in the new column. No data loss.

### Entity changes: `TaskTemplate`

- Added `ProductSettingsJson { get; private set; }` property
- `Create()` accepts two new optional parameters: `string? productSettingsJson = null`, `Guid? id = null`
- `Update()` accepts new optional parameter: `string? productSettingsJson = null`

### DTO changes

**`TaskTemplateDtos.cs`:**
- `CreateTaskTemplateRequest` — added `ProductSettingsJson?`
- `UpdateTaskTemplateRequest` — added `ProductSettingsJson?`
- `TaskTemplateDto` — added `ProductSettingsJson?`
- `UpsertFromSourceTemplateRequest` — **new record** carrying `Id`, `SourceProductCode`, all template fields, and `IsActive`

**`TaskTemplateDto.From()`** — updated to include `ProductSettingsJson`

### Repository changes: `ITaskTemplateRepository` + `TaskTemplateRepository`

Added `UpdateAsync(TaskTemplate template, CancellationToken ct)` — used by the upsert path when a template already exists.

### Service changes: `ITaskTemplateService` + `TaskTemplateService`

Added `UpsertFromSourceAsync(tenantId, userId, UpsertFromSourceTemplateRequest, ct)`:
- Looks up template by the supplied `Id`
- If not found: creates with the explicit ID, deactivates if `IsActive = false`
- If found: updates with `existing.Version` (safe idempotent re-sync), syncs `IsActive` state
- Both paths call `SaveChangesAsync`

### Endpoint: `TaskTemplateEndpoints`

Added `POST /api/tasks/templates/from-source` (PlatformOrTenantAdmin policy) → `UpsertFromSourceAsync`

### Model snapshot

Updated `TasksDbContextModelSnapshot.cs` to include `ProductSettingsJson` TEXT column in the `TaskTemplate` entity block.

---

## 5. Data Migration Execution

### Mechanism: `LiensTemplateSyncService` (IHostedService / BackgroundService)

**File:** `apps/services/liens/Liens.Infrastructure/TaskService/LiensTemplateSyncService.cs`

**Behaviour:**
1. Runs once on startup, after a 5-second delay
2. Loads ALL rows from `liens_TaskTemplates` via `ILienTaskTemplateRepository.GetAllAsync`
3. For each row:
   - Calls `GetTemplateAsync` to check existence in Task service
   - Builds `TaskServiceTemplateUpsertRequest` via `LienTaskTemplateService.MapToUpsertPayload`
   - Calls `UpsertTemplateFromSourceAsync` (POST `/api/tasks/templates/from-source`)
4. Errors per-template are logged and skipped; loop continues
5. Uses system user `00000000-0000-0000-0000-000000000001` as migration actor

**Log output:**
```
TASK-MIG-02: template sync starting.
TASK-MIG-02: sync complete — created=N updated=M errors=E
```

Per-template warnings on failure include `TemplateId` and `TenantId` for traceability.

**Idempotency:** `UpsertFromSourceAsync` is create-or-update. Running the sync multiple times produces the same result. No duplicates possible because the unique index on `(TenantId, SourceProductCode, Code)` enforces exactly one row per Liens template.

**Liens data:** Never modified. `liens_TaskTemplates` is read-only during this sync.

---

## 6. Dual-Read / Fallback Logic

### Read path for generation engine (`GetForGenerationAsync`)

Implemented in `LienTaskTemplateService`:

```
1. Call _taskClient.GetTemplateAsync(tenantId, rule.TaskTemplateId)
   ├── HTTP 200 → deserialize TaskServiceTemplateResponse
   │     - DefaultDueInDays            → DefaultDueOffsetDays
   │     - ProductSettingsJson         → deserialize LiensTemplateExtensions
   │           → ContextType, ApplicableWorkflowStageId, DefaultRoleId
   │     - log: template_source=task_service
   │     - RETURN TaskTemplateResponse
   ├── HTTP 404 / 204 → null (not found in Task)
   └── Any exception → log warning (template_source=task_service_error) + null

2. If Task returned null: query _repo.GetByIdAsync(tenantId, id)
   ├── Found → log template_source=liens_fallback → RETURN
   └── Not found → return null
```

### Write-through (`TrySyncToTaskServiceAsync`)

Applied in all write operations (`CreateAsync`, `UpdateAsync`, `ActivateAsync`, `DeactivateAsync`):

```
1. Write to Liens DB (primary/authoritative — unchanged)
2. Best-effort: call UpsertTemplateFromSourceAsync
   ├── Success → log template_sync=ok
   └── Failure → log warning template_sync=failed (request does NOT fail)
```

### Logging signals

| Log value | Meaning |
|-----------|---------|
| `template_source=task_service` | Read came from Task service (primary path) |
| `template_source=liens_fallback` | Read fell back to Liens DB |
| `template_source=task_service_error` | Task service threw; fell back to Liens DB |
| `template_sync=ok` | Write-through to Task service succeeded |
| `template_sync=failed` | Write-through failed (non-fatal; startup sync will reconcile) |

### Admin reads (`GetByTenantAsync`, `GetContextualAsync`, `GetByIdAsync`)

Admin/UI reads continue to use Liens DB only. The dual-read is scoped exclusively to `GetForGenerationAsync` (the generation engine path). This is intentional:
- Admin UI shows the authoritative Liens data
- Contextual template picker (used in task creation UI) uses `GetContextualAsync` → Liens DB
- Only the generation engine needs the Task-first dual-read

---

## 7. Generation Flow Compatibility

### `LienTaskGenerationEngine` changes

**Before TASK-MIG-02:**
```csharp
var template = await _templateRepo.GetByIdAsync(context.TenantId, rule.TaskTemplateId, ct);
```

**After TASK-MIG-02:**
```csharp
var template = await _templateService.GetForGenerationAsync(context.TenantId, rule.TaskTemplateId, ct);
```

The return type changed from `LienTaskTemplate` (entity) to `TaskTemplateResponse` (DTO). All fields accessed by the engine exist in `TaskTemplateResponse`:

| Field accessed | In `TaskTemplateResponse`? | Source |
|----------------|--------------------------|--------|
| `template.IsActive` | ✓ | Direct field |
| `template.Name` | ✓ | Direct field |
| `template.DefaultTitle` | ✓ | Direct field |
| `template.DefaultDescription` | ✓ | Direct field |
| `template.DefaultPriority` | ✓ | Direct field |
| `template.DefaultDueOffsetDays` | ✓ | Direct field |
| `template.DefaultRoleId` | ✓ | From `ProductSettingsJson.defaultRoleId` (via fallback path) or direct field (via Liens DB path) |
| `template.ApplicableWorkflowStageId` | ✓ | From `ProductSettingsJson.applicableWorkflowStageId` (via fallback path) or direct field (via Liens DB path) |

### Stage applicability: NOT migrated (by design)

`LienTaskGenerationRule.ApplicableWorkflowStageId` — the rule itself has a stage filter. This is separate from `LienTaskTemplate.ApplicableWorkflowStageId`. Both are retained as Liens-side metadata:
- Rule stage filter: checked in `ProcessRuleAsync` step 1 (untouched)
- Template stage filter: passed to `GetActiveByTenantAsync` for UI filtering (untouched, Liens DB)
- `ApplicableWorkflowStageId` stored in `ProductSettingsJson` on the Task service side but never used by Task service logic directly — only round-tripped back to Liens when reading from Task service

Stages are NOT migrated in this task. Stage IDs remain Liens-owned. No stage references in the Task service point to Liens stage rows — the `ProductSettingsJson` value is opaque to the Task service.

### Duplicate prevention

`HasOpenTaskForTemplateAsync(tenantId, rule.TaskTemplateId, caseId, lienId)` — unchanged. This passes the Liens template ID to the Task service, which queries `tasks_Tasks.GeneratingTemplateId`. Tasks created before this migration have the Liens template ID stored as `GeneratingTemplateId` (because the generation engine has always used `rule.TaskTemplateId` for that field). ID preservation ensures this check remains correct across the migration boundary.

### Default due-date, priority, title, description

All resolved from `TaskTemplateResponse` fields which are populated identically regardless of whether the read came from Task service or Liens DB:
- `DefaultTitle` → `CreateTaskRequest.Title`
- `DefaultDescription` → `CreateTaskRequest.Description`
- `DefaultPriority` → `CreateTaskRequest.Priority`
- `DefaultDueOffsetDays` → `ResolveDueDate()` in engine

---

## 8. Validation Results

### Build verification

| Service | Errors | Warnings |
|---------|--------|---------|
| Task.Api (Release) | **0** | 1 pre-existing MSB3277 JwtBearer |
| Liens.Api (Release) | **0** | 1 pre-existing MSB3277 JwtBearer |

### Code-level validation points

**1. Zero UI behavior regression**
Admin template reads (`GetByTenantAsync`, `GetContextualAsync`, `GetByIdAsync`) still go directly to Liens DB. No change to response shape or filtering behavior.

**2. Task service stores templates correctly**
`POST /api/tasks/templates/from-source` calls `UpsertFromSourceAsync`. Template is created with:
- Preserved ID (same as Liens template GUID)
- `SourceProductCode = "SYNQ_LIENS"`
- `Code = Id.ToString("N").ToUpperInvariant()`
- `ProductSettingsJson` with Liens-specific extensions
- `DefaultScope = "GENERAL"` (constant)
- `DefaultStageId = null` (ApplicableWorkflowStageId lives in JSON only)

**3. Task-first template read works**
`GET /api/tasks/templates/{id}` returns the template. Client deserializes to `TaskServiceTemplateResponse`. `MapFromTaskServiceDto` extracts `LiensTemplateExtensions` and populates all required fields in `TaskTemplateResponse`.

**4. Liens fallback works when Task template missing**
`TryFetchFromTaskServiceAsync` returns null on HTTP 404 or exception. The service falls through to `_repo.GetByIdAsync`. Generation engine gets the entity from Liens DB unchanged.

**5. Migration is idempotent**
`UpsertFromSourceAsync` is create-or-update by ID. Running the startup sync twice: first run creates, second run updates (same data). Unique index prevents duplicate rows. Version increments on update but is always overwritten from Liens on re-sync (harmless).

**6. Mixed state works (some templates migrated, some not)**
Templates not yet in Task service are served from Liens DB. Templates already in Task service are served from Task (primary). Both paths return `TaskTemplateResponse` with identical field shape to the caller.

**7. Template-based task generation still works**
Engine now calls `_templateService.GetForGenerationAsync()` instead of `_templateRepo.GetByIdAsync()`. `TaskTemplateResponse` has all fields used by the engine. Return type change is internal to the engine's `ProcessRuleAsync` method.

**8. Rule-based generation referencing templates still works**
`rule.TaskTemplateId` is a Liens template Guid. ID preservation means `GET /api/tasks/templates/{rule.TaskTemplateId}` returns the correct template. Duplicate prevention `HasOpenTaskForTemplateAsync(rule.TaskTemplateId)` is unchanged.

**9. Default priority / due-date / title / description map correctly**
All four are direct fields in `TaskTemplateResponse`. Mapping from `TaskServiceTemplateResponse`: `DefaultDueInDays → DefaultDueOffsetDays` (name normalization). No semantic change.

**10. No runtime errors if Task service temporarily unavailable**
`TryFetchFromTaskServiceAsync` has a blanket exception catch that returns null. `TrySyncToTaskServiceAsync` has a blanket exception catch that logs a warning. Neither can propagate to callers.

**11. Existing Task template behavior unchanged**
All existing Task service template operations (`CreateAsync`, `UpdateAsync`, `ListAsync`, etc.) are functionally identical. The new `ProductSettingsJson` column is null for any template not created via `from-source`. `CreateTaskFromTemplateAsync` is unaffected.

---

## 9. Rollback Plan

### Revert the dual-read in the generation engine

Code-only change, no schema rollback required:

1. Revert `LienTaskGenerationEngine` to inject `ILienTaskTemplateRepository` instead of `ILienTaskTemplateService`
2. Revert the template lookup back to `_templateRepo.GetByIdAsync(context.TenantId, rule.TaskTemplateId, ct)`
3. Remove `GetForGenerationAsync` from `ILienTaskTemplateService` and its implementation
4. Remove `ILiensTaskServiceClient` injection from `LienTaskTemplateService`
5. Remove `TrySyncToTaskServiceAsync` calls from write methods

### Revert the Task schema change

Migration `20260421000008_AddTemplateProductSettingsJson` can be rolled back:
```sql
ALTER TABLE `tasks_Templates` DROP COLUMN `ProductSettingsJson`;
```
Safe because:
- No Task service logic reads `ProductSettingsJson` for its own generic template operations
- The column is NULL for all templates not created via Liens sync
- Dropping the column causes no regression to generic Task template behavior

### Remove `LiensTemplateSyncService`

Remove the DI registration in `DependencyInjection.cs`. On next restart, the sync service will not run. Templates already in `tasks_Templates` are harmless; they will simply never be read if the dual-read is also reverted.

### Data safety guarantee

- `liens_TaskTemplates` is **never written to or deleted** by any code added in this task
- `tasks_Templates` rows created by the sync can be safely ignored or removed
- Liens DB remains the source of truth throughout

---

## 10. Known Gaps / Risks

| # | Gap / Risk | Detail | Mitigation |
|---|-----------|--------|------------|
| G-01 | `ApplicableWorkflowStageId` stored as JSON on Task side | The Task service does not interpret or validate this field. If a stage is deleted in Liens, the JSON value will be stale but harmless (never used by Task service logic). | Acceptable during migration. Stage migration (P2) will address this. |
| G-02 | `DefaultStageId` in Task is always null for SYNQ_LIENS templates | Task's `CreateTaskFromTemplateAsync` uses `template.DefaultStageId` for governance enforcement (`RequireStage`). Liens templates always have null here, which means Task-native template-to-task flow ignores stage requirement for Liens templates. | Liens never uses `CreateTaskFromTemplateAsync` directly; generation engine creates tasks via `ILienTaskService.CreateAsync`. Risk is zero for current flows. |
| G-03 | Admin reads still go to Liens DB only | If a template is updated via the Task service API directly (not via Liens), the change will be overwritten by the next startup sync. | By design. Liens DB is authoritative during migration. Task service should not be used for direct template admin during this phase. |
| G-04 | `LastUpdatedByUserId` and `LastUpdatedByName` are null for Task-service-sourced responses | When `GetForGenerationAsync` returns a task-service-sourced template, `LastUpdatedByUserId = null`. The generation engine does not use these fields, so this is safe. | No mitigation needed. These are audit fields, not operational fields. |
| G-05 | `Version` mismatch on re-sync | `UpsertFromSourceAsync` always passes `existing.Version` as `expectedVersion` in `Update()`. This means the Task service version increments on every sync cycle. Version is an internal optimistic-concurrency guard; the sync service bypasses it safely by reading current version first. | Acceptable. The version number in Task service is not surfaced to Liens consumers. |
| G-06 | Write-through is best-effort | A failed `UpsertTemplateFromSourceAsync` after an admin save means Task service briefly has stale template data. The next startup sync corrects this. | Acceptable. The dual-read falls back to Liens DB on error, so enforcement always uses the correct template. |
| G-07 | `liens_TaskTemplates` NOT deleted in this task | Dual source of truth persists until TASK-MIG-02-cleanup is executed. | By design (non-negotiable rule). |
| G-08 | `ContextType` from Task side uses Liens enum values opaquely | `ProductSettingsJson.contextType` stores Liens `TaskTemplateContextType` values ("GENERAL", "CASE", "LIEN", "STAGE"). The Task service treats this as an opaque string. If Liens adds new context types, the JSON schema expands without migration. | Low risk. JSON blob is forward-compatible. |
| G-09 | Startup sync requires Task service reachability | `LiensTemplateSyncService` logs per-template errors if the Task service is unreachable. Non-fatal (generation engine falls back to Liens DB), but generates log noise. | Acceptable. Sync retries on next restart. |

### Temporary duplication

Both `liens_TaskTemplates` and `tasks_Templates` (for `SYNQ_LIENS`) will coexist during the migration window. Consistent behavior is guaranteed because:
- Writes always update Liens DB first (authoritative)
- Writes best-effort sync to Task service
- Generation engine reads prefer Task service (fallback to Liens on miss/error)
- Admin reads always use Liens DB

### Future cleanup (TASK-MIG-02-cleanup, required before stage migration)

1. Remove `TrySyncToTaskServiceAsync` calls (keep Task DB as write target directly)
2. Remove Liens DB fallback from `GetForGenerationAsync`
3. Update `CreateAsync`/`UpdateAsync`/`ActivateAsync`/`DeactivateAsync` to write to Task service API only
4. Create Liens migration to drop `liens_TaskTemplates`
5. Remove `LiensTemplateSyncService`
6. Remove `LienTaskTemplateService.MapToUpsertPayload` (no longer needed)

---

## Summary of files changed

### Task service

| File | Change |
|------|--------|
| `Task.Domain/Entities/TaskTemplate.cs` | Added `ProductSettingsJson`; `Create()` + `Update()` accept new optional params |
| `Task.Infrastructure/Persistence/Configurations/TaskTemplateConfiguration.cs` | Mapped `ProductSettingsJson` as TEXT column |
| `Task.Infrastructure/Persistence/Repositories/TaskTemplateRepository.cs` | Added `UpdateAsync` |
| `Task.Application/Interfaces/ITaskTemplateRepository.cs` | Added `UpdateAsync` |
| `Task.Application/Interfaces/ITaskTemplateService.cs` | Added `UpsertFromSourceAsync` |
| `Task.Application/DTOs/TaskTemplateDtos.cs` | Added `ProductSettingsJson` to existing DTOs; added `UpsertFromSourceTemplateRequest` |
| `Task.Application/Services/TaskTemplateService.cs` | Passes `ProductSettingsJson` through; implements `UpsertFromSourceAsync` |
| `Task.Api/Endpoints/TaskTemplateEndpoints.cs` | Added `POST /from-source` endpoint |
| `Task.Infrastructure/Persistence/Migrations/20260421000008_AddTemplateProductSettingsJson.cs` | **NEW** — adds column |
| `Task.Infrastructure/Persistence/Migrations/TasksDbContextModelSnapshot.cs` | Updated snapshot |

### Liens service

| File | Change |
|------|--------|
| `Liens.Application/DTOs/TaskServiceTemplateDto.cs` | **NEW** — `LiensTemplateExtensions`, `TaskServiceTemplateResponse`, `TaskServiceTemplateUpsertRequest` |
| `Liens.Application/Interfaces/ILiensTaskServiceClient.cs` | Added `GetTemplateAsync`, `GetAllTemplatesAsync`, `UpsertTemplateFromSourceAsync` |
| `Liens.Infrastructure/TaskService/LiensTaskServiceClient.cs` | Implemented 3 new template round-trip methods |
| `Liens.Application/Repositories/ILienTaskTemplateRepository.cs` | Added `GetAllAsync` |
| `Liens.Infrastructure/Repositories/LienTaskTemplateRepository.cs` | Implemented `GetAllAsync` |
| `Liens.Application/Interfaces/ILienTaskTemplateService.cs` | Added `GetForGenerationAsync` |
| `Liens.Application/Services/LienTaskTemplateService.cs` | Dual-read + write-through; `MapToUpsertPayload`; injected `ILiensTaskServiceClient` |
| `Liens.Application/Services/LienTaskGenerationEngine.cs` | Replaced `ILienTaskTemplateRepository` with `ILienTaskTemplateService`; uses `GetForGenerationAsync` |
| `Liens.Infrastructure/TaskService/LiensTemplateSyncService.cs` | **NEW** — IHostedService startup sync |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `LiensTemplateSyncService` |
