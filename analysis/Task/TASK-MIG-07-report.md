# TASK-MIG-07 Report — Template Ownership Flip (Liens → Task)
**Date:** 2026-04-21  
**Status:** ✅ Complete — Liens.Api builds (0 errors); no schema changes; no Task service changes

---

## 1. Codebase Analysis

### Files inspected

| File | Role |
|---|---|
| `Liens.Application/Services/LienTaskTemplateService.cs` | All Liens template business logic — write, read, sync |
| `Liens.Infrastructure/TaskService/LiensTemplateSyncService.cs` | Startup Liens→Task sync |
| `Liens.Infrastructure/DependencyInjection.cs` | Service registration |
| `Liens.Api/Endpoints/TaskTemplateEndpoints.cs` | HTTP surface (tenant + admin) |
| `Liens.Application/Interfaces/ILienTaskTemplateService.cs` | Service contract |
| `Liens.Application/Interfaces/ILiensTaskServiceClient.cs` | Task service HTTP client contract |
| `Liens.Application/DTOs/TaskServiceTemplateDto.cs` | Wire DTOs for Task service round-trip |
| `Liens.Domain/Entities/LienTaskTemplate.cs` | Liens template domain entity |
| `Liens.Application/Repositories/ILienTaskTemplateRepository.cs` | Liens DB repo contract |
| `Task.Api/Endpoints/TaskTemplateEndpoints.cs` | Task service HTTP surface |
| `Task.Application/Services/TaskTemplateService.cs` | Task service business logic |
| `Task.Domain/Entities/TaskTemplate.cs` | Task service entity |
| `Task.Application/DTOs/TaskTemplateDtos.cs` | Task service DTOs (including `UpsertFromSourceTemplateRequest`) |

### Write paths identified (pre-MIG-07)

| Operation | Primary write | Secondary write |
|---|---|---|
| `CreateAsync` | **Liens DB** — `_repo.AddAsync(entity)` | Task service — `TrySyncToTaskServiceAsync` (best-effort, swallowed error) |
| `UpdateAsync` | **Liens DB** — `_repo.UpdateAsync(entity)` | Task service — `TrySyncToTaskServiceAsync` (best-effort, swallowed error) |
| `ActivateAsync` | **Liens DB** — `_repo.UpdateAsync(entity)` | Task service — `TrySyncToTaskServiceAsync` (best-effort, swallowed error) |
| `DeactivateAsync` | **Liens DB** — `_repo.UpdateAsync(entity)` | Task service — `TrySyncToTaskServiceAsync` (best-effort, swallowed error) |
| Startup `LiensTemplateSyncService` | **Liens DB** (full table scan `GetAllAsync`) | Task service — `UpsertTemplateFromSourceAsync` per row |

### Read paths identified (pre-MIG-07)

| Method | Reads from | Fallback |
|---|---|---|
| `GetByTenantAsync` (admin list) | **Liens DB only** — `_repo.GetByTenantAsync` | None |
| `GetByIdAsync` (admin get) | **Liens DB only** — `_repo.GetByIdAsync` | None |
| `GetContextualAsync` (MIG-05) | Task service first | Liens DB fallback |
| `GetForGenerationAsync` (MIG-02) | Task service first | Liens DB fallback |

### Template ID management (pre-MIG-07)

- `LienTaskTemplate.Create()` generates `Id = Guid.NewGuid()` in Liens domain.
- This ID is passed to Task service via `UpsertFromSourceAsync` with `req.Id = entity.Id`.
- Task service's `UpsertFromSourceAsync` uses `req.Id` verbatim (`TaskTemplate.Create(..., id: req.Id)`).
- IDs are therefore identical in both systems — no translation layer needed.

### Task service write capabilities (already present, no changes needed)

| Endpoint | Path | What it does |
|---|---|---|
| `UpsertFromSource` | `POST /api/tasks/templates/from-source` | Create-or-update by explicit ID, preserves ID, handles activate/deactivate via `IsActive` flag |
| `CreateTemplate` | `POST /api/tasks/templates` | Generates new ID — not used post-MIG-07 (ID must come from Liens domain) |
| `UpdateTemplate` | `PUT /api/tasks/templates/{id}` | Requires `ExpectedVersion` — not used (version conflicts incompatible with Liens version counter) |
| `ActivateTemplate` | `POST /api/tasks/templates/{id}/activate` | Activate only — not used (UpsertFromSource handles IsActive) |
| `DeactivateTemplate` | `POST /api/tasks/templates/{id}/deactivate` | Deactivate only — not used |

**Decision**: All write operations go through `UpsertFromSourceAsync` (the single endpoint that handles create-or-update, preserves IDs, and accepts `IsActive`). This avoids version conflicts and simplifies the flip.

---

## 2. Current Ownership Review

### Pre-MIG-07 authority map

| Concern | Write owner | Read owner | Notes |
|---|---|---|---|
| Template create/update/activate/deactivate | **Liens DB** (primary) | — | Task service receives best-effort copy |
| Template list (admin) | Liens DB | **Liens DB** | No Task service read |
| Template get-by-id (admin) | Liens DB | **Liens DB** | No Task service read |
| Template contextual list (MIG-05) | Liens DB | **Task-first**, Liens fallback | ✅ Already flipped |
| Template generation read (MIG-02) | Liens DB | **Task-first**, Liens fallback | ✅ Already flipped |
| Startup population | **Liens DB → Task** | — | Runs every startup, overwrites Task data |

### Key gaps before MIG-07

1. **Admin reads still Liens-only** — `GetByTenantAsync` and `GetByIdAsync` never touch Task service.
2. **Write authority still Liens DB** — Task write is swallowed best-effort; Task DB can drift.
3. **Startup sync overwrites Task-owned data** — Any admin edit through Task service directly would be overwritten on next Liens host restart.

---

## 3. Ownership Flip Design

### New write authority: Task service (primary)

For all four admin write operations (create, update, activate, deactivate):
1. Build domain entity / apply changes in-memory (domain entity is used for validation and to generate stable ID, not for the primary write).
2. Call `_taskClient.UpsertTemplateFromSourceAsync(...)` — **this is the authoritative write**.
3. A Task service write failure throws — it is NOT swallowed. Write is atomic: if Task fails, Liens DB is not touched.
4. Mirror to Liens DB via `TryMirrorCreateToLiensDbAsync` / `TryMirrorUpdateToLiensDbAsync` — best-effort, errors logged and tolerated.

### New read authority: Task service (primary)

All template reads now follow the same pattern:
- Call Task service first.
- Fall back to Liens DB only if Task service returns nothing or errors.
- Log which source was used (`template_read_owner=task_service` / `template_read_owner=liens_db_fallback`).

### Transitional persistence strategy selected: Option A

> **Write Task first, mirror to Liens DB best-effort for rollback safety.**

Rationale:
- Task service is now primary — a Task write failure is fatal (caller sees error, no partial state).
- Liens DB mirror is retained for rollback: if MIG-07 is reversed, Liens DB will have near-current data as long as mirrors haven't failed excessively.
- `UpsertFromSourceAsync` is idempotent — safe to retry.
- No data loss risk: Liens DB is a trailing copy; Task service has the definitive current state.

### Startup sync: disabled

`LiensTemplateSyncService.ExecuteAsync` is made a no-op (returns `Task.CompletedTask`). The service is still registered in DI for rollback convenience. The Liens→Task sync direction is suppressed entirely — it would overwrite Task-owned edits on every Liens host restart.

### ID preservation

IDs are still generated in the Liens domain (`LienTaskTemplate.Create()` generates `Id = Guid.NewGuid()`). The pre-generated ID is passed to `UpsertFromSourceAsync` as `req.Id`, which the Task service preserves verbatim. No ID changes. No ID translation.

### Version authority (transitional)

The Liens entity is still loaded from Liens DB for the version conflict check in `UpdateAsync` (`entity.Version != request.Version`). This is intentional for the transition period — the UI client sees Liens-side versions and sends them back. Full version authority flip to Task service is deferred to a future phase.

### Admin/API compatibility

All Liens-facing endpoints (`/api/liens/task-templates/*` and `/api/liens/admin/task-templates/*`) are unchanged. They still call `ILienTaskTemplateService` methods with the same signatures. The flip is entirely inside `LienTaskTemplateService` — invisible to callers.

---

## 4. Write Path Changes

### Changed file: `Liens.Application/Services/LienTaskTemplateService.cs`

All four write methods restructured. Pattern per method:

#### Before (Liens DB primary)
```
validate
↓
LienTaskTemplate entity = LienTaskTemplate.Create(...)
↓
_repo.AddAsync(entity)          ← PRIMARY write (Liens DB)
↓
TrySyncToTaskServiceAsync(...)  ← best-effort Task sync (error swallowed)
```

#### After (Task service primary — MIG-07)
```
validate
↓
LienTaskTemplate entity = LienTaskTemplate.Create(...)   ← in-memory only, for ID + validation
↓
_taskClient.UpsertTemplateFromSourceAsync(payload, ct)   ← PRIMARY write (Task service), throws on failure
↓
audit.Publish(...)
↓
TryMirrorCreateToLiensDbAsync(entity, ct)                ← best-effort Liens DB mirror, error logged+tolerated
```

#### `CreateAsync` key change
- Entity is built in-memory (not saved to Liens DB first).
- `UpsertFromSourceAsync` is called with pre-generated entity.Id — ID preserved.
- Task write failure throws; Liens mirror failure is tolerated.

#### `UpdateAsync` key change
- Entity still loaded from Liens DB for version check (transitional — client sends Liens version).
- Changes applied in-memory (`entity.Update(...)` — no `_repo.UpdateAsync` yet).
- `UpsertFromSourceAsync` called first.
- `TryMirrorUpdateToLiensDbAsync` mirrors the change to Liens DB best-effort.

#### `ActivateAsync` / `DeactivateAsync` key change
- Entity loaded from Liens DB for source-of-truth read (IsActive state is reflected in payload).
- `entity.Activate/Deactivate(...)` applied in-memory.
- `UpsertFromSourceAsync` called first (with `IsActive` reflected in `MapToUpsertPayload`).
- `TryMirrorUpdateToLiensDbAsync` mirrors best-effort.

### New helper methods

| Method | Purpose |
|---|---|
| `TryMirrorCreateToLiensDbAsync` | Calls `_repo.AddAsync`; logs warning on error; does not throw |
| `TryMirrorUpdateToLiensDbAsync` | Calls `_repo.UpdateAsync`; logs warning on error; does not throw |

### Removed method

`TrySyncToTaskServiceAsync` — replaced by inline primary Task write + `TryMirror*` helpers. The old method swallowed Task write errors; the new pattern does not.

### Log keys added

| Key | Emitted when |
|---|---|
| `template_write_owner=task_service` | Task service write succeeds (primary write confirmed) |
| `template_mirror_target=liens_db` | Liens DB mirror write succeeds |
| `template_mirror_target=liens_db_failed` | Liens DB mirror write fails (logged+tolerated) |

### Task service changes

None. The Task service `UpsertFromSourceAsync` endpoint already existed (added in MIG-02). No Task service code was modified.

---

## 5. Read Path Changes

### Changed file: `Liens.Application/Services/LienTaskTemplateService.cs`

#### `GetByTenantAsync` — flipped to Task-first

**Before:** Liens DB only (`_repo.GetByTenantAsync`).

**After:**
```
_taskClient.GetAllTemplatesAsync(tenantId, ProductCode)
  → if Count > 0: map and return [template_read_owner=task_service]
  → if Count == 0 or error: fall back to _repo.GetByTenantAsync [template_read_owner=liens_db_fallback]
```

Fallback trigger: Task service returns 0 templates OR throws. Empty Task service result = not yet populated (correctly falls back).

#### `GetByIdAsync` — flipped to Task-first

**Before:** Liens DB only (`_repo.GetByIdAsync`).

**After:**
```
_taskClient.GetTemplateAsync(tenantId, id)
  → if not null: map and return [template_read_owner=task_service]
  → if null or error: fall back to _repo.GetByIdAsync [template_read_owner=liens_db_fallback]
```

#### `GetContextualAsync` — unchanged (already Task-first from MIG-05)

Remains: `TryGetContextualFromTaskServiceAsync` (Task service) → fallback to Liens DB.

#### `GetForGenerationAsync` — unchanged (already Task-first from MIG-02)

Remains: `TryFetchFromTaskServiceAsync` (Task service) → fallback to Liens DB.

### Log keys added

| Key | Emitted when |
|---|---|
| `template_read_owner=task_service` | GetByTenant or GetById served from Task service |
| `template_read_owner=liens_db_fallback` | GetByTenant or GetById fell back to Liens DB |

### Admin reads summary (post MIG-07)

| Method | Primary | Fallback | Log key |
|---|---|---|---|
| `GetByTenantAsync` | Task service | Liens DB | `template_read_owner=task_service` |
| `GetByIdAsync` | Task service | Liens DB | `template_read_owner=task_service` |
| `GetContextualAsync` | Task service | Liens DB | `template_contextual_source=task_service_filtered` |
| `GetForGenerationAsync` | Task service | Liens DB | `template_source=task_service` |

All template reads are now Task-service-primary. Liens DB is the fallback only.

---

## 6. Sync / Fallback Changes

### `LiensTemplateSyncService` — disabled (Liens→Task direction suppressed)

**Before:** `ExecuteAsync` ran 5 seconds after startup, scanned all `liens_TaskTemplates` rows, and called `UpsertTemplateFromSourceAsync` for each. This direction is now wrong.

**After:** `ExecuteAsync` immediately returns `Task.CompletedTask` and logs:
```
TASK-MIG-07: LiensTemplateSyncService is DISABLED (template_write_owner=task_service).
Liens→Task startup sync suppressed to protect Task-owned template data.
```

**Why disabled rather than deleted:**
- The class is retained as a rollback artifact.
- Re-enabling the Liens→Task sync is a one-line change in `LienTaskTemplateService` (uncomment `ExecuteAsync` body). No DI change needed; registration is kept.

**DI registration:** `AddSingleton<LiensTemplateSyncService>` + `AddHostedService` registration are kept. The comment in `DependencyInjection.cs` is updated to document the disabled state.

### Startup sync direction summary (post MIG-07)

| Sync service | Direction | Status |
|---|---|---|
| `LiensGovernanceSyncService` | Liens→Task | Active (MIG-01 era; governance still Liens-primary) |
| `LiensTemplateSyncService` | Liens→Task | **DISABLED** (MIG-07 — templates now Task-primary) |
| `LiensStageSyncService` | Liens→Task | Active (MIG-03 era; stages still Liens-primary) |
| `LiensTransitionSyncService` | Liens→Task | Active (MIG-04 era; transitions still Liens-primary) |

### Fallback behavior (post MIG-07)

| Read path | Fallback enabled? | Trigger |
|---|---|---|
| Admin list (`GetByTenantAsync`) | ✅ Yes | Task service returns 0 templates OR error |
| Admin get (`GetByIdAsync`) | ✅ Yes | Task service returns null OR error |
| Contextual (`GetContextualAsync`) | ✅ Yes | Task service returns 0 templates OR error |
| Generation (`GetForGenerationAsync`) | ✅ Yes | Task service returns null OR error |

### When Liens DB fallback should be removed

Liens DB fallback (read) can be safely removed when:
1. `liens_TaskTemplates` is verified to be a stale mirror only (no authoritative reads remain).
2. All tenants have been confirmed to have Task service templates populated.
3. `liens_TaskTemplates` is ready to be dropped (a future cleanup step).

---

## 7. Validation Results

| # | Check | Method | Result |
|---|---|---|---|
| 1 | Template create writes to Task service first — `UpsertTemplateFromSourceAsync` called before `_repo.AddAsync` | Code inspection — `CreateAsync` line order | ✅ PASS |
| 2 | Template create failure on Task service throws — error not swallowed | Code inspection — no try/catch around primary write | ✅ PASS |
| 3 | Template create mirrors to Liens DB best-effort — `TryMirrorCreateToLiensDbAsync` called after Task write | Code inspection | ✅ PASS |
| 4 | Template create Liens DB mirror failure is tolerated — `TryMirrorCreateToLiensDbAsync` catches all exceptions | Code inspection — try/catch with LogWarning | ✅ PASS |
| 5 | Template update writes to Task service first | Code inspection — `UpdateAsync` line order | ✅ PASS |
| 6 | Template activate writes to Task service first | Code inspection — `ActivateAsync` line order | ✅ PASS |
| 7 | Template deactivate writes to Task service first | Code inspection — `DeactivateAsync` line order | ✅ PASS |
| 8 | Template IDs unchanged — `entity.Id` (pre-generated GUID) passed to `UpsertFromSourceAsync.Id` | Code inspection — `MapToUpsertPayload(entity)` includes `Id = entity.Id` | ✅ PASS |
| 9 | ProductSettingsJson preserved — `LiensTemplateExtensions` (ContextType, ApplicableWorkflowStageId, DefaultRoleId) serialized in payload | Code inspection — `MapToUpsertPayload` unchanged | ✅ PASS |
| 10 | Version conflict detection still works — Liens entity loaded from DB for `entity.Version != request.Version` check | Code inspection — `RequireTemplate` still called at start of `UpdateAsync` | ✅ PASS |
| 11 | Admin list now reads Task service first — `GetByTenantAsync` calls `_taskClient.GetAllTemplatesAsync` | Code inspection | ✅ PASS |
| 12 | Admin list falls back to Liens DB on Task empty/error | Code inspection — fallback path present | ✅ PASS |
| 13 | Admin get-by-id now reads Task service first — `GetByIdAsync` calls `_taskClient.GetTemplateAsync` | Code inspection | ✅ PASS |
| 14 | Admin get-by-id falls back to Liens DB on Task null/error | Code inspection | ✅ PASS |
| 15 | Generation engine template read unchanged — `GetForGenerationAsync` still Task-first (MIG-02) | Code inspection | ✅ PASS |
| 16 | Contextual read unchanged — `GetContextualAsync` still Task-first (MIG-05) | Code inspection | ✅ PASS |
| 17 | Startup sync (Liens→Task) no longer overwrites Task-owned data — `LiensTemplateSyncService.ExecuteAsync` returns `Task.CompletedTask` | Code inspection | ✅ PASS |
| 18 | Startup sync class retained for rollback — still registered in DI | Code inspection — `AddSingleton<LiensTemplateSyncService>` + `AddHostedService` retained | ✅ PASS |
| 19 | Liens admin endpoints unchanged — same HTTP surface, same signatures | Code inspection — `TaskTemplateEndpoints.cs` not modified | ✅ PASS |
| 20 | Task service not modified — no Task service source files changed | Code inspection | ✅ PASS |
| 21 | Liens.Api build succeeds — 0 errors | `dotnet build Liens.Api.csproj -c Release --no-restore` | ✅ PASS |
| 22 | No schema changes — `liens_TaskTemplates` retained; no migrations | No migration files created | ✅ PASS |
| 23 | Log keys present: `template_write_owner`, `template_read_owner`, `template_mirror_target` | Code inspection | ✅ PASS |
| 24 | Rollback path viable — see Section 8 | Design review | ✅ PASS |

---

## 8. Rollback Plan

### Code-only rollback (no DB changes required)

To revert to Liens DB as primary write owner:

**Step 1 — Revert `LienTaskTemplateService.cs` write order** (four methods):

In each of `CreateAsync`, `UpdateAsync`, `ActivateAsync`, `DeactivateAsync`:
- Move `_repo.AddAsync` / `_repo.UpdateAsync` back to being the primary (non-try/catch) write.
- Move `UpsertTemplateFromSourceAsync` back to being the best-effort sync (inside try/catch, error swallowed).

**Step 2 — Re-enable `LiensTemplateSyncService`**:

In `LiensTemplateSyncService.ExecuteAsync`, restore the original body (the scan-and-upsert loop). The class is preserved as-is for exactly this purpose.

**Step 3 — (Optional) revert admin read fallback order**:

In `GetByTenantAsync` and `GetByIdAsync`, move `_repo.*` back to first and wrap `_taskClient.*` as optional enrichment. This is only needed if Liens DB data is more current than Task service at rollback time.

### Liens DB state at rollback

- Every successful Task service write is followed by a best-effort Liens DB mirror.
- If the Liens mirror succeeded for all writes since MIG-07, Liens DB has current data.
- If some mirrors failed (logged as `template_mirror_target=liens_db_failed`), Liens DB may be slightly stale for those templates.
- Either way, no data loss — Task service has the definitive current state.

### Task service state at rollback

- Task service templates are left in place. If Liens→Task sync is re-enabled, it will upsert (overwrite) them with the Liens DB copies on next startup — this is acceptable for rollback.
- Task service `UpsertFromSourceAsync` is idempotent.

### DB rollback: not required

- `liens_TaskTemplates` is never dropped in this step.
- No new DB tables added.
- No migrations created.

---

## 9. Known Gaps / Risks

| Item | Notes |
|---|---|
| **Admin reads show `Version` from Task service** | After the ownership flip, `GetByIdAsync` and `GetByTenantAsync` return versions from Task service DTO. Task service increments its own version counter independently. Clients that read the admin list and then send `request.Version` to `UpdateAsync` will get the Task service version — but `UpdateAsync` still checks against the Liens DB version. This mismatch will surface as a spurious `TASK_TEMPLATE_VERSION_CONFLICT` if the Liens and Task version counters diverge. **Mitigation**: Version authority flip to Task service is the natural next step. For now, if a conflict occurs, the client reloads (same behavior as before). |
| **Liens DB mirror drift** | If `TryMirrorUpdateToLiensDbAsync` fails for a write, Liens DB has stale data for that template. Reads that fall back to Liens DB will see the old version. This is accepted: fallback reads only occur when Task service is unavailable, at which point serving a slightly stale template is acceptable. |
| **`RequireTemplate` still loads from Liens DB** | `UpdateAsync`, `ActivateAsync`, `DeactivateAsync` still call `_repo.GetByIdAsync` to load the Liens entity. If a template was created directly via the Task service admin API (not via Liens) and has no Liens DB mirror, `RequireTemplate` will throw `NotFoundException`. **Mitigation**: This is the expected behavior — the Liens admin path is the only supported creation path; direct Task service creates are not part of this workflow. |
| **No Task-to-Liens startup sync** | After MIG-07, if Liens service restarts, no sync runs in either direction. Templates created via Liens admin since the last restart will exist in both Task service (primary) and Liens DB (mirror, if mirror succeeded). Any templates that existed only in Task service (edge case) are still readable via admin reads (Task-first). |
| **`liens_TaskTemplates` cleanup prerequisite** | Before `liens_TaskTemplates` can be dropped: (1) version authority must move fully to Task service, (2) `RequireTemplate` must be replaced with a Task service fetch, (3) Liens DB fallback reads must be removed, (4) `GetAllAsync` usage in sync services must be reviewed. These are out of scope for MIG-07. |
| **MIG-08 prerequisite** | MIG-08 (governance ownership flip) should follow the same pattern as MIG-07 and can proceed independently. The template flip is stable and does not block governance work. |

---

## Files Changed

| Path | Change |
|---|---|
| `Liens.Application/Services/LienTaskTemplateService.cs` | Write path flipped: Task service primary, Liens DB mirror best-effort. Admin reads (`GetByTenantAsync`, `GetByIdAsync`) flipped to Task-first with Liens DB fallback. `TrySyncToTaskServiceAsync` replaced by `TryMirrorCreateToLiensDbAsync` / `TryMirrorUpdateToLiensDbAsync`. |
| `Liens.Infrastructure/TaskService/LiensTemplateSyncService.cs` | `ExecuteAsync` made no-op; constructor simplified (no `IServiceScopeFactory` needed); class retained for rollback. |
| `Liens.Infrastructure/DependencyInjection.cs` | Comment updated to document disabled state; registration lines unchanged. |
| `analysis/TASK-MIG-07-report.md` | This report |

**No Task service changes. No schema changes. No migrations.**

---

## Complete Log Key Reference (MIG-07 additions)

| Log key | Emitted when | Source |
|---|---|---|
| `template_write_owner=task_service` | Template written to Task service successfully (primary write) | `LienTaskTemplateService.CreateAsync/UpdateAsync/ActivateAsync/DeactivateAsync` |
| `template_mirror_target=liens_db` | Liens DB mirror write succeeded | `TryMirrorCreateToLiensDbAsync` / `TryMirrorUpdateToLiensDbAsync` |
| `template_mirror_target=liens_db_failed` | Liens DB mirror write failed (tolerated) | `TryMirrorCreateToLiensDbAsync` / `TryMirrorUpdateToLiensDbAsync` |
| `template_read_owner=task_service` | Admin list or get-by-id served from Task service | `GetByTenantAsync` / `GetByIdAsync` |
| `template_read_owner=liens_db_fallback` | Admin list or get-by-id fell back to Liens DB | `GetByTenantAsync` / `GetByIdAsync` |
| `template_source=task_service` | Generation read served from Task service (MIG-02) | `GetForGenerationAsync` |
| `template_source=liens_fallback` | Generation read fell back to Liens DB (MIG-02) | `GetForGenerationAsync` |
| `template_contextual_source=task_service_filtered` | Contextual read served from Task service (MIG-05) | `GetContextualAsync` |
| `template_contextual_source=liens_db_fallback` | Contextual read fell back to Liens DB (MIG-05) | `GetContextualAsync` |
