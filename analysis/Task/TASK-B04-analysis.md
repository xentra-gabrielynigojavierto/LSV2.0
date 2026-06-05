# TASK-B04 — Liens Consumer Cutover & Data Migration
## Architecture Analysis & Implementation Report

**Date:** 2026-04-21  
**Service scope:** `apps/services/liens` · `apps/services/task`  
**Status:** Implementation complete — awaiting production backfill run and drop-migration apply

---

## 1. Objective

Remove the duplicate task runtime that existed inside the Liens service and replace it with HTTP delegation to the canonical Task microservice. After cutover:

- The Task service is the single source of truth for all task state, history, and notes across the platform.
- Liens retains only governance rules, templates, generation rules, workflow configuration, and the audit/notification orchestration layer.
- Liens task IDs equal Task service IDs (ExternalId identity approach), so all existing references (case pages, notification payloads, Flow callbacks) remain valid without ID remapping.

---

## 2. Before / After Architecture

### Before (dual runtime)

```
Liens API
  └─ LienTaskService          ──writes──►  liens_Tasks
  └─ LienTaskNoteService      ──writes──►  liens_TaskNotes
  └─ FlowEventHandler         ──writes──►  liens_Tasks (WorkflowStepKey)
  └─ LienTaskGenerationEngine ──writes──►  liens_Tasks + liens_GeneratedTaskMetadata
```

Task service existed in parallel but was never used by Liens.

### After (proxy consumer)

```
Liens API
  └─ LienTaskService          ──HTTP──►  Task service  (owns tasks_Tasks)
  └─ LienTaskNoteService      ──HTTP──►  Task service  (owns tasks_Notes)
  └─ FlowEventHandler         ──HTTP──►  Task service  (flow-callback endpoint)
  └─ LienTaskGenerationEngine ──HTTP──►  LienTaskService.CreateAsync  [unchanged call site]
                               ──SQL──►  liens_GeneratedTaskMetadata  [kept]
                               ──SQL──►  liens_Tasks (dup-check only) [until drop migration]
```

---

## 3. Status Mapping

Liens uses a 5-value status vocabulary; Task uses a 6-value vocabulary with different names for the initial state.

| Liens status   | Task status      | Direction       |
|---------------|-----------------|-----------------|
| `NEW`          | `OPEN`           | write (→ Task)  |
| `OPEN`         | `NEW`            | read  (← Task)  |
| `IN_PROGRESS`  | `IN_PROGRESS`    | both (1:1)      |
| `WAITING_BLOCKED` | `WAITING_BLOCKED` | both (1:1)   |
| `COMPLETED`    | `COMPLETED`      | both (1:1)      |
| `CANCELLED`    | `CANCELLED`      | both (1:1)      |

Implemented in `LiensTaskServiceClient`:

```csharp
private static string ToTaskStatus(string liensStatus) =>
    string.Equals(liensStatus, "NEW", StringComparison.OrdinalIgnoreCase) ? "OPEN" : liensStatus.ToUpperInvariant();

private static string ToLiensStatus(string taskStatus) =>
    string.Equals(taskStatus, "OPEN", StringComparison.OrdinalIgnoreCase) ? "NEW" : taskStatus.ToUpperInvariant();
```

`WAITING_BLOCKED` was a net-new status added to the Task service `TaskStatus` enum as part of this task.

---

## 4. ExternalId Identity Approach

Rather than maintaining a mapping table between Liens task IDs and Task service IDs, the backfill uses **deterministic ID seeding**: each task is created in the Task service with `ExternalId = LienTask.Id`. The `PlatformTask` entity honours this:

```csharp
// Task.Domain — PlatformTask factory
Id = externalId ?? Guid.NewGuid()
```

Consequences:
- All existing Liens task IDs are valid Task service IDs with no translation layer.
- `BackfillTaskAsync` checks for prior existence with a `GET /api/tasks/{externalId}` before creating, making the backfill fully idempotent.
- New tasks created after the code cutover (but before the drop migration) also receive a freshly minted `externalId` in `LienTaskService.CreateAsync`, so they are stored canonically in the Task service and can survive the eventual drop of `liens_Tasks`.

---

## 5. Source / Linked Entity Constants

All tasks created by Liens carry these context markers so the Task service can filter them unambiguously:

| Field               | Value           |
|--------------------|-----------------|
| `SourceProductCode` | `SYNQ_LIENS`    |
| `SourceEntityType`  | `LIEN_CASE`     |
| `SourceEntityId`    | `CaseId` (Guid) |
| `LinkedEntityType`  | `LIEN`          |
| `LinkedRelationship`| `RELATED`       |
| `Scope`             | `PRODUCT`       |

Lien links are stored as `tasks_LinkedEntities` rows (one row per lien), queried via the new `linkedEntityType` / `linkedEntityId` search parameters.

---

## 6. Task Service Extensions

The following additions were made to `apps/services/task` to support the Liens consumer.

### 6.1 TaskNote entity enhancements

| Field        | Type          | Purpose                                         |
|-------------|---------------|-------------------------------------------------|
| `AuthorName`  | `varchar(200)` | Display name from the originating product      |
| `IsEdited`    | `bool`         | Set `true` when note body is updated after creation |
| `IsDeleted`   | `bool`         | Soft-delete; rows are never physically removed |
| `Note`        | `varchar(5000)` | Extended from 4000 to match Liens note limit  |

### 6.2 New service methods

```csharp
// ITaskService
Task<TaskNoteDto> EditNoteAsync(Guid tenantId, Guid taskId, Guid noteId,
    Guid actingUserId, string newContent, CancellationToken ct);

Task DeleteNoteAsync(Guid tenantId, Guid taskId, Guid noteId,
    Guid actingUserId, CancellationToken ct);
```

`EditNoteAsync` sets `IsEdited = true` and updates the `Note` field.  
`DeleteNoteAsync` sets `IsDeleted = true`; the row remains for audit history.  
`TaskNoteRepository.GetByIdAsync` and `GetByTaskAsync` filter `WHERE IsDeleted = false`.

### 6.3 New HTTP endpoints

```
PUT    /api/tasks/{id}/notes/{noteId}   → EditNoteAsync
DELETE /api/tasks/{id}/notes/{noteId}   → DeleteNoteAsync
```

### 6.4 SearchAsync filter extensions

Added to `ITaskRepository.SearchAsync` / `TaskRepository.SearchAsync` / `ITaskService.SearchAsync` / `TaskEndpoints`:

| Parameter         | SQL translation                                                              |
|------------------|------------------------------------------------------------------------------|
| `sourceEntityType` | `WHERE SourceEntityType = @v`                                               |
| `sourceEntityId`   | `WHERE SourceEntityId = @v`                                                 |
| `linkedEntityType` + `linkedEntityId` | `INNER JOIN tasks_LinkedEntities WHERE EntityType=@t AND EntityId=@id` |
| `assignmentScope`  | `ME` → `WHERE AssignedUserId = @currentUserId`                             |
| `currentUserId`    | companion to `assignmentScope`                                              |

### 6.5 Database migration

Migration `20260421000004_LiensConsumerCutover` (`Task.Infrastructure`):

```sql
ALTER TABLE tasks_Notes ADD COLUMN AuthorName varchar(200);
ALTER TABLE tasks_Notes ADD COLUMN IsEdited   tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE tasks_Notes ADD COLUMN IsDeleted  tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE tasks_Notes MODIFY Note varchar(5000);

CREATE INDEX IX_Notes_TenantId_TaskId_IsDeleted
    ON tasks_Notes (TenantId, TaskId, IsDeleted);

CREATE INDEX IX_Tasks_TenantId_SourceEntityType_SourceEntityId
    ON tasks_Tasks (TenantId, SourceEntityType, SourceEntityId);
```

---

## 7. Liens Infrastructure

### 7.1 TaskServiceAuthDelegatingHandler

```
apps/services/liens/Liens.Infrastructure/TaskService/TaskServiceAuthDelegatingHandler.cs
```

Mirrors `NotificationsAuthDelegatingHandler`. Reads `X-Tenant-Id` from the outbound request, mints a short-lived HS256 JWT via `IServiceTokenIssuer` (signed with `FLOW_SERVICE_TOKEN_SECRET`), and injects `Authorization: Bearer`. Falls back silently when the key is not configured (dev / test environments without secrets).

### 7.2 LiensTaskServiceClient

```
apps/services/liens/Liens.Infrastructure/TaskService/LiensTaskServiceClient.cs
```

Full CRUD surface:

| Method                   | HTTP call                                        |
|--------------------------|--------------------------------------------------|
| `CreateTaskAsync`        | `POST /api/tasks` + `POST /api/tasks/{id}/linked-entities` per lien |
| `GetTaskAsync`           | `GET  /api/tasks/{id}`                           |
| `SearchTasksAsync`       | `GET  /api/tasks?{qs}` (builds query string)     |
| `UpdateTaskAsync`        | `PUT  /api/tasks/{id}`                           |
| `AssignTaskAsync`        | `POST /api/tasks/{id}/assign`                    |
| `TransitionStatusAsync`  | `POST /api/tasks/{id}/status`                    |
| `AddNoteAsync`           | `POST /api/tasks/{id}/notes`                     |
| `GetNotesAsync`          | `GET  /api/tasks/{id}/notes`                     |
| `UpdateNoteAsync`        | `PUT  /api/tasks/{id}/notes/{noteId}`            |
| `DeleteNoteAsync`        | `DELETE /api/tasks/{id}/notes/{noteId}`          |
| `AddLinkedEntityAsync`   | `POST /api/tasks/{id}/linked-entities`           |
| `TriggerFlowCallbackAsync` | `POST /api/tasks/internal/flow-callback`       |
| `BackfillTaskAsync`      | idempotent create + notes + extra lien links     |

All calls inject `X-Tenant-Id` and optionally `X-User-Id` headers. Status bridging is applied transparently on all write and read paths.

### 7.3 DI registration

```
apps/services/liens/Liens.Infrastructure/DependencyInjection.cs
```

```csharp
services.AddTransient<TaskServiceAuthDelegatingHandler>();

var taskBaseUrl = configuration["ExternalServices:Task:BaseUrl"] ?? "http://localhost:5016";
services.AddHttpClient<ILiensTaskServiceClient, LiensTaskServiceClient>(client =>
{
    client.BaseAddress = new Uri(taskBaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<TaskServiceAuthDelegatingHandler>();

services.AddScoped<ILienTaskBackfillService, LienTaskBackfillService>();
```

Configuration key: `ExternalServices:Task:BaseUrl` in `appsettings.json`  
Default (development): `http://localhost:5016`

---

## 8. Proxy Layer

### 8.1 LienTaskService (rewritten)

```
apps/services/liens/Liens.Application/Services/LienTaskService.cs
```

**Retained responsibilities:**
- Governance enforcement (`ILienTaskGovernanceSettingsRepository`) — `RequireAssigneeOnCreate`, `RequireCaseLinkOnCreate`, `RequireWorkflowStageOnCreate`, auto-derive start stage
- Workflow stage transition validation (`IWorkflowTransitionValidationService`)
- Flow instance resolution (`IFlowInstanceResolver.ResolveAsync`) on task create
- Audit event publishing (`IAuditPublisher`) on create / update / assign / status-change (both `LienTask` and `Case` entity types)
- Notification publishing (`INotificationPublisher`) on create with assignee and on reassign

**Removed responsibilities (now Task service):**
- All SQL writes to `liens_Tasks`
- Note persistence
- Status machine enforcement (delegated to Task service)

### 8.2 LienTaskNoteService (rewritten)

```
apps/services/liens/Liens.Application/Services/LienTaskNoteService.cs
```

Thin proxy with input validation (content required, max 5000 chars) and audit events (`liens.task_note.created`, `.updated`, `.deleted`). All storage delegated to `ILiensTaskServiceClient`.

### 8.3 FlowEventHandler

`FlowEventHandler.HandleAsync` now calls `_taskClient.TriggerFlowCallbackAsync(...)` instead of querying `liens_Tasks` directly. The callback payload (`workflowInstanceId`, `newStepKey`, `tenantId`) is identical; only the transport changed from in-process SQL to HTTP.

---

## 9. Backfill System

### 9.1 LienTaskBackfillService

```
apps/services/liens/Liens.Application/Services/LienTaskBackfillService.cs
```

Algorithm:
1. Page through `liens_Tasks` in batches of `batchSize` (default 100, max 500).
2. For each task, load its notes (`ILienTaskNoteRepository.GetByTaskIdAsync`) and lien links (`ILienTaskRepository.GetLienLinksForTaskAsync`).
3. Call `ILiensTaskServiceClient.BackfillTaskAsync` which:
   - Does `GET /api/tasks/{task.Id}` — skips if already exists (idempotent)
   - Does `POST /api/tasks` with `externalId = task.Id` (deterministic ID seeding)
   - Adds each note via `POST /api/tasks/{id}/notes` (preserves author attribution)
   - Adds each lien link via `POST /api/tasks/{id}/linked-entities`
4. If the original task had a non-`NEW` status, call `TransitionStatusAsync` to replicate it.
5. Tracks: `attempted`, `created`, `alreadyExisted`, `failed`, `totalNotes`, `totalLinks`, `elapsed`.

Note and link failures are non-fatal (logged as warnings); task-level failures are fatal for that row (logged as errors) but do not stop the batch.

### 9.2 Admin endpoint

```
POST /api/liens/internal/task-backfill
Header: X-Internal-Service-Token: {FLOW_SERVICE_TOKEN_SECRET}
Body:   { "actingAdminUserId": "<guid>", "batchSize": 100 }
```

Protected by the same shared-secret pattern as `FlowEventsEndpoints`. Returns:

```json
{
  "attempted":     150,
  "created":       148,
  "alreadyExisted": 2,
  "failed":         0,
  "totalNotes":    430,
  "totalLinks":    290,
  "elapsedMs":    4200
}
```

Idempotent — safe to run multiple times.

---

## 10. Database Migrations

### 10.1 Task service — `20260421000004_LiensConsumerCutover`

Applied to `tasks_db`. Additive only; no existing data affected.

- `tasks_Notes`: adds `AuthorName`, `IsEdited`, `IsDeleted` columns; extends `Note` to 5000 chars.
- New composite index `IX_Notes_TenantId_TaskId_IsDeleted` (supports soft-delete filter).
- New composite index `IX_Tasks_TenantId_SourceEntityType_SourceEntityId` (supports Liens search by case).

### 10.2 Liens service — `20260421000001_LiensTaskRuntimeRemoval`

Applied to `liens_db` **after** backfill is confirmed complete. Destructive — drops three tables:

```sql
DROP TABLE liens_TaskNotes;
DROP TABLE liens_TaskLienLinks;
DROP TABLE liens_Tasks;
```

`Down()` fully recreates all three tables with all original columns and indexes (rollback safe).

**Do not apply this migration until:**
1. New binaries are deployed and healthy.
2. Backfill endpoint has returned `failed = 0`.
3. A spot-check confirms task counts match between `liens_Tasks` and `tasks_Tasks WHERE SourceProductCode = 'SYNQ_LIENS'`.

---

## 11. Production Cutover Runbook

```
Step 1 — Deploy new binaries
  • Both Task service and Liens service must be deployed together.
  • Task service migration 20260421000004 runs on startup (EF auto-migrate or manual).
  • Liens service is now live as a proxy but liens_Tasks still exists — dual state.

Step 2 — Run backfill
  curl -X POST https://<liens-host>/api/liens/internal/task-backfill \
    -H "X-Internal-Service-Token: $FLOW_SERVICE_TOKEN_SECRET" \
    -H "Content-Type: application/json" \
    -d '{"actingAdminUserId": "<admin-uuid>", "batchSize": 100}'

Step 3 — Verify
  • Confirm response: failed = 0
  • Query: SELECT COUNT(*) FROM tasks_Tasks WHERE SourceProductCode = 'SYNQ_LIENS'
    should equal SELECT COUNT(*) FROM liens_Tasks
  • Run backfill a second time — created should be 0, alreadyExisted = full count.

Step 4 — Apply drop migration
  dotnet ef database update LiensTaskRuntimeRemoval \
    --project apps/services/liens/Liens.Infrastructure \
    --startup-project apps/services/liens/Liens.Api

Step 5 — Smoke test
  • Create a task via Liens API → verify it appears in Task service.
  • Update status → verify status reflects in Task service.
  • Add a note → verify note in Task service.
  • Trigger a Flow callback → verify task_WorkflowStepKey updated in Task service.
```

---

## 12. Rollback Plan

| Trigger                                 | Action                                                    |
|----------------------------------------|-----------------------------------------------------------|
| Backfill endpoint returns `failed > 0` | Re-run backfill; investigate per-task errors in Liens logs |
| Task service unreachable after deploy  | Revert Liens binaries to previous version; `liens_Tasks` still intact |
| Drop migration applied and data loss suspected | Run `Down()` migration to recreate tables; redeploy old binaries |
| Task service API contract breaks       | Pin `LiensTaskServiceClient` to prior API version; hotfix |

All rollback paths are non-destructive as long as the drop migration has not been applied.

---

## 13. Known Post-Cutover Work

### 13.1 LienTaskGenerationEngine duplicate checks

`LienTaskGenerationEngine` still queries `liens_Tasks` directly for duplicate prevention:

```csharp
var hasDup = await _taskRepo.HasOpenTaskForRuleAsync(...)     // queries liens_Tasks
var hasDup = await _taskRepo.HasOpenTaskForTemplateAsync(...) // queries liens_Tasks
```

These calls will fail with a missing-table error after the drop migration is applied. Before Step 4 of the runbook, these must be replaced with calls to the Task service search API:

```
GET /api/tasks?sourceProductCode=SYNQ_LIENS&sourceEntityId={caseId}&status=OPEN&...
```

This is scoped as a follow-up task (TASK-B04a or equivalent).

### 13.2 LinkedLiens field on TaskResponse

`MapToTaskResponse` in `LiensTaskServiceClient` returns `LinkedLiens = []` (empty). The correct approach is to call `GET /api/tasks/{id}/linked-entities` and filter by `EntityType = LIEN`. This incurs an extra round-trip per task and should be batched or cached when the case task board is rendered.

### 13.3 ILienTaskRepository / ILienTaskNoteRepository registration

Both repositories remain registered in DI (needed by `LienTaskBackfillService` and `LienTaskGenerationEngine`). After the drop migration and after the generation engine is updated (§13.1), both registrations can be removed along with their EF DbSets from `LiensDbContext`.

---

## 14. Files Changed

### New files

| File | Purpose |
|------|---------|
| `Liens.Infrastructure/TaskService/TaskServiceAuthDelegatingHandler.cs` | HS256 bearer token handler for outbound Task service calls |
| `Liens.Infrastructure/TaskService/LiensTaskServiceClient.cs` | Full HTTP client — CRUD, notes, backfill |
| `Liens.Application/Services/LienTaskBackfillService.cs` | Paginated idempotent backfill runner |
| `Liens.Api/Endpoints/LienTaskBackfillEndpoints.cs` | Admin HTTP endpoint for backfill |
| `Task.Infrastructure/Persistence/Migrations/20260421000004_LiensConsumerCutover.cs` | Task DB schema changes |
| `Liens.Infrastructure/Persistence/Migrations/20260421000001_LiensTaskRuntimeRemoval.cs` | Drop Liens task runtime tables (deferred) |

### Modified files

| File | Change |
|------|--------|
| `Liens.Application/Services/LienTaskService.cs` | Rewritten as proxy; removed all SQL, kept governance/audit/notifications |
| `Liens.Application/Services/LienTaskNoteService.cs` | Rewritten as proxy with audit events |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered auth handler, HTTP client, backfill service |
| `Liens.Api/appsettings.json` | Added `ExternalServices:Task:BaseUrl` |
| `Task.Domain/Entities/TaskNote.cs` | Added `AuthorName`, `IsEdited`, `IsDeleted` fields |
| `Task.Infrastructure/Persistence/Configurations/TaskNoteConfiguration.cs` | Column mappings for new fields; `Note` max 5000 |
| `Task.Application/Interfaces/ITaskService.cs` | Added `EditNoteAsync`, `DeleteNoteAsync` signatures |
| `Task.Application/Services/TaskService.cs` | Implemented `EditNoteAsync`, `DeleteNoteAsync` |
| `Task.Infrastructure/Persistence/Repositories/TaskRepository.cs` | Extended `SearchAsync` with 5 new filters |
| `Task.Api/Endpoints/TaskNoteEndpoints.cs` | Added `PUT` and `DELETE` routes |
| `Task.Domain/Enums/TaskStatus.cs` | Added `WAITING_BLOCKED` value |

---

## 15. Build Status

```
Task.Api    — Build succeeded  0 Error(s)
Liens.Api   — Build succeeded  0 Error(s)
```

One compile error was found and resolved during implementation:
`UpdateTaskRequest` in Liens does not have an `AssignedUserId` property (assignment is a separate operation via `AssignAsync`). The `LiensTaskServiceClient.UpdateTaskAsync` body was corrected to exclude that field.
