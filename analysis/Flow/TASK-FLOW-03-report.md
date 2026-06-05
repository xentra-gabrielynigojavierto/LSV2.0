# TASK-FLOW-03 — Drop `flow_workflow_tasks` Shadow Table

**Status**: COMPLETE  
**Date**: 2026-04-21  
**Build verification**: Task.Api ✅ 0 errors · Flow.Api ✅ 0 errors

---

## Objective

Remove the `flow_workflow_tasks` shadow table from Flow DB. The table was
introduced (LS-FLOW-E11.1 through E14.1) as an in-process projection of the
Task service's `platform_tasks` table. It caused dual-write bugs, SLA drift,
and made the Task service the non-authoritative copy of its own data.

After TASK-FLOW-03 the Task service is the **sole write authority** for all
task data. Flow services call the Task service API for reads and writes.

---

## Services Migrated (all Flow.Application services)

| Service | Change |
|---|---|
| `WorkflowTaskLifecycleService` | Removed `ApplyCasAsync` + `_db` shadow writes |
| `WorkflowTaskCompletionService` | Uses `_taskClient.GetTaskSnapshotAsync` instead of shadow read |
| `WorkflowTaskAssignmentService` | Removed `_db` + `ExecuteUpdateAsync` CAS shadow writes |
| `WorkflowTaskFromWorkflowFactory` | Removed `_db.WorkflowTasks.Add` + local dedup (uses `_taskClient.HasActiveStepTaskAsync`) |
| `TaskRecommendationService` | Uses `_taskClient.GetWorkloadCountsAsync` instead of `_db` |
| `WorkflowTaskSlaEvaluator` | **Full rewrite** — reads via `GetTasksForSlaEvaluationAsync`, pushes via `UpdateSlaStateAsync` |

---

## New Task Service Endpoints Added (TASK-FLOW-03 scope)

### `GET /api/tasks/internal/flow-sla-batch`

Cross-tenant batch read for Flow's `WorkflowTaskSlaEvaluator`.

**Query params**:
- `batchSize` (default 100) — max rows to return
- `dueSoonMinutes` (default 60) — DueSoon horizon in minutes from now

**Response** (`FlowSlaBatchResponse`):
```json
{
  "items": [
    {
      "taskId": "...",
      "tenantId": "...",
      "dueAt": "2026-04-21T10:00:00Z",
      "slaStatus": "DueSoon",
      "slaBreachedAt": null
    }
  ]
}
```

**Semantics**:
- No tenant filter — worker runs cross-tenant (internal service auth only).
- Returns OPEN / IN_PROGRESS tasks where `DueAt <= now + dueSoonMinutes`
  OR `SlaStatus != "OnTrack"` (keeps re-evaluating promoted tasks).
- Ordered by `DueAt ASC` (nearest deadline first).

---

## SLA Evaluator — New Tick Flow

```
1. GetTasksForSlaEvaluationAsync(batchSize, dueSoonMinutes)   [Task service read]
2. For each item:
     newStatus   = WorkflowTaskSlaPolicy.ComputeStatus(now, dueAt, threshold)
     newBreached = WorkflowTaskSlaPolicy.ComputeBreachedAt(newStatus, item.SlaBreachedAt, now)
3. Collect items where status or breachedAt changed
4. Group by TenantId → UpdateSlaStateAsync per tenant              [Task service write]
```

No Flow DB writes during a tick. The `LastSlaEvaluatedAt` field (which was
only on the shadow table) is no longer tracked; re-evaluation of the same
urgent batch is idempotent and acceptable for batch sizes ≤ 100.

---

## FlowDbContext Changes

| Location | Change |
|---|---|
| `IFlowDbContext` — `DbSet<WorkflowTask> WorkflowTasks` | **Removed** |
| `FlowDbContext` — `DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>()` | **Removed** |
| `FlowDbContext.OnModelCreating` — full `flow_workflow_tasks` entity config block | **Removed** (≈90 lines) |
| `FlowDbContext.SaveChangesAsync` — `EnsureValid()` loop for WorkflowTask entries | **Removed** |
| `FlowDbContextModelSnapshot.cs` — WorkflowTask entity config + navigation blocks | **Removed** |

---

## EF Migration

**File**: `20260421000000_DropWorkflowTaskShadowTable_TaskFlow03.cs`

```
Up:   DropIndex (8 indexes) → DropTable "flow_workflow_tasks"
Down: intentionally empty (no back-fill possible via EF rollback alone)
```

Production DBs are migrated automatically via `db.Database.MigrateAsync()` on startup.

---

## Status Normalisation (NormalizeStatus helper)

Task service uses UPPERCASE statuses (`OPEN`, `IN_PROGRESS`, `COMPLETED`, `CANCELLED`).  
Flow uses PascalCase (`Open`, `InProgress`, `Completed`, `Cancelled`).  
A `NormalizeStatus` helper in `WorkflowTaskLifecycleService` maps between the two.  
SlaStatus is already PascalCase on both sides (`OnTrack`, `DueSoon`, `Overdue`).

---

## Pre-existing Issues NOT Fixed in This Task

- Priority mismatch: Flow uses `Normal`, Task service uses `MEDIUM`. Pre-existing bug;
  a separate task is needed to align the enums.
- `WorkflowTask` domain entity class (`Flow.Domain.Entities.WorkflowTask`) is retained
  as a domain model — only the shadow DB table is dropped.

---

## Files Changed

**Task service (new/modified)**:
- `Task.Application/DTOs/TaskDtos.cs` — `FlowSlaBatchItem` + `FlowSlaBatchResponse` records
- `Task.Application/Interfaces/ITaskRepository.cs` — `GetFlowSlaBatchAsync` signature
- `Task.Infrastructure/Persistence/Repositories/TaskRepository.cs` — implementation
- `Task.Application/Interfaces/ITaskService.cs` — `GetFlowSlaBatchAsync` signature
- `Task.Application/Services/TaskService.cs` — implementation
- `Task.Api/Endpoints/TaskFlowEndpoints.cs` — `GET /flow-sla-batch` handler

**Flow service (new/modified)**:
- `Flow.Application/Interfaces/IFlowTaskServiceClient.cs` — `FlowSlaBatchItem` record + `GetTasksForSlaEvaluationAsync`
- `Flow.Infrastructure/TaskService/FlowTaskServiceClient.cs` — HTTP GET implementation
- `Flow.Infrastructure/Outbox/WorkflowTaskSlaEvaluator.cs` — full rewrite (removed `_db`, uses `_taskClient`)
- `Flow.Application/Interfaces/IFlowDbContext.cs` — removed `DbSet<WorkflowTask>`
- `Flow.Infrastructure/Persistence/FlowDbContext.cs` — removed DbSet, entity config, EnsureValid loop
- `Flow.Infrastructure/Persistence/Migrations/FlowDbContextModelSnapshot.cs` — removed WorkflowTask blocks
- `Flow.Infrastructure/Persistence/Migrations/20260421000000_DropWorkflowTaskShadowTable_TaskFlow03.cs` — new
- `Flow.Infrastructure/Persistence/Migrations/20260421000000_DropWorkflowTaskShadowTable_TaskFlow03.Designer.cs` — new
