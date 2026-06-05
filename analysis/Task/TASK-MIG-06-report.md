# TASK-MIG-06 Report — Generation Rule Alignment
**Date:** 2026-04-21  
**Status:** ✅ Complete — Liens.Api builds (0 errors); no schema changes; no Task service changes

---

## 1. Codebase Analysis

### Generation rule entity — Liens DB fields

| Field | Type | Used by engine for |
|---|---|---|
| `Id` | Guid | `generationRuleId` stored in Task service on task create; dup-prevention rule query |
| `TenantId` | Guid | All queries |
| `ProductCode` | string | Always `"SYNQ_LIENS"` |
| `Name` | string | Audit/log messages |
| `EventType` | string | Matched against `context.EventType` in `GetActiveByTenantAndEventAsync` |
| `TaskTemplateId` | Guid | Passed to `GetForGenerationAsync` (Task-first lookup); stored as `generatingTemplateId` in Task service |
| `ContextType` | string | Stored on rule for admin context but not used in runtime filtering |
| `ApplicableWorkflowStageId` | Guid? | Stage filter: compared with `context.WorkflowStageId` |
| `DuplicatePreventionMode` | string | Selects dup-check strategy |
| `AssignmentMode` | string | Controls how `assignedUserId` is derived |
| `DueDateMode` | string | Controls how `dueDate` is derived |
| `DueDateOffsetDays` | int? | Override due-date offset in `OVERRIDE_OFFSET_DAYS` mode |
| `IsActive` | bool | Filtered in `GetActiveByTenantAndEventAsync` |
| `Version` | int | Optimistic concurrency on admin writes |

### Generation engine execution flow (7 steps)

| Step | Code location | Data source(s) | Status |
|---|---|---|---|
| 1. Event guard | `TriggerAsync` line 41 | `TaskGenerationEventType.All` (Liens enum) | ✅ Liens-owned; correct |
| 2. Rule fetch | `_ruleRepo.GetActiveByTenantAndEventAsync` | **Liens DB** | ✅ Liens-owned; correct |
| 3. Stage filter | `ProcessRuleAsync` line 91 | `rule.ApplicableWorkflowStageId` vs `context.WorkflowStageId` — both Liens semantics; GUIDs match across systems (MIG-03) | ✅ Correct |
| 4. Template check | `_templateService.GetForGenerationAsync` | **Task service first** (MIG-02), Liens DB fallback | ✅ Aligned (MIG-02) |
| 5. Dup prevention | `_taskServiceClient.HasOpenTaskForRuleAsync` / `HasOpenTaskForTemplateAsync` | **Task service HTTP** | ✅ Aligned (TASK-B04-01) |
| 6. Task create | `_taskService.CreateAsync` → `_taskServiceClient.CreateTaskAsync` | **Task service** | ✅ Task service owns tasks |
| 7. Metadata save | `_taskRepo.AddGeneratedMetadataAsync` | **Liens DB** (`liens_GeneratedTaskMetadata`) | ✅ Liens-side traceability; correct |

### Governance path (inside `LienTaskService.CreateAsync`)

| Step | Data source | Status |
|---|---|---|
| `_governanceService.GetAsync` | Task service first (MIG-01), Liens DB fallback | ✅ Aligned (MIG-01) |
| `DeriveStartStageAsync` — explicit stage | `_workflowConfigService.GetStageForRuntimeAsync` — Task-first (MIG-03) | ✅ Aligned (MIG-03) |
| `DeriveStartStageAsync` — first active stage | `_workflowConfigService.GetByTenantAsync` — Task-first (MIG-03) | ✅ Aligned (MIG-03) |

### Template fields consumed by the engine

All template fields arrive via `GetForGenerationAsync` which is Task-first (MIG-02):

| Field | Mapping | Source |
|---|---|---|
| `template.DefaultTitle` | Task service `title` | Task service (via `GetForGenerationAsync`) |
| `template.DefaultDescription` | Task service `description` | Task service |
| `template.DefaultPriority` | Task service `priority` | Task service |
| `template.DefaultDueOffsetDays` | Task service `DefaultDueInDays` | Task service |
| `template.DefaultRoleId` | `ProductSettingsJson.defaultRoleId` | Task service (via `LiensTemplateExtensions.Deserialize`) |
| `template.ApplicableWorkflowStageId` | `ProductSettingsJson.applicableWorkflowStageId` | Task service (via `LiensTemplateExtensions.Deserialize`) |
| `template.IsActive` | Task service `isActive` | Task service |

### Admin read/write paths

`LienTaskGenerationRuleService` (CRUD + activate/deactivate) reads and writes **exclusively to Liens DB**. This is correct — rules are Liens-owned. No dual-read is needed or appropriate for rule admin.

### Trigger entry points

| Trigger | Event | Context | Source |
|---|---|---|---|
| `CaseService.CreateAsync` | `CASE_CREATED` | `WorkflowStageId = null` | Fire-and-observe (does not block case creation) |
| `LienService.CreateAsync` | `LIEN_CREATED` | `WorkflowStageId = null`, `CaseId = entity.CaseId` | Fire-and-observe |
| `POST /api/liens/task-generation/trigger` | Stage-change events only (`CaseWorkflowStageChanged`, `LienWorkflowStageChanged`) | `WorkflowStageId` from request | HTTP endpoint |

### Duplicate-prevention mechanics

`HasOpenTaskForRuleAsync` / `HasOpenTaskForTemplateAsync` query the Task service using:
- `generationRuleId={ruleId}` or `generatingTemplateId={templateId}`
- `sourceProductCode=SYNQ_LIENS`
- `excludeTerminal=true` (only open/active tasks)
- `pageSize=1` (existence check only)

These fields are populated in the Task service when a task is created via `CreateTaskAsync` (which passes `generationRuleId` and `generatingTemplateId` in the create body). The Task service stores them as-is and supports filtering by them. No Liens DB query is made for tasks.

---

## 2. Current Rule Dependency Map

### Dependency sources during generation (post MIG-01 through MIG-05)

| Concern | Source | How |
|---|---|---|
| **Rule definitions** | Liens DB only | `liens_TaskGenerationRules` via `LienTaskGenerationRuleRepository` |
| **Event type semantics** | Liens domain (enums) | `TaskGenerationEventType` static class |
| **EventType matching** | Liens DB | `GetActiveByTenantAndEventAsync(tenantId, eventType)` |
| **Stage filter comparison** | Liens DB (rule) + Liens runtime (context) | `rule.ApplicableWorkflowStageId` vs `context.WorkflowStageId` — GUIDs match across both systems (MIG-03) |
| **Template lookup** | **Task service first**, Liens DB fallback | `GetForGenerationAsync` (MIG-02) |
| **Template fields** (title, desc, priority, roleId, dueOffset, stageId) | **Task service** | Via `ProductSettingsJson` deserialization |
| **Duplicate-prevention check** | **Task service** | `HasOpenTaskForRuleAsync` / `HasOpenTaskForTemplateAsync` via HTTP |
| **Governance enforcement** | **Task service first**, Liens DB fallback | `LienTaskGovernanceService.GetAsync` (MIG-01) |
| **Start-stage derivation** | **Task service first**, Liens DB fallback | `GetStageForRuntimeAsync` / `GetByTenantAsync` (MIG-03) |
| **Task creation** | **Task service** | `CreateTaskAsync` via HTTP |
| **Traceability metadata** | **Liens DB** | `liens_GeneratedTaskMetadata` (correct — rule traceability is Liens-owned) |
| **AssignmentMode / DueDateMode** | Liens DB (rule fields) | Interpreted by engine; no Task service lookup needed |

### Remaining Liens-only dependencies (intentional)

| Dependency | Why Liens-only is correct |
|---|---|
| Rule entity (`liens_TaskGenerationRules`) | Rules not migrated in this phase — Liens owns rule semantics |
| `rule.ApplicableWorkflowStageId` | Rule-side stage filter; no Task equivalent; stage GUIDs are compatible |
| `rule.AssignmentMode`, `DueDateMode`, `DuplicatePreventionMode` | Liens-specific operational concepts not represented in Task service |
| `LienGeneratedTaskMetadata` | Local traceability for rule-triggered tasks; Liens-side record |

---

## 3. Alignment Design

### Decision 1: Rules remain Liens-owned
**Decision:** Generation rules stay in `liens_TaskGenerationRules`. The Task service has no concept of event-driven rules, contextual triggers, or Liens business events (case created, lien created, stage changed). These are pure Liens semantics.  
**Consequence:** `GetActiveByTenantAndEventAsync` always hits Liens DB. No dual-read needed.

### Decision 2: Template data is now Task-first
**Decision:** `GetForGenerationAsync` (called in step 4 of engine) is Task-first (MIG-02 already done). Template fields (title, desc, priority, roleId, dueOffset, stage) are deserialized from Task service `ProductSettingsJson` via `LiensTemplateExtensions`.  
**Consequence:** No change needed in MIG-06.

### Decision 3: Duplicate prevention stays on Task service
**Decision:** Dup checks query the Task service exclusively (TASK-B04-01 already done). The Task service stores `generationRuleId` and `generatingTemplateId` on every auto-generated task via the create payload.  
**Consequence:** No change needed in MIG-06.

### Decision 4: Stage filter comparison is safe
**Decision:** `rule.ApplicableWorkflowStageId` (from Liens DB) is compared directly with `context.WorkflowStageId` (from the calling event). Both reference stage GUIDs that were preserved verbatim in MIG-03. No GUID translation layer needed.  
**Consequence:** No change needed in MIG-06.

### Decision 5: Assignee resolution is partially deferred
**Decision:** `AssignmentMode.AssignByRole` is already marked deferred ("no role-to-user resolution available"). `USE_TEMPLATE_DEFAULT` falls through to `null`. `ASSIGN_EVENT_ACTOR` uses `actorUserId` from context. This is pre-existing behavior.  
**Template's `DefaultRoleId`** is now correctly sourced from Task service (via `ProductSettingsJson`) — but since `AssignByRole` is deferred, it is not yet consumed. This remains a known gap (see Section 8).  
**Consequence:** No change needed in MIG-06 — behavior preserved exactly.

### Decision 6: Governance and stage derivation are already Task-first
**Decision:** `LienTaskService.CreateAsync` calls `_governanceService.GetAsync` (Task-first, MIG-01) and `DeriveStartStageAsync` which uses `_workflowConfigService` (Task-first, MIG-03).  
**Consequence:** No change needed in MIG-06.

### Decision 7: Engine-level observability logging is the primary MIG-06 deliverable
**Decision:** All prior MIG tasks added source-of-truth logging at the service level (`template_source`, `governance_source`, `stage_source`). The engine itself previously had no corresponding generation-level log keys. MIG-06 adds them:
- `generation_rule_source=liens_db` — at rule fetch
- `generation_stage_filter_source=liens_db` — at stage filter (pass or fail)
- `generation_duplicate_check_source=task_service` — at each dup-check call
- `generation_duplicate_check_source=none` — when dup prevention mode is NONE

---

## 4. Runtime Compatibility Changes

### Change: `LienTaskGenerationEngine` — structured generation-level logging (MIG-06)

**File:** `Liens.Application/Services/LienTaskGenerationEngine.cs`

**All changes are pure logging additions. No logic changes. No behavior changes.**

#### A. Rule fetch log (in `TriggerAsync`)

```
generation_rule_source=liens_db TenantId={TenantId} EventType={EventType} RuleCount={RuleCount}
```
Emitted immediately after `GetActiveByTenantAndEventAsync` returns. Makes explicit that rule data is Liens-DB-only at the engine level.

#### B. Stage filter log (in `ProcessRuleAsync`)

```
generation_stage_filter_source=liens_db Rule {RuleId}: stage filter passed (required={Required} context={Actual}).
```
or
```
generation_stage_filter_source=liens_db Rule {RuleId}: stage mismatch (rule requires {Required}, context has {Actual}). Skipping.
```
Emitted on every rule's stage-filter evaluation. Replaces/augments the previous plain log message (which lacked the `generation_stage_filter_source` key). Clarifies that `ApplicableWorkflowStageId` is a Liens-DB-owned field compared against the Liens-side context GUID.

#### C. Duplicate-check logs (in `ProcessRuleAsync`)

```
generation_duplicate_check_source=task_service Rule {RuleId}: checking SAME_RULE dup (caseId={CaseId} lienId={LienId}).
generation_duplicate_check_source=task_service Rule {RuleId}: duplicate found (SAME_RULE). Skipping.

generation_duplicate_check_source=task_service Rule {RuleId}: checking SAME_TEMPLATE dup (templateId={TemplateId} caseId={CaseId} lienId={LienId}).
generation_duplicate_check_source=task_service Rule {RuleId}: duplicate found (SAME_TEMPLATE). Skipping.

generation_duplicate_check_source=none Rule {RuleId}: dup prevention mode is NONE; skipping dup check.
```
Makes explicit that dup-prevention always queries the Task service (never Liens DB directly).

### No other changes

All prior behavioral alignment was completed in MIG-01 through MIG-05:

| Path | Aligned in |
|---|---|
| Template fetch (generation engine) | MIG-02 |
| Dup-prevention via Task service | TASK-B04-01 |
| Governance enforcement (Task-first) | MIG-01 |
| Stage derivation (Task-first) | MIG-03 |
| Contextual template filter (UI) | MIG-05 |

---

## 5. Dual-Read / Fallback Adjustments

### Summary of all dual-read paths consumed by generation (post MIG-06)

| Path | Primary | Fallback | Engine-level log key | Service-level log key |
|---|---|---|---|---|
| **Rule fetch** | Liens DB only | — (rules not migrated) | `generation_rule_source=liens_db` | — |
| **Stage filter** | Liens DB (rule) vs runtime context | — (GUID comparison) | `generation_stage_filter_source=liens_db` | — |
| **Template fetch** | Task service | Liens DB | _(template service logs inline)_ | `template_source=task_service` / `template_source=liens_fallback` |
| **Duplicate check** | Task service only | — | `generation_duplicate_check_source=task_service` | — |
| **Governance read** | Task service | Liens DB | _(governance service logs inline)_ | `governance_source=task_service` / `governance_source=liens_fallback` |
| **Start-stage (explicit)** | Task service | Liens DB | _(workflowConfig service logs inline)_ | `stage_source=task_service` / `stage_source=liens_fallback` |
| **Start-stage (first active)** | Task service | Liens DB | _(workflowConfig service logs inline)_ | `stage_source=task_service` / `stage_source=liens_db_fallback` |

### Fallback trigger rules

| Scenario | Behavior |
|---|---|
| Task service returns no template | Falls back to Liens DB template (`template_source=liens_fallback`) |
| Task service template call throws | Falls back to Liens DB template (`template_source=task_service_error`) |
| Task service template returns inactive template | Engine skips rule; no fallback (correct answer) |
| Task service dup-check throws | Exception propagates; engine catches per-rule, increments `skipped` counter |
| Task service dup-check returns no tasks | Proceeds with task creation |
| No caseId or lienId provided for dup check | `HasOpenTaskForRuleAsync` / `HasOpenTaskForTemplateAsync` return `false` immediately (`BuildDupCheckQuery` returns null) — no Task service call made |

### Intentionally Liens-only paths

| Path | Reason |
|---|---|
| Rule definitions | Rules not migrated; Liens owns event-rule semantics |
| `rule.ApplicableWorkflowStageId` | Liens-owned rule field; no Task equivalent |
| `LienGeneratedTaskMetadata` | Liens-side traceability; rule-to-task mapping |
| Rule admin CRUD | Authoritative writes; no dual-read needed for admin paths |

---

## 6. Validation Results

| # | Check | Method | Result |
|---|---|---|---|
| 1 | Rule-based generation still works — engine fetches rules from Liens DB, template from Task service (first), creates task via Task service | Code inspection — full flow traced in Section 1 | ✅ PASS |
| 2 | Template-based generation still works — `GetForGenerationAsync` is Task-first; all template fields correctly mapped from Task service `ProductSettingsJson` | Code inspection — `MapFromTaskServiceDto` maps all required fields | ✅ PASS |
| 3 | Duplicate-prevention still correct — both `SameRuleSameEntityOpenTask` and `SameTemplateSameEntityOpenTask` query Task service; `None` mode skips check | Code inspection — `BuildDupCheckQuery` constructs correct query with `excludeTerminal=true` | ✅ PASS |
| 4 | Stage applicability checks still work — `rule.ApplicableWorkflowStageId` compared to `context.WorkflowStageId`; GUIDs preserved from MIG-03 | Code inspection — direct GUID comparison; MIG-03 confirmed verbatim preservation | ✅ PASS |
| 5 | Governance-driven start-stage logic still works — `_governanceService.GetAsync` (Task-first), `DeriveStartStageAsync` uses `GetStageForRuntimeAsync` (Task-first) | Code inspection — `LienTaskService.CreateAsync` lines 104-130 | ✅ PASS |
| 6 | Default role / assignee logic still works — `AssignmentMode.AssignByRole` deferred (pre-existing); `AssignEventActor` uses context actor; `LeaveUnassigned` returns null; `UseTemplateDefault` returns null (template has no direct assignedUserId) | Code inspection — `ResolveAssignee` static method unchanged | ✅ PASS |
| 7 | Rule execution correct when Task service data present — template from Task service used directly; dup-check queries Task service | Code inspection | ✅ PASS |
| 8 | Rule execution falls back safely when Task service data missing — `GetForGenerationAsync` falls to Liens DB; dup-check throws are caught per-rule (engine increments `skipped`) | Code inspection — `ProcessRuleAsync` try/catch in `TriggerAsync` | ✅ PASS |
| 9 | Mixed state works — some tenants synced, some not; template fallback triggers per template fetch attempt; rule fetch always from Liens DB | Code inspection | ✅ PASS |
| 10 | No UI/admin regression — rule admin endpoints untouched; UI generation trigger unchanged | Code inspection — no endpoint changes | ✅ PASS |
| 11 | No Flow orchestration concepts pulled into Task | Search confirmed — no Flow references in changed code | ✅ PASS |
| 12 | Liens.Api builds 0 errors | `dotnet build Liens.Api.csproj -c Release --no-restore` | ✅ PASS |
| 13 | Task.Api unchanged — no Task service code modified | No Task service files changed | ✅ PASS |
| 14 | Engine-level log keys present and correct | Code inspection — three categories of log keys added | ✅ PASS |
| 15 | New log keys added without changing any conditional logic | Code inspection — all additions are pure `_logger.LogDebug/LogInformation` calls | ✅ PASS |

---

## 7. Rollback Plan

**MIG-06 is a single-file, logging-only change in `LienTaskGenerationEngine.cs`.**

To revert:
1. Remove or revert the three `_logger.LogDebug` calls added to `TriggerAsync` and `ProcessRuleAsync`
2. No logic change was made — rollback has zero behavioral impact

No Task schema changes were made → no DB rollback required.  
No Task service code was changed → Task service is unaffected.  
No Liens DB schema was changed → Liens data is fully intact.  
No new service dependencies → no registration changes to undo.

---

## 8. Known Gaps / Risks

| Item | Notes |
|---|---|
| **Rules not migrated to Task service** | Generation rules (`liens_TaskGenerationRules`) are still Liens-DB-only. This is intentional for this phase. A future rule-migration task would need to: define a Task-service representation for `EventType`, `ContextType`, `ApplicableWorkflowStageId`, `DuplicatePreventionMode`, `AssignmentMode`, `DueDateMode`. |
| **`AssignmentMode.AssignByRole` is deferred** | The `ASSIGN_BY_ROLE` mode currently returns `null` (unassigned). `template.DefaultRoleId` is now correctly sourced from Task service (via `ProductSettingsJson`) but is not yet consumed by the resolver. A future implementation would need to call an identity service to resolve role → user. |
| **`rule.ApplicableWorkflowStageId` is Liens-DB-only** | This field on the rule entity is not replicated to the Task service. Until rules are migrated, the stage filter comparison remains an in-process Liens DB comparison. This is safe because stage GUIDs are identical across both systems (MIG-03). |
| **`LienGeneratedTaskMetadata` is Liens-side only** | The traceability record lives in `liens_GeneratedTaskMetadata`. After a future ownership cutover, this table should remain as historical traceability even if new auto-generated tasks no longer write to it. |
| **Fire-and-observe generation in CaseService/LienService** | Both creation paths fire generation with `CancellationToken.None`. Failures are swallowed via `.ContinueWith`. This means generation errors are logged but do not surface to callers. This is correct behavior (generation failure should not block entity creation) but means no retry logic exists. |
| **Dup-check with no entity scope** | `BuildDupCheckQuery` returns `null` (skips dup check) when both `caseId` and `lienId` are `null`. This is by design — dup prevention is entity-scoped and cannot function without at least one anchor. |
| **Ownership flip prerequisites** | Before Liens rule ownership can be flipped to Task: (1) Task service must support an event-rule abstraction, (2) `EventType` must be expressible in Task service terms, (3) `ApplicableWorkflowStageId` must be a first-class Task service field on rules. These are all out of scope for this phase. |

---

## Files Changed

| Path | Change |
|---|---|
| `apps/services/liens/Liens.Application/Services/LienTaskGenerationEngine.cs` | Added structured generation-level log keys: `generation_rule_source`, `generation_stage_filter_source`, `generation_duplicate_check_source` |
| `analysis/TASK-MIG-06-report.md` | This report |

**No Task service changes. No schema changes. No migrations. No logic changes.**

---

## Generation Log Key Reference (post MIG-06)

| Log key | Emitted when | Source |
|---|---|---|
| `generation_rule_source=liens_db` | Every engine trigger (rules fetched) | `LienTaskGenerationEngine.TriggerAsync` |
| `generation_stage_filter_source=liens_db` | Every rule evaluated (pass or skip) | `LienTaskGenerationEngine.ProcessRuleAsync` |
| `generation_duplicate_check_source=task_service` | Dup check performed (found or not found) | `LienTaskGenerationEngine.ProcessRuleAsync` |
| `generation_duplicate_check_source=none` | DuplicatePreventionMode is NONE | `LienTaskGenerationEngine.ProcessRuleAsync` |
| `template_source=task_service` | Template fetched from Task service | `LienTaskTemplateService.GetForGenerationAsync` |
| `template_source=liens_fallback` | Template fell back to Liens DB | `LienTaskTemplateService.GetForGenerationAsync` |
| `template_source=task_service_error` | Task service call failed; Liens DB used | `LienTaskTemplateService.TryFetchFromTaskServiceAsync` |
| `governance_source=task_service` | Governance fetched from Task service | `LienTaskGovernanceService.TryFetchFromTaskServiceAsync` |
| `governance_source=liens_fallback` | Governance fell back to Liens DB | `LienTaskGovernanceService.GetAsync` |
| `stage_source=task_service` | Stage fetched from Task service | `LienWorkflowConfigService` |
| `stage_source=liens_fallback` | Stage fell back to Liens DB | `LienWorkflowConfigService` |
