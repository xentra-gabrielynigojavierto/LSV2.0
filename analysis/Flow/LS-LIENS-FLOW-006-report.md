# LS-LIENS-FLOW-006 — Task Creation Governance + Email Notifications

## 1. Executive Summary

### What was implemented
- **Task Governance Settings domain**: `LienTaskGovernanceSettings` entity with all required governance flags and metadata
- **Backend enforcement**: `LienTaskService.CreateAsync` now validates assignee requirement, case-link requirement, and workflow start-stage requirement against the tenant's governance settings
- **Start-stage derivation**: `FIRST_ACTIVE_STAGE` mode selects the lowest-order active stage from the tenant's workflow config; `EXPLICIT_STAGE` mode uses a configured stage ID
- **Email notification improvements**: Changed create-with-assignee event key from `liens.task.assigned` to `liens.task.created_assigned` to eliminate duplicate create+assign noise; `liens.task.assigned` and `liens.task.reassigned` remain for post-create assignment flows
- **Governance API**: Dual-path tenant + platform admin endpoints following the same pattern as `WorkflowConfigEndpoints`
- **Automation compatibility**: `LienTaskGenerationEngine` calls `LienTaskService.CreateAsync` — governance is enforced there automatically; if governance fails on automation, the rule skips with a clear audit log entry
- **Tenant settings page**: `/lien/settings/task-governance` (new page)
- **Control center page**: `/control-center/liens/task-governance` (new page)
- **Task creation form updated**: Assignee picker added, case selector added (when not prefilled by context), governance settings loaded on open, required-field markers applied, workflow stage auto-populated from governance

### What was partially implemented
- **Assignee validation**: The backend confirms `assignedUserId` is non-null when required, but does not validate that the user ID belongs to the tenant. A full user-exists check would require a synchronous call to the Identity service on every task creation. This is deferred; the field is validated structurally.
- **Case existence validation**: `CaseId` is validated as non-null when required. A case-belongs-to-tenant check is skipped on creation to avoid adding a second DB round-trip on a hot path — it was already handled in `CaseService` when creating cases.

### What was deferred
- `allowMultipleAssignees = false` is hardcoded — the backend task model only supports a single `AssignedUserId`. The flag exists in the entity for future expansion but no multi-assignee logic was built.
- Role-based assignee resolution in automation (`AssignmentMode.AssignByRole`) was already deferred in FLOW-003 and remains deferred.
- User display name enrichment in the task form is not implemented (shows user ID); full name resolution would require a user roster cache.

---

## 2. Codebase Assessment

### Assignee Model
The `LienTask` entity has a single `AssignedUserId: Guid?` — one assignee only. No multi-assignee support exists in schema, entity, DTOs, or frontend. Governance setting `allowMultipleAssignees` is stored but always interpreted as false in enforcement logic.

### Workflow Entry Stage
`LienWorkflowStage` has `StageOrder: int` and `IsActive: bool`. No explicit "IsStart" flag. The start stage is determined by convention: lowest `StageOrder` among active stages in the tenant's workflow config. The `LienTaskGovernanceSettings.DefaultStartStageMode` field encodes this as `FIRST_ACTIVE_STAGE` or `EXPLICIT_STAGE`.

### Current Notification Integration
`INotificationPublisher.PublishAsync(notificationType, tenantId, data, ct)` — fire-and-forget. Before this feature:
- On create with assignee: `liens.task.assigned` (changed to `liens.task.created_assigned`)
- On `AssignAsync` with new assignee: `liens.task.assigned`
- On `AssignAsync` with changed assignee: `liens.task.reassigned`

After this feature:
- On create with assignee: `liens.task.created_assigned` (new event key)
- On assign after creation: `liens.task.assigned`
- On reassign: `liens.task.reassigned`

### Automation Compatibility
`LienTaskGenerationEngine.ProcessRuleAsync` calls `_taskService.CreateAsync`. Governance rules are enforced at the service layer. If governance requires an assignee and automation's `AssignmentMode` is `LEAVE_UNASSIGNED`, the creation will throw a `ValidationException`, caught by the engine's per-rule try/catch, which logs an error and audits `liens.task.auto_generation_skipped`. No invalid task is created.

---

## 3. Files Changed

### Backend — New Files
| File | Purpose |
|------|---------|
| `Liens.Domain/Entities/LienTaskGovernanceSettings.cs` | Governance settings entity |
| `Liens.Domain/Enums/StartStageMode.cs` | `FIRST_ACTIVE_STAGE` / `EXPLICIT_STAGE` enum constants |
| `Liens.Application/DTOs/TaskGovernanceDto.cs` | Request/response DTOs |
| `Liens.Application/Interfaces/ILienTaskGovernanceService.cs` | Service interface |
| `Liens.Application/Repositories/ILienTaskGovernanceSettingsRepository.cs` | Repository interface |
| `Liens.Application/Services/LienTaskGovernanceService.cs` | Governance service impl |
| `Liens.Infrastructure/Repositories/LienTaskGovernanceSettingsRepository.cs` | EF Core repository |
| `Liens.Infrastructure/Persistence/Configurations/LienTaskGovernanceSettingsConfiguration.cs` | EF entity config |
| `Liens.Api/Endpoints/TaskGovernanceEndpoints.cs` | REST endpoints |
| `Liens.Infrastructure/Persistence/Migrations/20260420000001_AddTaskGovernanceSettings.cs` | EF migration |

### Backend — Modified Files
| File | Change |
|------|--------|
| `Liens.Infrastructure/Persistence/LiensDbContext.cs` | Added `DbSet<LienTaskGovernanceSettings>` |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered repository and service |
| `Liens.Application/Services/LienTaskService.cs` | Governance enforcement on `CreateAsync`; notification key fix |
| `Liens.Application/Interfaces/ILienTaskService.cs` | Added `ILienTaskGovernanceService` dependency via constructor |
| `Liens.Api/Program.cs` | `app.MapTaskGovernanceEndpoints()` |

### Frontend — New Files
| File | Purpose |
|------|---------|
| `apps/web/src/lib/liens/lien-task-governance.service.ts` | API calls for governance settings |
| `apps/web/src/app/(platform)/lien/settings/task-governance/page.tsx` | Tenant settings page |
| `apps/web/src/app/(control-center)/control-center/liens/task-governance/page.tsx` | Admin settings page |

### Frontend — Modified Files
| File | Change |
|------|--------|
| `apps/web/src/lib/liens/lien-tasks.types.ts` | Added `TaskGovernanceSettings` type |
| `apps/web/src/components/lien/forms/create-edit-task-form.tsx` | Governance-aware form with assignee picker, case selector |
| `apps/web/src/lib/nav.ts` | Added Task Governance to settings nav |
| `apps/web/src/lib/control-center-nav.ts` | Added Task Governance to liens section |
| `apps/web/src/lib/control-center-routes.ts` | Added route constant |

---

## 4. Database / Schema Changes

### New Table: `liens_TaskGovernanceSettings`
| Column | Type | Description |
|--------|------|-------------|
| `Id` | char(36) PK | GUID |
| `TenantId` | char(36) | Tenant isolator |
| `ProductCode` | varchar(50) | Always `SYNQ_LIENS` |
| `RequireAssigneeOnCreate` | tinyint(1) | Default true |
| `RequireCaseLinkOnCreate` | tinyint(1) | Default true |
| `AllowMultipleAssignees` | tinyint(1) | Default false (not yet enforced) |
| `RequireWorkflowStageOnCreate` | tinyint(1) | Default true |
| `DefaultStartStageMode` | varchar(30) | `FIRST_ACTIVE_STAGE` or `EXPLICIT_STAGE` |
| `ExplicitStartStageId` | char(36) NULL | Used when mode = EXPLICIT_STAGE |
| `Version` | int | Optimistic concurrency |
| `LastUpdatedAt` | datetime(6) | |
| `LastUpdatedByUserId` | char(36) NULL | |
| `LastUpdatedByName` | varchar(200) NULL | |
| `LastUpdatedSource` | varchar(50) | `TENANT_PRODUCT_SETTINGS` or `CONTROL_CENTER` |
| `CreatedByUserId` | char(36) | |
| `UpdatedByUserId` | char(36) NULL | |
| `CreatedAtUtc` | datetime(6) | |
| `UpdatedAtUtc` | datetime(6) | |

**Unique index**: `UX_TaskGovernance_TenantId_ProductCode` on `(TenantId, ProductCode)`

**Migration**: `20260420000001_AddTaskGovernanceSettings`

---

## 5. API Changes

### New Endpoints

#### Tenant (requires `workflow:manage` permission)
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/liens/task-governance` | Get or create tenant governance settings |
| PUT | `/api/liens/task-governance` | Update governance settings |

#### Admin / Platform (requires PlatformOrTenantAdmin)
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/liens/admin/task-governance/tenants/{tenantId}` | Get governance settings for a tenant |
| PUT | `/api/liens/admin/task-governance/tenants/{tenantId}` | Update governance settings for a tenant |

### Updated Behavior: `POST /api/liens/tasks`
Now validates (based on governance settings):
- `assignedUserId` must be present when `requireAssigneeOnCreate = true`
- `caseId` must be present when `requireCaseLinkOnCreate = true`
- `workflowStageId` is auto-derived from start-stage rules when `requireWorkflowStageOnCreate = true`

Returns validation errors with field-level messages:
```json
{
  "assignedUserId": ["Task assignee is required."],
  "caseId": ["Task must be linked to a case."]
}
```

### Request / Response Shapes

**GET/PUT response**:
```json
{
  "id": "...",
  "tenantId": "...",
  "productCode": "SYNQ_LIENS",
  "requireAssigneeOnCreate": true,
  "requireCaseLinkOnCreate": true,
  "allowMultipleAssignees": false,
  "requireWorkflowStageOnCreate": true,
  "defaultStartStageMode": "FIRST_ACTIVE_STAGE",
  "explicitStartStageId": null,
  "version": 1,
  "lastUpdatedAt": "...",
  "lastUpdatedByUserId": "...",
  "lastUpdatedByName": "...",
  "lastUpdatedSource": "TENANT_PRODUCT_SETTINGS",
  "createdAtUtc": "...",
  "updatedAtUtc": "..."
}
```

**PUT request**:
```json
{
  "requireAssigneeOnCreate": true,
  "requireCaseLinkOnCreate": true,
  "allowMultipleAssignees": false,
  "requireWorkflowStageOnCreate": true,
  "defaultStartStageMode": "FIRST_ACTIVE_STAGE",
  "explicitStartStageId": null,
  "updateSource": "TENANT_PRODUCT_SETTINGS",
  "version": 1,
  "updatedByName": "Jane Admin"
}
```

---

## 6. UI Changes

### Task Governance Settings Page (Tenant)
- Path: `/lien/settings/task-governance`
- Toggle cards for: Require Assignee, Require Case Link, Allow Multiple Assignees, Require Workflow Stage
- Start stage mode selector: First Active Stage / Explicit Stage
- Explicit stage selector (shown when mode = EXPLICIT_STAGE, lists tenant's active workflow stages)
- Metadata: version, last updated, updated by, source
- Save button triggers PUT, shows success/error feedback

### Task Governance Settings Page (Control Center)
- Path: `/control-center/liens/task-governance`
- Same layout as tenant page
- Selects tenant via URL param (tenant browsing in admin interface)
- Uses admin API endpoints

### Task Creation Form Changes
- **Assignee picker**: New field "Assigned To" — dropdown from tenant user list, required marker when governance requires it
- **Case selector**: New field "Case" — shown as input when no `prefillCaseId` provided, required marker when governance requires it
- **Workflow stage**: Auto-populated from governance rules; displayed as read-only info text
- **Required markers** (`*`) on all required fields
- **Governance loading**: Fetches governance settings once per form open; shows a subtle loading state
- **Error messages**: Field-level errors from API are shown inline

### Context-Aware Behavior
- From Case page: `prefillCaseId` provided → case field hidden, auto-linked
- From Lien page: `prefillLienId` provided → lien linked; case can be selected
- From My Tasks page: no prefill → case selector required when governance mandates it

---

## 7. Flow Service Alignment

### Architecture Reality (as of 2026-04-20)

This section documents the honest transitional state of the Flow service boundary for Synq Liens, as required by the specification.

---

### 7.1 What is Flow-Owned at Runtime

The **Flow service** (`apps/services/flow/`) is a live, production workflow instance engine. It owns:

| Concern | Detail |
|---------|--------|
| Case/lien **process workflow instances** | When a tenant starts a case workflow (e.g., "Process Lien Application"), Flow creates a `WorkflowInstance` that tracks the case through a multi-step process definition |
| Workflow step advancement | `IFlowClient.AdvanceWorkflowAsync` / `AdvanceProductWorkflowAsync` move an instance from one defined step to the next |
| Workflow completion | `IFlowClient.CompleteWorkflowAsync` / `CompleteProductWorkflowAsync` |
| Workflow definition listing | `IFlowClient.ListDefinitionsAsync` — used by tenant portal to show "Start workflow" modal |
| Product-scoped workflow passthroughs | `IFlowClient.GetProductWorkflowAsync` — atomic ownership-validated access for each product (SynqLien, CareConnect, SynqFund) |

Synq Liens already has a live `WorkflowEndpoints.cs` (`/api/liens/cases/{id}/workflows`) that routes to Flow. Product slug `"synqlien"` is registered. Case workflows **already run through Flow**.

---

### 7.2 What is Synq Liens-Owned at Runtime

The **Liens service** owns all of the following directly:

| Concern | Detail |
|---------|--------|
| **My Tasks** (LienTask) | Individual operational tasks ("Follow up with owner", "Prepare documents") |
| Task workflow stages | `LienWorkflowConfig`, `LienWorkflowStage`, `LienWorkflowTransition` — a lightweight Liens-local staging system for task progress |
| Task assignment | `LienTask.AssignedUserId` — single-assignee model |
| Task templates | `LienTaskTemplate` — pre-filled task blueprints |
| Task automation rules | `LienTaskGenerationRule` + `LienTaskGenerationEngine` — event-triggered generation |
| Task governance settings | `LienTaskGovernanceSettings` (added in this feature) |
| Case/lien context linkage | `LienTask.CaseId`, `LienTaskLienLink` |

The `IFlowClient` interface has **no task management methods** (`CreateTaskAsync`, `AssignTaskAsync`, etc.). Flow manages workflow instances (case-level processes); it does not manage individual operational work items.

---

### 7.3 Explicit Boundary Map

```
Flow Service                          Synq Liens Service
─────────────────────────────         ──────────────────────────────────────────
WorkflowInstance (per case)           LienTask (individual work items)
WorkflowDefinition                    LienWorkflowConfig (task staging)
AdvanceWorkflow                       LienWorkflowStage + Transitions
CompleteWorkflow                      Task Status (NEW → IN_PROGRESS → COMPLETED)
Product-scoped auth checks            Task assignment (single AssignedUserId)
                                      Task templates + automation
                                      Task governance settings (this feature)
                                      Case/lien context linkage
```

**Synq Liens calls Flow** to manage case process workflows.  
**Flow does not call Liens** for task management.  
These are two separate concern planes — they coexist, not replace each other.

---

### 7.4 What This Feature Aligns to Flow Direction

This feature does NOT add any new permanent Liens-local divergence. It does the following that aligns correctly with the Flow direction:

1. **Governance validation runs in the Liens service** — correct, because task creation belongs in Liens. There is no Flow API surface for individual task intake. Running governance in Liens is not a workaround; it is the right boundary.

2. **Workflow stage derivation uses Liens-local `LienWorkflowConfig`** — this is the correct stage configuration for My Tasks, and is separate from Flow's `WorkflowDefinition` stages (which are for case-level process execution). These are different granularity concerns.

3. **`StartStageMode.FIRST_ACTIVE_STAGE`** defaults to the same implicit first-step semantics that Flow uses when starting a workflow instance.

4. **No new `IFlowClient` calls were added** for task creation — this is correct. Flow does not accept individual task creation requests. Adding such calls would be incorrect architecture.

5. **`liens.task.created_assigned` / `liens.task.assigned` / `liens.task.reassigned` notification events** — fire through the shared Notifications service, which is also how Flow-side events publish notifications. The event taxonomy is consistent with the platform notification pattern.

---

### 7.5 Transitional Boundaries (Honest Documentation)

The following remain transitional and should be addressed in future platform work:

| Transitional Item | Current State | Preferred Direction |
|-------------------|--------------|---------------------|
| Task workflow stages in Liens | Liens-local `LienWorkflowConfig` | Long-term: evaluate whether My Tasks stages should map to Flow workflow steps. Requires Flow to expose a task-level API, which it does not currently. |
| Task creation stays in Liens | Task service is Liens-owned | Long-term: if Flow adds a product task management module, Liens tasks could become Flow-managed work items scoped to a workflow instance |
| Task governance settings in Liens | Settings entity in Liens DB | Remains correct as a product-facing configuration layer regardless of runtime changes |
| Automation (FLOW-003) | `LienTaskGenerationEngine` in Liens | Already uses Liens task service; governance enforcement added in this feature. Will naturally inherit any future Flow task routing if task creation is ever moved |

**Critically: this feature does not introduce any new permanent Liens-only divergence from the platform direction.** The Liens-local task system is the correct current runtime owner, and this governance layer is placed at the correct boundary — the task creation gate.

---

### 7.6 Governance as the Flow-Synq Liens Bridge

The governance settings (`requireCaseLinkOnCreate`) ensure that every task created in Synq Liens is linked to a `CaseId`. This is the same case entity that already has a Flow `WorkflowInstance` running against it. The linkage means:

- A task in My Tasks is always traceable to a case that may have a live Flow workflow
- Future versions could use `CaseId` as the `SourceEntityId` to read the current Flow instance state and use it during task creation (e.g., auto-setting the task stage to match the current Flow workflow step)

This makes the governance feature a **foundation for deeper Flow alignment** in future releases.

---

## 8. Task Governance Behavior

### Create-Time Validation Rules
```
IF requireAssigneeOnCreate = true
  → assignedUserId must not be null
  → Error: "Task assignee is required."

IF requireCaseLinkOnCreate = true
  → caseId must not be null
  → Error: "Task must be linked to a case."

IF requireWorkflowStageOnCreate = true AND workflowStageId not supplied
  → Derive start stage from DefaultStartStageMode
    FIRST_ACTIVE_STAGE: SELECT min(StageOrder) active stage from tenant workflow config
    EXPLICIT_STAGE: use ExplicitStartStageId (validated as active and belonging to tenant)
  → If no stage can be derived → Error: "A valid workflow stage is required for task creation."
```

### Initial Stage Placement
- Setting a `WorkflowStageId` on creation is NOT subject to transition validation
- FLOW-005 transition engine only applies when moving from an existing stage to a new one during `UpdateAsync`

### Template/Automation Compatibility
- Templates pre-fill fields; governance validation still runs after pre-fill
- Automation engine catches `ValidationException` from `CreateAsync`, logs the error, audits `liens.task.auto_generation_skipped`, and returns false (skips rule silently)
- No invalid tasks are created by automation

---

## 9. Notifications / Email Integration

### Event Keys
| Event | Trigger | Description |
|-------|---------|-------------|
| `liens.task.created_assigned` | Task created with assignee already present | Single combined event, avoids double-fire |
| `liens.task.assigned` | Task had no assignee → now assigned | Post-creation first assignment |
| `liens.task.reassigned` | Task changed from assignee A to assignee B | Notify new assignee |

### Anti-Duplicate Logic
Before this feature, creating a task with an assignee fired `liens.task.assigned`. This was the same event key used for standalone assignment, creating potential template confusion. Now:
- Create-with-assignee → `liens.task.created_assigned` (distinct event/template)
- Standalone assign → `liens.task.assigned`
No duplicate emails for initial create-with-assignee scenario.

### Notification Payload
```json
{
  "taskId": "...",
  "taskTitle": "...",
  "assignedTo": "<userId>",
  "assignedBy": "<userId>",
  "caseId": "...",
  "priority": "HIGH",
  "workflowStageId": "...",
  "dueDate": "2026-05-01"
}
```
`caseId`, `priority`, `workflowStageId`, `dueDate` are included when available.

### Failure Behavior
Notification publish is fire-and-forget (`_ = _notifications.PublishAsync(...)`). Task creation/update succeeds regardless of notification outcome. Failures are surfaced in the Notifications service logs, not in the task API response.

---

## 10. Permissions / Security

### Reused Permissions
| Action | Permission |
|--------|-----------|
| Get/update governance settings (tenant) | `SYNQ_LIENS.workflow:manage` |
| Get/update governance settings (admin) | `PlatformOrTenantAdmin` policy |

No new permission was introduced — task governance settings are a natural extension of workflow management.

### Tenant Isolation
- All settings queries are scoped by `TenantId`
- Unique constraint prevents multiple governance records per tenant/product
- Admin endpoints require explicit `tenantId` in path and validate it

### Assignee Validation
- Structural validation only: `AssignedUserId` must be a non-null GUID when required
- Full user-exists check deferred (would require Identity service call on every create)

---

## 11. Audit Integration

### Governance Settings Events
| Event | Trigger |
|-------|---------|
| `liens.task_governance.created` | First-time governance settings created for tenant |
| `liens.task_governance.updated` | Governance settings updated |

### Automation Governance Failure
When automation skips a task because governance validation fails, the existing `liens.task.auto_generation_skipped` audit event is reused with a descriptive message including the governance reason.

### Existing Task Events Preserved
`liens.task.created`, `liens.task.updated`, `liens.task.assigned`, `liens.task.completed`, `liens.task.cancelled` — all unchanged.

---

## 12. Validation Results

### Backend Build
- Both Liens.Api and Liens.Infrastructure compile cleanly
- Migration created and applied via startup `EnsureLiensSchemaTablesAsync`

### Frontend
- TypeScript types added; components compile with no new type errors
- Form governance integration uses graceful fallback if governance endpoint returns error (all settings default to false, preserving old behavior)

### Manual Validation
- `POST /api/liens/tasks` with missing assignee when `requireAssigneeOnCreate=true` → 422 with field error
- `POST /api/liens/tasks` with missing caseId when `requireCaseLinkOnCreate=true` → 422 with field error
- `POST /api/liens/tasks` with no workflowStageId when `requireWorkflowStageOnCreate=true` → stage auto-derived
- `GET /api/liens/task-governance` for new tenant → creates default settings
- `PUT /api/liens/task-governance` → version bumps, metadata updates

---

## 13. Known Gaps / Risks

1. **Assignee existence validation**: `assignedUserId` is checked for presence but not confirmed to be a valid user in the tenant. A future improvement is an `IIdentityClient.UserExistsInTenantAsync` check.
2. **`allowMultipleAssignees`**: Stored in settings but not enforced — single-assignee is the only supported model. When the domain is extended to support multi-assignee, this flag will gate the behavior.
3. **Explicit stage validation**: When `EXPLICIT_STAGE` mode is used, the stage is validated to be active at governance setting save time, but not re-validated at task creation time if the stage is later deactivated. A task creation that references a now-deactivated explicit stage will fall through to the `FIRST_ACTIVE_STAGE` fallback.
4. **Automation assignee gap**: Auto-generated tasks with `AssignmentMode.LeaveUnassigned` will fail governance when `requireAssigneeOnCreate=true`. Tenants must configure automation rules with `AssignEventActor` or role-based assignment to meet governance. This is by design — documented behavior.
5. **No governance bypass flag**: There is no `skipGovernance` flag for internal or system calls. This is intentional — governance is always enforced.
6. **Flow task management module**: The `IFlowClient` has no `CreateTask` or `AssignTask` methods. My Tasks in Liens remains Liens-local. If the Flow service adds a product task management API in the future, task creation should be re-evaluated to route through Flow rather than the Liens-local service. When that happens, the `LienTaskGovernanceSettings` configuration layer would remain Liens-owned, and Flow would become the execution owner.
7. **Task-to-workflow-instance correlation**: Tasks linked to a `CaseId` could theoretically be correlated with the active Flow `WorkflowInstance` for that case to inherit stage context. This is not currently implemented — it would require a synchronous `IFlowClient.ListBySourceEntityAsync` call on every task creation to look up the active instance, which is deferred to avoid latency on the create path.
8. **Assignee display names**: The task creation form shows users by userId in the assignee dropdown when user list fails to load. A future improvement is a user roster cache that enriches display names across the Liens module.

---

## 14. Run Instructions

### Migration
The migration `20260420000001_AddTaskGovernanceSettings` is included as a code file. EF Core's `MigrateAsync()` in `Program.cs` will apply it on startup. The `EnsureLiensSchemaTablesAsync` safety-net also handles the table creation.

### Build
```bash
cd apps/services/liens
dotnet build Liens.Api/Liens.Api.csproj --no-restore
```

### Frontend
```bash
cd apps/web
pnpm typecheck
```

### Test Governance API
```bash
# Get/create governance settings
GET /api/liens/task-governance
Authorization: Bearer <tenant_user_token>
X-Tenant-Id: <tenantId>

# Update settings
PUT /api/liens/task-governance
{
  "requireAssigneeOnCreate": true,
  "requireCaseLinkOnCreate": false,
  "allowMultipleAssignees": false,
  "requireWorkflowStageOnCreate": true,
  "defaultStartStageMode": "FIRST_ACTIVE_STAGE",
  "updateSource": "TENANT_PRODUCT_SETTINGS",
  "version": 1
}
```

### Test Governance Enforcement
```bash
# Should return 422 when assignee missing and required
POST /api/liens/tasks
{ "title": "Test Task" }
```
