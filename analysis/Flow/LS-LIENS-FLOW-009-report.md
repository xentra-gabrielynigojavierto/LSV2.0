# LS-LIENS-FLOW-009 — Flow Event Consumption (Real-Time Task Sync) Report

**Date:** 2026-04-20  
**Feature:** Event-driven synchronization of Flow workflow step changes into Synq Liens tasks  
**Status:** Complete

---

## 1. Executive Summary

### What Was Implemented

- **`TaskFlowSyncService`** — centralized, reusable synchronization logic that keeps `LienTask.WorkflowStepKey` aligned with a Flow workflow instance's current step. Idempotent: no-ops when already aligned.
- **`FlowStepChangedEvent` DTO** — standardized internal event contract for Flow step-change notifications
- **`IFlowEventHandler` / `FlowEventHandler`** — dedicated event handler service: validates event, finds all tasks linked to the workflow instance, delegates sync to `TaskFlowSyncService`, emits audit events only on actual changes
- **`GetByWorkflowInstanceIdAsync`** — new repository method on `ILienTaskRepository` / `LienTaskRepository` for batch lookup of all tasks linked to a Flow instance
- **`LienTask.SyncWorkflowStep()`** — new domain method for safe, idempotent step-key update
- **`POST /api/liens/internal/flow-events`** — internal-only ingestion endpoint protected by `X-Internal-Service-Token` header using `FLOW_SERVICE_TOKEN_SECRET`; dispatches to `FlowEventHandler`
- **Audit event `liens.task.flow_step_synced`** — emitted per task when step key actually changes; not emitted for no-ops or unmapped events
- **DI registrations** for all new services

### What Was Partially Implemented

- **Stage mapping (FLOW-008 prerequisite):** `LienWorkflowStage` does not have a `FlowStepKey` field. As a result, `WorkflowStepKey` on `LienTask` is updated by FLOW-009 events, but `WorkflowStageId` is not re-mapped. Stage ID mapping requires FLOW-008 schema additions (`FlowStepKey` column on `LienWorkflowStage` + mapping table/config). Once FLOW-008 is implemented, `TaskFlowSyncService` is the correct hook — no new service needed.

### What Was Deferred

- **`WorkflowStageId` re-assignment on step change**: requires FLOW-008 stage mapping table
- **Bidirectional orchestration** (task → Flow advancement): future work, not in scope
- **Real-time push to frontend** (WebSockets/SSE): not required; backend sync is sufficient; frontend picks up changes on next fetch

---

## 2. Codebase Assessment

### Flow Integration Points Found

| Component | Details |
|-----------|---------|
| `IFlowClient.ListBySourceEntityAsync` | Used by `FlowInstanceResolver` (FLOW-007) and `WorkflowEndpoints` to list Flow instances for a case |
| `IFlowClient.GetWorkflowInstanceAsync` | Used by `FlowInstanceResolver` to get `CurrentStepKey` |
| `LienTask.WorkflowInstanceId` | Guid? — soft link to Flow instance (added FLOW-007) |
| `LienTask.WorkflowStepKey` | string? — step key at link time (added FLOW-007) |
| `FlowInstanceResolver` | Resolves best active Flow instance for a case at task creation |

### FLOW-008 State

FLOW-008 was not implemented in the codebase at the time FLOW-009 was implemented. Specifically:
- `LienWorkflowStage` has no `FlowStepKey` field
- No `TaskFlowSyncService` existed
- No stage-mapping logic existed

**Decision:** FLOW-009 creates `TaskFlowSyncService` as the canonical shared sync service. It syncs `WorkflowStepKey` now. When FLOW-008 adds the stage mapping schema, the `SyncAsync` method gains one additional step (resolve stage from step key, update `WorkflowStageId`). No architectural changes needed.

### Internal Auth Pattern

- **CareConnect** uses `X-Internal-Service-Token` header validated against `InternalServiceToken` config key
- **Liens** already has `FLOW_SERVICE_TOKEN_SECRET` available as an environment secret (used for service token minting for outbound Flow calls)
- **FLOW-009** reuses `FLOW_SERVICE_TOKEN_SECRET` as the shared secret for the internal endpoint — consistent, uses an already-managed credential

### Chosen MVP Ingestion Strategy

Internal HTTP endpoint (`POST /api/liens/internal/flow-events`) protected by `X-Internal-Service-Token: <FLOW_SERVICE_TOKEN_SECRET>`. This is:
- Simple and consistent with platform patterns
- Easy for Flow service to call (already has the secret)
- Does not expose any public auth surface
- Testable immediately via curl or the test helper endpoint

---

## 3. Files Changed

### Backend

| File | Change |
|------|--------|
| `Liens.Domain/Entities/LienTask.cs` | Added `SyncWorkflowStep(stepKey, updatedBySystemUserId)` domain method |
| `Liens.Application/Events/FlowStepChangedEvent.cs` | New — event contract DTO |
| `Liens.Application/Interfaces/IFlowEventHandler.cs` | New — handler interface |
| `Liens.Application/Interfaces/ITaskFlowSyncService.cs` | New — sync service interface |
| `Liens.Application/Repositories/ILienTaskRepository.cs` | Added `GetByWorkflowInstanceIdAsync` |
| `Liens.Application/Services/FlowEventHandler.cs` | New — validates event, finds tasks, delegates sync, emits audit |
| `Liens.Application/Services/TaskFlowSyncService.cs` | New — reusable sync logic (idempotent step-key update) |
| `Liens.Infrastructure/Repositories/LienTaskRepository.cs` | Implemented `GetByWorkflowInstanceIdAsync` |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `FlowEventHandler` + `TaskFlowSyncService` |
| `Liens.Api/Endpoints/FlowEventsEndpoints.cs` | New — internal event ingestion endpoint |
| `Liens.Api/Program.cs` | Mapped `FlowEventsEndpoints` |

### Frontend

No frontend changes required. `WorkflowStepKey` is already surfaced in `task-detail-drawer.tsx` (FLOW-007). The updated step key is visible on next task fetch.

### Database / Config

No schema migrations required. `WorkflowStepKey` (varchar 200) already exists on `liens_Tasks` from FLOW-007 migration `20260420000002_AddTaskFlowLinkage`.

---

## 4. Database / Schema Changes

**None.** All columns required for FLOW-009 were added in migration `20260420000002_AddTaskFlowLinkage`:
- `WorkflowInstanceId` (uuid, nullable)
- `WorkflowStepKey` (varchar 200, nullable)

Index `IX_Tasks_TenantId_WorkflowInstanceId` already exists and is used by `GetByWorkflowInstanceIdAsync`.

---

## 5. API Changes

### New Internal Endpoint

```
POST /api/liens/internal/flow-events
```

**Auth:** `X-Internal-Service-Token: <FLOW_SERVICE_TOKEN_SECRET>` header (shared secret, not a user JWT)

**Request:**
```json
{
  "eventType": "workflow.step.changed",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "productCode": "SYNQ_LIENS",
  "workflowInstanceId": "7c9e8b3a-2f14-4d85-b3a1-9e2c5f7d8a1b",
  "previousStepKey": "document_review",
  "currentStepKey": "final_approval",
  "timestamp": "2026-04-20T10:30:00Z"
}
```

**Response:**
```json
{ "processed": 2, "noOp": 0 }
```

**Validation:**
- `eventType` must be `"workflow.step.changed"`
- `tenantId` must be non-empty Guid
- `workflowInstanceId` must be non-empty Guid
- `currentStepKey` must be non-empty string
- `productCode` must be `"SYNQ_LIENS"` (product isolation guard)
- Header `X-Internal-Service-Token` must match `FLOW_SERVICE_TOKEN_SECRET`

### No Public API Changes

All existing task endpoints, DTOs, and response shapes are unchanged.

---

## 6. UI Changes

No UI changes. The existing "Linked Workflow Instance" section in `task-detail-drawer.tsx` (added in FLOW-007) already surfaces `workflowStepKey`. After a FLOW-009 event updates the step key, the next task fetch returns the updated value and the drawer reflects it.

---

## 7. Event Consumption Model

### Event Contract

```
eventType     : "workflow.step.changed"   (required, fixed)
tenantId      : Guid                      (required; enforces tenant isolation)
productCode   : "SYNQ_LIENS"              (required; enforces product isolation)
workflowInstanceId : Guid                 (required; used for task lookup)
previousStepKey : string | null           (informational)
currentStepKey  : string                  (required; new step to sync to)
timestamp       : ISO 8601                (optional; informational, not used for ordering in MVP)
```

### Ingestion Design

```
POST /api/liens/internal/flow-events
  → validate X-Internal-Service-Token header (FLOW_SERVICE_TOKEN_SECRET)
  → validate payload
  → dispatch to FlowEventHandler.HandleStepChangedAsync()
    → GetByWorkflowInstanceIdAsync(instanceId)     [batch, no N+1]
    → foreach task:
        TaskFlowSyncService.SyncAsync(task, currentStepKey, actorId)
          → if task.WorkflowStepKey == currentStepKey → no-op
          → else: task.SyncWorkflowStep(currentStepKey)
                  repo.UpdateAsync(task)
                  audit: liens.task.flow_step_synced
    → return { processed: N, noOp: M }
```

### Tenant / Product Scoping

- `tenantId` from event payload is used in `GetByWorkflowInstanceIdAsync(tenantId, instanceId)` — tasks from other tenants are never touched
- `productCode == "SYNQ_LIENS"` guard at ingestion layer rejects events for other products before any DB access

---

## 8. Runtime Event Handling Behavior

### Task Lookup

`GetByWorkflowInstanceIdAsync(tenantId, instanceId)` queries `liens_Tasks` on the `IX_Tasks_TenantId_WorkflowInstanceId` index. Returns all tasks for the tenant linked to that instance. Typically 0-few; in degenerate cases could be many if a case has dozens of auto-generated tasks.

### Sync Applied

For each task:
1. `TaskFlowSyncService.SyncAsync(task, currentStepKey)` evaluates:
   - If `task.WorkflowStepKey == currentStepKey` (string comparison) → **no-op**, return `SyncResult.NoOp`
   - Else → call `task.SyncWorkflowStep(currentStepKey)`, persist, audit → return `SyncResult.Synced`

### Idempotency Behavior

| Scenario | Result |
|----------|--------|
| Event replayed with same `currentStepKey` | No-op — step key already matches |
| Event replayed with same content twice | No-op on second delivery — idempotent |
| Two events for different step keys arrive in order | Each applied correctly |
| Stale event with old `currentStepKey` arrives late | Applied (updates to stale key). Limitation documented below. |

**Note on stale events:** The current implementation does not guard against stale event replay (e.g., a delayed event with `currentStepKey="document_review"` arriving after the task has already been updated to `"final_approval"`). The `timestamp` field is received but not used to order events in this MVP. Safe mitigation: Flow should only emit events from its authoritative state, so replay with genuinely stale content should be rare. Full event ordering is deferred to FLOW-010+.

### No-Op Behavior

- `productCode` mismatch → 400 rejected immediately
- `workflowInstanceId` not linked to any tasks → `{ processed: 0, noOp: 0 }` — not an error
- Task step key already matches → counted as `noOp`, not re-persisted
- Task with null `WorkflowStepKey` → always synced (first sync)

---

## 9. Flow Integration Architecture

### Ownership Boundaries

| Domain | Owner | Notes |
|--------|-------|-------|
| Workflow instance execution | Flow | Advances steps, emits events |
| `CurrentStepKey` authority | Flow | Liens reads this, never writes to it |
| `LienTask.WorkflowInstanceId` | Liens | Set at creation (FLOW-007) |
| `LienTask.WorkflowStepKey` | Liens | Updated by FLOW-007 (create-time) and FLOW-009 (event-driven) |
| Task runtime (status, assignment, etc.) | Liens | Fully Liens-owned, never modified by Flow events |
| Stage ID assignment | Liens | FLOW-008 will add mapping; FLOW-009 ready to call it |

### Data Flow

```
Flow Service                    Liens Service
─────────────                   ─────────────
workflow step advances
→ emit POST /api/liens/internal/flow-events
                                → validate token + payload
                                → find tasks by WorkflowInstanceId
                                → for each task:
                                    SyncWorkflowStep(currentStepKey)
                                    [future: also remap WorkflowStageId via FLOW-008]
                                → audit: liens.task.flow_step_synced
→ 200 OK { processed: N }
```

### What This Feature Enables

1. Tasks stay aligned with Flow workflow progression without requiring user read access
2. Audit trail of Flow-driven task changes (separate from user-driven changes)
3. Foundation for bidirectional orchestration in future phases

### What Remains Future Work

- **FLOW-008**: Add `FlowStepKey` column to `LienWorkflowStage`, implement step→stage mapping, wire into `TaskFlowSyncService.SyncAsync`
- **FLOW-010+**: Task → Flow advancement (task completion triggers Flow step advance)
- **Event ordering**: Timestamp-based stale event rejection
- **Retry/dead-letter**: If the Liens endpoint is temporarily unavailable, Flow needs a retry strategy

---

## 10. Permissions / Security

### Internal Endpoint Auth

The `POST /api/liens/internal/flow-events` endpoint:
- Uses `.AllowAnonymous()` (bypasses JWT auth) since it uses a separate shared-secret scheme
- Validates `X-Internal-Service-Token` header against `FLOW_SERVICE_TOKEN_SECRET` env var
- Returns 401 immediately on missing/wrong token, before any business logic runs
- Not mapped through the standard API gateway that requires user JWTs

### Why `FLOW_SERVICE_TOKEN_SECRET`

- Already provisioned as a managed secret in the environment
- Already used for Liens → Flow outbound auth
- Reusing it for Flow → Liens inbound eliminates a new secret management requirement
- Consistent with "internal service token" semantics

### Tenant Isolation

- `tenantId` from event payload is mandatory and used in all DB queries
- Tasks from other tenants are never touched (query is always scoped to `tenantId`)
- A malicious caller who knows the secret but provides a wrong `tenantId` cannot access other tenants' tasks because `GetByWorkflowInstanceIdAsync` scopes by both `TenantId` AND `WorkflowInstanceId`

---

## 11. Audit Integration

### New Event: `liens.task.flow_step_synced`

Emitted per task when FLOW-009 event-driven sync actually changes a task's step key.

| Field | Value |
|-------|-------|
| `eventType` | `liens.task.flow_step_synced` |
| `action` | `update` |
| `description` | `Task '{title}' step synced from '{prev}' to '{new}' via Flow event` |
| `entityType` | `LienTask` |
| `entityId` | task ID |

### Non-Noisy Policy

| Scenario | Audit |
|----------|-------|
| Step key unchanged (no-op) | None |
| Product code rejected | None (just 400 response) |
| No tasks found for instance | None |
| Valid sync applied | `liens.task.flow_step_synced` per task changed |

---

## 12. Validation Results

### Backend Build

- `Liens.Api` builds with 0 errors after all changes

### Frontend Typecheck

- No frontend changes; existing `pnpm type-check` continues to pass

### Runtime

- Application starts cleanly
- Migration coverage probe finds no missing columns

### Manual Test via Internal Endpoint

```bash
curl -X POST https://<env>/api/liens/internal/flow-events \
  -H "Content-Type: application/json" \
  -H "X-Internal-Service-Token: <FLOW_SERVICE_TOKEN_SECRET>" \
  -d '{
    "eventType": "workflow.step.changed",
    "tenantId": "<your-tenant-id>",
    "productCode": "SYNQ_LIENS",
    "workflowInstanceId": "<instance-id>",
    "previousStepKey": "initial_review",
    "currentStepKey": "final_approval",
    "timestamp": "2026-04-20T10:30:00Z"
  }'
```

Expected: `{"processed": N, "noOp": M}` where N = tasks actually updated, M = tasks already aligned.

---

## 13. Known Gaps / Risks

1. **FLOW-008 stage mapping not yet implemented.** `WorkflowStageId` is not updated by FLOW-009 events. The `TaskFlowSyncService.SyncAsync` method has a documented hook where FLOW-008's mapping logic plugs in. Until FLOW-008 is done, only `WorkflowStepKey` is updated.

2. **Stale event replay.** If Flow delivers a delayed event for a step that has since advanced, the task will be written with the stale step key. No timestamp-ordering guard in this MVP. Low risk if Flow delivers events reliably in order.

3. **No retry/dead-letter.** If the Liens endpoint is unavailable when Flow emits an event, the event is lost unless Flow implements retry. Recommend Flow side retries with exponential backoff.

4. **Single active step only.** The event model assumes a single `currentStepKey` per workflow instance. If Flow supports parallel steps in future, the event model needs updating.

5. **`FLOW_SERVICE_TOKEN_SECRET` shared for both directions.** Reusing the same secret for Liens → Flow and Flow → Liens calls is pragmatic but means if the secret rotates, both directions break simultaneously. Considered acceptable for MVP.

6. **No event at-least-once guarantee.** The current design is fire-and-forget from Flow's perspective. If Liens processes the event and then crashes before commit, the task may be inconsistent until the next event arrives. EF SaveChanges is atomic per task update but there's no transactional envelope around the whole batch.

---

## 14. Run Instructions

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

### Start Application

```bash
bash scripts/run-dev.sh
```

### Test Event Ingestion

Find a task's `workflowInstanceId` (from task detail API or drawer), then:

```bash
curl -X POST http://localhost:<liens-port>/api/liens/internal/flow-events \
  -H "Content-Type: application/json" \
  -H "X-Internal-Service-Token: <FLOW_SERVICE_TOKEN_SECRET>" \
  -d '{
    "eventType": "workflow.step.changed",
    "tenantId": "<your-tenant-id>",
    "productCode": "SYNQ_LIENS",
    "workflowInstanceId": "<task-workflowInstanceId>",
    "previousStepKey": null,
    "currentStepKey": "step_two",
    "timestamp": "2026-04-20T10:30:00Z"
  }'
```

Then fetch the task (`GET /api/liens/tasks/<taskId>`) and verify `workflowStepKey` equals `"step_two"`.

For no-op test, send the same event twice. Second response should have `noOp: 1`.

For auth failure test, use a wrong token value — expect 401.
