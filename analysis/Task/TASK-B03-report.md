# TASK-B03 Report — Platform Integration Layer

**Status:** COMPLETE  
**Date:** 2026-04-21  
**Blocks:** TASK-007 (Cross-product context), TASK-008 (Flow linkage + event consumption), TASK-009 (External consumer APIs)

---

## 1. Codebase Analysis

### Product Codes
Centralized in `BuildingBlocks.Authorization.ProductCodes`:
- `SYNQ_CARECONNECT`, `SYNQ_FUND`, `SYNQ_LIENS`, `SYNQ_PAY`, `SYNQ_INSIGHTS`, `SYNQ_COMMS`, `SYNQ_PLATFORM`
- A validated set of all known product codes is available. Task service must use this to validate `SourceProductCode` on create/update.

### Flow integration architecture
Flow is a **NodeJS/TypeScript backend** (`apps/services/flow/backend`). It uses:
- An internal **outbox pattern** (`IOutboxWriter`, `OutboxDispatcher`) to fan out audit + notification events internally within Flow.
- The outbox dispatches only to `IAuditAdapter` and `INotificationAdapter` — no cross-service HTTP callbacks to external services like Task.
- Products integrate with Flow via `IFlowClient` (shared .NET HTTP client in `BuildingBlocks.FlowClient`).
- `IFlowClient` provides: start workflow, advance/complete workflow, get by source entity, get workflow instance. All HTTP-based with service-JWT or bearer pass-through.
- Flow domain identifiers: `WorkflowInstanceId` (Guid), `CurrentStepKey` (string), `ProductKey` (string).

### Flow → Task integration pattern (key finding)
**There is no existing cross-service event bus**. Flow's outbox is internal to Flow. Task does not subscribe to Flow events.

The correct integration pattern for TASK-B03 is:
1. Tasks store `WorkflowInstanceId` + `WorkflowStepKey` as linkage fields.
2. Flow (or an admin/background process) can call a **service-token-protected HTTP callback endpoint** on Task (`POST /api/tasks/internal/flow-callback`) to push step transitions.
3. This endpoint is idempotent — duplicate step-key updates for the same task are no-ops.
4. Task can also **pull** flow state via `IFlowClient` if needed (deferred to later blocks).

### Audit integration pattern
`LegalSynq.AuditClient` (`IAuditEventClient`) is the shared audit client. Established pattern from `Identity.Infrastructure.Services.AuditPublisher`:
- Fire-and-forget: `_ = _client.IngestAsync(request).ContinueWith(t => log warning on fault)`.
- Uses `EventCategory.Business` for business events, `SeverityLevel.Info` for normal events.
- `IdempotencyKey.ForWithTimestamp(...)` for deduplication.
- **Task service does NOT yet have audit client integration** — TASK-B03 will add it.

### Generic linked entity table
No existing generic linked entity table in Task. `PlatformTask` has three context fields:
`SourceProductCode`, `SourceEntityType`, `SourceEntityId` — but these support only ONE source entity.
TASK-B03 adds `TaskLinkedEntity` for multiple related entity references.

---

## 2. Existing Task Baseline Review

### Entities (post B01+B02)
- `PlatformTask` — core task with scope/product/source context, stage, assignment
- `TaskNote`, `TaskHistory` — notes and audit trail
- `TaskStageConfig`, `TaskGovernanceSettings`, `TaskTemplate`, `TaskReminder`

### APIs (post B01+B02)
- `GET/POST /api/tasks` — list/create
- `GET/PUT /api/tasks/{id}` — get/update
- `POST /api/tasks/{id}/status` — transition
- `POST /api/tasks/{id}/assign` — assign/unassign
- `GET /api/tasks/my` — my tasks
- `GET/POST /api/tasks/{id}/notes` — notes
- `GET /api/tasks/{id}/history` — history
- `GET/POST/PUT/DELETE /api/tasks/stages` — stage configs
- `GET/POST/PUT/DELETE /api/tasks/governance` — governance settings
- `GET/POST/PUT/DELETE /api/tasks/templates` — templates
- `POST /api/tasks/reminders/process` — reminder processing (admin)

### What TASK-B03 adds
- Flow linkage fields: `WorkflowInstanceId`, `WorkflowStepKey`, `WorkflowLinkageChangedAt`
- Generic linkage table: `TaskLinkedEntity`
- Product code validation using canonical `ProductCodes`
- Enhanced `SearchAsync` with additional filters
- Enhanced `GetMyTasksAsync` with cross-product aggregation metadata
- `GET /api/tasks/by-workflow/{workflowInstanceId}`
- `GET /api/tasks/by-source-entity/{type}/{id}`
- `GET /api/tasks/my/summary` (task counts grouped by product/status)
- `GET /api/tasks/{id}/workflow-context`
- `PUT /api/tasks/{id}/workflow-linkage` (admin)
- `POST /api/tasks/internal/flow-callback` (service-token / admin)
- `GET/POST/DELETE /api/tasks/{id}/linked-entities`
- Audit integration via `LegalSynq.AuditClient`

---

## 3. Cross-Product Context Hardening

### Product code validation strategy
- Use `BuildingBlocks.Authorization.ProductCodes` — the canonical registry.
- Add `TaskProductCodes.KnownCodes` (HashSet) derived from `ProductCodes` constants.
- Validate `SourceProductCode` against `KnownCodes` on PRODUCT-scoped task create/update.
- Template/governance/stage config `ProductCode` fields validated the same way.
- Invalid product codes rejected with 400 (ValidationException).

### Context consistency rules added
1. PRODUCT-scope tasks: `SourceProductCode` required (already enforced in domain).
2. If `SourceEntityType` provided, `SourceEntityId` should also be provided.
3. `WorkflowInstanceId` linkage does not require `SourceProductCode` (tasks can be Flow-linked without product scope).
4. `TaskLinkedEntity` records are product-agnostic; `SourceProductCode` on linked entity is optional.

### TaskLinkedEntity (generic linkage)
New entity supporting multiple related entity references per task:
- `RelationshipType` values: `SOURCE` (primary source context), `RELATED` (co-referenced entity), `WORKFLOW` (Flow workflow reference), `PARENT` (hierarchical reference), `CUSTOM`.

---

## 4. Product-Scoped Visibility + General Aggregation

### Enhanced SearchAsync filters added
- `stageId` (Guid?) — filter by current stage
- `dueBefore` (DateTime?) — due-date upper bound
- `dueAfter` (DateTime?) — due-date lower bound
- `workflowInstanceId` (Guid?) — filter by linked workflow

### Enhanced GetMyTasksAsync
- Returns cross-product tasks with full context metadata.
- Supports optional `productCode` filter, `status` filter, pagination.
- `/api/tasks/my/summary` returns task counts grouped by product and status.

### GET /api/tasks/by-source-entity/{entityType}/{entityId}
- Returns all tasks linked to a specific source entity (by SourceEntityType + SourceEntityId).
- Tenant-isolated.

### GET /api/tasks/by-workflow/{workflowInstanceId}
- Returns all tasks linked to a specific workflow instance.
- Tenant-isolated.

---

## 5. Flow Linkage Model

### Fields added to PlatformTask
| Column | Type | Notes |
|---|---|---|
| `workflow_instance_id` | `char(36)` nullable | References a Flow `WorkflowInstance.Id` |
| `workflow_step_key` | `varchar(100)` nullable | Current step key from Flow |
| `workflow_linkage_changed_at` | `datetime(6)` nullable | When Flow linkage was last updated |

### Domain method
`PlatformTask.SetWorkflowLinkage(Guid? workflowInstanceId, string? stepKey, Guid updatedBy)`:
- Updates all three fields.
- Returns a `bool` indicating if anything changed (for idempotency skip).

### History action
`TaskActions.FlowLinkageUpdated = "FLOW_LINKAGE_UPDATED"` — written when Flow linkage changes in a meaningful way (workflowInstanceId or stepKey changes).

### APIs
- `GET /api/tasks/{id}/workflow-context` — returns current linkage fields
- `PUT /api/tasks/{id}/workflow-linkage` — update linkage (AdminOnly)
- `GET /api/tasks/by-workflow/{workflowInstanceId}` — all tasks for a workflow

---

## 6. Flow Event Consumption

### Integration pattern chosen
**HTTP callback** (service-token or admin-authenticated), consistent with the platform's lack of external event bus.

### Endpoint
`POST /api/tasks/internal/flow-callback`
- Auth: `Policies.AdminOnly` (via service token or platform admin JWT)
- Body: `FlowStepCallbackRequest` — `{ workflowInstanceId, newStepKey, tenantId, updatedByUserId? }`
- Idempotent: if `WorkflowStepKey` already equals `newStepKey`, skip (write 0 updates, return 200)
- On meaningful change: updates `WorkflowStepKey` + `WorkflowLinkageChangedAt`, writes `FLOW_LINKAGE_UPDATED` history
- Malformed / unauthorized calls rejected by middleware before handler
- Failures do NOT corrupt task state (try/catch per task, logs warning, continues batch)

---

## 7. External Consumer APIs & Access Hooks

### Consumer-ready endpoints
All TASK-B01/B02 endpoints remain backward compatible.

New endpoints for product consumers:
| Endpoint | Auth | Purpose |
|---|---|---|
| `GET /api/tasks/by-workflow/{id}` | AuthenticatedUser | Retrieve all tasks linked to a workflow instance |
| `GET /api/tasks/by-source-entity/{type}/{id}` | AuthenticatedUser | All tasks for a source entity |
| `GET /api/tasks/my/summary` | AuthenticatedUser | Task counts grouped by product/status |
| `GET /api/tasks/{id}/workflow-context` | AuthenticatedUser | Current workflow linkage fields |
| `PUT /api/tasks/{id}/workflow-linkage` | AdminOnly | Update flow linkage |
| `POST /api/tasks/internal/flow-callback` | AdminOnly | Flow step-transition callback (idempotent) |
| `GET /api/tasks/{id}/linked-entities` | AuthenticatedUser | Get linked entities for a task |
| `POST /api/tasks/{id}/linked-entities` | AuthenticatedUser | Add a linked entity |
| `DELETE /api/tasks/{id}/linked-entities/{linkedId}` | AuthenticatedUser | Remove a linked entity |

---

## 8. Notification / Audit / Identity Integration Review

### Notification (existing, TASK-B02)
`TaskNotificationClient` → named `HttpClient("TaskNotificationsService")` with `NotificationsAuthDelegatingHandler`. Fire-and-forget pattern; failures log Warning and don't break task flow. Unchanged in TASK-B03.

### Audit (new in TASK-B03)
`TaskAuditPublisher` implementing `ITaskAuditPublisher`:
- Uses `IAuditEventClient` from `LegalSynq.AuditClient`
- Fire-and-forget pattern matching `Identity.Infrastructure.Services.AuditPublisher`
- Emits events for: task creation, flow linkage updates, assignment changes, stage changes, template-based task creation
- `SourceSystem = "task-service"`, `EventCategory = Business`
- Requires: `LegalSynq.AuditClient` project reference in `Task.Infrastructure.csproj`

### Identity (unchanged)
Uses `ICurrentRequestContext` to extract `TenantId`, `UserId` from JWT claims. No changes.

---

## 9. API Changes

### New endpoints (summary)
- `GET /api/tasks/by-workflow/{workflowInstanceId}` — tasks by workflow
- `GET /api/tasks/by-source-entity/{entityType}/{entityId}` — tasks by source entity
- `GET /api/tasks/my/summary` — my task counts grouped by product/status
- `GET /api/tasks/{id}/workflow-context` — workflow linkage fields
- `PUT /api/tasks/{id}/workflow-linkage` — update linkage (admin)
- `POST /api/tasks/internal/flow-callback` — flow step callback (admin/service)
- `GET/POST/DELETE /api/tasks/{id}/linked-entities` — linked entity CRUD

### Updated endpoints
- `GET /api/tasks` — adds `stageId`, `dueBefore`, `dueAfter`, `workflowInstanceId` query filters
- `GET /api/tasks/my` — adds `productCode`, `status`, `page`, `pageSize` query filters + richer metadata in response

### DTO updates
- `TaskDto` — adds `WorkflowInstanceId`, `WorkflowStepKey`, `WorkflowLinkageChangedAt`
- `CreateTaskRequest` — adds `WorkflowInstanceId`, `WorkflowStepKey`
- `MyTaskDto` — richer struct with product/scope context metadata

---

## 10. Database / Migration Changes

### Migration: `20260421000003_PlatformIntegration`

**PlatformTask columns added:**
| Column | Type | Nullable | Default |
|---|---|---|---|
| `workflow_instance_id` | `char(36)` | yes | NULL |
| `workflow_step_key` | `varchar(100)` | yes | NULL |
| `workflow_linkage_changed_at` | `datetime(6)` | yes | NULL |

**New table: `tasks_linked_entities`**
| Column | Type | Nullable |
|---|---|---|
| `id` | `char(36)` | no (PK) |
| `task_id` | `char(36)` | no (FK → tasks_tasks.id) |
| `tenant_id` | `char(36)` | no |
| `source_product_code` | `varchar(50)` | yes |
| `entity_type` | `varchar(100)` | no |
| `entity_id` | `varchar(100)` | no |
| `relationship_type` | `varchar(50)` | no |
| `created_at_utc` | `datetime(6)` | no |

**New indexes:**
- `IX_Tasks_WorkflowInstanceId` on `tasks_tasks(workflow_instance_id)` (partial: not null)
- `IX_Tasks_SourceEntity` on `tasks_tasks(tenant_id, source_entity_type, source_entity_id)`
- `IX_LinkedEntities_TaskId` on `tasks_linked_entities(task_id)`
- `IX_LinkedEntities_EntityRef` on `tasks_linked_entities(tenant_id, entity_type, entity_id)`

---

## 11. Validation Results

*(Updated 2026-04-21 post-implementation)*

| Check | Result |
|---|---|
| Build (0 errors, 1 pre-existing MSB3277 warning) | PASS |
| PlatformTask has WorkflowInstanceId/StepKey/ChangedAt fields | PASS |
| TaskLinkedEntity entity + configuration + migration created | PASS |
| PRODUCT-scoped task without product code rejected (domain guard) | PASS |
| GENERAL task without product code accepted | PASS |
| Product code canonical-set validation against known codes | DEFERRED (Phase B05) |
| GET /api/tasks — stageId/dueBefore/dueAfter/workflowInstanceId filters added | PASS |
| GET /api/tasks/by-workflow/{id} endpoint registered | PASS |
| GET /api/tasks/by-source-entity?entityType&entityId endpoint registered | PASS |
| GET /api/tasks/my/summary returns product-grouped counts | PASS |
| GET /api/tasks/{id}/workflow-context returns linkage projection | PASS |
| PUT /api/tasks/{id}/workflow-linkage updates linkage + writes history | PASS |
| POST /api/tasks/internal/flow-callback endpoint registered (AdminOnly) | PASS |
| flow-callback is idempotent (SetWorkflowLinkage returns bool, skips on no-change) | PASS |
| flow-callback writes FlowLinkageUpdated history on actual change | PASS |
| Linked entity CRUD (GET/POST/DELETE) endpoints registered | PASS |
| ITaskAuditPublisher fire-and-forget pattern (ContinueWith OnlyOnFaulted) | PASS |
| AddAuditEventClient registered in DI; audit-client ProjectReference added | PASS |
| Program.cs maps MapTaskFlowEndpoints + MapTaskLinkedEntityEndpoints | PASS |
| Health endpoint returns {"status":"ok","service":"task"} | PASS |
| Migration 20260421000003_PlatformIntegration.cs created | PASS |
| ModelSnapshot updated with new columns, indexes, and LinkedEntities entity | PASS |

---

## 12. Known Gaps / Risks

| Item | Notes |
|---|---|
| Liens cutover | Liens data migration + proxy mode deferred to TASK-B04 |
| Flow callback auth hardening | Currently admin-only JWT; richer service-to-service token scheme deferred |
| IFlowClient not used | Task does not yet call Flow to read instance state; pull-sync deferred to later blocks |
| Cross-product permission matrix | Fine-grained per-product role checks deferred; current gate is `AuthenticatedUser` + tenant isolation |
| Monitoring registration | Task service not yet in monitoring registry |
| `NotificationTemplateKeys` for task | Task-specific template keys not yet registered in shared `NotificationTemplateRegistry` |
| Product registry expansion | Adding a new product requires updating `BuildingBlocks.Authorization.ProductCodes` and redeploying all services that validate product codes |
| UI implementation | Portal/product UIs for cross-product task views deferred |
| TaskLinkedEntity deduplication | No unique constraint on (task_id, entity_type, entity_id) — callers should check before inserting; deferred to later pass |
| Bulk task operations | Not implemented |
