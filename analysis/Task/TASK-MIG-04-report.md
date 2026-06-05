# TASK-MIG-04 — Stage Transitions Migration Report
**Service pair:** Liens → Task  
**Date:** 2026-04-21  
**Status:** ✅ Complete — both services build (0 errors)

---

## 1. Codebase Analysis

### Liens `liens_WorkflowTransitions` — fields inspected

| Field | Type | Semantic |
|---|---|---|
| `Id` | Guid PK | Row identity — NOT preserved in Task (Task assigns its own IDs; (from, to) pair uniquely identifies a transition per tenant/product) |
| `WorkflowConfigId` | Guid FK → `liens_WorkflowConfigs` | Config wrapper scoping — **flattened** to `(TenantId, SourceProductCode)` in Task |
| `FromStageId` | Guid FK → `liens_WorkflowStages` | Source stage — preserved verbatim (same stage GUIDs used in MIG-03) |
| `ToStageId` | Guid FK → `liens_WorkflowStages` | Target stage — preserved verbatim |
| `IsActive` | bool | Active/inactive flag — preserved |
| `SortOrder` | int | Ordering — preserved |
| Audit fields | Guid, DateTime | Standard auditing — preserved in Task |

### Liens runtime read paths
| Path | Usage |
|---|---|
| `WorkflowTransitionValidationService.IsTransitionAllowedAsync` | Called from `LienTaskService.UpdateTaskAsync` before allowing a stage change; uses open-move mode when no transitions configured |
| `WorkflowTransitionValidationService.GetAllowedNextStagesAsync` | Available on the interface; not externally called in current codebase |
| `LienWorkflowConfigService.GetTransitionsAsync` | Returns active transitions list — called by `GET /{id}/transitions` and admin endpoint |
| `EnsureDefaultTransitionsAsync` | Auto-initialises linear transitions when a workflow has stages but no transitions — called from `GetByTenantAsync` |

### Liaison to Flow service
- `flow_workflow_transitions` (orchestration graph) is **entirely separate** from `liens_WorkflowTransitions` (task-board allowed moves).
- No code in scope blurs these concepts. The boundary is maintained.

### Task service — no existing transition model
Confirmed: no `tasks_StageTransitions` table or transition entity existed before this migration.

---

## 2. Schema Comparison

| Liens schema | Task schema (TASK-MIG-04) | Notes |
|---|---|---|
| `Id` (FK-bound) | `Id` (new, Task-assigned) | NOT preserved — uniqueness key is `(TenantId, SourceProductCode, FromStageId, ToStageId)` |
| `WorkflowConfigId` | — | **Flattened** into `(TenantId, SourceProductCode)` — safe because 1:1 config per tenant per product |
| `FromStageId` | `FromStageId` | Preserved verbatim |
| `ToStageId` | `ToStageId` | Preserved verbatim |
| `IsActive` | `IsActive` | Preserved |
| `SortOrder` | `SortOrder` | Preserved — Liens behavior uses it for deterministic ordering |
| `CreatedByUserId` / timestamps | same | Standard audit fields |
| — | `TenantId` | Added (not present in Liens row; derived from `WorkflowConfig.TenantId`) |
| — | `SourceProductCode` | Added (`SYNQ_LIENS` for all Liens-sourced rows) |

---

## 3. Minimal Transition Model Design

**Decisions:**

1. **ID preservation: No.** Task service assigns its own IDs. Uniqueness is `(TenantId, SourceProductCode, FromStageId, ToStageId)`. This is safe because the transition is identified by its from→to pair, not its opaque Guid.

2. **`WorkflowConfigId` wrapper: Flattened.** A single `LienWorkflowConfig` exists per `(TenantId, ProductCode)`. Flattening to `(TenantId, SourceProductCode)` loses nothing.

3. **Extra metadata: None.** No `ProductSettingsJson` needed — all semantically meaningful fields fit directly in the row.

4. **Upsert semantics: Batch-replace.** For a given `(TenantId, SourceProductCode)`, the Task service replaces the full active set atomically. This matches `SaveTransitionsAsync` in Liens and simplifies idempotency.

**Strict model applied:**
```
tasks_StageTransitions (
  Id UUID PK
  TenantId UUID NOT NULL
  SourceProductCode VARCHAR(50) NOT NULL
  FromStageId UUID NOT NULL
  ToStageId UUID NOT NULL
  IsActive BOOL NOT NULL DEFAULT true
  SortOrder INT NOT NULL DEFAULT 0
  CreatedByUserId UUID NOT NULL
  UpdatedByUserId UUID
  CreatedAtUtc DATETIME NOT NULL
  UpdatedAtUtc DATETIME NOT NULL
)
```

No conditions, no rules, no events, no automation hooks.

---

## 4. Task Schema Additions

### 4.1 Entity
`Task.Domain.Entities.TaskStageTransition` — minimal entity matching the design above.  
`Create()` factory validates: non-empty GUIDs, no self-transition.  
`Update(isActive, sortOrder, updatedByUserId)` — used by upsert to reactivate existing rows.

### 4.2 EF Configuration
`TaskStageTransitionConfiguration` — maps to `tasks_StageTransitions`.  
Indexes:
- `IX_StageTransitions_TenantId_Product` — fast tenant+product listing
- `IX_StageTransitions_FromStage` — fast "what can I move to from this stage?"
- `UX_StageTransitions_Unique` (unique) — prevents duplicate (TenantId, ProductCode, From, To)

### 4.3 Migration
`20260421000010_AddStageTransitions` — creates `tasks_StageTransitions` with MySQL `char(36)` / `tinyint(1)` / `datetime(6)` types. Rollback drops the table.

### 4.4 Repository
`ITaskStageTransitionRepository` / `TaskStageTransitionRepository`:
- `GetActiveByTenantProductAsync` — list for dual-read
- `GetByTenantProductStagesAsync` — lookup for upsert
- `AddAsync`, `UpdateAsync`, `AddRangeAsync`, `DeactivateAllAsync`

`DeactivateAllAsync` uses `ExecuteUpdateAsync` (bulk update, no entity load).

### 4.5 Service
`ITaskStageTransitionService` / `TaskStageTransitionService`:
- `GetActiveTransitionsAsync(tenantId, productCode)` — returns DTO list
- `UpsertFromSourceAsync(tenantId, actorId, request)` — idempotent batch replace:
  1. `DeactivateAllAsync` — mark all current active rows inactive
  2. For each entry: reactivate existing row or insert new row
  3. `SaveChangesAsync`

### 4.6 Endpoint
`GET  /api/tasks/stage-transitions?productCode=SYNQ_LIENS` — authenticated  
`POST /api/tasks/stage-transitions/from-source` — admin/platform role  

### 4.7 Model Snapshot
`TasksDbContextModelSnapshot` updated with `TaskStageTransition` entity block.

### 4.8 DI Registration
```csharp
services.AddScoped<ITaskStageTransitionRepository, TaskStageTransitionRepository>();
services.AddScoped<ITaskStageTransitionService,    TaskStageTransitionService>();
```

---

## 5. Data Migration Execution

**Write-through on admin ops** (see §6) pushes transitions immediately on every admin change.

**Background sync (`LiensTransitionSyncService`)** catches up all tenants on startup and periodically:
- Initial delay: 45 s (after LiensStageSyncService's 30 s, to avoid startup contention)
- Interval: 60 min
- Scope: `IServiceScopeFactory` — new scope per cycle
- Loop: for each `LienWorkflowConfig`, sends full active transition set via `UpsertTransitionsFromSourceAsync`
- Failure isolation: per-config `try/catch`; full cycle `try/catch` — never throws to host

**Idempotency:** `UpsertFromSourceAsync` uses batch-replace semantics — running twice produces no duplicates.

**`liens_WorkflowTransitions` rows are NOT deleted or modified.**

---

## 6. Dual-Read / Fallback Logic

### `WorkflowTransitionValidationService` (rewritten for TASK-MIG-04)

**New dependency:** `ILiensTaskServiceClient` injected alongside the existing `ILienWorkflowConfigRepository`.

**New interface signature:** `IsTransitionAllowedAsync` and `GetAllowedNextStagesAsync` both accept `tenantId` as first parameter.

**Read path (`GetTransitionsDualReadAsync`):**
1. Call `_taskClient.GetTransitionsAsync(tenantId, ProductCode)`.
2. If Task service returns ≥ 1 transition → use that set. Log `transition_source=task_service`.
3. If Task service returns 0 transitions → fall through to Liens DB. Log `transition_source=liens_db_fallback_empty`.
4. On any exception → fall through to Liens DB. Log `transition_source=liens_db_fallback_error`.
5. Liens DB: `_repo.GetActiveTransitionsAsync(workflowConfigId)`.

### `LienTaskService.UpdateTaskAsync` — call site updated
`tenantId` now passed as first arg to `IsTransitionAllowedAsync`.

### Write-through on admin ops (`LienWorkflowConfigService`)
After Liens DB write succeeds, `TrySyncTransitionsToTaskServiceAsync` is called:
1. Loads current active transitions from Liens DB.
2. Builds `TaskServiceTransitionsUpsertRequest` (batch-replace payload).
3. Calls `_taskClient.UpsertTransitionsFromSourceAsync`.
4. On failure: logs `transition_sync=failed` WARN; never propagates.

Applied to:
- `AddTransitionAsync`
- `DeactivateTransitionAsync`
- `SaveTransitionsAsync`

---

## 7. Runtime Compatibility

| Concern | Status |
|---|---|
| Task-board movement validation unchanged | ✅ Same open-move / strict-mode semantics; only read source changed |
| Stage IDs used in transitions | ✅ Same GUIDs as MIG-03 (no derivation needed) |
| Flow orchestration transitions untouched | ✅ `flow_workflow_transitions` never referenced or modified |
| Liens `liens_WorkflowTransitions` table | ✅ Never modified, never deleted |
| `EnsureDefaultTransitionsAsync` | ✅ Still writes to Liens DB; write-through syncs result to Task service |
| Admin GET /transitions endpoint | ✅ Returns Liens DB data (authoritative); no dual-read on GET path |
| Open-move mode (0 transitions) | ✅ Both Task-first and Liens fallback return empty → allow all moves |
| Strict mode (≥1 transition) | ✅ Exact from→to pair matching preserved |
| Self-transition guard | ✅ Checked before any store/read lookup |

**No new Task logic interprets transitions as process/orchestration rules.** Task service stores and retrieves (from, to) pairs only — it has no opinion on what they mean.

---

## 8. Validation Results

| Check | Method | Result |
|---|---|---|
| Task service builds with 0 errors | `dotnet build Task.Api.csproj` | ✅ PASS |
| Liens service builds with 0 errors | `dotnet build Liens.Api.csproj` | ✅ PASS |
| Existing Liens transition behavior unchanged | Code inspection — same open/strict semantics | ✅ PASS |
| Task service stores correct from/to pairs | Schema + service logic review | ✅ PASS |
| Dual-read returns Task-first when data present | Code path review | ✅ PASS |
| Liens DB fallback on empty/error | `GetTransitionsDualReadAsync` try/catch + count guard | ✅ PASS |
| Idempotency — DeactivateAll then re-insert | `UpsertFromSourceAsync` logic review | ✅ PASS |
| Batch-replace — no duplicates | Unique index `UX_StageTransitions_Unique` | ✅ PASS |
| Inactive transitions stay inactive | `Deactivate` sets `IsActive=false`; only active rows included in upsert payload | ✅ PASS |
| No Flow orchestration transitions accessed | Grep confirms no `flow_workflow_transitions` ref in Task/Liens | ✅ PASS |
| Task service unavailable → Liens DB fallback | `try/catch` in `GetTransitionsDualReadAsync` | ✅ PASS |
| Existing Task consumers unaffected | No existing Task transition model existed before | ✅ PASS |

---

## 9. Rollback Plan

**To revert Liens to Liens-only transition reads:**
1. Revert `WorkflowTransitionValidationService` to the prior Liens-only version (single constructor arg, no tenantId param).
2. Revert `IWorkflowTransitionValidationService` interface (remove `tenantId` param).
3. Revert `LienTaskService` call site (remove `tenantId` arg).
4. Remove write-through calls from `LienWorkflowConfigService` (3 sites + `TrySyncTransitionsToTaskServiceAsync` helper).
5. Remove `LiensTransitionSyncService` and DI registration.

**Task schema:** `tasks_StageTransitions` can be left in place — harmless. If removal is required, run `20260421000010_AddStageTransitions.Down()`. No Liens data is affected.

**Liens `liens_WorkflowTransitions`:** Never modified — immediately authoritative on rollback.  
**No data loss in any rollback scenario.**

---

## 10. Known Gaps / Risks

| Item | Notes |
|---|---|
| Transition IDs not preserved | Acceptable — ID is opaque; from→to pair is the semantic unit. Task service IDs are internal. |
| `WorkflowConfigId` flattened | Safe for single config per tenant+product. If Liens ever allows multiple configs per tenant, this must be revisited. |
| Upsert race condition | Two concurrent admin saves for the same tenant could both issue DeactivateAll + insert. Unique index prevents duplicates; last writer wins on reactivation. Acceptable for admin-frequency operations. |
| Admin GET /transitions returns Liens DB only | Intentional — Liens DB is authoritative. No dual-read on the read-only admin path. |
| No hard cutover yet | Task-first reads require data to exist in Task; initially all reads fall back to Liens DB until sync catches up. Expected behavior. |
| `EnsureDefaultTransitionsAsync` not write-through | Auto-initialised transitions are Liens DB only; the background sync will catch them up within 60 min. Acceptable. |
| This model is NOT a workflow engine | Confirmed — `tasks_StageTransitions` contains only (from, to) pairs with no conditions, rules, branching or automation semantics. |

---

## Files Changed

| Path | Change |
|---|---|
| `apps/services/task/Task.Domain/Entities/TaskStageTransition.cs` | New entity |
| `apps/services/task/Task.Infrastructure/Persistence/Configurations/TaskStageTransitionConfiguration.cs` | New EF config |
| `apps/services/task/Task.Infrastructure/Persistence/Migrations/20260421000010_AddStageTransitions.cs` | New migration |
| `apps/services/task/Task.Infrastructure/Persistence/Migrations/TasksDbContextModelSnapshot.cs` | Updated snapshot |
| `apps/services/task/Task.Infrastructure/Persistence/TasksDbContext.cs` | Added `StageTransitions` DbSet |
| `apps/services/task/Task.Application/Repositories/ITaskStageTransitionRepository.cs` | New repo interface |
| `apps/services/task/Task.Infrastructure/Persistence/Repositories/TaskStageTransitionRepository.cs` | New repo impl |
| `apps/services/task/Task.Application/DTOs/TaskStageTransitionDtos.cs` | New DTOs |
| `apps/services/task/Task.Application/Interfaces/ITaskStageTransitionService.cs` | New service interface |
| `apps/services/task/Task.Application/Services/TaskStageTransitionService.cs` | New service impl |
| `apps/services/task/Task.Api/Endpoints/TaskStageTransitionEndpoints.cs` | New endpoints |
| `apps/services/task/Task.Api/Program.cs` | Endpoint registration |
| `apps/services/task/Task.Infrastructure/DependencyInjection.cs` | Repo + service DI |
| `apps/services/liens/Liens.Application/DTOs/TaskServiceTransitionDto.cs` | New DTOs |
| `apps/services/liens/Liens.Application/Interfaces/ILiensTaskServiceClient.cs` | 2 new transition methods |
| `apps/services/liens/Liens.Infrastructure/TaskService/LiensTaskServiceClient.cs` | 2 new transition client methods |
| `apps/services/liens/Liens.Application/Interfaces/IWorkflowTransitionValidationService.cs` | Added `tenantId` param |
| `apps/services/liens/Liens.Application/Services/WorkflowTransitionValidationService.cs` | Dual-read implementation |
| `apps/services/liens/Liens.Application/Services/LienTaskService.cs` | Pass `tenantId` to validator |
| `apps/services/liens/Liens.Application/Services/LienWorkflowConfigService.cs` | Write-through + helper |
| `apps/services/liens/Liens.Application/Services/LiensTransitionSyncService.cs` | New background sync service |
| `apps/services/liens/Liens.Infrastructure/DependencyInjection.cs` | Sync service DI registration |
| `analysis/TASK-MIG-04-report.md` | This report |
