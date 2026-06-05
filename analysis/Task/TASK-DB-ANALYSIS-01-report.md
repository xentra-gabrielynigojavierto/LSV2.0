# TASK-DB-ANALYSIS-01 Report — Workflow vs Task Table Ownership

_Analysis date: 2026-04-21 | Status: COMPLETE_

---

## 1. Codebase / Schema Discovery

### Schema source locations

| Service | Schema source file | Migration directory |
|---------|-------------------|---------------------|
| Flow | `apps/services/flow/backend/src/Flow.Infrastructure/Persistence/FlowDbContext.cs` | `…/Flow.Infrastructure/Persistence/Migrations/` |
| Task | `apps/services/task/Task.Infrastructure/Persistence/TasksDbContext.cs` | `…/Task.Infrastructure/Persistence/Migrations/` |
| Liens | `apps/services/liens/Liens.Infrastructure/Persistence/LiensDbContext.cs` | `…/Liens.Infrastructure/Persistence/Migrations/` |

All three services use EF Core with MySQL. Schemas are defined through a combination of:
- `DbContext.OnModelCreating` (Flow, Liens) and `ApplyConfigurationsFromAssembly` (Task, Liens)
- Separate `IEntityTypeConfiguration<T>` classes in a `Configurations/` folder
- `DesignTimeDbContextFactory` for design-time tooling

### Migration history summary

**Flow migrations (13 total):**
| File | Description |
|------|-------------|
| `20260415223942_InitialCreate` | Initial Flow definition + stage + transition + task_items tables |
| `20260415225117_TaskEngineRefinement` | Task engine refinements |
| `20260416024819_WorkflowEngineFoundation` | Automation hooks + execution logs |
| `20260416041431_AddTransitionRulesJson` | Transition rules JSON blob |
| `20260416053042_AddNotifications` | Notifications table |
| `20260416054235_RenameTablesFlowPrefix` | All tables renamed to `flow_` prefix |
| `20260416054716_AddTenantId` | Tenant scoping added |
| `20260416063259_AddCanvasPositionToStages` | UI canvas metadata |
| `20260416194337_AddAutomationActions` | Automation action sub-table |
| `20260416195855_AddLogActionSnapshot` | Snapshot on execution logs |
| `20260416205848_AddActionConditionJson` | Condition JSON on actions |
| `20260416230823_AddActionRetryAndLogAttempts` | Retry + attempt count |
| `20260417013714_AddProductKey` | ProductKey column on definitions/hooks |
| `20260417031705_AddProductWorkflowMappingsP3` | Product-entity → workflow mapping table |
| `20260417034039_AddWorkflowInstancesP4` | Dedicated `flow_workflow_instances` table |
| `20260417042541_AddWorkflowInstanceExecutionStateP5` | Execution state columns on instance |
| `20260417152404_AddOutboxMessagesE10_2` | Transactional outbox |
| `20260417165304_AddWorkflowSlaE10_3` | SLA columns on instances |
| `20260417182606_AddWorkflowTaskE11_1` | First-class `flow_workflow_tasks` table |
| `20260417220000_AddTaskAssignmentModelE14_1` | Assignment model on workflow tasks |
| `20260418024120_AddWorkflowTaskSlaE10_3` | SLA columns on workflow tasks |

**Task migrations (6 total):**
| File | Description |
|------|-------------|
| `20260421000001_InitialCreate` | tasks_Tasks, tasks_Notes, tasks_History |
| `20260421000002_ExecutionEngine` | Reminders, StageConfigs |
| `20260421000003_PlatformIntegration` | GovernanceSettings, Templates, LinkedEntities |
| `20260421000004_LiensConsumerCutover` | Indexes for Liens migration support |
| `20260421000005_GenerationMetadataColumns` | GenerationRuleId + GeneratingTemplateId on tasks_Tasks |
| `20260421000006_LinkedEntityUniqueConstraint` | Unique index on LinkedEntities |

**Liens migrations (9 total):**
| File | Description |
|------|-------------|
| `20260414041807_InitialCreate` | Core domain tables (cases, liens, contacts, etc.) |
| `20260414144025_AddServicingItem` | Servicing items |
| `20260418000001_AddTasksAndWorkflowConfig` | liens_Tasks + liens_WorkflowConfigs + Stages |
| `20260418152345_AddTaskTemplates` | liens_TaskTemplates |
| `20260418161849_…AddTaskGenerationRules` | liens_TaskGenerationRules |
| `20260418164631_…AddTaskNotes` | liens_TaskNotes |
| `20260418172126_AddCaseNotes` | liens_CaseNotes |
| `20260418200000_AddWorkflowTransitions` | liens_WorkflowTransitions |
| `20260420000001_AddTaskGovernanceSettings` | liens_TaskGovernanceSettings |
| `20260420000002_AddTaskFlowLinkage` | WorkflowInstanceId + WorkflowStepKey on liens_Tasks |
| `20260421000001_LiensTaskRuntimeRemoval` | **DROPS** liens_Tasks, liens_TaskNotes, liens_TaskLienLinks (task runtime moved to Task service) |

---

## 2. Flow Tables Identified

All tables carry the `flow_` prefix. All are tenant-scoped (TenantId, with query filter on all except outbox).

| Table | EF Entity | Type | Purpose | Key Columns |
|-------|-----------|------|---------|-------------|
| `flow_definitions` | `FlowDefinition` | Config | Workflow definition / template; contains the step graph | Id, TenantId, Name, Version, Status, ProductKey |
| `flow_workflow_stages` | `WorkflowStage` | Config | Steps in a workflow definition; ordered graph nodes | Id, TenantId, WorkflowDefinitionId, Key (string), Name, MappedStatus, Order, CanvasPosition |
| `flow_workflow_transitions` | `WorkflowTransition` | Config | Allowed edges between stages in a definition; guards via RulesJson | Id, TenantId, WorkflowDefinitionId, FromStageId, ToStageId, RulesJson |
| `flow_automation_hooks` | `WorkflowAutomationHook` | Config | Trigger declarations: "on event E at transition T, run actions" | Id, TenantId, WorkflowDefinitionId, WorkflowTransitionId, TriggerEventType, ActionType, ProductKey |
| `flow_automation_actions` | `AutomationAction` | Config | Ordered action steps within a hook | Id, TenantId, HookId, ActionType, ConfigJson, ConditionJson, Order, RetryCount, StopOnFailure |
| `flow_automation_execution_logs` | `AutomationExecutionLog` | Runtime | Audit log for automation executions | Id, TenantId, TaskId (legacy), WorkflowAutomationHookId, ActionId, Status, Attempts, Message |
| `flow_notifications` | `Notification` | Runtime | In-system notifications generated by Flow engine events | Id, TenantId, Type, Title, Message, TargetUserId/Role/OrgId, Status, TaskId, WorkflowDefinitionId |
| `flow_product_workflow_mappings` | `ProductWorkflowMapping` | Integration | Maps external product entities (case, lien) to their workflow instance | Id, TenantId, ProductKey, SourceEntityType, SourceEntityId, WorkflowDefinitionId, WorkflowInstanceId, Status, CorrelationKey |
| `flow_workflow_instances` | `WorkflowInstance` | Runtime | Live execution grain; tracks current step, SLA, lifecycle state | Id, TenantId, WorkflowDefinitionId, ProductKey, Status, CurrentStageId, CurrentStepKey, AssignedToUserId, DueAt, SlaStatus, EscalationLevel |
| `flow_outbox_messages` | `OutboxMessage` | Infrastructure | Transactional outbox; durable side-effect delivery (no tenant query filter) | Id, TenantId, EventType, Status, PayloadJson, AttemptCount, NextAttemptAt, WorkflowInstanceId |
| `flow_workflow_tasks` | `WorkflowTask` | Work item | First-class human work item created by Flow for a workflow step (E11.1) | Id, TenantId, WorkflowInstanceId, StepKey, Title, Status, Priority, AssignedUserId/Role/OrgId, AssignmentMode, DueAt, SlaStatus, MetadataJson |
| `flow_task_items` | `TaskItem` | Legacy | Pre-P4 surrogate work item living at the definition layer | Id, TenantId, Title, Status, ProductKey, FlowDefinitionId, WorkflowStageId, AssignedToUserId, DueDate |

### Key observations about Flow tables

- `flow_definitions`, `flow_workflow_stages`, `flow_workflow_transitions` are **definition-layer config** — they answer "what is the orchestration model?"
- `flow_workflow_instances` is the **runtime execution grain** — it answers "where is this process right now?"
- `flow_workflow_tasks` is the **work-item layer** — it answers "what human action does this step require?"
- `flow_task_items` is a **legacy surrogate** — superseded by `WorkflowInstance` (P4) and `WorkflowTask` (E11.1)
- `flow_product_workflow_mappings` is an **integration table** — it links external domain entities to their workflow instance
- `flow_outbox_messages` is **pure infrastructure** — durable delivery, internal to Flow

---

## 3. Liens Task / Workflow Tables Identified

After migration `LiensTaskRuntimeRemoval` (TASK-B04), the following task runtime tables were **dropped** from Liens: `liens_Tasks`, `liens_TaskNotes`, `liens_TaskLienLinks`. These are now owned by the Task service.

The tables that **remain** in Liens after TASK-B04:

| Table | EF Entity | Type | Purpose | Key Columns |
|-------|-----------|------|---------|-------------|
| `liens_WorkflowConfigs` | `LienWorkflowConfig` | Config | Liens-product task board configuration; defines the "workflow" for task progression on the My Tasks UI | Id, TenantId, ProductCode, WorkflowName, Version, IsActive |
| `liens_WorkflowStages` | `LienWorkflowStage` | Config | Stages within a Liens workflow config (kanban columns) | Id, WorkflowConfigId, StageName, StageOrder, IsActive, DefaultOwnerRole, SlaMetadata |
| `liens_WorkflowTransitions` | `LienWorkflowTransition` | Config | Allowed stage-to-stage moves in a Liens workflow config | Id, WorkflowConfigId, FromStageId, ToStageId, IsActive, SortOrder |
| `liens_TaskTemplates` | `LienTaskTemplate` | Config | Task templates for automated generation in Liens | Id, TenantId, ProductCode, Name, DefaultTitle, DefaultDescription, DefaultPriority, DefaultDueOffsetDays, ApplicableWorkflowStageId, ContextType, IsActive |
| `liens_TaskGenerationRules` | `LienTaskGenerationRule` | Config | Rules that define when + how to auto-generate tasks in Liens | Id, TenantId, ProductCode, EventType, TaskTemplateId, ContextType, ApplicableWorkflowStageId, DuplicatePreventionMode, AssignmentMode, DueDateMode, IsActive |
| `liens_GeneratedTaskMetadata` | `LienGeneratedTaskMetadata` | Runtime | Duplicate-prevention provenance keyed by the canonical Task Id | TaskId (PK), TenantId, GenerationRuleId, TaskTemplateId, TriggerEventType/EntityType/EntityId, SourceType, GeneratedAt |
| `liens_TaskGovernanceSettings` | `LienTaskGovernanceSettings` | Config | Governance rules for task creation/management in Liens | Id, TenantId, ProductCode, RequireAssigneeOnCreate, RequireCaseLinkOnCreate, AllowMultipleAssignees, RequireWorkflowStageOnCreate, DefaultStartStageMode |

### Key observations about Liens tables

- `liens_WorkflowConfigs/Stages/Transitions` form a **task-board stage model** for Liens — they are NOT process orchestration; they define how tasks progress visually in the Liens task UI.
- `liens_TaskTemplates` and `liens_TaskGenerationRules` are **task generation config** — Liens-product-specific rules for when and how tasks are auto-created.
- `liens_TaskGovernanceSettings` is **task governance config** — rules about what's required when creating/managing tasks in Liens.
- `liens_GeneratedTaskMetadata` is a **transitional runtime table** — it stores generation provenance for dedup. The Task service's `tasks_Tasks` now has `GenerationRuleId` and `GeneratingTemplateId` columns, making this largely superseded.

---

## 4. Task Service Tables Identified

All tables carry the `tasks_` prefix. All are tenant-scoped.

| Table | EF Entity | Type | Purpose | Key Columns |
|-------|-----------|------|---------|-------------|
| `tasks_Tasks` | `PlatformTask` | Runtime | The canonical platform-wide task runtime record | Id, TenantId, Title, Description, Status, Priority, Scope, AssignedUserId, SourceProductCode, SourceEntityType, SourceEntityId, GenerationRuleId, GeneratingTemplateId, CurrentStageId, WorkflowInstanceId, WorkflowStepKey, WorkflowLinkageChangedAt, DueAt, CompletedAt |
| `tasks_Notes` | `TaskNote` | Runtime | Notes added to a task (soft-delete) | Id, TaskId, TenantId, Note, AuthorName, IsEdited, IsDeleted |
| `tasks_History` | `TaskHistory` | Runtime | Audit trail for task lifecycle events | Id, TaskId, TenantId, Action, Details, PerformedByUserId, CreatedAtUtc |
| `tasks_StageConfigs` | `TaskStageConfig` | Config | Generic task stage definitions, scoped per (TenantId, SourceProductCode) | Id, TenantId, SourceProductCode, Code, Name, DisplayOrder, IsActive |
| `tasks_GovernanceSettings` | `TaskGovernanceSettings` | Config | Generic governance rules for task lifecycle, scoped per (TenantId, SourceProductCode) | Id, TenantId, SourceProductCode, RequireAssignee, RequireDueDate, RequireStage, AllowUnassign, AllowCancel, DefaultPriority, DefaultTaskScope |
| `tasks_Templates` | `TaskTemplate` | Config | Generic task templates for programmatic task creation, scoped per (TenantId, SourceProductCode) | Id, TenantId, SourceProductCode, Code, Name, DefaultTitle, DefaultDescription, DefaultPriority, DefaultScope, DefaultDueInDays, DefaultStageId, IsActive |
| `tasks_Reminders` | `TaskReminder` | Runtime | Scheduled notification reminders tied to a task | Id, TaskId, TenantId, ReminderType, RemindAt, Status, LastAttemptAt, SentAt, FailureReason |
| `tasks_LinkedEntities` | `TaskLinkedEntity` | Runtime | Many-to-many links from a task to external domain entities | Id, TaskId, TenantId, SourceProductCode, EntityType, EntityId, RelationshipType |

### Key observations about Task service tables

- The Task service was built as the **canonical, product-agnostic** task platform. All runtime tables are already present.
- Config tables (`tasks_StageConfigs`, `tasks_GovernanceSettings`, `tasks_Templates`) are all scoped by `(TenantId, SourceProductCode)` — they serve as the generic host for what each product currently holds in its own `links_*` tables.
- `tasks_Tasks` carries both Flow linkage fields (`WorkflowInstanceId`, `WorkflowStepKey`) as **reference-only soft links** — no FK, because task outlives the orchestration step.
- `tasks_LinkedEntities` with its unique constraint (`UX_LinkedEntities_TaskId_EntityType_EntityId`) serves as the generic lien-link replacement.

---

## 5. Ownership Classification Matrix

> Decision rule:
> - Answers "what work should a person do / how is that work managed?" → **TASK**
> - Answers "where is the process instance in its orchestration lifecycle?" → **FLOW**

| Table / Model | Current Service | Category | Recommended Owner | Rationale | Confidence |
|---------------|----------------|----------|------------------|-----------|------------|
| `flow_definitions` | Flow | Config | **STAY IN FLOW** | Orchestration definition; describes the process model | High |
| `flow_workflow_stages` | Flow | Config | **STAY IN FLOW** | Defines orchestration step graph; tightly coupled to engine | High |
| `flow_workflow_transitions` | Flow | Config | **STAY IN FLOW** | Defines legal orchestration state transitions; engine-owned | High |
| `flow_automation_hooks` | Flow | Config | **STAY IN FLOW** | Automation triggers within orchestration definitions | High |
| `flow_automation_actions` | Flow | Config | **STAY IN FLOW** | Action sub-steps within automation hooks | High |
| `flow_automation_execution_logs` | Flow | Runtime | **STAY IN FLOW** | Execution audit for orchestration automation | High |
| `flow_notifications` | Flow | Runtime | **STAY IN FLOW** | Flow-generated system notifications; tightly coupled to Flow events | Medium (could move to a dedicated Notifications service) |
| `flow_product_workflow_mappings` | Flow | Integration | **STAY IN FLOW** | Maps external entities to workflow instances; orchestration metadata | High |
| `flow_workflow_instances` | Flow | Runtime | **STAY IN FLOW** | The execution grain; "where is the process right now?" | High |
| `flow_outbox_messages` | Flow | Infrastructure | **STAY IN FLOW** | Durable side-effect delivery for Flow engine | High |
| `flow_workflow_tasks` | Flow | Work item | **MOVE TO TASK** (future) | Answers "what human work exists for this step?" — work management, not orchestration state | Medium (significant effort; see §6) |
| `flow_task_items` | Flow | Legacy | **DEPRECATED** | Superseded by WorkflowInstance (P4) + WorkflowTask (E11.1); decommission after WorkflowTask migration | High |
| `liens_WorkflowConfigs` | Liens | Config | **MOVE TO TASK** | Task board stage configuration — answers "how are tasks organized?", not "where is the process?" Exact generic equivalent: `tasks_StageConfigs` (parent wrapper) | High |
| `liens_WorkflowStages` | Liens | Config | **MOVE TO TASK** | Task kanban stage definitions — work management config. Generic equivalent is `tasks_StageConfigs` rows | High |
| `liens_WorkflowTransitions` | Liens | Config | **MOVE TO TASK** (partial) | Allowed stage moves for tasks — work management config. Task service doesn't yet have a transitions table; must be added | High (data belongs in Task) / Medium (implementation gap) |
| `liens_TaskTemplates` | Liens | Config | **MOVE TO TASK** | Template definitions for task generation. Generic equivalent: `tasks_Templates`. Data should be migrated | High |
| `liens_TaskGenerationRules` | Liens | Config | **KEEP IN LIENS** (near-term) | Event-driven generation rules are Liens-domain-specific (EventType values like `lien.filed`). No generic equivalent yet in Task service | Medium (can generalize later) |
| `liens_GeneratedTaskMetadata` | Liens | Runtime | **DEPRECATED** | Superseded by `GenerationRuleId` + `GeneratingTemplateId` on `tasks_Tasks` (added TASK-B04-01). Duplicate-prevention queries now served by Task service | High |
| `liens_TaskGovernanceSettings` | Liens | Config | **MOVE TO TASK** | Governance settings for task lifecycle — identical purpose to `tasks_GovernanceSettings`. Data should be migrated | High |
| `tasks_Tasks` | Task | Runtime | **STAY IN TASK** (authoritative) | The canonical task runtime | High |
| `tasks_Notes` | Task | Runtime | **STAY IN TASK** | Task notes | High |
| `tasks_History` | Task | Runtime | **STAY IN TASK** | Task audit trail | High |
| `tasks_StageConfigs` | Task | Config | **STAY IN TASK** | Generic stage config host | High |
| `tasks_GovernanceSettings` | Task | Config | **STAY IN TASK** | Generic governance config host | High |
| `tasks_Templates` | Task | Config | **STAY IN TASK** | Generic template host | High |
| `tasks_Reminders` | Task | Runtime | **STAY IN TASK** | Task reminder scheduling | High |
| `tasks_LinkedEntities` | Task | Runtime | **STAY IN TASK** | Generic entity linking (replaces liens_TaskLienLinks) | High |

---

## 6. Tables That Should Move to Task

### 6a. `liens_WorkflowConfigs` + `liens_WorkflowStages`

**Current location:** Liens (`liens_WorkflowConfigs`, `liens_WorkflowStages`)

**Why it belongs in Task:**
- These tables define how tasks are organized into visual stages (kanban columns). This is "work management configuration" not "process orchestration".
- `liens_WorkflowConfigs` has a `ProductCode` key (per-product, per-tenant config) — identical pattern to `tasks_GovernanceSettings` and `tasks_StageConfigs`.
- `liens_WorkflowStages` carries fields like `DefaultOwnerRole`, `SlaMetadata` — all task-work-management attributes.
- The Task service's `tasks_StageConfigs` (with `SourceProductCode`) is the direct generic equivalent.

**Migration approach:**
- Each `LienWorkflowStage` row maps to a `TaskStageConfig` row with `SourceProductCode = "SYNQ_LIENS"`.
- The `LienWorkflowConfig` wrapper (one per tenant/product) is implicit in `tasks_StageConfigs` via the `(TenantId, SourceProductCode)` group — no separate config-wrapper table needed.
- After data migration, `liens_WorkflowConfigs` and `liens_WorkflowStages` can be dropped.

**Generic vs product-specific:**
- The stage concept is fully generic (already exists in Task). The Liens-specific `DefaultOwnerRole` and `SlaMetadata` fields would need to be added to `tasks_StageConfigs` or stored in a metadata JSON column.

### 6b. `liens_WorkflowTransitions`

**Current location:** Liens (`liens_WorkflowTransitions`)

**Why it belongs in Task:**
- Allowed stage transitions for tasks ("from stage A, you can move to B or C") is work management UI/UX configuration, not orchestration.
- `liens_WorkflowTransitions` enforces the kanban movement rules.

**Migration approach:**
- The Task service currently has no transitions table. A `tasks_StageTransitions` table would need to be added before migration.
- Each `LienWorkflowTransition` row maps to a `TaskStageTransition` row.

**Gap:** Task service must add a `tasks_StageTransitions` table (migration required) before this can move.

### 6c. `liens_TaskTemplates`

**Current location:** Liens (`liens_TaskTemplates`)

**Why it belongs in Task:**
- Task templates are "blueprint for work to be created" — canonical work management config.
- The Task service already has `tasks_Templates` as the direct generic equivalent.
- Schema is nearly identical: both have `DefaultTitle`, `DefaultPriority`, `DefaultDueOffset`/`DefaultDueInDays`.

**Differences to reconcile:**
- `liens_TaskTemplates` has `ContextType` (CASE / LIEN), `ApplicableWorkflowStageId`, `DefaultRoleId` — Liens-specific fields.
- `tasks_Templates` has `Code`, `DefaultScope`, `DefaultStageId`, `Version`.
- Migration requires either: (a) adding Liens-specific fields to `tasks_Templates` as optional columns, or (b) storing them in a metadata JSON extension.

**Data migration:** Each `LienTaskTemplate` row maps to a `TaskTemplate` row with `SourceProductCode = "SYNQ_LIENS"`.

### 6d. `liens_TaskGovernanceSettings`

**Current location:** Liens (`liens_TaskGovernanceSettings`)

**Why it belongs in Task:**
- Governance settings control what's required when creating and managing tasks — pure work management policy.
- Task service already has `tasks_GovernanceSettings` as the generic equivalent.

**Schema differences:**
- `liens_TaskGovernanceSettings` has `RequireCaseLinkOnCreate`, `RequireWorkflowStageOnCreate`, `AllowMultipleAssignees`, `DefaultStartStageMode` — Liens-specific fields.
- `tasks_GovernanceSettings` has `RequireAssignee`, `RequireDueDate`, `RequireStage`, `AllowUnassign`, `AllowCancel`, `AllowCompleteWithoutStage`, `DefaultPriority`, `DefaultTaskScope`.
- Partially overlapping. `RequireWorkflowStageOnCreate` maps to `RequireStage`. `RequireCaseLinkOnCreate` has no generic equivalent yet.

**Data migration:** One `LienTaskGovernanceSettings` row maps to one `TaskGovernanceSettings` row per tenant with `SourceProductCode = "SYNQ_LIENS"`. Liens-only fields require either generic additions or a product-settings extension mechanism.

### 6e. `flow_workflow_tasks` (future)

**Current location:** Flow (`flow_workflow_tasks`)

**Why it belongs in Task (long-term):**
- `flow_workflow_tasks` answers "what human action is required at this workflow step?" — classic work management.
- The entity carries: Title, Description, Status, Priority, AssignedUserId/Role/OrgId, AssignmentMode, DueAt, SlaStatus — identical concerns to `tasks_Tasks`.
- The Task service's `PlatformTask` already has `WorkflowInstanceId` and `WorkflowStepKey` linkage fields — it was designed to host flow-originated work items.

**Why it is NOT ready to move yet:**
- `flow_workflow_tasks` is created and lifecycle-managed by the Flow engine. The move requires the Flow engine to call the Task service API instead of writing to its own DB — a significant coupling change.
- E11.2+ phases (claim, reassign, progress) have not yet been wired to the Task service.
- The Task service currently has no `AssignmentMode` equivalent (user/role/org assignment model).

**Recommendation:** Defer this move to a dedicated TASK-DB-MIGRATION phase. The architecture is already positioned for it (Task has the linkage fields; Flow has HTTP client patterns from TASK-B04).

---

## 7. Tables That Should Stay in Flow

| Table | Reason |
|-------|--------|
| `flow_definitions` | The orchestration schema itself — defines "what is the process model". Tightly coupled to WorkflowEngine, WorkflowStage, WorkflowTransition resolution. Moving it to Task would mean Task owns process definitions, which violates the boundary. |
| `flow_workflow_stages` | Process step graph nodes within a definition. The engine resolves `CurrentStepKey` against these rows. They are orchestration config, not task config. Note: these are distinct from `tasks_StageConfigs` — Flow stages are orchestration steps; Task stages are kanban UI columns for work tracking. |
| `flow_workflow_transitions` | Legal state transitions in the orchestration graph. The engine validates every `Advance()` call against these rows. Pure orchestration logic. |
| `flow_automation_hooks` / `flow_automation_actions` | Automation triggers wired to specific transitions in specific workflow definitions. They answer "what happens automatically when the process moves?" — orchestration automation, not human work management. |
| `flow_automation_execution_logs` | Execution audit for automation. Internal to Flow. No value in Task service. |
| `flow_notifications` | Generated by Flow events. Tightly coupled to Flow's event model. (Future: could move to a dedicated Notification service.) |
| `flow_product_workflow_mappings` | Maps external domain entities (case, lien) to their `WorkflowInstance`. This is the integration table that answers "which process instance is associated with this entity?" — orchestration metadata. |
| `flow_workflow_instances` | The execution state of a running process: CurrentStepKey, SlaStatus, EscalationLevel, StartedAt, CompletedAt. This is the heart of "where is the process?" and must remain in Flow. |
| `flow_outbox_messages` | Infrastructure for durable side-effect delivery. Internal to the Flow engine. |

### Why `flow_workflow_stages` ≠ `tasks_StageConfigs`

This is the critical distinction that must be preserved:

| Dimension | `flow_workflow_stages` | `tasks_StageConfigs` (`liens_WorkflowStages`) |
|-----------|----------------------|----------------------------------------------|
| Belongs to | A `FlowDefinition` | A product code + tenant combination |
| Purpose | Orchestration graph node; the engine advances `WorkflowInstance.CurrentStepKey` through these | Kanban column config for the task board UI |
| Mutability | Versioned with the definition; changing stages changes the process | Changeable by admins without affecting process execution |
| FK dependencies | Referenced by `WorkflowInstance.CurrentStageId`, `WorkflowTransition` | Referenced by `PlatformTask.CurrentStageId` (optional) |
| Movement | Stays in Flow | Moves to Task |

---

## 8. Tables That Should Be Referenced Only

### 8a. `flow_workflow_instances` → referenced from `tasks_Tasks`

**What Task stores locally:** `WorkflowInstanceId` (Guid, nullable, no FK), `WorkflowStepKey` (string, nullable), `WorkflowLinkageChangedAt` (timestamp).

**What stays external:** The actual `WorkflowInstance` row, its `CurrentStepKey`, `Status`, `DueAt`, `SlaStatus` — all remain in Flow.

**Relationship:** One-way reference from Task → Flow. Task stores the ID so it can display "this task is part of workflow X" without cross-service joins. The flow engine does not store a FK back to `tasks_Tasks` (it uses `flow_product_workflow_mappings` instead).

**What must NOT be synchronized locally:** The instance execution state (CurrentStepKey, SlaStatus, EscalationLevel). Task service must never cache or replicate these; callers must query Flow if they need real-time orchestration state.

### 8b. `flow_workflow_tasks.WorkflowInstanceId` → references `flow_workflow_instances`

`WorkflowTask` already holds a FK to `WorkflowInstance` within Flow. This is an internal Flow relationship and should remain so until `WorkflowTask` migrates to Task.

### 8c. `liens_TaskGenerationRules` → referenced by `liens_GeneratedTaskMetadata` + `tasks_Tasks`

**What Task stores locally:** `GenerationRuleId` and `GeneratingTemplateId` on `tasks_Tasks` rows (added TASK-B04-01) — provenance only, no FK.

**What stays external:** The rule definition, event binding, and template configuration remain in Liens.

**Relationship:** Task stores the ID as provenance/dedup metadata. Liens calls the Task service API to check for duplicates; Task never calls Liens to resolve rule definitions.

### 8d. `flow_workflow_stages` → referenced by `tasks_Tasks.CurrentStageId` (optional)

`tasks_Tasks.CurrentStageId` stores a Guid that **currently** references `tasks_StageConfigs.Id` (Task's own stage config), not `flow_workflow_stages.Id`. These are separate ID spaces. This reference is internal to the Task service. If a task is also bound to a workflow step, that is captured via `WorkflowStepKey` (string), not via the stage FK.

---

## 9. Risks / Ambiguities

| # | Area | Ambiguity | Recommendation |
|---|------|-----------|----------------|
| R-01 | `liens_WorkflowStages.ApplicableWorkflowStageId` in templates/rules | `LienTaskTemplate` and `LienTaskGenerationRule` carry `ApplicableWorkflowStageId` — this references `liens_WorkflowStages.Id`. When stages move to `tasks_StageConfigs`, these FKs need to be repointed. | Resolve during template/rule migration: map old stage IDs to new `tasks_StageConfigs` IDs. |
| R-02 | `liens_WorkflowConfigs` has no direct equivalent in Task service | The `LienWorkflowConfig` is a parent wrapper for stages (one per tenant/product). Task service's `tasks_StageConfigs` groups implicitly by `(TenantId, SourceProductCode)` — no wrapper row exists. | Either drop the wrapper (stages are sufficient) or add a `tasks_StageGroups` config table. Defer the decision; dropping the wrapper is simpler. |
| R-03 | `liens_TaskGovernanceSettings` fields with no generic Task equivalent | `RequireCaseLinkOnCreate`, `AllowMultipleAssignees`, `DefaultStartStageMode` — Liens-specific governance fields. | Add as nullable columns to `tasks_GovernanceSettings` with a TASK-migration, populated only when `SourceProductCode = "SYNQ_LIENS"`. |
| R-04 | `flow_workflow_tasks` overlaps conceptually with `tasks_Tasks` | Both are "work for a person". However, `flow_workflow_tasks` is created and managed by the orchestration engine; `tasks_Tasks` is created by applications/users. | Keep separate until the Flow→Task API integration is designed. The migration is complex (Flow engine must become a client of Task service). |
| R-05 | `flow_task_items` (legacy TaskItem) still referenced by `flow_workflow_instances.InitialTaskId` | The `WorkflowInstance.InitialTask` navigation property holds a FK to `flow_task_items`. | Deprecation path: null out `InitialTaskId` for all new instances (use `WorkflowTask` instead), then drop FK and table. Do NOT migrate `flow_task_items` to Task — they are legacy orchestration surrogates, not user tasks. |
| R-06 | `liens_GeneratedTaskMetadata` and `tasks_Tasks.GenerationRuleId` dual-tracking | Both store generation provenance. `tasks_Tasks` has `GenerationRuleId` + `GeneratingTemplateId` at the row level; `liens_GeneratedTaskMetadata` stores them separately keyed by TaskId. | Treat `liens_GeneratedTaskMetadata` as transitional; deprecate after verifying Task service serves all dedup queries. Do NOT migrate to Task — provenance is already on `tasks_Tasks`. |
| R-07 | `liens_WorkflowTransitions` has no Task service equivalent | Task service has no stage transitions table. Moving `liens_WorkflowTransitions` requires schema work in Task first. | **Defer** — add `tasks_StageTransitions` table in a future TASK migration before moving this data. |
| R-08 | `liens_TaskGenerationRules` references Liens-domain event types | The `EventType` values (e.g. `lien.filed`, `case.opened`) are Liens-domain concepts. A generic Task generation-rules engine would need its own event abstraction. | **Keep in Liens** for now. Generalize only when a second product needs rule-based task generation. |

---

## 10. Recommended Migration Sequence

### Phase 1 — Low risk (data migration only, no schema gaps in Task)

**P1-A: Migrate `liens_TaskGovernanceSettings` → `tasks_GovernanceSettings`**
- Prerequisite: Add Liens-specific nullable columns to `tasks_GovernanceSettings` (or accept loss of Liens-only fields on first pass).
- Risk: Low — Task service already has the table. One row per tenant/product.
- Action: Write a one-off migration script; update Liens application to read governance from Task service API instead of its own DB; drop `liens_TaskGovernanceSettings`.

**P1-B: Migrate `liens_TaskTemplates` → `tasks_Templates`**
- Prerequisite: Reconcile schema differences (`ContextType`, `ApplicableWorkflowStageId`, `DefaultRoleId` have no equivalent). Add nullable extension columns or use a JSON metadata bag.
- Risk: Low — Task service already has the table.
- Action: Data migration script; update Liens to call Task template API; drop `liens_TaskTemplates`.

### Phase 2 — Medium risk (requires schema addition in Task)

**P2-A: Add `tasks_StageTransitions` table to Task service**
- Add migration to create the table.
- Define the entity configuration and repository.

**P2-B: Migrate `liens_WorkflowStages` → `tasks_StageConfigs`**
- After P2-A preparation.
- Each `LienWorkflowStage` → one `TaskStageConfig` row with `SourceProductCode = "SYNQ_LIENS"`.
- Update `ApplicableWorkflowStageId` FK references in templates/rules to point to new `tasks_StageConfigs.Id`.
- Drop `liens_WorkflowStages` and `liens_WorkflowConfigs`.

**P2-C: Migrate `liens_WorkflowTransitions` → `tasks_StageTransitions`**
- After P2-A and P2-B.
- Drop `liens_WorkflowTransitions`.

### Phase 3 — Deferred (significant architectural work)

**P3-A: Deprecate `liens_GeneratedTaskMetadata`**
- Verify all dedup queries are served by `tasks_Tasks.GenerationRuleId` + Task service HTTP API.
- Drop `liens_GeneratedTaskMetadata` after a safe observation period.

**P3-B: Deprecate `flow_task_items`**
- Null out all `InitialTaskId` references on new `WorkflowInstance` rows.
- Verify no active code paths write to `flow_task_items`.
- Drop FK from `flow_workflow_instances.InitialTaskId`, then drop table.

**P3-C: Migrate `flow_workflow_tasks` → `tasks_Tasks` (long-term)**
- Requires Flow engine to become a client of the Task service API.
- Requires `tasks_Tasks` to gain `AssignmentMode` and role/org assignment columns.
- Very high risk; must be scoped as its own TASK-FLOW-CONVERGENCE work stream.

### What must NEVER move

- `flow_definitions`, `flow_workflow_stages`, `flow_workflow_transitions` — these ARE the orchestration model.
- `flow_workflow_instances` — this IS the runtime execution state.
- `flow_product_workflow_mappings` — this IS the integration linkage.
- `flow_outbox_messages` — internal infrastructure.

---

## 11. Final Recommendation Summary

### Classification table

| Current Table / Model | Current Service | Recommended Disposition | Future Owner | Why |
|-----------------------|----------------|------------------------|-------------|-----|
| `flow_definitions` | Flow | STAY | Flow | Orchestration schema definition |
| `flow_workflow_stages` | Flow | STAY | Flow | Orchestration step graph |
| `flow_workflow_transitions` | Flow | STAY | Flow | Legal orchestration transitions |
| `flow_automation_hooks` | Flow | STAY | Flow | Orchestration automation triggers |
| `flow_automation_actions` | Flow | STAY | Flow | Orchestration automation steps |
| `flow_automation_execution_logs` | Flow | STAY | Flow | Orchestration execution audit |
| `flow_notifications` | Flow | STAY | Flow (or Notifications service) | Flow-event notifications |
| `flow_product_workflow_mappings` | Flow | STAY | Flow | Entity→instance integration mapping |
| `flow_workflow_instances` | Flow | STAY | Flow | The execution runtime grain |
| `flow_outbox_messages` | Flow | STAY | Flow | Durable side-effect infrastructure |
| `flow_workflow_tasks` | Flow | MOVE TO TASK (future, P3-C) | Task | Work items are work management, not orchestration state |
| `flow_task_items` | Flow | DEPRECATE (P3-B) | — | Legacy surrogate superseded by WorkflowInstance + WorkflowTask |
| `liens_WorkflowConfigs` | Liens | MOVE TO TASK (P2-B) | Task | Task board config wrapper — implicit in Task's (tenant, product) grouping |
| `liens_WorkflowStages` | Liens | MOVE TO TASK (P2-B) | Task → `tasks_StageConfigs` | Task kanban stage config, not orchestration |
| `liens_WorkflowTransitions` | Liens | MOVE TO TASK (P2-C) | Task → `tasks_StageTransitions` (new) | Allowed task-stage moves, not orchestration transitions |
| `liens_TaskTemplates` | Liens | MOVE TO TASK (P1-B) | Task → `tasks_Templates` | Generic task template |
| `liens_TaskGenerationRules` | Liens | KEEP IN LIENS | Liens | Event types are Liens-domain-specific; no generic engine yet |
| `liens_GeneratedTaskMetadata` | Liens | DEPRECATE (P3-A) | — | Superseded by `tasks_Tasks.GenerationRuleId/GeneratingTemplateId` |
| `liens_TaskGovernanceSettings` | Liens | MOVE TO TASK (P1-A) | Task → `tasks_GovernanceSettings` | Generic governance settings |
| `tasks_Tasks` | Task | STAY (authoritative) | Task | Canonical task runtime |
| `tasks_Notes` | Task | STAY | Task | Task notes |
| `tasks_History` | Task | STAY | Task | Task audit trail |
| `tasks_StageConfigs` | Task | STAY + RECEIVE migrations | Task | Generic stage config; will absorb `liens_WorkflowStages` |
| `tasks_GovernanceSettings` | Task | STAY + RECEIVE migrations | Task | Generic governance; will absorb `liens_TaskGovernanceSettings` |
| `tasks_Templates` | Task | STAY + RECEIVE migrations | Task | Generic templates; will absorb `liens_TaskTemplates` |
| `tasks_Reminders` | Task | STAY | Task | Task reminder scheduling |
| `tasks_LinkedEntities` | Task | STAY | Task | Generic entity linking |

---

### Concise recommendation

**What belongs in Task (and is already there):**
All task runtime (tasks_Tasks, tasks_Notes, tasks_History, tasks_LinkedEntities, tasks_Reminders) and all generic config (tasks_StageConfigs, tasks_GovernanceSettings, tasks_Templates) should stay in Task. The Task service is correctly positioned as the authoritative owner.

**What should move from Liens to Task (in phases):**
- `liens_TaskGovernanceSettings` → `tasks_GovernanceSettings` (Phase 1, low risk)
- `liens_TaskTemplates` → `tasks_Templates` (Phase 1, low risk)
- `liens_WorkflowStages` + `liens_WorkflowConfigs` → `tasks_StageConfigs` (Phase 2, needs reconciliation of Liens-specific fields)
- `liens_WorkflowTransitions` → `tasks_StageTransitions` (Phase 2, new table required in Task)

**What must stay in Flow:**
All orchestration definition tables (`flow_definitions`, `flow_workflow_stages`, `flow_workflow_transitions`), all runtime execution tables (`flow_workflow_instances`, `flow_product_workflow_mappings`), and all infrastructure tables (`flow_outbox_messages`, `flow_automation_*`). These answer "where is the process?" and must not move.

**Critical distinction — two "stage" concepts must not be confused:**
`flow_workflow_stages` (orchestration step graph, stays in Flow) is fundamentally different from `liens_WorkflowStages` / `tasks_StageConfigs` (task board kanban columns, belong in Task). The former drives process execution; the latter drives task UI organization. They may coincidentally share similar names but serve entirely different masters.

**What should be deprecated (no migration needed):**
- `liens_GeneratedTaskMetadata` — superseded by Task service provenance columns
- `flow_task_items` — superseded by WorkflowInstance (P4) and WorkflowTask (E11.1)

**Long-term convergence path:**
`flow_workflow_tasks` ultimately belongs in Task (it is human work management), but this is a Phase 3 architectural move requiring Flow to become a Task service API client. Do not attempt this until Phase 1+2 migrations are complete and stable.
