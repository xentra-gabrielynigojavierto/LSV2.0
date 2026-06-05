# TASK-FLOW-03 — Shadow Table Drop (`flow_workflow_tasks`)

**Date:** 2026-04-21
**Scope:** Remove the `flow_workflow_tasks` dual-write shadow from Flow. Task service is now the sole authority for task data (TASK-FLOW-01 created it; TASK-FLOW-02 migrated all reads). This task stops the dual-write, retires the SLA evaluator, removes the EF entity, and drops the table.

---

## 1. Executive Summary

After TASK-FLOW-02, every **read** consumer has been migrated to the Task service. The shadow table is now write-only from Flow's perspective — it receives mirrored writes but no code reads from it at runtime (except the SLA evaluator, which reads to compute what to push to Task service). TASK-FLOW-03 eliminates those remaining writes and the evaluator, leaving `flow_workflow_tasks` unused and safe to drop.

**Analytics carve-out:** `FlowAnalyticsService` has 15+ complex queries against `flow_workflow_tasks`. Migrating them requires new Task service analytics endpoints that do not yet exist. These are deferred to **TASK-FLOW-04** with a bridge strategy documented in §7. All other consumers are fully migrated in this task.

---

## 2. Remaining Shadow-Table Consumers After TASK-FLOW-02

### Write paths (shadow receives mirrored writes)

| File | Method | Shadow operation | TASK-FLOW-03 action |
|---|---|---|---|
| `WorkflowTaskFromWorkflowFactory.cs` | `EnsureTaskForStepAsync` | `_db.WorkflowTasks.Add(task)` | **Remove** shadow write; keep Task service call |
| `WorkflowTaskLifecycleService.cs` | `ApplyCasAsync` | `ExecuteUpdateAsync` (Status, timestamps × 3) | **Remove** shadow CAS |
| `WorkflowTaskLifecycleService.cs` | `ReadCurrentStatusAsync` | `_db.WorkflowTasks.Select(t => t.Status)` | **Remove** — replace with `GetTaskByIdAsync` |
| `WorkflowTaskAssignmentService.cs` | `ReadSnapshotAsync` | `_db.WorkflowTasks.Select(...)` | **Remove** — replace with `GetTaskByIdAsync` |
| `WorkflowTaskAssignmentService.cs` | `ApplyTransitionAsync` | `ExecuteUpdateAsync` (assignment fields) | **Remove** shadow CAS |

### Read paths (shadow queried for business logic)

| File | Method | Shadow read | TASK-FLOW-03 action |
|---|---|---|---|
| `WorkflowTaskCompletionService.cs` | `CompleteAndProgressAsync` | `_db.WorkflowTasks.Select(Status, StepKey, WorkflowInstanceId)` | **Replace** with `GetTaskByIdAsync` |
| `TaskRecommendationService.cs` | `GetRecommendationAsync` | `_db.WorkflowTasks.Select(AssignmentMode, AssignedRole, AssignedOrgId, SlaStatus, Priority)` | **Replace** with `GetTaskByIdAsync` |
| `WorkloadService.cs` | `GetAssignedTaskCountAsync`, `GetActiveTaskIdsAsync` | 3 queries on Status, AssignedUserId | **Replace** with `ListTasksAsync` |
| `WorkflowTaskSlaEvaluator.cs` | `TickAsync` | Batch scan of `DueAt`, `SlaStatus` across all tenants | **Retire** entire evaluator |
| `FlowAnalyticsService.cs` | 10+ analytics methods | 15+ complex aggregate queries | **Deferred to TASK-FLOW-04** (bridge strategy §7) |

---

## 3. Step-by-Step Migration Plan

### Step 1 — Stop shadow write in `WorkflowTaskFromWorkflowFactory`

**File:** `Flow.Application/Services/WorkflowTaskFromWorkflowFactory.cs`

**Change:** Remove `_db.WorkflowTasks.Add(task)` and the subsequent `await db.SaveChangesAsync()`. Remove the local `WorkflowTask` entity instantiation (lines that build the shadow entity). Keep the `CreateWorkflowTaskAsync` Task service call and all assignment/SLA-clock logic — those outputs now feed Task service only, not the shadow.

The factory currently builds a `WorkflowTask` entity, calls Task service, then adds the entity to EF context. After the change:
- Build the `CreateWorkflowTaskAsync` body inline from the same inputs (title, priority, dueAt, assignment, stepKey, workflowInstanceId, externalId).
- Remove all `var task = new WorkflowTask(...)` and `_db.WorkflowTasks.Add(task)`.
- `task.Id` (used as `externalId`) should be generated as `Guid.NewGuid()` before the call and returned to the engine if needed.
- The factory still returns `Guid` (the task ID) so the engine can wire `InitialTaskId` on the workflow instance.

**Also remove:**
- `CheckForDuplicateAsync` — the deduplication query against `_db.WorkflowTasks.Local` + DB.
- `EnsureValid` calls (entity-level validation; request-level validation stays at Task service).

**Residual dependencies removed:** `IFlowDbContext` can be dropped from the factory's constructor. The factory reduces to: resolve SLA clock, resolve assignment, call Task service.

---

### Step 2 — Remove shadow CAS from `WorkflowTaskLifecycleService`

**File:** `Flow.Application/Services/WorkflowTaskLifecycleService.cs`

**Change:** `ApplyCasAsync` currently does `ExecuteUpdateAsync` against `flow_workflow_tasks` as the final step of every transition. After this step, Task service is the only state store — the CAS is not needed at all.

**`ReadCurrentStatusAsync`:** Currently reads `t.Status` from `flow_workflow_tasks` to validate allowed transitions before delegating to Task service. After the change, replace this with `GetTaskByIdAsync`. The pre-check is still valuable (avoids a round-trip to Task service if the transition is impossible) so keep the pattern but source data from Task service.

Implementation shape:
```csharp
private async Task<string> ReadCurrentStatusAsync(Guid taskId, CancellationToken ct)
{
    var dto = await _taskClient.GetTaskByIdAsync(taskId, ct);
    if (dto is null) throw new NotFoundException(nameof(WorkflowTask), taskId);
    return MapFlowStatus(dto.Status);  // "OPEN" → "Open" etc.
}
```

**`ApplyCasAsync`:** Becomes a no-op or is deleted entirely. The CAS role is now fulfilled by the Task service's own optimistic concurrency (if any) or by the pre-check in `ReadCurrentStatusAsync` being sequenced before the Task service call. The shadow write is simply deleted.

**`_db` dependency:** After removing `ReadCurrentStatusAsync` and `ApplyCasAsync`, `IFlowDbContext` is no longer used. Remove from constructor and registration if no other methods remain.

---

### Step 3 — Remove snapshot read + shadow CAS from `WorkflowTaskAssignmentService`

**File:** `Flow.Application/Services/WorkflowTaskAssignmentService.cs`

**`ReadSnapshotAsync`:** Reads `{ Id, WorkflowInstanceId, Status }` from shadow. Replace with `GetTaskByIdAsync`. Map status and validate as before.

**`ApplyTransitionAsync`:** Currently:
1. Reads snapshot from shadow (→ now GetTaskByIdAsync, already done in Step 3's ReadSnapshotAsync)
2. Validates `prevStatus` (→ keep, sourced from Task service DTO)
3. Calls `SetQueueAssignmentAsync` (already Task service)
4. Calls `ExecuteUpdateAsync` on `_db.WorkflowTasks` (→ **remove**)

After the change, `ApplyTransitionAsync` is a pure read-then-Task-service-delegate:
```
ReadSnapshot → validate → SetQueueAssignmentAsync → done
```

No DB write, no CAS, no `_db` reference.

**`_db` dependency:** Removable from constructor after these changes.

---

### Step 4 — Replace shadow read in `WorkflowTaskCompletionService`

**File:** `Flow.Application/Services/WorkflowTaskCompletionService.cs`

**What it reads:** `{ Status, StepKey, WorkflowInstanceId }` — all present in `TaskServiceTaskDto` (as `Status`, `WorkflowStepKey`, `WorkflowInstanceId`).

**Change:**
```csharp
var dto = await _taskClient.GetTaskByIdAsync(taskId, ct);
if (dto is null) throw new NotFoundException(nameof(WorkflowTask), taskId);
if (!string.Equals(dto.Status, "IN_PROGRESS", ...))
    throw new InvalidStateTransitionException(...);
if (string.IsNullOrWhiteSpace(dto.WorkflowStepKey))
    throw new ValidationException(...);
// Continue with engine advance using dto.WorkflowInstanceId, dto.WorkflowStepKey
```

The completion service then calls `CompleteTaskAsync` on the lifecycle service (which delegates to Task service) and `AdvanceAsync` on the workflow engine. Both are unchanged.

**Inject:** `IFlowTaskServiceClient` added to constructor.
**Remove:** `IFlowDbContext _db` from constructor (if no other usage remains).

---

### Step 5 — Replace shadow read in `TaskRecommendationService`

**File:** `Flow.Application/Services/TaskRecommendationService.cs`

**What it reads:** `{ AssignmentMode, AssignedRole, AssignedOrgId, SlaStatus, Priority }` — all present in `TaskServiceTaskDto`.

**Change:**
```csharp
var dto = await _taskClient.GetTaskByIdAsync(taskId, ct);
if (dto is null) throw new NotFoundException(nameof(WorkflowTask), taskId);
var ctx = new TaskRecommendationContext(
    dto.TaskId,
    dto.AssignmentMode ?? WorkflowTaskAssignmentMode.Unassigned,
    dto.AssignedRole,
    dto.AssignedOrgId,
    dto.SlaStatus,
    MapPriority(dto.Priority));
```

**Inject:** `IFlowTaskServiceClient`.
**Remove:** `IFlowDbContext _db`.

---

### Step 6 — Replace shadow queries in `WorkloadService`

**File:** `Flow.Application/Services/WorkloadService.cs`

The workload service counts active tasks per user. It makes 3 queries:
1. Count of `Status IN (Open, InProgress)` for `AssignedUserId = userId`
2. List of task IDs `Status IN (Open, InProgress)` for `AssignedUserId = userId`
3. Count of `AssignmentMode = RoleQueue` tasks for a role

**Change:** Replace with `ListTasksAsync` calls:
```csharp
// Count
var result = await _taskClient.ListTasksAsync(
    assignedUserId: userId,
    status: "OPEN",    // call twice: OPEN + IN_PROGRESS, or accept combined count
    pageSize: 1,       // use Total for count, not Items
    ct: ct);
var count = result.Total;

// IDs (up to configured cap)
var page = await _taskClient.ListTasksAsync(
    assignedUserId: userId,
    pageSize: 500,
    ct: ct);
var ids = page.Items.Select(t => t.TaskId).ToList();
```

The exact query shape depends on the `WorkloadService`'s method signatures; adapt to maintain the same return type contract for callers.

---

### Step 7 — Retire `WorkflowTaskSlaEvaluator`

**File:** `Flow.Infrastructure/Outbox/WorkflowTaskSlaEvaluator.cs`

**Decision:** The evaluator exists to compute SLA state transitions across all tenants and push to Task service. In TASK-FLOW-03, the Task service takes over SLA computation natively.

**Task service changes needed (minor):**
- The Task service already stores `SlaStatus`, `SlaBreachedAt`, `DueAt`, `LastSlaEvaluatedAt` (added in TASK-FLOW-02).
- Add a background `IHostedService` in Task service that evaluates SLA state (mirrors the same `WorkflowTaskSlaPolicy.ComputeStatus` logic). This is a Task service–internal concern and does not affect Flow.

**Flow changes:**
1. Remove `services.AddHostedService<WorkflowTaskSlaEvaluator>()` from `PlatformAdapterRegistration.cs`.
2. Delete `WorkflowTaskSlaEvaluator.cs`.
3. Remove `WorkflowTaskSlaOptions` if used only by the evaluator (check `WorkflowTaskFromWorkflowFactory` still uses `IWorkflowTaskSlaClock` — keep that interface and its impl, which reads `WorkflowTaskSlaOptions.Durations` for computing `DueAt`).

---

### Step 8 — Remove `WorkflowTask` entity from `IFlowDbContext` / `FlowDbContext`

**Files:**
- `Flow.Application/Interfaces/IFlowDbContext.cs` — remove `DbSet<WorkflowTask> WorkflowTasks`
- `Flow.Infrastructure/Persistence/FlowDbContext.cs` — remove `WorkflowTasks` property, entity configuration block, EF query filter for `WorkflowTask`

**EF config to remove (in `FlowDbContext.OnModelCreating`):**
```csharp
// Remove the WorkflowTask entity config block (~40 lines covering:
//   - table name "flow_workflow_tasks"
//   - indexes: IX_workflow_tasks_tenant_status, IX_workflow_tasks_tenant_mode_role,
//              IX_workflow_tasks_tenant_mode_org, IX_workflow_tasks_sla_evaluator_hot_path
//   - foreign key to flow_workflow_instances
//   - global query filter: modelBuilder.Entity<WorkflowTask>().HasQueryFilter(...)
// )
```

---

### Step 9 — Create Drop Table Migration

**Migration name:** `DropWorkflowTasksShadow` (timestamp: `20260421_XXXXXXXX`)

**Migration content:**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Drop indexes first to satisfy FK constraints.
    migrationBuilder.DropIndex("IX_workflow_tasks_sla_evaluator_hot_path",   "flow_workflow_tasks");
    migrationBuilder.DropIndex("IX_workflow_tasks_tenant_mode_org",           "flow_workflow_tasks");
    migrationBuilder.DropIndex("IX_workflow_tasks_tenant_mode_role",          "flow_workflow_tasks");
    migrationBuilder.DropIndex("IX_workflow_tasks_tenant_status",             "flow_workflow_tasks");
    migrationBuilder.DropForeignKey("FK_flow_workflow_tasks_..._WorkflowInstanceId", "flow_workflow_tasks");
    migrationBuilder.DropTable("flow_workflow_tasks");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Re-create table + indexes if rollback is needed.
    // (Populate from AddWorkflowTaskE11_1 + AddTaskAssignmentModelE14_1
    //  + AddWorkflowTaskSlaE10_3 Up() methods.)
}
```

The `Down()` method is a safety net; rollback should be preferred via the TASK-FLOW-03 rollback plan (§9) rather than migration reversal.

---

### Step 10 — Delete or archive domain files

**Files to delete** (no remaining references after Steps 1–8):
- `Flow.Domain/Entities/WorkflowTask.cs` — shadow entity
- `Flow.Infrastructure/Persistence/Configurations/WorkflowTaskConfiguration.cs` (if extracted; otherwise cleaned inline)

**Files to keep** (still referenced by engine / assignment logic / Task service calls):
- `Flow.Domain/Common/WorkflowTaskStatus.cs` — string constants, still used as status values when calling Task service
- `Flow.Domain/Common/WorkflowTaskPriority.cs` — string constants, still used in factory SLA clock
- `Flow.Domain/Common/WorkflowTaskAssignmentMode.cs` — string constants, still used in assignment resolution

---

## 4. Analytics Bridge Strategy (TASK-FLOW-04 scope)

`FlowAnalyticsService` (`Flow.Application/Services/FlowAnalyticsService.cs`, 661 lines) has 15+ queries against `_db.WorkflowTasks` spread across four analytics methods:
- `GetSlaAnalyticsAsync` — aggregate SLA counts, breach windows, on-time completion rates
- `GetQueueAnalyticsAsync` — per-role/org queue depths, age distributions, per-user workloads
- `GetAssignmentAnalyticsAsync` — mode breakdowns, assignment rates, top assignees
- `GetDashboardSummaryAsync` — orchestrates all three above

**Why deferred:**
The queries use SQL `GROUP BY`, window functions, and `COUNT / SUM / AVG` aggregations that map poorly to paginated REST calls. Replacing them requires either:
1. New Task service analytics endpoints (`GET /api/tasks/analytics/sla`, `/queue`, `/assignment`) backed by indexed aggregate queries on the Task DB, or
2. A Task service internal bulk-export endpoint that Flow fetches and aggregates in-memory (viable for small tenants; risky at scale).

**Bridge for TASK-FLOW-03 unblocking:**
During TASK-FLOW-03, `FlowAnalyticsService` retains its `IFlowDbContext _db` dependency and its `_db.WorkflowTasks` queries. The shadow table can be retained **for analytics only** even after all other consumers are removed. A feature flag (`AnalyticsShadow:Enabled`) controls whether the evaluator (now in Task service) also mirrors data back to `flow_workflow_tasks` via the existing `flow-sla-update` endpoint — but this is an opt-in compatibility bridge, not required for TASK-FLOW-03 gate.

Alternatively, analytics can be marked as **best-effort** during the bridge period (return stale data from a snapshot materialized view) until TASK-FLOW-04 delivers native Task service analytics APIs.

---

## 5. Task Service Changes Required

| Change | Scope | Notes |
|---|---|---|
| Native SLA evaluator background service | New `IHostedService` in Task.Infrastructure | Mirrors `WorkflowTaskSlaPolicy.ComputeStatus` logic; operates on `PlatformTask.DueAt` |
| Analytics endpoints (`/analytics/sla`, `/queue`, `/assignment`) | New endpoints in Task.Api | Required for TASK-FLOW-04; not blocking TASK-FLOW-03 |
| `GET /api/tasks/{id}` already supports status + queue + SLA fields | Done in TASK-FLOW-02 | No change needed |

---

## 6. Files Changed in TASK-FLOW-03

### Flow service — stop dual-write

| File | Action | Detail |
|---|---|---|
| `Flow.Application/Services/WorkflowTaskFromWorkflowFactory.cs` | MODIFY | Remove `WorkflowTask` entity construction + `_db.WorkflowTasks.Add`; remove `IFlowDbContext` dependency |
| `Flow.Application/Services/WorkflowTaskLifecycleService.cs` | MODIFY | Remove `ApplyCasAsync` shadow CAS; replace `ReadCurrentStatusAsync` with `GetTaskByIdAsync`; remove `IFlowDbContext` |
| `Flow.Application/Services/WorkflowTaskAssignmentService.cs` | MODIFY | Replace `ReadSnapshotAsync` with `GetTaskByIdAsync`; remove `ExecuteUpdateAsync` shadow CAS; remove `IFlowDbContext` |
| `Flow.Application/Services/WorkflowTaskCompletionService.cs` | MODIFY | Replace `_db.WorkflowTasks.Select(...)` with `GetTaskByIdAsync`; inject `IFlowTaskServiceClient`; remove `IFlowDbContext` |
| `Flow.Application/Services/TaskRecommendationService.cs` | MODIFY | Replace `_db.WorkflowTasks.Select(...)` with `GetTaskByIdAsync`; inject `IFlowTaskServiceClient`; remove `IFlowDbContext` |
| `Flow.Application/Services/WorkloadService.cs` | MODIFY | Replace 3 shadow queries with `ListTasksAsync`; inject `IFlowTaskServiceClient`; remove `IFlowDbContext` |

### Flow service — retire SLA evaluator

| File | Action | Detail |
|---|---|---|
| `Flow.Infrastructure/Outbox/WorkflowTaskSlaEvaluator.cs` | DELETE | Background evaluator retired; Task service takes over SLA computation |
| `Flow.Infrastructure/Adapters/PlatformAdapterRegistration.cs` | MODIFY | Remove `AddHostedService<WorkflowTaskSlaEvaluator>()` |
| `Flow.Infrastructure/Outbox/WorkflowTaskSlaOptions.cs` | RETAIN | Still needed by `IWorkflowTaskSlaClock` / `WorkflowTaskSlaClock` for `DueAt` computation on create |

### Flow service — remove entity + table

| File | Action | Detail |
|---|---|---|
| `Flow.Application/Interfaces/IFlowDbContext.cs` | MODIFY | Remove `DbSet<WorkflowTask> WorkflowTasks` property |
| `Flow.Infrastructure/Persistence/FlowDbContext.cs` | MODIFY | Remove `WorkflowTasks` DbSet, entity config block, query filter |
| `Flow.Domain/Entities/WorkflowTask.cs` | DELETE | Shadow entity no longer needed |
| `Flow.Infrastructure/Persistence/Migrations/YYYYMMDD_DropWorkflowTasksShadow.cs` | CREATE | Drops `flow_workflow_tasks` table + indexes |
| `Flow.Infrastructure/Persistence/Migrations/FlowDbContextModelSnapshot.cs` | MODIFY | Remove WorkflowTask model snapshot |

### Flow service — keep unchanged

| File | Reason |
|---|---|
| `Flow.Domain/Common/WorkflowTaskStatus.cs` | String constants still used by Task service calls and assignment logic |
| `Flow.Domain/Common/WorkflowTaskPriority.cs` | Still used by SLA clock |
| `Flow.Domain/Common/WorkflowTaskAssignmentMode.cs` | Still used by assignment resolver |
| `Flow.Application/Services/FlowAnalyticsService.cs` | Analytics bridge — deferred to TASK-FLOW-04 |
| `Flow.Infrastructure/Outbox/WorkflowTaskSlaOptions.cs` | Still needed by `WorkflowTaskSlaClock` |

### Task service

| File | Action | Detail |
|---|---|---|
| `Task.Infrastructure/Outbox/PlatformTaskSlaEvaluator.cs` | CREATE | New background service that computes SLA state natively in Task service |
| `Task.Infrastructure/Adapters/TaskServiceRegistration.cs` | MODIFY | Register `AddHostedService<PlatformTaskSlaEvaluator>()` |

---

## 7. Concurrency and Ordering Notes

**Write stop ordering matters.** The sequence must be:
1. Deploy TASK-FLOW-03 code with dual-write stopped (Steps 1–3 above).
2. Run the `DropWorkflowTasksShadow` migration.

**No data migration required.** All task state is live in the Task service DB. The shadow table can be dropped without backfill.

**In-flight requests at deployment boundary.** A short deployment window (blue/green or rolling) means some requests may hit the old pod (still writing shadow) while new pods skip the shadow write. Since the shadow is being dropped anyway, shadow inconsistency during the rollover is inconsequential. The Task service is the authority throughout.

**SLA evaluator handoff.** The Flow evaluator (TASK-FLOW-02) already pushes SLA state to Task service on every tick. Before retiring it, confirm the Task service's native evaluator (`PlatformTaskSlaEvaluator`) is deployed and ticking. The TASK-FLOW-02 push continues until the Flow evaluator is removed — there is no gap.

---

## 8. Dependency Graph

```
TASK-FLOW-01  ← dual-write established, Task service = write authority
     │
TASK-FLOW-02  ← all reads migrated; SLA push started; ExternalId alignment fixed
     │
TASK-FLOW-03  ← [THIS TASK]
     │    ├── Stop dual-write (Steps 1–3)
     │    ├── Stop shadow reads (Steps 4–6)
     │    ├── Retire evaluator (Step 7)
     │    ├── Remove entity + table (Steps 8–10)
     │    └── Task service: native SLA evaluator
     │
TASK-FLOW-04  ← Analytics migration (FlowAnalyticsService → Task service analytics APIs)
```

---

## 9. Validation Checklist

| Check | Method |
|---|---|
| Flow.Api builds with 0 errors after all shadow removes | `dotnet build` |
| Task.Api builds with 0 errors after native evaluator add | `dotnet build` |
| `flow_workflow_tasks` table is not referenced by any EF model | EF `DbContext.Model` inspection or build-time error |
| `IFlowDbContext.WorkflowTasks` removed — callers fail at compile time | Build error surfaced by compiler |
| `WorkflowTaskSlaEvaluator` not registered — no background scan | Log inspection at startup |
| Task service SLA evaluator ticking — `PlatformTaskSlaEvaluator` logs visible | Startup + tick logs |
| Task detail endpoint (`GET /api/v1/workflow-tasks/{id}`) returns correct data after drop | Smoke test with a live token |
| Analytics endpoints degrade gracefully if shadow read fails | Review `FlowAnalyticsService` error handling |

---

## 10. Rollback Plan

**Rollback scope:** Code-only rollback via feature flag or redeploy; migration reversal is the last resort.

1. **Redeploy TASK-FLOW-02 code** (last known good) — re-enables dual-write. Shadow data will be stale if the table was not yet dropped.
2. **If table already dropped:** Run `Down()` of `DropWorkflowTasksShadow` migration to re-create the empty table. Then run a backfill from Task service (export all `WORKFLOW`-scoped tasks via `GET /api/tasks?scope=WORKFLOW&pageSize=500` and insert into shadow). A one-time backfill script is recommended as part of the rollback playbook.
3. **Feature-flag strategy (preferred):** Gate the dual-write removal behind `FlowTasks:ShadowWriteEnabled = false`. Set to `true` to re-enable shadow writes without a redeploy. This allows safe, incremental rollback without migration reversal.

---

## 11. Known Risks

| Risk | Probability | Mitigation |
|---|---|---|
| Task service `GetTaskByIdAsync` latency increases P99 for `CompleteAndProgressAsync` | Low | Pre-check already does one Task service HTTP call; latency budget unchanged |
| Analytics dark period during TASK-FLOW-04 gap | Medium | Bridge strategy (§4); mark analytics as `best-effort` in API response |
| `PlatformTaskSlaEvaluator` in Task service missing ticks on startup | Low | Evaluator is idempotent; first tick catches all stale rows |
| Concurrent deployment window: new Flow pod + old Task pod (TASK-FLOW-02 → 03 boundary) | Very Low | Task service HTTP is backward-compatible; new Flow calls still work against old Task pods |
| `WorkflowTaskSlaOptions.Durations` still needed by `WorkflowTaskSlaClock` after evaluator removal | — | `WorkflowTaskSlaOptions` and `IWorkflowTaskSlaClock` are explicitly retained (§6) |
