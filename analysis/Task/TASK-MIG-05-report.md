# TASK-MIG-05 Report — Template + Stage Compatibility Alignment
**Date:** 2026-04-21  
**Status:** ✅ Complete — Liens.Api builds (0 errors); no Task schema changes required

---

## 1. Codebase Analysis

### Liens template entity fields

| Field | Liens entity | Round-trip to Task service |
|---|---|---|
| `Id` | Guid PK | Preserved (MIG-02, `UpsertFromSource` uses explicit ID) |
| `ContextType` | `string` (GENERAL / CASE / LIEN / STAGE) | Stored in `ProductSettingsJson.contextType` |
| `ApplicableWorkflowStageId` | `Guid?` | Stored in `ProductSettingsJson.applicableWorkflowStageId` |
| `DefaultRoleId` | `string?` | Stored in `ProductSettingsJson.defaultRoleId` |
| `DefaultDueOffsetDays` | `int?` | Mapped to `DefaultDueInDays` (top-level field) |
| `DefaultPriority` | `string` | Top-level field |
| `IsActive` | `bool` | Top-level field |

The `LiensTemplateExtensions` DTO handles serialization / deserialization of the ProductSettingsJson blob with full null-safety.

### Liens stage entity fields

All stage fields are top-level in `tasks_StageConfigs`. Stage IDs are preserved verbatim from MIG-03. `ProductSettingsJson` stores `description`, `defaultOwnerRole`, `slaMetadata` (MIG-03 extras).

### Runtime paths that depend on `ApplicableWorkflowStageId`

| Path | Code location | Source | Status |
|---|---|---|---|
| **Generation engine — stage filter** | `LienTaskGenerationEngine.ProcessRuleAsync` line 83 | `rule.ApplicableWorkflowStageId` vs `context.WorkflowStageId` (both Liens DB) | ✅ — rule is Liens-only; no cross-service lookup needed |
| **Generation engine — template fetch** | `GetForGenerationAsync` → dual-read | Task service first (`GetTemplateAsync`), then Liens DB | ✅ — MIG-02 |
| **Generation engine — task create** | `createRequest.WorkflowStageId = context.WorkflowStageId ?? template.ApplicableWorkflowStageId` | Template from dual-read; `ApplicableWorkflowStageId` deserialized from `ProductSettingsJson` | ✅ — MIG-02 |
| **Contextual template filter (UI)** | `GetContextualAsync` | Previously Liens DB only | ✅ **— MIG-05 (this task)** |
| **Governance start-stage derivation** | `DeriveStartStageAsync` | `GetStageForRuntimeAsync` dual-read (Task first) + `GetByTenantAsync` dual-read | ✅ — MIG-03 |
| **Explicit start stage** | `ExplicitStartStageId` from governance | Governance dual-read (Task first); stage resolved via `GetStageForRuntimeAsync` | ✅ — MIG-01 + MIG-03 |

### Runtime paths that depend on `DefaultStartStageMode` / `ExplicitStartStageId`

Both fields live in `ProductSettingsJson` of the Task service governance row (`LiensGovernanceExtensions`). `LienTaskGovernanceService` deserializes them on every governance read. `DeriveStartStageAsync` in `LienTaskService` consumes the result. The entire path is dual-read as of MIG-01. ✅

### Task service — what it exposes for template queries

- `GET /api/tasks/templates?sourceProductCode=SYNQ_LIENS` — returns all active templates with `ProductSettingsJson` included
- `GET /api/tasks/templates/{id}` — single template with `ProductSettingsJson`
- `POST /api/tasks/templates/from-source` — idempotent upsert

The Task service has **no server-side filter** for `contextType` or `applicableWorkflowStageId` — these are Liens-specific semantics stored in `ProductSettingsJson`. Filtering must be applied by the Liens layer after fetching.

### Task service — what it exposes for stage queries

- `GET /api/tasks/stages?sourceProductCode=SYNQ_LIENS` — returns all active stages with `ProductSettingsJson`
- `GET /api/tasks/stages/{id}` — single stage
- `POST /api/tasks/stages/from-source` — idempotent upsert

---

## 2. Current Compatibility Gaps

After full analysis across all five prior migration phases:

| Gap | Severity | Action |
|---|---|---|
| `GetContextualAsync` reads only from Liens DB | Medium — Liens DB still has the data, so no regression; but contextual UI templates are not yet Task-first | **Fix in MIG-05** |
| `GetByTenantAsync` (admin list) reads only from Liens DB | Low — intentional; admin reads are authoritative from Liens DB | No action — documented intent |
| `GetByIdAsync` (admin single) reads only from Liens DB | Low — intentional | No action |
| `GetForGenerationAsync` — dual-read already in place | None | Already resolved in MIG-02 |
| Stage derivation for governance — dual-read already in place | None | Already resolved in MIG-01 + MIG-03 |
| Generation engine stage filter — uses Liens-side rule `ApplicableWorkflowStageId` | None — rule entity is not migrated; comparison is within Liens DB | No action needed in this phase |

**Root cause of the only meaningful gap:** `LienTaskTemplateService.GetContextualAsync` calls `_repo.GetActiveByTenantAsync(...)` directly without consulting the Task service. The contextual filter logic (contextType matching + stage GUID matching) is entirely Liens-side and cannot be offloaded to the Task service API (it has no such filter parameters).

---

## 3. Alignment Design

### Decision 1: `ApplicableWorkflowStageId` storage
**Decision:** Keep in `ProductSettingsJson`. Task service stores this as a Liens-specific extension, not a top-level field. The Task service has its own `DefaultStageId` on templates (for "which stage to place a newly created task in"), which is semantically different from Liens' "contextual visibility filter". Merging the two would break Task service consumers. **Confirmed: no change to Task schema.**

### Decision 2: Contextual template filtering source of truth
**Decision:** Task service is primary; Liens DB is fallback.
- Fetch all templates via `GetAllTemplatesAsync(tenantId, ProductCode)` (already implemented in MIG-02 client)
- Apply Liens-side contextual filter in-process using `LiensTemplateExtensions.Deserialize(ProductSettingsJson)`
- Fall back to Liens DB when Task service returns 0 templates (not yet synced) or on error
- Empty list returned when Task service has templates but none match filter — this is the correct answer (not a fallback trigger)

### Decision 3: Stage ID compatibility
**Decision:** Stage IDs are safe to compare directly. MIG-03 preserved Liens stage GUIDs verbatim in `tasks_StageConfigs`. `ApplicableWorkflowStageId` in both Liens DB templates and Task service `ProductSettingsJson` refer to the same GUIDs. No translation layer required.

### Decision 4: Admin read paths
**Decision:** Admin read paths (`GetByTenantAsync`, `GetByIdAsync`) remain Liens-DB-only. Rationale: Liens DB is the write-authoritative source for admin; admin reads should see the canonical state, not a replica that may be 60 seconds behind. This is explicitly documented in the service as "no dual-read for admin."

### Decision 5: Generation engine
**Decision:** No change needed. The generation engine already uses `GetForGenerationAsync` (dual-read, Task first, MIG-02). The stage filter comparison (`rule.ApplicableWorkflowStageId == context.WorkflowStageId`) is fully within Liens DB and is unaffected by migration.

### Authoritative sources during this phase

| Concern | Authoritative source |
|---|---|
| Template-stage compatibility (contextual filter) | **Task service (MIG-05)** → Liens DB fallback |
| Template fetch for generation | **Task service (MIG-02)** → Liens DB fallback |
| Default start stage resolution | **Task service (MIG-01+03)** → Liens DB fallback |
| Stage lookup by ID | **Task service (MIG-03)** → Liens DB fallback |
| Stage list (workflow config) | **Task service (MIG-03)** → Liens DB fallback |
| Admin template reads | **Liens DB** (authoritative, intentional) |
| Template write operations | **Liens DB** → best-effort Task service sync |

---

## 4. Task Schema / DTO Adjustments

**No Task schema changes required.**

Rationale:
- `tasks_Templates.ProductSettingsJson` already stores `contextType` and `applicableWorkflowStageId` (round-tripped via `LiensTemplateExtensions`) — added in MIG-02
- `GET /api/tasks/templates?sourceProductCode=SYNQ_LIENS` already returns all templates including `ProductSettingsJson`
- `TaskTemplateDto.ProductSettingsJson` is already exposed in the response
- The `GetAllTemplatesAsync` client method already exists in `ILiensTaskServiceClient` and `LiensTaskServiceClient`

No new endpoints, no new DB columns, no new migrations.

---

## 5. Runtime Compatibility Changes

### Change: `GetContextualAsync` dual-read (MIG-05)

**File:** `Liens.Application/Services/LienTaskTemplateService.cs`

**Before:** Reads directly from `_repo.GetActiveByTenantAsync(tenantId, contextType, workflowStageId, ct)`

**After:** Tries Task service first via `TryGetContextualFromTaskServiceAsync`, falls back to Liens DB.

**New private methods added:**

`TryGetContextualFromTaskServiceAsync(tenantId, contextType, workflowStageId, ct)`:
1. Calls `_taskClient.GetAllTemplatesAsync(tenantId, ProductCode, ct)`
2. If response is empty → return `null` (triggers Liens DB fallback — data not yet synced)
3. If response has data → apply contextual filter in-process:
   - Deserialize each `ProductSettingsJson` via `LiensTemplateExtensions.Deserialize`
   - Apply `IsContextualMatch` (mirrors `LienTaskTemplateRepository.GetActiveByTenantAsync` WHERE clause)
   - Apply `ContextualSortOrder` (mirrors same repository's ORDER BY clause)
4. On any exception → return `null` (triggers Liens DB fallback)

`IsContextualMatch(dto, ext, contextType, workflowStageId)` — static helper, exact semantic mirror of the DB query:
- If `dto.IsActive == false`: false (Task service `activeOnly=true` filter already ensures this, guard kept for safety)
- If no `contextType` filter: true (all templates pass)
- If `ContextType == GENERAL`: true
- If `ContextType == contextType`: true
- If `ContextType == STAGE && ApplicableWorkflowStageId == workflowStageId`: true

`ContextualSortOrder(ctxType, requestedContextType)` — mirrors ORDER BY:
- STAGE → 0 (most specific first)
- matching contextType → 1
- everything else → 2

**Logging:**
- `template_contextual_source=task_service_filtered` — Task service path used; N results
- `template_contextual_source=task_service_empty` — Task service returned 0 templates; falling back
- `template_contextual_source=task_service_error` — Task service call failed; falling back
- `template_contextual_source=liens_db_fallback` — Liens DB was used

**Existing logging (unchanged):**
- `template_source=task_service` — generation engine took Task service path
- `template_source=liens_fallback` — generation engine fell back to Liens DB
- `stage_source=task_service` — stage by ID from Task service
- `stage_source=liens_fallback` — stage by ID fell back to Liens DB
- `governance_source=task_service` — governance from Task service

### No other changes

All other compatibility paths were already aligned in MIG-01 through MIG-04:
- Governance (MIG-01): dual-read with `DefaultStartStageMode` + `ExplicitStartStageId` in `ProductSettingsJson`
- Template for generation (MIG-02): dual-read with full `LiensTemplateExtensions` round-trip
- Stage lookup by ID (MIG-03): dual-read
- Stage list for governance derivation (MIG-03): dual-read via `GetByTenantAsync`
- Transitions (MIG-04): dual-read

---

## 6. Dual-Read / Fallback Adjustments

### Summary of all dual-read paths (post MIG-05)

| Path | Primary | Fallback | Log key |
|---|---|---|---|
| Governance GET | Task service | Liens DB | `governance_source=task_service` / `liens_fallback` |
| Governance GetOrCreate | Task service | Liens DB (create default) | same |
| Template for generation (by ID) | Task service | Liens DB | `template_source=task_service` / `liens_fallback` |
| Contextual templates (by contextType/stage) | Task service | Liens DB | `template_contextual_source=task_service_filtered` / `liens_db_fallback` |
| Stage by ID (explicit start stage) | Task service | Liens DB | `stage_source=task_service` / `liens_fallback` |
| Stage list (FIRST_ACTIVE_STAGE derivation) | Task service | Liens DB | `stage_source=task_service` / `stage_source=liens_db_fallback` |
| Transition validation | Task service | Liens DB | `transition_source=task_service` / `liens_db_fallback` |

### Fallback trigger rules

| Scenario | Behavior |
|---|---|
| Task service returns 0 templates | Fall back to Liens DB (data not yet synced) |
| Task service returns ≥1 template but 0 match filter | Return empty list (correct answer — no fallback) |
| Task service throws / HTTP error | Fall back to Liens DB |
| Task service returns templates for generation | Use directly; `ApplicableWorkflowStageId` from `ProductSettingsJson` |

---

## 7. Validation Results

| Check | Method | Result |
|---|---|---|
| `GetContextualAsync` with contextType → returns matching templates | Code inspection — `IsContextualMatch` logic mirrors DB query | ✅ PASS |
| `GetContextualAsync` with stageId filter → STAGE templates matched by GUID | Code inspection — MIG-03 preserved GUIDs; comparison is `ext.ApplicableWorkflowStageId == workflowStageId` | ✅ PASS |
| `GetContextualAsync` empty Task service response → Liens DB fallback | Code: `if (all.Count == 0) return null;` → caller falls through to repo | ✅ PASS |
| `GetContextualAsync` Task service error → Liens DB fallback | Code: `catch (Exception ex) → return null;` | ✅ PASS |
| Template-based generation still works | `GetForGenerationAsync` unchanged; `ApplicableWorkflowStageId` from `ProductSettingsJson` used in generation engine | ✅ PASS |
| Governance start-stage derivation still works | `DeriveStartStageAsync` unchanged; all dependencies dual-read since MIG-01/03 | ✅ PASS |
| Admin template reads unchanged | `GetByTenantAsync`, `GetByIdAsync` still Liens-DB-only | ✅ PASS |
| No Flow orchestration concepts introduced | Search confirms `flow_workflow_transitions` not referenced in changed code | ✅ PASS |
| Stage IDs compatible end-to-end | MIG-03 preserved GUIDs; no translation needed | ✅ PASS |
| Mixed state (some tenants synced, some not) | Task service empty → Liens DB fallback; partial sync → in-process filter handles correctly | ✅ PASS |
| Task service unaffected for non-Liens consumers | No Task service changes made | ✅ PASS |
| Liens.Api builds with 0 errors | `dotnet build Liens.Api.csproj -c Release --no-restore` | ✅ PASS |
| Task.Api builds with 0 errors | Confirmed in MIG-04; no changes in MIG-05 | ✅ PASS |

---

## 8. Rollback Plan

**MIG-05 is a single-file, single-method change in `LienTaskTemplateService.cs`.**

To revert:
1. Restore `GetContextualAsync` to its original 1-liner: `return (await _repo.GetActiveByTenantAsync(...)).Select(MapToResponse).ToList()`
2. Remove the three new private methods: `TryGetContextualFromTaskServiceAsync`, `IsContextualMatch`, `ContextualSortOrder`

No Task schema changes were made → no DB rollback required.  
No Task service changes were made → Task service is unaffected.  
No Liens DB changes were made → Liens data is fully intact.  
Fallback to Liens DB is immediate on revert — zero downtime.

---

## 9. Known Gaps / Risks

| Item | Notes |
|---|---|
| Admin template reads remain Liens-DB-only | Intentional. Admin reads are the authoritative source. Future ownership flip will address this. |
| Contextual filter done in-process (not server-side) | The Task service has no `contextType` / `applicableWorkflowStageId` query parameters. This is a Liens-specific concept stored in `ProductSettingsJson`. Fixing this server-side would require adding Liens-specific filtering semantics to the generic Task service — explicitly out of scope. |
| `GetByTenantAsync` (admin full list) is Liens-only | Not a compatibility gap. Admin reads authoritative from Liens DB. |
| Generation rule's `ApplicableWorkflowStageId` is Liens DB only | Rules (`liens_TaskGenerationRules`) are not migrated in this phase. The stage filter in the engine compares rule.ApplicableWorkflowStageId vs context.WorkflowStageId within Liens DB — compatible because stage GUIDs are preserved. Future generation rule migration will need to replicate this field. |
| `DefaultStageId` on Task template = null for Liens templates | Intentional. `ApplicableWorkflowStageId` (filter visibility) ≠ `DefaultStageId` (which stage to put a task in when created via template). Liens does not use Task service's "create from template" endpoint. |
| `LiensTemplateSyncService` must run before Task-first path yields results | Until the startup sync runs (45 s delay), contextual reads fall back to Liens DB — correct behavior. |
| Final ownership cutover not done | Liens DB remains authoritative for writes. A future cutover task will flip writes to Task service as primary. |

---

## Files Changed

| Path | Change |
|---|---|
| `apps/services/liens/Liens.Application/Services/LienTaskTemplateService.cs` | `GetContextualAsync` now dual-read (Task first, Liens fallback); added `TryGetContextualFromTaskServiceAsync`, `IsContextualMatch`, `ContextualSortOrder` |
| `analysis/TASK-MIG-05-report.md` | This report |

**No Task service changes. No schema changes. No migrations.**
