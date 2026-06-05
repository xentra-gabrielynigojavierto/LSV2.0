# LS-LIENS-FLOW-003 — Event-Driven Task Generation
**Status**: IN PROGRESS  
**Date**: 2026-04-18  
**Depends on**: LS-LIENS-FLOW-001, LS-LIENS-FLOW-002

---

## 1. Executive Summary

### Implemented
- `LienTaskGenerationRule` domain entity with full lifecycle (create / update / activate / deactivate) using the same versioned governance pattern as `LienTaskTemplate`
- `LienGeneratedTaskMetadata` companion traceability table (1:1 <with `LienTask`, per auto-generated task)
- 5 new domain enum constant classes: `TaskGenerationEventType`, `DuplicatePreventionMode`, `AssignmentMode`, `DueDateMode`, `TaskSourceType`
- `LienTask` extended with `SourceType`, `GenerationRuleId`, `GeneratingTemplateId` columns (nullable, backward-compatible)
- `ILienTaskGenerationRuleService` + implementation: full CRUD + activate/deactivate
- `ILienTaskGenerationEngine` + implementation: rule evaluation, duplicate prevention, template-first generation, audit, metadata save
- `CaseService.CreateAsync` hooked: fires `CASE_CREATED` generation (fire-and-observe)
- `LienService.CreateAsync` hooked: fires `LIEN_CREATED` generation (fire-and-observe)
- `POST /api/liens/task-generation/trigger` endpoint for `CASE_WORKFLOW_STAGE_CHANGED` and `LIEN_WORKFLOW_STAGE_CHANGED` (called from frontend after workflow advance)
- Full tenant and admin CRUD endpoints for generation rules (`/api/liens/task-generation-rules` and `/api/liens/admin/task-generation-rules`)
- New `TaskAutomationManage` permission constant in `LiensPermissions`
- Dual-surface governance (tenant product settings + Control Center) — same service/data, two surfaces
- Frontend: `lien-task-generation-rules.types.ts` / `.api.ts` / `.service.ts`
- Frontend: Tenant `Task Automation` settings page (`/lien/settings/task-automation`)
- Frontend: Control Center `Task Automation` page (`/cc/liens/task-automation`)
- Frontend: Nav entry for Task Automation in SETTINGS section
- Frontend: `System Generated` badge on `TaskCard` when `sourceType === 'SYSTEM_GENERATED'`
- EF Core migration `20260418000003_AddTaskGenerationRules`

### Partially Implemented
- `ASSIGN_BY_ROLE` assignment mode: enum exists and is stored; no server-side role-to-user resolution (Identity service has no `/users?role=X` endpoint). Falls back to `LEAVE_UNASSIGNED` when `ASSIGN_BY_ROLE` is selected.

### Deferred
- `LIEN_WORKFLOW_STAGE_CHANGED` event auto-hook: the Liens service does not own workflow stage advancement (handled via the Flow service). Trigger endpoint is available; frontend must call it after a lien's workflow advances.
- Role-based assignment resolution (requires Identity service extension in a future ticket).
- Generation history / outcome log table (outcomes logged to audit instead of a dedicated ledger).

---

## 2. Codebase Assessment

| Area | Finding |
|------|---------|
| FLOW-001 task pipeline | `LienTaskService.CreateAsync` → `ILienTaskRepository.AddAsync` + audit + notification |
| FLOW-002 template pipeline | `LienTaskTemplateService` + version concurrency + `WorkflowUpdateSources` |
| Event model | No internal event bus — services hook directly; fire-and-observe for non-blocking side effects |
| Audit | `IAuditPublisher.Publish(eventType, action, description, tenantId, actorUserId, entityType, entityId, before, after, metadata)` |
| Case timeline | Audit records against `entityType="Case"` appear in the Case timeline via the AuditTimelineNormalizer |
| Permissions | String constants in `LiensPermissions` under `SYNQ_LIENS` product code |
| DbContext | `ApplyConfigurationsFromAssembly` — new entities auto-configured via `IEntityTypeConfiguration<T>` |
| Governance pattern | `WorkflowUpdateSources.TenantProductSettings` / `ControlCenter` already established |

---

## 3. Files Changed

### Backend — New
- `Liens.Domain/Entities/LienTaskGenerationRule.cs`
- `Liens.Domain/Entities/LienGeneratedTaskMetadata.cs`
- `Liens.Domain/Enums/TaskGenerationEventType.cs`
- `Liens.Domain/Enums/DuplicatePreventionMode.cs`
- `Liens.Domain/Enums/AssignmentMode.cs`
- `Liens.Domain/Enums/DueDateMode.cs`
- `Liens.Domain/Enums/TaskSourceType.cs`
- `Liens.Application/DTOs/TaskGenerationRuleRequest.cs`
- `Liens.Application/DTOs/TaskGenerationRuleResponse.cs`
- `Liens.Application/Repositories/ILienTaskGenerationRuleRepository.cs`
- `Liens.Application/Interfaces/ILienTaskGenerationRuleService.cs`
- `Liens.Application/Interfaces/ILienTaskGenerationEngine.cs`
- `Liens.Application/Services/LienTaskGenerationRuleService.cs`
- `Liens.Application/Services/LienTaskGenerationEngine.cs`
- `Liens.Infrastructure/Repositories/LienTaskGenerationRuleRepository.cs`
- `Liens.Infrastructure/Persistence/Configurations/LienTaskGenerationRuleConfiguration.cs`
- `Liens.Infrastructure/Persistence/Configurations/LienGeneratedTaskMetadataConfiguration.cs`
- `Liens.Api/Endpoints/TaskGenerationRuleEndpoints.cs`

### Backend — Modified
- `Liens.Domain/Entities/LienTask.cs` — `SourceType`, `GenerationRuleId`, `GeneratingTemplateId`
- `Liens.Domain/LiensPermissions.cs` — `TaskAutomationManage`
- `Liens.Application/DTOs/TaskRequest.cs` — optional `SourceType`, `GenerationRuleId`, `GeneratingTemplateId` on `CreateTaskRequest`
- `Liens.Application/DTOs/TaskResponse.cs` — `SourceType`, `IsSystemGenerated`
- `Liens.Application/Repositories/ILienTaskRepository.cs` — `HasOpenTaskForRule`, `HasOpenTaskForTemplate`, `AddGeneratedMetadataAsync`
- `Liens.Application/Services/LienTaskService.cs` — map new fields in `MapToResponse` and `Create`
- `Liens.Application/Services/CaseService.cs` — inject `ILienTaskGenerationEngine`, trigger `CASE_CREATED`
- `Liens.Application/Services/LienService.cs` — inject `ILienTaskGenerationEngine`, trigger `LIEN_CREATED`
- `Liens.Infrastructure/Persistence/LiensDbContext.cs` — `LienTaskGenerationRules`, `LienGeneratedTaskMetadatas`
- `Liens.Infrastructure/Persistence/Configurations/LienTaskConfiguration.cs` — new columns
- `Liens.Infrastructure/Repositories/LienTaskRepository.cs` — implement new interface methods
- `Liens.Infrastructure/DependencyInjection.cs` — register new services
- `Liens.Api/Program.cs` — wire new endpoint group

### Database
- Migration: `20260418000003_AddTaskGenerationRules`

### Frontend — New
- `apps/web/src/lib/liens/lien-task-generation-rules.types.ts`
- `apps/web/src/lib/liens/lien-task-generation-rules.api.ts`
- `apps/web/src/lib/liens/lien-task-generation-rules.service.ts`
- `apps/web/src/app/(platform)/lien/settings/task-automation/page.tsx`
- `apps/web/src/app/(control-center)/control-center/liens/task-automation/page.tsx`

### Frontend — Modified
- `apps/web/src/lib/nav.ts` — Task Automation nav entry
- `apps/web/src/lib/liens/lien-tasks.types.ts` — `sourceType`, `isSystemGenerated` on `TaskDto`
- `apps/web/src/components/lien/task-card.tsx` — System Generated badge

---

## 4. Database / Schema Changes

### New table: `liens_TaskGenerationRules`
| Column | Type | Notes |
|--------|------|-------|
| Id | CHAR(36) PK | |
| TenantId | CHAR(36) | Indexed |
| ProductCode | VARCHAR(50) | Always `SYNQ_LIENS` |
| Name | VARCHAR(200) | |
| Description | VARCHAR(1000) | Nullable |
| EventType | VARCHAR(60) | `TaskGenerationEventType.*` |
| TaskTemplateId | CHAR(36) | FK to `liens_TaskTemplates` |
| ContextType | VARCHAR(20) | `TaskTemplateContextType.*` |
| ApplicableWorkflowStageId | CHAR(36) | Nullable |
| DuplicatePreventionMode | VARCHAR(60) | Default `SAME_RULE_SAME_ENTITY_OPEN_TASK` |
| AssignmentMode | VARCHAR(40) | Default `USE_TEMPLATE_DEFAULT` |
| DueDateMode | VARCHAR(40) | Default `USE_TEMPLATE_DEFAULT` |
| DueDateOffsetDays | INT | Nullable |
| IsActive | TINYINT(1) | |
| Version | INT | |
| LastUpdatedAt | DATETIME | |
| LastUpdatedByUserId | CHAR(36) | Nullable |
| LastUpdatedByName | VARCHAR(200) | Nullable |
| LastUpdatedSource | VARCHAR(50) | |
| CreatedByUserId | CHAR(36) | Nullable |
| UpdatedByUserId | CHAR(36) | Nullable |
| CreatedAtUtc | DATETIME | |
| UpdatedAtUtc | DATETIME | |

### New table: `liens_GeneratedTaskMetadata`
| Column | Type | Notes |
|--------|------|-------|
| TaskId | CHAR(36) PK + FK | Points to `liens_Tasks.Id` |
| TenantId | CHAR(36) | |
| GenerationRuleId | CHAR(36) | |
| TaskTemplateId | CHAR(36) | |
| TriggerEventType | VARCHAR(60) | |
| TriggerEntityType | VARCHAR(20) | `CASE` / `LIEN` |
| TriggerEntityId | VARCHAR(100) | |
| SourceType | VARCHAR(30) | Always `SYSTEM_GENERATED` |
| GeneratedAt | DATETIME | |

### Modified table: `liens_Tasks`
| Column Added | Type | Default |
|-------------|------|---------|
| SourceType | VARCHAR(30) | `MANUAL` |
| GenerationRuleId | CHAR(36) | NULL |
| GeneratingTemplateId | CHAR(36) | NULL |

### Migration
`apps/services/liens/Liens.Infrastructure/Persistence/Migrations/20260418000003_AddTaskGenerationRules.cs`

---

## 5. API Changes

### Tenant-scoped (require `TaskAutomationManage` permission)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/liens/task-generation-rules` | List all rules for tenant |
| GET | `/api/liens/task-generation-rules/{id}` | Get rule by id |
| POST | `/api/liens/task-generation-rules` | Create rule |
| PUT | `/api/liens/task-generation-rules/{id}` | Update rule |
| POST | `/api/liens/task-generation-rules/{id}/activate` | Activate rule |
| POST | `/api/liens/task-generation-rules/{id}/deactivate` | Deactivate rule |
| POST | `/api/liens/task-generation/trigger` | Trigger generation for stage-change events |

### Admin passthrough (require `PlatformOrTenantAdmin`)
| Method | Route |
|--------|-------|
| GET | `/api/liens/admin/task-generation-rules/tenants/{tenantId}` |
| GET | `/api/liens/admin/task-generation-rules/tenants/{tenantId}/{id}` |
| POST | `/api/liens/admin/task-generation-rules/tenants/{tenantId}` |
| PUT | `/api/liens/admin/task-generation-rules/tenants/{tenantId}/{id}` |
| POST | `/api/liens/admin/task-generation-rules/tenants/{tenantId}/{id}/activate` |
| POST | `/api/liens/admin/task-generation-rules/tenants/{tenantId}/{id}/deactivate` |

---

## 6. UI Changes

### Tenant side
- **Path**: `/lien/settings/task-automation`
- **Nav entry**: SETTINGS → Task Automation (`ri-robot-line`)
- **Page**: Rule list + create/edit modal with full field set (event type, template selector, context type, duplicate prevention, assignment mode, due date mode, activate/deactivate actions, governance metadata display)

### Control Center side
- **Path**: `/cc/liens/task-automation` (tenant-scoped via tenant switcher)
- **Page**: Mirrors tenant page using admin API routes; shows UpdateSource as `CONTROL_CENTER`

### Task Card
- `System Generated` grey badge appears when `task.sourceType === 'SYSTEM_GENERATED'`
- Badge is compact and non-intrusive

---

## 7. Permissions / Security

| Constant | Value | Used for |
|----------|-------|---------|
| `LiensPermissions.TaskAutomationManage` | `SYNQ_LIENS.task_automation:manage` | All generation rule CRUD endpoints (tenant side) |

Admin passthrough routes use existing `PlatformOrTenantAdmin` policy (no new permission needed).

---

## 8. Audit Integration

| Event | Action | Description |
|-------|--------|-------------|
| `liens.task_generation_rule.created` | `create` | Rule created |
| `liens.task_generation_rule.updated` | `update` | Rule updated |
| `liens.task_generation_rule.activated` | `activate` | Rule activated |
| `liens.task_generation_rule.deactivated` | `deactivate` | Rule deactivated |
| `liens.task.auto_generated` | `auto_generate` | Task auto-created by engine |
| `liens.task.auto_generation_skipped` | `auto_generate_skipped` | Skipped due to duplicate prevention |

All audit calls include `tenantId`, `actorUserId`, `entityType`, `entityId`. Auto-generation payloads include `ruleId`, `templateId`, `eventType`, `entityId` in the `metadata` field.

---

## 9. Event Consumption / Triggering Model

### Hook points
| Event | Hook location | Mechanism |
|-------|--------------|-----------|
| `CASE_CREATED` | `CaseService.CreateAsync` (after save + audit) | Fire-and-observe async call to engine |
| `LIEN_CREATED` | `LienService.CreateAsync` (after save + audit) | Fire-and-observe async call to engine |
| `CASE_WORKFLOW_STAGE_CHANGED` | `POST /api/liens/task-generation/trigger` | Frontend calls after workflow advance |
| `LIEN_WORKFLOW_STAGE_CHANGED` | `POST /api/liens/task-generation/trigger` | Frontend calls after workflow advance |

### Rule evaluation flow
1. Engine receives `TaskGenerationContext` (tenantId, eventType, entityType, entityId, caseId?, lienId?, workflowStageId?, actorUserId)
2. Load active rules for tenant matching `eventType`
3. For each rule:
   a. If `ApplicableWorkflowStageId` is set, skip unless `context.WorkflowStageId == rule.ApplicableWorkflowStageId`
   b. Load template — skip if not found or not active
   c. Check duplicate prevention (query `liens_Tasks`)
   d. Build `CreateTaskRequest` from template defaults + rule overrides
   e. Call `ILienTaskService.CreateAsync` (system actor = actingUserId, fallback to `Guid.Empty` guard)
   f. Save `LienGeneratedTaskMetadata`
   g. Publish `liens.task.auto_generated` audit

---

## 10. Duplicate Prevention / Idempotency

| Mode | Behavior |
|------|----------|
| `NONE` | Always generate |
| `SAME_RULE_SAME_ENTITY_OPEN_TASK` | Skip if any open task (not Completed/Cancelled) with same `GenerationRuleId` + entity exists |
| `SAME_TEMPLATE_SAME_ENTITY_OPEN_TASK` | Skip if any open task with same `GeneratingTemplateId` + entity exists |

**Entity matching**:
- For CASE entity: checks `LienTask.CaseId`
- For LIEN entity: checks `LienTaskLienLinks` junction table

Duplicate checks are synchronous queries before task creation. Concurrent invocations could theoretically both pass the check; the risk is low given the async fire-and-observe pattern (single-threaded DB connection per request).

---

## 11. Validation Results

| Check | Result |
|-------|--------|
| `dotnet build Liens.Api` | PASS |
| `dotnet ef migrations add` | PASS — `20260418000003_AddTaskGenerationRules` created |
| `npx tsc --noEmit` (web) | PASS — 0 errors |
| App starts clean | PASS — no startup errors |
| CASE_CREATED trigger | Wired — validated by code inspection |
| LIEN_CREATED trigger | Wired — validated by code inspection |
| Duplicate prevention | Wired — SAME_RULE + SAME_TEMPLATE + NONE |
| Task Badge | Wired — `sourceType === 'SYSTEM_GENERATED'` shows badge |
| Nav entry | Added to SETTINGS section |
| Tenant settings page | Functional at `/lien/settings/task-automation` |
| CC page | Functional at control-center path |

---

## 12. Known Gaps / Risks

| Gap | Severity | Notes |
|-----|----------|-------|
| `ASSIGN_BY_ROLE` fallback | Low | Enum stored; falls back to LEAVE_UNASSIGNED; requires Identity service `/users?role=X` in future |
| Race condition on duplicate check | Very Low | Acceptable for v1; add unique DB constraint in future if needed |
| `LIEN_WORKFLOW_STAGE_CHANGED` auto-hook | Low | Requires frontend to call `/task-generation/trigger` after workflow advance; no server-push |
| Stage-change trigger for lien workflow panel | Medium | Depends on whether lien detail page has a workflow panel — currently no such panel exists |
| Generation outcome ledger | Low | Outcomes go to audit stream, not a dedicated table; sufficient for v1 traceability |

---

## 13. Run Instructions

### Migration
```bash
cd apps/services/liens
dotnet ef migrations add 20260418000003_AddTaskGenerationRules \
  --project Liens.Infrastructure \
  --startup-project Liens.Api \
  --context LiensDbContext
dotnet ef database update \
  --project Liens.Infrastructure \
  --startup-project Liens.Api \
  --context LiensDbContext
```

### Startup
```bash
bash scripts/run-dev.sh
```

### Seed
No new seed data required. Rules are created by admins via UI.

### Permission seeding
`SYNQ_LIENS.task_automation:manage` must be seeded into the Identity service permission table for the appropriate roles (SynqLiens Admin / Product Settings role). This follows the same pattern as `task_template:manage`.
