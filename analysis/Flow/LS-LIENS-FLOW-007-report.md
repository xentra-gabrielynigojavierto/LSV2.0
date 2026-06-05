# LS-LIENS-FLOW-007 — Task ↔ Flow Instance Linking Report

**Date:** 2026-04-20  
**Feature:** First-class linkage between Synq Liens tasks and Flow workflow instances  
**Status:** Complete

---

## 1. Executive Summary

### What Was Implemented

- Extended `LienTask` domain entity with `WorkflowInstanceId` (Guid?) and `WorkflowStepKey` (string?) Flow linkage fields
- Added `SetWorkflowLink` domain method for safe, idempotent Flow linkage assignment
- EF configuration updated with new columns and a `(TenantId, WorkflowInstanceId)` index
- Migration `20260420000002_AddTaskFlowLinkage` adds the two columns and index
- New `IFlowInstanceResolver` / `FlowInstanceResolver` service resolves the best active Flow workflow instance for a given case using `IFlowClient.ListBySourceEntityAsync` + `GetWorkflowInstanceAsync`
- `LienTaskService.CreateAsync` now attempts Flow instance resolution after entity create; sets `WorkflowInstanceId` + `WorkflowStepKey` on success; emits `liens.task.workflow_linked` audit event; task creation never fails due to Flow lookup failure
- `TaskResponse` DTO extended with `WorkflowInstanceId` and `WorkflowStepKey` fields
- `FlowInstanceResolver` registered in `Program.cs`
- Frontend `TaskDto` extended with `workflowInstanceId?` and `workflowStepKey?`
- Task detail drawer displays a "Linked Workflow" section when `workflowInstanceId` is present

### What Was Partially Implemented

- `WorkflowStepKey` requires a second `GetWorkflowInstanceAsync` call after resolving the instance ID. This is implemented with its own try/catch so step key resolution failure does not prevent instance ID from being stored.

### What Was Deferred

- Task ↔ Flow stage synchronization (LS-LIENS-FLOW-008) — no stage sync attempted here
- Automatic re-linking on task update when `CaseId` changes — linkage is established at create time; subsequent updates do not re-resolve
- Multiple active instance UI disambiguation — current selection rule is deterministic (most recently updated active instance for `synqlien` product)

---

## 2. Codebase Assessment

### Available Flow APIs

| Method | Usage |
|--------|-------|
| `IFlowClient.ListBySourceEntityAsync(productSlug, sourceEntityType, sourceEntityId)` | **Primary lookup** — lists all Flow product-workflow records for a case entity. Returns `FlowProductWorkflowResponse[]` which includes `WorkflowInstanceId` and `Status` |
| `IFlowClient.GetWorkflowInstanceAsync(workflowInstanceId)` | **Secondary call** — fetches `FlowWorkflowInstanceResponse` including `CurrentStepKey` for the active step |
| `IFlowClient.StartWorkflowAsync` | Starts new Flow instance (not used here) |
| `IFlowClient.AdvanceWorkflowAsync` / `CompleteWorkflowAsync` | Execution surface (not used here) |

`ListBySourceEntityAsync` is already used in `WorkflowEndpoints.cs` with `productSlug="synqlien"`, `sourceEntityType="lien_case"`. This feature uses the same constants.

### Task Create / Runtime Findings

- `LienTaskService.CreateAsync` is the single task creation entry point
- `LienTaskGenerationEngine` calls `_taskService.CreateAsync` — automation tasks use the same path and gain Flow linkage automatically
- The create path: basic validation → governance validation → entity create → persist → lien links → audit → notifications → response
- Flow linkage slot inserted between: persist + lien links and audit

### Workflow Instance Lookup Findings

- `FlowProductWorkflowResponse.WorkflowInstanceId` is `Guid?` — the workflow instance ID
- `FlowProductWorkflowResponse.Status` is `string` — `"Active"` for active instances
- `FlowProductWorkflowResponse.UpdatedAt` is `DateTime?` — used for deterministic selection
- `FlowWorkflowInstanceResponse.CurrentStepKey` is `string?` — the current step key string

### Selection Rule Rationale

When `ListBySourceEntityAsync` returns multiple results:
1. Filter to instances with `Status == "Active"` and `WorkflowInstanceId.HasValue`
2. Of active instances, prefer those with the `synqlien` product key (already filtered by product slug at the API level)
3. Among active instances, select the **most recently updated** (by `UpdatedAt`, falling back to `CreatedAt`) — deterministic, no randomness

When zero active instances remain after filtering → no linkage (task created with null fields).

---

## 3. Files Changed

### Backend

| File | Change |
|------|--------|
| `Liens.Domain/Entities/LienTask.cs` | Added `WorkflowInstanceId`, `WorkflowStepKey` fields + `SetWorkflowLink` method |
| `Liens.Infrastructure/Persistence/Configurations/LienTaskConfiguration.cs` | Added new column mappings + `(TenantId, WorkflowInstanceId)` index |
| `Liens.Infrastructure/Persistence/Migrations/20260420000002_AddTaskFlowLinkage.cs` | New migration adding two columns and index |
| `Liens.Application/Interfaces/IFlowInstanceResolver.cs` | New interface for Flow instance resolution |
| `Liens.Application/Services/FlowInstanceResolver.cs` | New service: resolves active Flow instance for case, wraps IFlowClient with soft fallback |
| `Liens.Application/Services/LienTaskService.cs` | Injected `IFlowInstanceResolver`; added Flow linkage in `CreateAsync`; added `WorkflowInstanceId`/`WorkflowStepKey` to `MapToResponse` |
| `Liens.Application/DTOs/TaskResponse.cs` | Added `WorkflowInstanceId` and `WorkflowStepKey` fields |
| `Liens.Api/Program.cs` | Registered `FlowInstanceResolver` |

### Frontend

| File | Change |
|------|--------|
| `apps/web/src/lib/liens/lien-tasks.types.ts` | Added `workflowInstanceId?: string` and `workflowStepKey?: string` to `TaskDto` |
| `apps/web/src/components/lien/task-detail-drawer.tsx` | Added "Linked Workflow" section in `ManualTaskDetails` (shown when `workflowInstanceId` is set) |

### Database

See section 4.

---

## 4. Database / Schema Changes

**Migration:** `20260420000002_AddTaskFlowLinkage`

Applied to table: `liens_Tasks`

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| `WorkflowInstanceId` | `uuid` | Yes | FK-free soft link to Flow instance |
| `WorkflowStepKey` | `varchar(200)` | Yes | Step key at time of linking |

**Index added:**
```sql
CREATE INDEX IF NOT EXISTS "IX_Tasks_TenantId_WorkflowInstanceId"
ON "liens_Tasks" ("TenantId", "WorkflowInstanceId");
```

No existing columns modified. Fully additive migration.

---

## 5. API Changes

### Task Response DTO (non-breaking additions)

`TaskResponse` now includes:

```json
{
  "workflowInstanceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "workflowStepKey": "document_review"
}
```

Both fields are nullable. Existing consumers that do not read these fields are unaffected.

### No New Request Fields

Task creation request DTOs are unchanged. Flow linkage is resolved by backend runtime only.

---

## 6. UI Changes

### Task Detail Drawer — "Linked Workflow" section

When a task has a `workflowInstanceId` set, a new read-only section appears in both the Notes tab area (Details panel) and the `ManualTaskDetails` sub-component:

```
[ Flow icon ] Linked Workflow Instance
  Instance ID: <uuid in monospace>
  [ if workflowStepKey ] Flow Step: <key value>
```

- Displayed as a highlighted info box to be distinct from audit-style rows
- Read-only — no edit controls
- Not shown when `workflowInstanceId` is absent (zero noise for unlinked tasks)

### No Create Form Changes

Task creation form is unchanged. Flow linkage is transparent to the user.

---

## 7. Flow Integration Behavior

### What is Flow-Owned

- Workflow instance execution, advancement, completion
- `WorkflowDefinition` catalog
- `CurrentStepKey` management as workflow steps advance

### What is Liens-Owned

- `LienTask` entity including new `WorkflowInstanceId` / `WorkflowStepKey` fields
- Resolution logic (`FlowInstanceResolver`) — reads Flow state, does not write to it
- Task CRUD operations
- Linkage establishment at create time

### Linkage Logic

```
CreateAsync called
  → entity created and persisted
  → if entity.CaseId.HasValue:
      → FlowInstanceResolver.ResolveAsync(caseId)
          → IFlowClient.ListBySourceEntityAsync("synqlien", "lien_case", caseId)
              [on exception → log warning → return null]
          → filter to Active instances with WorkflowInstanceId present
          → select most recently updated
          → IFlowClient.GetWorkflowInstanceAsync(instanceId)
              [on exception → log warning → use null stepKey]
          → return (instanceId, currentStepKey)
      → if resolved:
          → entity.SetWorkflowLink(instanceId, stepKey)
          → _taskRepo.UpdateAsync(entity)
          → audit: liens.task.workflow_linked
  → return MapToResponse(entity, links)
```

### Fallback Behavior

| Scenario | Result |
|----------|--------|
| No `CaseId` on task | No Flow lookup attempted; `WorkflowInstanceId = null` |
| Flow service unavailable | Exception caught; warning logged; task saved without linkage |
| Zero active instances for case | `WorkflowInstanceId = null` — no linkage |
| One active instance | Linked |
| Multiple active instances | Most recently updated selected |
| `GetWorkflowInstanceAsync` fails | `WorkflowInstanceId` set, `WorkflowStepKey = null` |

---

## 8. Runtime Linking Behavior

### When Linking Happens

Linkage is established at task **creation time** only. Task updates do not re-resolve Flow linkage. Rationale: re-resolution on every update would add latency and complexity; and the spec does not require it. The linkage represents the Flow state at the moment the task was created.

### Instance Selection

1. `ListBySourceEntityAsync("synqlien", "lien_case", caseId.ToString())`
2. Filter: `Status == "Active"` AND `WorkflowInstanceId.HasValue`
3. Order by `UpdatedAt DESC` (nulls last by `CreatedAt DESC`)
4. Take first — deterministic

### Automation Tasks

`LienTaskGenerationEngine` calls `_taskService.CreateAsync`, so auto-generated tasks gain the same Flow linkage behavior automatically. No duplication of logic.

---

## 9. Permissions / Security

- No new permissions added
- `FlowInstanceResolver` uses `IFlowClient` which is already registered with M2M token minting for `"synqlien"` — no additional auth configuration needed
- Flow lookup is scoped to the product slug `"synqlien"` — cross-product workflow instances are never returned
- Tenant isolation: `caseId` comes from the validated task request; task's `TenantId` gates all task operations
- `FlowInstanceResolver` does not expose any new API surface — it is an internal service only

---

## 10. Audit Integration

### New Event: `liens.task.workflow_linked`

Emitted when a task is successfully linked to a Flow workflow instance at creation time.

| Field | Value |
|-------|-------|
| `eventType` | `liens.task.workflow_linked` |
| `action` | `update` |
| `description` | `Task '{title}' linked to Flow instance {instanceId} (step: {stepKey ?? 'N/A'})` |
| `entityType` | `LienTask` |
| `entityId` | task ID |

**Non-noisy logging policy:**
- Flow lookup failure → `_logger.LogWarning` only; no audit event emitted (not a business event)
- Step key fetch failure → `_logger.LogWarning` only; instance ID still linked + audit emitted with step key "N/A"
- Zero active instances → no audit event, task created normally

---

## 11. Validation Results

### Backend Build

- `Liens.Api` builds with 0 errors after all changes

### Frontend Typecheck

- `pnpm type-check` passes with 0 errors after all changes

### Migration

- `20260420000002_AddTaskFlowLinkage` applied on startup via `MigrateAsync()` in `Program.cs`

### Runtime Validation Checklist

| Scenario | Expected |
|----------|---------|
| Task created with caseId, active Flow instance exists | `workflowInstanceId` set in response |
| Task created without caseId | `workflowInstanceId` null, task succeeds |
| Task created with caseId, no Flow instance | `workflowInstanceId` null, task succeeds |
| Task created with caseId, Flow unavailable | Warning logged, `workflowInstanceId` null, task succeeds |
| Auto-generated task via engine | Same Flow resolution behavior as manual task |
| Task detail drawer for linked task | "Linked Workflow Instance" section visible |
| Task detail drawer for unlinked task | No Flow section shown |

---

## 12. Known Gaps / Risks

1. **Two-call latency at create time.** `ListBySourceEntityAsync` + `GetWorkflowInstanceAsync` = 2 async calls. Both are in the hot path of task creation. If Flow is slow (>100ms each), this adds noticeable latency. Mitigation: both calls are wrapped in try/catch with timeout; task creation always succeeds. Future mitigation: cache active instance per case for a short TTL.

2. **Linkage is point-in-time, not live.** `WorkflowStepKey` reflects the step at creation time. As the workflow advances, this field becomes stale. LS-LIENS-FLOW-008 will address step synchronization.

3. **Multiple active instances per case.** The selection rule (most recently updated active instance) is deterministic but may not always select the "right" instance in complex scenarios. Documented and deterministic is preferred over complex disambiguation logic.

4. **No re-linking on `CaseId` change.** If a task's `CaseId` is changed via `UpdateAsync`, the `WorkflowInstanceId` is not re-resolved. A future update could add this, or leave it as create-time only by policy.

5. **`WorkflowStepKey` max length.** Set to 200 chars. If Flow step keys are ever longer, this will truncate silently. Flow's current step key format (slug-style strings) fits well within 200 chars.

6. **`FlowClientUnavailableException` handling.** The `FlowInstanceResolver` catches `Exception` broadly to protect task creation. A more targeted catch of `FlowClientUnavailableException` and `HttpRequestException` with logging is implemented but broad safety net is also present.

---

## 13. Run Instructions

### Migration

The migration `20260420000002_AddTaskFlowLinkage` is applied automatically on startup via `MigrateAsync()`. No manual SQL needed.

### Build

```bash
cd apps/services/liens
dotnet build Liens.Api/Liens.Api.csproj --no-restore
```

Expected: 0 errors.

### Typecheck

```bash
cd apps/web
pnpm type-check
```

Expected: 0 errors.

### Start Application

```bash
bash scripts/run-dev.sh
```

### Manual Testing

1. Create a task linked to a case that has an active Flow workflow instance → verify `workflowInstanceId` in response
2. Create a task without a case link → verify `workflowInstanceId` is null in response
3. Open task detail drawer for a Flow-linked task → verify "Linked Workflow Instance" section is shown
4. Create a task linked to a case with no active Flow instance → verify task succeeds with null linkage
5. Auto-generated task via event trigger → verify same linkage behavior
