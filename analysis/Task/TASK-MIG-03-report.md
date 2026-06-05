# TASK-MIG-03 — Stage Configuration Migration Report
**Service pair:** Liens → Task  
**Date:** 2026-04-21  
**Status:** ✅ Complete — both services build (0 errors)

---

## 1. Objective

Migrate `liens_WorkflowStages` stage configuration records into the Task service (`tasks_StageConfigs`) following the same dual-write / dual-read pattern established in TASK-MIG-01 (Governance) and TASK-MIG-02 (Templates).

The Liens service remains authoritative for all stage data during the migration window.  The Task service becomes the canonical read-source as tenants are progressively synced.

---

## 2. Scope

### In scope
| Item | Decision |
|---|---|
| `liens_WorkflowStages` rows | Migrated — write-through + background sync |
| `liens_WorkflowConfigs` wrapper rows | **Not migrated** — Task has no per-workflow wrapper; tenant+product grouping suffices |
| `liens_WorkflowTransitions` rows | **Not migrated** — transition graph is Liens-owned indefinitely (no Task schema equivalent yet) |
| Config-level admin fields (`WorkflowName`, `Version`, `LastUpdatedSource`) | **Stay Liens-only** — no equivalent in Task |

### Liens-specific stage metadata preserved via `ProductSettingsJson`
```json
{
  "description": "string?",
  "defaultOwnerRole": "string?",
  "slaMetadata": "string?"
}
```
Shape defined in `LiensStageExtensions` (round-trips through JSON on every push/pull).

---

## 3. Task Service Changes

### 3.1 Schema
**Migration:** `20260421000009_AddStageProductSettingsJson`  
Added nullable `TEXT` column `ProductSettingsJson` to `tasks_StageConfigs`.

### 3.2 Entity
`TaskStageConfig` — added `ProductSettingsJson` property; constructor updated to accept optional `id` parameter for ID preservation during upsert.

### 3.3 Repository
`ITaskStageConfigRepository` / `TaskStageConfigRepository` — two new methods:
- `GetByIdAnyTenantAsync(Guid id, CancellationToken)` — needed for upsert lookup across tenants
- `UpdateAsync(TaskStageConfig, CancellationToken)` — save updated entity

### 3.4 DTO
`UpsertFromSourceStageRequest` — inbound payload shape:
```csharp
Guid    Id
string  SourceProductCode
string  Name
int     DisplayOrder
bool    IsActive
string? ProductSettingsJson
```

### 3.5 Service
`TaskStageService.UpsertFromSourceAsync` — idempotent upsert:
- Looks up by `Id` across all tenants.
- If found: updates `Name`, `DisplayOrder`, `IsActive`, `ProductSettingsJson`.
- If not found: creates new `TaskStageConfig` using the supplied GUID (ID preserved).

### 3.6 Endpoint
`POST /api/tasks/stages/from-source` — internal (service-token–protected) endpoint wired to `UpsertFromSourceAsync`.

### 3.7 Model snapshot
EF model snapshot updated to include `ProductSettingsJson`.

---

## 4. Liens Service Changes

### 4.1 DTOs (`Liens.Application/DTOs/`)
| File | Purpose |
|---|---|
| `TaskServiceStageDto.cs` | `TaskServiceStageResponse` (GET response shape), `TaskServiceStageUpsertRequest` (push payload), `LiensStageExtensions` (JSON bag inside ProductSettingsJson) |

### 4.2 Task service client (`Liens.Infrastructure/TaskService/`)
Three new methods added to `ILiensTaskServiceClient` / `LiensTaskServiceClient`:
| Method | HTTP call |
|---|---|
| `GetStageAsync(tenantId, stageId, ct)` | `GET /api/tasks/stages/{stageId}` |
| `GetAllStagesAsync(tenantId, productCode, ct)` | `GET /api/tasks/stages?productCode=SYNQ_LIENS` |
| `UpsertStageFromSourceAsync(tenantId, actorId, req, ct)` | `POST /api/tasks/stages/from-source` |

### 4.3 Repository
`ILienWorkflowConfigRepository` / `LienWorkflowConfigRepository` — new method:
- `GetAllConfigsAsync(CancellationToken)` — returns all configs with stages (used by background sync service)

### 4.4 LienWorkflowConfigService — dual-read + write-through

#### Dual-read — `GetByTenantAsync`
1. Load entity from Liens DB.
2. Call `TryLoadStagesFromTaskServiceAsync` → tries Task service `GetAllStagesAsync`.
3. If Task service returns ≥ 1 stage → use those stages in the response.
4. On empty result or any error → fall back to `entity.Stages` from Liens DB.

#### Dual-read — `GetStageForRuntimeAsync(tenantId, stageId)`
1. Try `_taskClient.GetStageAsync` → deserialise `ProductSettingsJson` → return `WorkflowStageResponse`.
2. On failure → query Liens DB via `GetStageGlobalAsync` → map to response.

#### Write-through — admin operations
`AddStageAsync`, `UpdateStageAsync`, `RemoveStageAsync`, `ReorderStagesAsync` each call `TrySyncStageToTaskServiceAsync` after the Liens DB write succeeds.  
Failures are caught, logged as `WARN`, and never propagated.

#### Private helpers added
| Helper | Purpose |
|---|---|
| `TryLoadStagesFromTaskServiceAsync` | Load full stage list from Task service; returns null on failure |
| `TrySyncStageToTaskServiceAsync` | Best-effort single-stage push to Task service |
| `BuildStageUpsertPayload` (public static) | Builds `TaskServiceStageUpsertRequest` from `LienWorkflowStage`; reused by sync service |
| `MapFromTaskServiceStage` | Task service DTO → `WorkflowStageResponse` |
| `MapStageToResponse` | `LienWorkflowStage` → `WorkflowStageResponse` |
| `MapToResponseWithStages` | Central mapper accepting an optional overriding stage list |
| `DeserializeStageExtensions` | Safe JSON deserialise of `ProductSettingsJson` → `LiensStageExtensions` |

All previous `MapToResponse` call sites replaced with `MapToResponseWithStages(entity, null)`.

### 4.5 LienTaskService — stage-lookup refactor

`LienTaskService` injected with `ILienWorkflowConfigService` (`workflowConfigService`).  
Three former `_workflowRepo.GetStageGlobalAsync` / `_workflowRepo.GetByTenantProductAsync` calls replaced:

| Former call | Replacement |
|---|---|
| `_workflowRepo.GetStageGlobalAsync(existing.WorkflowStageId)` (fromStage in transition validation) | `_workflowConfigService.GetStageForRuntimeAsync(tenantId, id)` |
| `_workflowRepo.GetStageGlobalAsync(request.WorkflowStageId)` (toStage in transition validation) | `_workflowConfigService.GetStageForRuntimeAsync(tenantId, id)` |
| `_workflowRepo.GetStageGlobalAsync(governance.ExplicitStartStageId)` (DeriveStartStageAsync) | `_workflowConfigService.GetStageForRuntimeAsync(tenantId, id)` |
| `_workflowRepo.GetByTenantProductAsync(…)` (DeriveStartStageAsync FIRST_ACTIVE_STAGE) | `_workflowConfigService.GetByTenantAsync(tenantId)` |

### 4.6 LiensStageSyncService (new — `IHostedService`)

**File:** `Liens.Application/Services/LiensStageSyncService.cs`

Background service that pushes all Liens stage configs to the Task service on startup and every 60 minutes.

| Parameter | Value |
|---|---|
| Initial startup delay | 30 s |
| Periodic interval | 60 min |
| Scope | `IServiceScopeFactory` — new scope per cycle |
| Failure isolation | Per-stage `try/catch`; full cycle `try/catch` — never throws to host |
| Authorship | Uses `config.CreatedByUserId` as actor (falls back to `Guid.Empty`) |

### 4.7 DI registration (`Liens.Infrastructure/DependencyInjection.cs`)
```csharp
services.AddSingleton<LiensStageSyncService>();
services.AddHostedService(sp => sp.GetRequiredService<LiensStageSyncService>());
```

---

## 5. ID Preservation Strategy

Liens stage `Guid` IDs are reused verbatim as Task service stage IDs (same approach as TASK-MIG-01/02).  
The incoming `Id` field is passed directly to `UpsertFromSourceAsync` — no hashing or derivation required.

---

## 6. Transition Graph — Not Migrated

`liens_WorkflowTransitions` are **not** synced to the Task service.  
Rationale:
- Task service has no `tasks_Transitions` schema equivalent.
- Transition enforcement remains in `WorkflowTransitionValidationService` (Liens-side).
- Stage-lookup refactor (§4.5) ensures transition validation still resolves stage names correctly via the dual-read path.

---

## 7. Build Verification

| Service | Result |
|---|---|
| `Task.Api` | ✅ Build succeeded — 0 errors, 1 pre-existing MSB3277 warning |
| `Liens.Api` | ✅ Build succeeded — 0 errors, pre-existing MSB3277 warnings only |

---

## 8. Rollback / Degraded-mode Behaviour

All Task service calls are best-effort.  If the Task service is unavailable:
- `GetByTenantAsync` falls back to `entity.Stages` from Liens DB.
- `GetStageForRuntimeAsync` falls back to `GetStageGlobalAsync` (Liens DB).
- Write-through failures in `AddStage`/`UpdateStage`/`RemoveStage`/`ReorderStages` are logged and swallowed — Liens DB write already committed.
- `LiensStageSyncService` logs error at cycle level and retries on the next 60-minute tick.

No circuit-breaker is added at this stage; HTTP client timeouts provide the hard bound.

---

## 9. Open Items / Follow-up

| Item | Priority | Notes |
|---|---|---|
| Remove unused `_workflowRepo` field from `LienTaskService` once stage lookups are fully on dual-read | Low | Still injected but no longer used for stages — clean-up pass |
| Cut-over flag (`StagesSource=TaskService`) to disable Liens-DB fallback once all tenants fully synced | Future | Track as TASK-MIG-03-CUTOVER |
| Add `tasks_Transitions` schema + migration if transition graph ever moves to Task service | Future | Architectural decision deferred |
| Timeout tuning for `GetAllStagesAsync` under large tenant count | Low | Default 30 s client timeout should suffice |

---

## 10. Files Changed

| Path | Change |
|---|---|
| `apps/services/task/Task.Infrastructure/Persistence/Migrations/20260421000009_AddStageProductSettingsJson.cs` | New migration |
| `apps/services/task/Task.Infrastructure/Persistence/Migrations/TaskDbContextModelSnapshot.cs` | Updated snapshot |
| `apps/services/task/Task.Domain/Entities/TaskStageConfig.cs` | Added `ProductSettingsJson`; optional `id` ctor param |
| `apps/services/task/Task.Application/Repositories/ITaskStageConfigRepository.cs` | `GetByIdAnyTenantAsync`, `UpdateAsync` |
| `apps/services/task/Task.Infrastructure/Repositories/TaskStageConfigRepository.cs` | Implemented new repo methods |
| `apps/services/task/Task.Application/DTOs/UpsertFromSourceStageRequest.cs` | New DTO |
| `apps/services/task/Task.Application/Services/TaskStageService.cs` | `UpsertFromSourceAsync` |
| `apps/services/task/Task.Api/Endpoints/TaskStageEndpoints.cs` | `POST /api/tasks/stages/from-source` |
| `apps/services/liens/Liens.Application/DTOs/TaskServiceStageDto.cs` | New DTOs |
| `apps/services/liens/Liens.Infrastructure/TaskService/LiensTaskServiceClient.cs` | 3 new stage client methods |
| `apps/services/liens/Liens.Application/Interfaces/ILiensTaskServiceClient.cs` | 3 new interface methods |
| `apps/services/liens/Liens.Application/Repositories/ILienWorkflowConfigRepository.cs` | `GetAllConfigsAsync` |
| `apps/services/liens/Liens.Infrastructure/Repositories/LienWorkflowConfigRepository.cs` | Implemented `GetAllConfigsAsync` |
| `apps/services/liens/Liens.Application/Services/LienWorkflowConfigService.cs` | Dual-read, write-through, all map helpers |
| `apps/services/liens/Liens.Application/Services/LienTaskService.cs` | Stage-lookup refactor; injected `ILienWorkflowConfigService` |
| `apps/services/liens/Liens.Application/Services/LiensStageSyncService.cs` | New background sync service |
| `apps/services/liens/Liens.Infrastructure/DependencyInjection.cs` | `LiensStageSyncService` DI registration |
| `analysis/TASK-MIG-03-report.md` | This report |
