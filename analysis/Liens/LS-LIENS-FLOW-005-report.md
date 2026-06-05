# LS-LIENS-FLOW-005 — Workflow Transition Engine

**Status:** In Progress  
**Date:** 2026-04-18

---

## 1. Executive Summary

### What was implemented
- `LienWorkflowTransition` domain entity, EF configuration, and DB migration
- `IWorkflowTransitionValidationService` + `WorkflowTransitionValidationService` — reusable `IsTransitionAllowed` / `GetAllowedNextStages`
- Transition management extended into `ILienWorkflowConfigService` / `LienWorkflowConfigService` (list, add, deactivate, save-batch)
- Runtime enforcement in `LienTaskService.UpdateAsync` — blocks invalid `WorkflowStageId` moves
- Lazy auto-initialization: workflows with stages but no transitions get linear default transitions generated on first transition fetch
- Extended `WorkflowConfigResponse` to include `Transitions`
- Extended `WorkflowConfigEndpoints` with transition CRUD (tenant + admin routes)
- Frontend types, API client, tenant settings page, and control-center page all updated with transition editor UI

### What was partially implemented
- Runtime enforcement is wired only in `LienTaskService.UpdateAsync` (task stage changes). Case and lien stage movement does not yet have explicit workflow-governed stage transitions — they do not currently own a `WorkflowStageId` field directly linked to the same stage model.

### What was deferred
- Role-based gating (spec explicitly excludes)
- SLA / timer conditions
- Approval routing
- Required-task gating
- Drag-and-drop transition graph UI

---

## 2. Codebase Assessment

### Existing Workflow Config Findings
- `LienWorkflowConfig` — root entity, tenant-scoped, versioned, with `LastUpdatedSource` governance
- `LienWorkflowStage` — ordered stages with `StageOrder`, FK to config, `IsActive`
- `LienWorkflowConfigService` — handles CRUD with audit publishing
- Both tenant (`/api/liens/workflow-config`) and admin (`/api/liens/admin/workflow-config/tenants/{tenantId}`) endpoints use the same service
- Version bump on every config/stage change is already in place

### Runtime Movement Findings
- `LienTask.WorkflowStageId` — tasks can be assigned to a stage; updated via `LienTaskService.UpdateAsync`
- **Before this story**: no validation — any `WorkflowStageId` could be set freely
- Cases and Liens do not currently have their own `WorkflowStageId` field governed by this model; they are advanced via a separate workflow service (the `WorkflowEndpoints.cs` flow service)
- Task status transitions (`todo/in_progress/done/cancelled`) are separate from stage transitions

### Enforcement Boundaries
- **Enforced**: Task `WorkflowStageId` changes in `LienTaskService.UpdateAsync`
- **Not enforced this phase**: Case/lien stage movement (no direct `WorkflowStageId` in those entities linked to `LienWorkflowStage.Id`)

---

## 3. Files Changed

### Backend — New Files
| File | Description |
|------|-------------|
| `Liens.Domain/Entities/LienWorkflowTransition.cs` | New domain entity |
| `Liens.Infrastructure/Persistence/Configurations/LienWorkflowTransitionConfiguration.cs` | EF mapping |
| `Liens.Infrastructure/Persistence/Migrations/20260418200000_AddWorkflowTransitions.cs` | DB migration |
| `Liens.Application/Interfaces/IWorkflowTransitionValidationService.cs` | Validation service contract |
| `Liens.Application/Services/WorkflowTransitionValidationService.cs` | Validation service impl |

### Backend — Modified Files
| File | Change |
|------|--------|
| `Liens.Domain/Entities/LienWorkflowConfig.cs` | Add `Transitions` nav, `AddTransition`, `DeactivateTransition` |
| `Liens.Infrastructure/Persistence/LiensDbContext.cs` | Add `DbSet<LienWorkflowTransition>` |
| `Liens.Application/Repositories/ILienWorkflowConfigRepository.cs` | Add transition repo methods |
| `Liens.Infrastructure/Repositories/LienWorkflowConfigRepository.cs` | Implement transition repo methods |
| `Liens.Application/DTOs/WorkflowConfigRequest.cs` | Add `AddWorkflowTransitionRequest`, `SaveWorkflowTransitionsRequest` |
| `Liens.Application/DTOs/WorkflowConfigResponse.cs` | Add `WorkflowTransitionResponse`, extend `WorkflowConfigResponse` |
| `Liens.Application/Interfaces/ILienWorkflowConfigService.cs` | Add transition methods |
| `Liens.Application/Services/LienWorkflowConfigService.cs` | Implement transition management + auto-init |
| `Liens.Application/Services/LienTaskService.cs` | Runtime validation on `WorkflowStageId` change |
| `Liens.Api/Endpoints/WorkflowConfigEndpoints.cs` | Add transition endpoints (tenant + admin) |
| `Liens.Infrastructure/DependencyInjection.cs` | Register new services |

### Frontend
| File | Change |
|------|--------|
| `apps/web/src/lib/liens/lien-workflow.types.ts` | Add transition types |
| `apps/web/src/lib/liens/lien-workflow.api.ts` | Add transition API calls |
| `apps/web/src/app/(platform)/lien/settings/workflow/page.tsx` | Transition editor section |
| `apps/web/src/app/(control-center)/control-center/liens/workflow/page.tsx` | Transition editor section |

---

## 4. Database / Schema Changes

### New Table: `liens_WorkflowTransitions`

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `char(36)` | PK, GUID |
| `WorkflowConfigId` | `char(36)` | FK → `liens_WorkflowConfigs` cascade delete |
| `FromStageId` | `char(36)` | FK → `liens_WorkflowStages` restrict |
| `ToStageId` | `char(36)` | FK → `liens_WorkflowStages` restrict |
| `IsActive` | `tinyint(1)` | Default true |
| `SortOrder` | `int` | Display ordering |
| `CreatedByUserId` | `char(36)` | Required |
| `UpdatedByUserId` | `char(36)` | Nullable |
| `CreatedAtUtc` | `datetime(6)` | Required |
| `UpdatedAtUtc` | `datetime(6)` | Required |

### Indexes
- `IX_WorkflowTransitions_WorkflowId_FromStage` on `(WorkflowConfigId, FromStageId)` — fast lookup of allowed targets
- `UX_WorkflowTransitions_Unique` unique on `(WorkflowConfigId, FromStageId, ToStageId)` — prevents duplicates

### Migration
`20260418200000_AddWorkflowTransitions`

### Default Initialization Strategy
**Service-level lazy initialization** — when `GetTransitionsAsync` is called for a workflow that has stages but zero transitions, the service auto-generates linear transitions (stage[0]→stage[1], stage[1]→stage[2], etc.) ordered by `StageOrder`, persists them, and returns them. This approach:
- Requires no migration seed data
- Works correctly for both existing and new tenants
- Is transparent to the caller
- Can be bypassed by explicitly saving an empty transition set

---

## 5. API Changes

### New Tenant Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/liens/workflow-config/{id}/transitions` | List transitions for a workflow |
| `POST` | `/api/liens/workflow-config/{id}/transitions` | Add a single transition |
| `DELETE` | `/api/liens/workflow-config/{id}/transitions/{transitionId}` | Deactivate a transition |
| `POST` | `/api/liens/workflow-config/{id}/transitions/save` | Batch save (replace all active transitions) |

### New Admin Endpoints (Control Center)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/liens/admin/workflow-config/tenants/{tenantId}/{id}/transitions` | Admin list |
| `POST` | `/api/liens/admin/workflow-config/tenants/{tenantId}/{id}/transitions` | Admin add |
| `DELETE` | `/api/liens/admin/workflow-config/tenants/{tenantId}/{id}/transitions/{transitionId}` | Admin deactivate |
| `POST` | `/api/liens/admin/workflow-config/tenants/{tenantId}/{id}/transitions/save` | Admin batch save |

### Existing `WorkflowConfigResponse` extended
`stages` array already present; `transitions` array added.

### Runtime change
`PUT /api/liens/tasks/{id}` now validates `WorkflowStageId` change against allowed transitions. Returns HTTP 422 if invalid.

---

## 6. UI Changes

### Tenant Workflow Settings (`/lien/settings/workflow`)
Added **Transition Rules** section below Stages:
- Per-source-stage, checkboxes for each destination stage
- Selecting/deselecting checkboxes calls `saveTransitions` API
- Disabled if fewer than 2 stages exist
- Shows current transition count in badge

### Control Center (`/control-center/liens/workflow`)
Same **Transition Rules** section added after the Stages block, mirrors tenant UI using admin API endpoints.

### Runtime UX (Task Stage Select)
- The task form (`create-edit-task-form.tsx`) already shows a stage selector; it is not gated by allowed transitions in the UI for the MVP (form shows all active stages)
- If an invalid transition is attempted, the backend blocks it and the UI shows the error toast
- Documented as known gap — future work can filter the stage dropdown by `GetAllowedNextStages`

---

## 7. Runtime Validation Behavior

### How it works
`WorkflowTransitionValidationService`:
```csharp
IsTransitionAllowed(workflowId, fromStageId, toStageId)
  → fetches active transitions for workflowId
  → checks if (fromStageId, toStageId) pair exists with IsActive=true

GetAllowedNextStages(workflowId, fromStageId)
  → returns all active ToStageId values for the given fromStageId
```

### Where enforced
- `LienTaskService.UpdateAsync` — if `WorkflowStageId` changes and:
  - The task currently has a stage (not null)
  - The new stage is different
  - The workflow has active transitions
  - Then: validates the move; throws `ValidationException` if invalid

### Where not enforced
- Initial task creation with a stage (no "from" stage to validate)
- Null → stage (first assignment; always allowed)
- Stage → null (removing from stage; always allowed)
- Case stage movement — not modeled via `LienWorkflowStage` in this service yet
- Lien stage movement — same; the generic workflow service is a separate system

### Error Behavior
```
ValidationException(422) — "Transition from 'In Progress' to 'To Do' is not allowed by the workflow configuration."
```

---

## 8. Permissions / Security

- Transition configuration uses existing `LiensPermissions.WorkflowManage` — no new permission
- Tenant isolation: all transition lookups filter by `WorkflowConfigId` which is already tenant-scoped
- Admin routes use `Policies.PlatformOrTenantAdmin` — same as existing stage admin routes
- No new auth surface introduced

---

## 9. Audit Integration

### Events Published

| Event | Trigger |
|-------|---------|
| `liens.workflow_transition.created` | `AddTransitionAsync` — single add |
| `liens.workflow_transition.deactivated` | `DeactivateTransitionAsync` |
| `liens.workflow_transition.saved` | `SaveTransitionsAsync` — batch replace |
| `liens.workflow_transition.initialized` | Auto-initialization of linear defaults |

### Payload fields
All events include: `tenantId`, `actorUserId`, `workflowId`, `entityId` (transitionId or configId for batch), `updateSource` in description.

### Runtime validation failures
Not logged to audit per spec guidance — keep minimal, avoid flooding.

---

## 10. Validation Results

| Check | Result |
|-------|--------|
| Backend builds — Application layer | ✅ |
| Backend builds — Infrastructure layer | ✅ |
| Backend builds — Api layer | ✅ |
| Frontend typechecks | ✅ |
| Migration applies | ✅ |
| Workflow loads with stages + transitions | ✅ |
| Transition can be added | ✅ |
| Transition can be deactivated | ✅ |
| Batch save replaces transitions | ✅ |
| Workflow version updates after transition change | ✅ |
| Default linear transitions generated for existing workflows | ✅ |
| Valid task stage move succeeds | ✅ |
| Invalid task stage move returns 422 | ✅ |
| Transition CRUD audit events emitted | ✅ |
| Admin (control center) and tenant surfaces use same API | ✅ |

---

## 11. Known Gaps / Risks

1. **Case/Lien stage enforcement not implemented** — Cases and Liens do not have `WorkflowStageId` linked to `LienWorkflowStage.Id` in this service. The generic Flow service governs those. Future work should route those stage changes through `IWorkflowTransitionValidationService`.

2. **Task stage selector not filtered** — The Create/Edit task form shows all active stages. Future work can query `GetAllowedNextStages` to filter the dropdown when editing an existing task.

3. **Self-transitions explicitly excluded** — Spec says "no self-transition unless justified." Validation service blocks `fromStageId == toStageId` even if accidentally stored.

4. **Background auto-initialization race** — If two requests arrive simultaneously for a workflow with no transitions, the lazy init could run twice. Service-level guard (`transitions.Count == 0`) is checked in-process but not distributed-locked. Low risk (idempotent UNIQUE index will reject duplicate, upsert logic handles it).

5. **Stale transition check on task update** — Task update validates the transition but does not re-check if the stage still belongs to the same workflow as the task's config. Future work should add this cross-reference.

---

## 12. Run Instructions

### Migration
```bash
cd apps/services/liens
dotnet ef database update --project Liens.Infrastructure --startup-project Liens.Api
```

### Build
```bash
# Backend
dotnet build apps/services/liens/Liens.Api/Liens.Api.csproj

# Frontend typecheck
cd apps/web && pnpm tsc --noEmit
```

### Manual Testing — Configuration
1. Load workflow config (GET `/api/liens/workflow-config/`)
2. Note `id` — POST to `/{id}/transitions` with `fromStageId` and `toStageId`
3. Confirm transition appears in GET response under `transitions`
4. DELETE `/{id}/transitions/{transitionId}` — confirm deactivated
5. Check workflow `version` incremented after each change

### Manual Testing — Runtime
1. Create a task with `workflowStageId = StageA`
2. Update task with `workflowStageId = StageB` where A→B is configured — should succeed
3. Update task with `workflowStageId = StageC` where A→C is NOT configured — should return 422

### Manual Testing — Default Init
1. Create workflow with 3 stages, no transitions
2. GET transitions — should auto-create and return: Stage1→Stage2, Stage2→Stage3
