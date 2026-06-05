# TASK-FLOW-02 — Read Path Migration (Flow → Task)

**Date:** 2026-04-21
**Scope:** Move Flow read paths off `flow_workflow_tasks` and onto the Task service. Extends the Phase 1 dual-write (TASK-FLOW-01) to full read ownership by Task service. Prerequisite for TASK-FLOW-03 (table drop).

---

## 1. Codebase Analysis

### Flow read surfaces currently using `flow_workflow_tasks`

| Service | Method | Query type | Table dependency |
|---|---|---|---|
| `MyTasksService` | `ListMyTasksAsync` | Assigned-user filter, status filter, paginated | `flow_workflow_tasks` |
| `MyTasksService` | `ListRoleQueueAsync` | `AssignmentMode=RoleQueue`, `AssignedRole IN (roles)`, `Status=Open` | `flow_workflow_tasks` |
| `MyTasksService` | `ListOrgQueueAsync` | `AssignmentMode=OrgQueue`, `AssignedOrgId=orgId`, `Status=Open` | `flow_workflow_tasks` |
| `MyTasksService` | `GetTaskDetailAsync` | Single row by ID + eligibility predicate | `flow_workflow_tasks` |
| `WorkflowTasksController` | `Timeline` | `TenantId` lookup by task ID (belt-and-braces audit filter) | `flow_workflow_tasks` |
| `WorkflowTaskSlaEvaluator` | `TickAsync` | Batch of active tasks with `DueAt`, ordered by `LastSlaEvaluatedAt` | `flow_workflow_tasks` |

### Flow write paths fixed in this task (TASK-FLOW-01 regression)

| Service | TASK-FLOW-01 gap | TASK-FLOW-02 fix |
|---|---|---|
| `WorkflowTaskFromWorkflowFactory` | Sent `assignedUserId` only; `AssignmentMode`, `AssignedRole`, `AssignedOrgId` not sent to Task service | Now sends all queue metadata |
| `WorkflowTaskAssignmentService` | `DirectUser` delegated; `RoleQueue`/`OrgQueue` shadow-only with warning | Now delegates all modes via `SetQueueAssignmentAsync` |

---

## 2. Current Read Dependency Map

```
MyTasksController (GET /api/v1/tasks/me, /role-queue, /org-queue)
  └── MyTasksService
        └── IFlowDbContext.WorkflowTasks  ← DEPENDENCY TO REMOVE

WorkflowTasksController (GET /api/v1/workflow-tasks/{id})
  └── MyTasksService.GetTaskDetailAsync
        └── IFlowDbContext.WorkflowTasks  ← DEPENDENCY TO REMOVE

WorkflowTasksController (GET /api/v1/workflow-tasks/{id}/timeline)
  └── _db.WorkflowTasks (TenantId lookup)  ← DEPENDENCY TO REMOVE

WorkflowTaskSlaEvaluator (background)
  └── FlowDbContext.WorkflowTasks (batch read + write)  ← RETAINED TEMPORARILY (see §6)
```

**`MyTaskDto` fields needed and their Task service coverage before this task:**

| Field | Task `TaskDto` field | Gap |
|---|---|---|
| `TaskId` | `Id` | Name difference only (mappable) |
| `Title` | `Title` | ✓ |
| `Description` | `Description` | ✓ |
| `Status` | `Status` | ✓ (case difference: Flow=PascalCase, Task=UPPER) |
| `Priority` | `Priority` | ✓ (case difference) |
| `StepKey` | `WorkflowStepKey` | Name difference only |
| `AssignmentMode` | — | **Missing** |
| `AssignedUserId` | `AssignedUserId` (Guid?) | Type difference (Flow=string) |
| `AssignedRole` | — | **Missing** |
| `AssignedOrgId` | — | **Missing** |
| `AssignedAt` | — | **Missing** |
| `AssignedBy` | — | **Missing** |
| `AssignmentReason` | — | **Missing** |
| `CreatedAt` | `CreatedAtUtc` | Name difference |
| `UpdatedAt` | `UpdatedAtUtc` | Name difference |
| `StartedAt` | — | **Missing** |
| `CompletedAt` | `CompletedAt` | ✓ |
| `CancelledAt` | — | **Missing** |
| `DueAt` | `DueAt` | ✓ |
| `SlaStatus` | — | **Missing** |
| `SlaBreachedAt` | — | **Missing** |
| `WorkflowInstanceId` | `WorkflowInstanceId` | ✓ |
| `WorkflowName` | — | **Flow-only** (join to WorkflowInstance/Definition) |
| `ProductKey` | — | **Flow-only** (join to WorkflowInstance) |

---

## 3. Task Query Gap Analysis

### Structural gaps (require Task schema + API changes)

| Gap | Impact | Resolution |
|---|---|---|
| No `AssignmentMode` field | Cannot filter for role/org queue reads | Add column to `PlatformTask`; add filter to `SearchAsync` |
| No `AssignedRole` field | Cannot filter `RoleQueue` tasks by role | Add column; add filter |
| No `AssignedOrgId` field | Cannot filter `OrgQueue` tasks by org | Add column; add filter |
| No `AssignedAt`, `AssignedBy`, `AssignmentReason` | `MyTaskDto` assignment context missing | Add columns |
| No `StartedAt`, `CancelledAt` | Lifecycle timestamps missing | Add columns; set during `TransitionStatus` |
| No `SlaStatus`, `SlaBreachedAt`, `LastSlaEvaluatedAt` | SLA display and evaluator state missing | Add columns; updated by Flow SLA evaluator push |

### Query shape gaps (require Task API changes only)

| Gap | Resolution |
|---|---|
| No `assignmentMode`, `assignedRole`, `assignedOrgId` filters on `GET /api/tasks` | Add params to `SearchAsync` + `ListTasks` endpoint |
| Default sort (`CreatedAtUtc DESC`) differs from Flow's `OrderActiveFirst` | Add `sort=flowActiveFirst` option to `SearchAsync` |
| Eligibility-filtered `GetTaskDetail` (role/org check in query) | Fetch from Task service by ID + eligibility check in Flow in-memory |
| Internal SLA update path | Add `POST /api/tasks/internal/flow-sla-update` endpoint |
| Internal queue assignment path | Add `POST /api/tasks/internal/flow-queue-assign` endpoint |

### Fields that remain in Flow (not moved to Task)

| Field | Reason |
|---|---|
| `WorkflowName` | Derived from `WorkflowDefinition.Name` — Flow orchestration data, not task data |
| `ProductKey` | From `WorkflowInstance.ProductKey` — Flow orchestration data |

Both are enriched in the new `MyTasksService` by querying Flow's own `WorkflowInstances` table (already available).

---

## 4. Task Service Read API Changes

### Schema additions to `PlatformTask` (new columns in `tasks_Tasks`)

```
AssignmentMode      VARCHAR(20)  NULL   -- DirectUser, RoleQueue, OrgQueue, Unassigned
AssignedRole        VARCHAR(100) NULL   -- role key when AssignmentMode = RoleQueue
AssignedOrgId       VARCHAR(100) NULL   -- org id when AssignmentMode = OrgQueue
AssignedAt          DATETIME     NULL   -- UTC timestamp of last assignment event
AssignedBy          VARCHAR(100) NULL   -- actor user ID string (Flow JWT sub)
AssignmentReason    VARCHAR(500) NULL   -- free-form note on last assignment
StartedAt           DATETIME     NULL   -- set when status → IN_PROGRESS
CancelledAt         DATETIME     NULL   -- set when status → CANCELLED
SlaStatus           VARCHAR(20)  NOT NULL DEFAULT 'OnTrack'
SlaBreachedAt       DATETIME     NULL
LastSlaEvaluatedAt  DATETIME     NULL
```

Index added: `IX_Tasks_TenantId_AssignmentMode_Role` on `(TenantId, AssignmentMode, AssignedRole)`.
Index added: `IX_Tasks_TenantId_AssignmentMode_Org` on `(TenantId, AssignmentMode, AssignedOrgId)`.

### DTO additions to `TaskDto`

All new schema fields exposed on `TaskDto`. Existing consumers are unaffected (new nullable fields in a record, additive).

### `GET /api/tasks` — new query params

| Param | Type | Purpose |
|---|---|---|
| `assignmentMode` | string? | Filter by assignment mode (DirectUser, RoleQueue, OrgQueue, Unassigned) |
| `assignedRole` | string? | Filter by role key (used for RoleQueue reads) |
| `assignedOrgId` | string? | Filter by org ID (used for OrgQueue reads) |
| `sort` | string? | `flowActiveFirst` applies Flow's 7-level active-first sort |

### New internal endpoints (service-token auth, `InternalService` policy)

| Endpoint | Purpose |
|---|---|
| `POST /api/tasks/internal/flow-sla-update` | Batch-push SLA status from Flow's SLA evaluator |
| `POST /api/tasks/internal/flow-queue-assign` | Set queue assignment metadata (for claim/reassign delegation) |

### Migration

`20260421000011_AddFlowTaskFields` — adds all new columns and indexes.

---

## 5. Flow Read Path Migration

### `MyTasksService` rewrite

`MyTasksService` now implements `IMyTasksService` by calling `IFlowTaskServiceClient` instead of `IFlowDbContext`. The interface shape (`ListMyTasksAsync`, `ListRoleQueueAsync`, `ListOrgQueueAsync`, `GetTaskDetailAsync`) is unchanged — controllers and other callers are not affected.

**`ListMyTasksAsync`**: calls `GET /api/tasks?assignedUserId={userId}&status={filter}&sort=flowActiveFirst`
**`ListRoleQueueAsync`**: calls `GET /api/tasks?assignmentMode=RoleQueue&assignedRole={role}&status=OPEN&sort=flowActiveFirst` per eligible role; admin bypass sends `assignmentMode=RoleQueue` only
**`ListOrgQueueAsync`**: calls `GET /api/tasks?assignmentMode=OrgQueue&assignedOrgId={orgId}&status=OPEN&sort=flowActiveFirst`
**`GetTaskDetailAsync`**: calls `GET /api/tasks/{id}` then enforces eligibility check in-memory (same predicate as before, but in Flow not SQL)

All calls forward the caller's bearer token via `FlowTaskServiceAuthDelegatingHandler`.

**WorkflowName / ProductKey enrichment**: after fetching from Task service, `MyTasksService` queries Flow's `WorkflowInstances` (with `WorkflowDefinition` LEFT JOIN) for the unique `WorkflowInstanceId` values, then enriches the DTOs in-memory. This is a single batch query (not N+1).

### `WorkflowTasksController.Timeline`

The `TenantId` lookup previously read `_db.WorkflowTasks`. It is replaced with `_db.WorkflowInstances` — since the `Timeline` endpoint already calls `GetTaskDetailAsync` (which provides `WorkflowInstanceId`), the `TenantId` can be read from the workflow instance (`WorkflowInstance.TenantId`). This removes the last direct `WorkflowTasks` read from the controller.

---

## 6. SLA Ownership / Background Read Changes

**Decision: Option C (retained, push to Task service)**

`WorkflowTaskSlaEvaluator` is retained in Flow for Phase 2 because:
- `flow_workflow_tasks` still exists (drop is TASK-FLOW-03)
- The evaluator reads `DueAt` and `Status` from the shadow table, which remains dual-written
- It correctly handles all-tenant cross-cutting (ignores query filters)

Changes in Phase 2:
- After computing SLA transitions, the evaluator calls `POST /api/tasks/internal/flow-sla-update` to push `(taskId, slaStatus, slaBreachedAt, lastSlaEvaluatedAt)` tuples to Task service
- This ensures Task service's `SlaStatus` / `SlaBreachedAt` fields are populated and fresh for read consumers
- The evaluator still writes to `flow_workflow_tasks` shadow (unchanged)

TASK-FLOW-03 will retire the evaluator entirely; Task service will own SLA computation natively.

---

## 7. Validation Results

| Check | Result | Notes |
|---|---|---|
| Task service builds cleanly | PASS | 0 errors, 0 warnings |
| Flow service builds cleanly | PASS | 0 errors, 0 warnings |
| `flow_workflow_tasks` no longer queried by `MyTasksService` | PASS | No `_db.WorkflowTasks` remaining in service |
| `WorkflowTasksController.Timeline` no longer queries `WorkflowTasks` for TenantId | PASS | Replaced with `WorkflowInstances` lookup |
| SLA evaluator still writes to shadow + pushes to Task service | PASS | Best-effort push; non-fatal on HTTP error |
| No interface regressions (`IMyTasksService` unchanged) | PASS | Controllers unmodified |
| `ExternalId` alignment: `PlatformTask.Id == WorkflowTask.Id` | PASS | Factory passes `task.Id` as `externalId` on creation; Task service uses `Id = externalId ?? Guid.NewGuid()` |
| All assignment modes delegated via internal service token | PASS | `SetQueueAssignmentAsync` replaces Phase 1 `AssignUserAsync`-only path |

---

## 8. Rollback Plan

Rollback is **code-only** — no DB rollback is required for the Flow read migration.

**Task service schema additions (new columns)**: safe to leave in place. They are all nullable (or have defaults) and additive. Existing Task service consumers are unaffected.

**Task service internal endpoints**: safe to leave in place. They are unused by anything except Flow's evaluator.

**Flow read path**: to revert:
1. Restore the original `MyTasksService` that queries `IFlowDbContext.WorkflowTasks` directly.
2. Restore the original `WorkflowTasksController.Timeline` `TenantId` lookup.
3. Remove the SLA evaluator Task service push call.

The TASK-FLOW-01 write path changes (queue metadata on create/assign) are additive and safe to leave in place even during rollback.

---

## 9. Known Gaps / Risks

| Item | Status | Notes |
|---|---|---|
| `flow_workflow_tasks` still exists | Retained | Drop is TASK-FLOW-03 prerequisite |
| `WorkflowTaskSlaEvaluator` still reads shadow table | Retained | Will be retired in TASK-FLOW-03 |
| Dual-write to `flow_workflow_tasks` still active | Retained | TASK-FLOW-01 write path unchanged |
| `SlaStatus` freshness on Task service | Push-based | Evaluator pushes on each tick; sub-minute lag acceptable |
| `RoleQueue` admin bypass query | Admin sees ALL role-queue tasks | Sent as `assignmentMode=RoleQueue` without role filter — Task service returns all for tenant |
| `flow_task_items` table | Not in scope | Separate concern; assess in TASK-FLOW-03 planning |
| TASK-FLOW-03 prerequisites | All met after TASK-FLOW-02 | `flow_workflow_tasks` no longer needed for runtime reads; dual-write can be stopped |

### Prerequisites for TASK-FLOW-03 (table drop)

1. Stop dual-write in `WorkflowTaskFromWorkflowFactory`, `WorkflowTaskLifecycleService`, `WorkflowTaskAssignmentService`
2. Retire `WorkflowTaskSlaEvaluator`
3. Remove shadow entity `WorkflowTask` from `IFlowDbContext` / `FlowDbContext`
4. Drop `flow_workflow_tasks` table via Flow migration
5. Remove `flow_workflow_tasks`-related EF entity configuration

---

## Files Changed

### Task service

| File | Action |
|---|---|
| `Task.Domain/Entities/PlatformTask.cs` | MODIFY — add 11 new fields + domain methods |
| `Task.Infrastructure/Persistence/Configurations/PlatformTaskConfiguration.cs` | MODIFY — add EF config + 2 indexes |
| `Task.Application/DTOs/TaskDtos.cs` | MODIFY — add new fields to TaskDto + new request/response DTOs |
| `Task.Application/Interfaces/ITaskRepository.cs` | MODIFY — add filter params to SearchAsync |
| `Task.Infrastructure/Persistence/Repositories/TaskRepository.cs` | MODIFY — implement new filter params + flowActiveFirst sort |
| `Task.Application/Interfaces/ITaskService.cs` | MODIFY — add UpdateFlowSlaStateAsync, SetFlowQueueAssignmentAsync |
| `Task.Application/Services/TaskService.cs` | MODIFY — implement new methods, update Create/Assign/TransitionStatus |
| `Task.Api/Endpoints/TaskEndpoints.cs` | MODIFY — add new query params to ListTasks |
| `Task.Api/Endpoints/InternalTaskEndpoints.cs` | CREATE — SLA update + queue assign internal endpoints |
| `Task.Infrastructure/Persistence/Migrations/20260421000011_AddFlowTaskFields.cs` | CREATE |
| `Task.Infrastructure/Persistence/Migrations/TasksDbContextModelSnapshot.cs` | MODIFY |

### Flow service

| File | Action |
|---|---|
| `Flow.Application/Interfaces/IFlowTaskServiceClient.cs` | MODIFY — add `externalId` param + read methods + queue assign + SLA update |
| `Flow.Infrastructure/TaskService/FlowTaskServiceClient.cs` | MODIFY — implement all new methods; use `IHttpClientFactory` for internal client |
| `Flow.Infrastructure/TaskService/FlowTaskInternalAuthHandler.cs` | CREATE — always-mints-service-token handler for internal endpoints |
| `Flow.Infrastructure/DependencyInjection.cs` | MODIFY — register `FlowTaskInternalAuthHandler` + "FlowTaskInternal" named HttpClient |
| `Flow.Application/Services/WorkflowTaskFromWorkflowFactory.cs` | MODIFY — pass `task.Id` as `externalId` + queue metadata on create |
| `Flow.Application/Services/WorkflowTaskAssignmentService.cs` | MODIFY — delegate all modes to Task service |
| `Flow.Application/Services/MyTasksService.cs` | MODIFY — rewrite reads to use IFlowTaskServiceClient |
| `Flow.Api/Controllers/V1/WorkflowTasksController.cs` | MODIFY — fix TenantId lookup in Timeline |
| `Flow.Infrastructure/Outbox/WorkflowTaskSlaEvaluator.cs` | MODIFY — push SLA updates to Task service |
