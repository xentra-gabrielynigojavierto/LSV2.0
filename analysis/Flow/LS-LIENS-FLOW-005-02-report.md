# LS-LIENS-FLOW-005-02 — Transition Engine Flow Alignment Report

**Date:** 2026-04-20  
**Feature:** Correct the architectural boundary between Liens Transition Engine and Flow service  
**Status:** Complete

---

## 1. Executive Summary

### What Was Implemented

- Corrected all misleading doc comments in `WorkflowTransitionValidationService` and `IWorkflowTransitionValidationService` to explicitly scope them to My Tasks stage-movement only
- Refined audit event descriptions for transition operations to state "task stage" context
- Added transitional architecture comments at the key service enforcement point in `LienTaskService.UpdateAsync`
- Added a readiness seam comment in `WorkflowTransitionValidationService` documenting where Flow instance context will attach in LS-LIENS-FLOW-007
- Updated UI labels in both tenant and control-center workflow settings pages to clearly identify the transition rules as "My Tasks Stage Transition Rules"
- Added descriptive helper text across both UI surfaces reinforcing that these transitions govern task-stage movement, not case/lien Flow workflow execution

### What Was Partially Implemented

- API route paths and public DTO field names were left unchanged (non-breaking requirement). Semantic clarity was achieved through doc comments, UI copy, and service-level comments rather than breaking contract changes.

### What Was Deferred

- Full task ↔ Flow instance context linkage (LS-LIENS-FLOW-007). The `IsTransitionAllowedAsync` signature includes an optional `FlowInstanceId` readiness note in comments; actual `FlowInstanceId` parameter will be added when LS-LIENS-FLOW-007 is implemented.
- SLA / timer logic, approval routing, and assignment intelligence — out of scope per specification.

---

## 2. Codebase Assessment

### Flow-Owned Findings

| Concern | Location | Status |
|---------|----------|--------|
| Case/lien process workflow instances | `WorkflowEndpoints.cs` → `IFlowClient` | Correctly routes all case-level workflow operations to Flow via `productSlug="synqlien"` |
| Workflow definition listing | `IFlowClient.ListDefinitionsAsync` | Flow-owned, called from `WorkflowEndpoints.cs` |
| Workflow advancement / completion | `IFlowClient.AdvanceWorkflowAsync`, `CompleteWorkflowAsync` | Flow-owned, no Liens involvement |
| Product-scoped workflow access | `IFlowClient.GetProductWorkflowAsync` | Flow-owned |

Flow integration is correctly placed in `WorkflowEndpoints.cs` and never touches `LienWorkflowConfig` or task stages.

### Liens-Owned Findings

| Concern | Location | Status |
|---------|----------|--------|
| My Tasks (LienTask) | `LienTaskService.cs` | Correctly Liens-owned |
| Task stage configuration | `LienWorkflowConfig`, `LienWorkflowStage` | Liens-owned — task-specific staging |
| Task stage transition rules | `LienWorkflowTransition`, `WorkflowTransitionValidationService` | Liens-owned — validated at task update |
| Audit for transition config | `LienWorkflowConfigService` audit events | Liens-owned |

### Transition Engine Findings (LS-LIENS-FLOW-005)

`WorkflowTransitionValidationService` is invoked at one point in `LienTaskService.UpdateAsync` when a task's `WorkflowStageId` changes. The logic is correct:
- If no transitions are configured → open-move mode (any stage allowed)
- If transitions exist → only explicitly allowed stage moves succeed

The runtime enforcement is correct. The problem was in the **language** and **implied scope** of the engine, not the runtime behavior:

1. `WorkflowTransitionValidationService` XML doc: *"Reusable by task, case, and lien stage-movement operations"* — this falsely implied Flow-owned case/lien workflows are controlled here
2. `IWorkflowTransitionValidationService` XML doc: *"Used by task, case, and lien stage-movement operations"* — same false implication
3. UI heading "Transition Rules" with no qualification — implied broad workflow authority
4. Control Center heading "Synq Liens — Workflow Configuration" with subtitle "View and manage per-tenant workflow settings" — implied full workflow governance scope

### Current Drift / Conflict Analysis

| Item | Drift / Conflict |
|------|-----------------|
| Service XML docs claiming "case and lien" scope | Language drift — implied broader ownership than exists |
| UI "Transition Rules" heading | Ambiguous — could be misread as case/lien Flow transition authority |
| CC page heading "Workflow Configuration" | Ambiguous — reads like a full workflow engine configuration, not task-stage only |
| No seam for Flow instance context | Structural gap — transition validation had no hook for future Flow linkage |

### What This Feature Corrected

All identified drift corrected via targeted comments, service-level doc updates, and UI wording changes. No new permanent Liens-local ownership was introduced. No runtime behavior changed.

---

## 3. Files Changed

### Backend

| File | Change |
|------|--------|
| `Liens.Application/Interfaces/IWorkflowTransitionValidationService.cs` | Corrected XML docs to explicitly scope to My Tasks stage-movement; added LS-LIENS-FLOW-007 readiness note |
| `Liens.Application/Services/WorkflowTransitionValidationService.cs` | Corrected class-level and method-level XML docs; added Flow boundary comment and LS-LIENS-FLOW-007 seam comment |
| `Liens.Application/Services/LienTaskService.cs` | Refined inline comment at transition enforcement block to explicitly state task-stage scope and note transitional architecture |
| `Liens.Application/Services/LienWorkflowConfigService.cs` | Refined audit descriptions for transition operations to include "task stage" context |

### Frontend

| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/lien/settings/workflow/page.tsx` | Updated page subtitle; updated "Transition Rules" section heading to "My Tasks — Stage Transition Rules"; added helper text clarifying scope |
| `apps/web/src/app/(control-center)/control-center/liens/workflow/page.tsx` | Updated page heading and subtitle; updated "Transition Rules" heading to "My Tasks — Stage Transition Rules"; added helper text |

### Config / DB / Migration

None. No schema changes required.

---

## 4. Database / Schema Changes

**None.** No new tables, columns, migrations, or schema modifications were made. The transition engine schema introduced in LS-LIENS-FLOW-005 (`LienWorkflowTransitions` table) is unchanged.

---

## 5. API Changes

**No breaking changes.** All public API routes, request/response field names, and HTTP methods remain identical to LS-LIENS-FLOW-005.

Non-breaking semantic improvements:
- Audit event descriptions for `liens.workflow_transition.created`, `liens.workflow_transition.deactivated`, `liens.workflow_transition.saved`, `liens.workflow_transition.initialized` now include "task stage" context in the description string
- No field additions or removals in any DTO
- No route renames

---

## 6. UI Changes

### 6.1 Tenant Workflow Settings (`/lien/settings/workflow`)

| Element | Before | After |
|---------|--------|-------|
| Page subtitle | "Configure the task workflow stages for Synq Liens" | "Configure the My Tasks stage progression and allowed stage transitions for Synq Liens. These settings govern task-stage movement only and are separate from case workflow execution managed by the Flow service." |
| Transitions section heading | "Transition Rules" | "My Tasks — Stage Transition Rules" |
| Transitions section description | "Check which stages a task can move to from each source stage..." | "Define which task stages can follow each other in My Tasks. These rules control task-stage movement only — they do not affect case or lien workflow execution." |

### 6.2 Control Center Workflow Settings (`/control-center/liens/workflow`)

| Element | Before | After |
|---------|--------|-------|
| Page heading | "Synq Liens — Workflow Configuration" | "Synq Liens — My Tasks Stage Configuration" |
| Page subtitle | "View and manage per-tenant workflow settings from Control Center" | "Configure per-tenant My Tasks stage definitions and transition rules. This governs task-stage movement only — case and lien workflow execution is managed separately by the Flow service." |
| Transitions section heading | "Transition Rules" | "My Tasks — Stage Transition Rules" |
| Transitions section description | "Check which stages a task can move to from each source stage..." | "Define which task stages can follow each other in My Tasks. These rules control task-stage movement only — they do not affect case or lien workflow execution." |

### 6.3 Runtime Task UI

The task form's workflow stage selector label was reviewed. It reads "Task Stage" — already semantically correct. No changes required. The error message returned when a transition is blocked was reviewed and left unchanged as it already refers to "Transition from 'X' to 'Y' is not allowed by the workflow configuration" which is task-context accurate.

---

## 7. Flow Alignment Outcome

### What is Flow-Owned

- Case/lien process workflow instance execution (`WorkflowEndpoints.cs` → `IFlowClient`)
- `WorkflowDefinition` catalog (`IFlowClient.ListDefinitionsAsync`)
- Workflow instance advancement (`IFlowClient.AdvanceWorkflowAsync` / `AdvanceProductWorkflowAsync`)
- Workflow instance completion (`IFlowClient.CompleteWorkflowAsync`)
- Product-scoped workflow passthrough (`productSlug="synqlien"`, `sourceEntityType="lien_case"`)

### What is Liens-Owned

- `LienTask` — individual operational work items
- `LienWorkflowConfig` — My Tasks stage definitions per tenant
- `LienWorkflowStage` — individual stages within a task workflow config
- `LienWorkflowTransition` — permitted stage-to-stage movements for My Tasks
- `WorkflowTransitionValidationService` — runtime gate for task-stage changes
- `LienWorkflowConfigService` — CRUD for task stage configs

### What is Transitional

| Item | Current State | Future Direction |
|------|-------------|-----------------|
| My Tasks stage transitions | Liens-owned runtime validation | Will accept optional Flow instance context in LS-LIENS-FLOW-007 |
| Task ↔ case linkage via `CaseId` | Task stores `CaseId` but does not read Flow instance state | LS-LIENS-FLOW-007: use `CaseId` to correlate with active Flow instance; optionally inherit Flow step context |
| Stage configuration for tasks | Liens-local `LienWorkflowConfig` | May align to Flow workflow steps in the future; current config is the correct task-level granularity |

### How This Feature Corrected the Boundary

1. Removed language in service interfaces that claimed "case and lien" scope for the Liens transition engine
2. Added explicit comments at the `WorkflowTransitionValidationService` class level stating: Flow owns case/lien execution; this service governs task-stage movement only
3. Updated UI copy to make the scope unmistakable to both tenants and platform administrators
4. Added a forward-looking seam comment at `IsTransitionAllowedAsync` for LS-LIENS-FLOW-007 Flow instance context integration

### How It Prepares LS-LIENS-FLOW-007

The `WorkflowTransitionValidationService` comment now explicitly marks the insertion point for an optional `flowInstanceId` parameter. When LS-LIENS-FLOW-007 is implemented:
- `IsTransitionAllowedAsync` can accept `Guid? flowInstanceId = null`
- The service can optionally read current Flow instance state to validate or enrich the task-stage decision
- The Liens-local transition rules and Flow instance state can be evaluated together without breaking the existing open-move / strict-mode behavior

---

## 8. Runtime Validation Behavior

### What Is Enforced

| Scenario | Behavior |
|----------|---------|
| Task stage unchanged in update | No validation triggered |
| Both from-stage and to-stage are null | No validation triggered |
| Task moving from stage A → stage B, no transitions configured | **Allowed** (open-move mode) |
| Task moving from stage A → stage B, transitions exist, A→B is listed | **Allowed** |
| Task moving from stage A → stage B, transitions exist, A→B is NOT listed | **Blocked** — `ValidationException` on `workflowStageId` field |
| Self-transition (A → A) | **Blocked** at domain entity create level |

### Error Message (Unchanged)

```
Transition from '{fromStageName}' to '{toStageName}' is not allowed by the workflow configuration.
```

This message is task-context appropriate. It refers to stage names configured in My Tasks workflow settings, not Flow instance steps.

### What Is NOT Enforced Here

- Case workflow instance step transitions (Flow-owned)
- Lien workflow instance step transitions (Flow-owned)
- Any cross-tenant workflow state
- Assignment validation
- SLA/timer evaluation
- Approval routing

---

## 9. Permissions / Security

No changes to authentication or authorization surfaces.

- Tenant endpoint group: `RequireAuthorization(Policies.AuthenticatedUser)` + `RequireProductAccess(LiensPermissions.ProductCode)` + `RequirePermission(LiensPermissions.WorkflowManage)` — unchanged
- Admin endpoint group: `RequireAuthorization(Policies.PlatformOrTenantAdmin)` — unchanged
- No new permissions added
- No permission removals
- No unintended auth surface changes

---

## 10. Audit Integration

### Events Preserved (No Breaking Changes)

| Event | Trigger | Description (After) |
|-------|---------|---------------------|
| `liens.workflow_transition.created` | Transition added to config | "Task stage transition {from} → {to} added to workflow '{name}'" |
| `liens.workflow_transition.deactivated` | Transition deactivated | "Task stage transition '{id}' deactivated in workflow '{name}'" |
| `liens.workflow_transition.saved` | Batch save (save-all) | "Workflow '{name}' task stage transitions replaced: {count} active" |
| `liens.workflow_transition.initialized` | Auto-linear init | "Workflow '{name}' auto-initialized with {count} linear task stage transitions" |

Audit events for stages (`liens.workflow_stage.added`, `.updated`, `.deactivated`, `.reordered`) and config (`liens.workflow_config.created`, `.updated`) are unchanged in event key and firing behavior.

**No runtime validation failures are logged to audit** — only configuration-level changes.

---

## 11. Validation Results

### Backend Build
- `Liens.Api` builds with 0 errors after all changes
- No new compilation warnings introduced

### Frontend Typecheck
- `pnpm type-check` passes with 0 errors after all changes

### Runtime Behavior Preserved
- Valid task stage transitions: still succeed (open-move and explicit-allow paths unchanged)
- Invalid task stage transitions: still blocked with `ValidationException`
- Workflow config CRUD: unchanged
- Audit events: still emit

### No Regression to LS-LIENS-FLOW-005
- All transition enforcement logic in `LienTaskService.UpdateAsync` is unchanged
- `WorkflowTransitionValidationService.IsTransitionAllowedAsync` logic is unchanged
- Auto-initialize linear transitions behavior is unchanged
- Open-move mode behavior (no transitions → allow all) is unchanged

---

## 12. Known Gaps / Risks

1. **LS-LIENS-FLOW-007 not yet implemented.** Task stages and Flow workflow instance steps remain uncorrelated at runtime. A task on stage "Initial Review" has no knowledge of the active Flow workflow step for its linked case. This is documented as the next planned integration.

2. **Task-to-Flow instance step mapping not established.** When LS-LIENS-FLOW-007 arrives, a mapping between `LienWorkflowStage.Id` and Flow `WorkflowStep.Id` will need to be established. This could be done via a new join table or via stage name/slug convention. The preferred approach is not prescribed here.

3. **`WorkflowName` in `LienWorkflowConfig` is tenant-defined free text.** It does not correspond to a Flow `WorkflowDefinition` name. Tenants naming it "Lien Workflow" could create conceptual confusion. A future improvement is to display a clarifying note in the UI that this name describes the My Tasks stage set, not the case-level Flow workflow.

4. **Stage configuration is separate from Flow workflow steps.** A tenant's `LienWorkflowConfig` stages are not automatically derived from or linked to Flow `WorkflowDefinition` steps. This is correct transitional architecture; when LS-LIENS-FLOW-007 links them, the two systems can be correlated.

5. **Automation tasks bypass transition validation.** Tasks created by `LienTaskGenerationEngine` can be assigned to any stage at creation time without going through transition validation. Transition validation only fires on `UpdateAsync` (stage changes on existing tasks). This is correct and intentional.

---

## 13. Run Instructions

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

### Manual Validation Checklist

**Tenant Workflow Settings (`/lien/settings/workflow`)**
1. Verify page subtitle mentions "My Tasks stage progression" and "Flow service"
2. Verify "My Tasks — Stage Transition Rules" heading in transitions section
3. Verify helper text explains task-stage scope

**Control Center (`/control-center/liens/workflow`)**
1. Verify page heading reads "Synq Liens — My Tasks Stage Configuration"
2. Verify page subtitle mentions task-stage scope and Flow service
3. Verify "My Tasks — Stage Transition Rules" heading in transitions section

**Task Stage Transition Enforcement**
1. Create a task with a workflow stage assigned
2. Attempt to move it to a non-allowed stage (with strict transitions configured) → should fail with validation error
3. Move to an allowed stage → should succeed

**Audit**
1. Add a transition rule in workflow settings
2. Verify audit event `liens.workflow_transition.created` fires with "task stage transition" description
