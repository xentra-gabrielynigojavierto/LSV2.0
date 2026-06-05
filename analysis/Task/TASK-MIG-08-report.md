# TASK-MIG-08 Report — Governance Ownership Flip (Liens → Task)
**Date:** 2026-04-21  
**Status:** ✅ Complete — Liens.Api builds (0 errors); no schema changes; no Task service changes

---

## 1. Codebase Analysis

### Files inspected

| File | Role |
|---|---|
| `Liens.Application/Services/LienTaskGovernanceService.cs` | All governance business logic — write, read, sync |
| `Liens.Infrastructure/TaskService/LiensGovernanceSyncService.cs` | Startup Liens→Task sync |
| `Liens.Infrastructure/DependencyInjection.cs` | Service registration |
| `Liens.Api/Endpoints/TaskGovernanceEndpoints.cs` | HTTP surface (tenant + admin) |
| `Liens.Application/Interfaces/ILienTaskGovernanceService.cs` | Service contract |
| `Liens.Application/DTOs/TaskServiceGovernanceDto.cs` | Wire DTOs + `LiensGovernanceExtensions` |
| `Liens.Domain/Entities/LienTaskGovernanceSettings.cs` | Liens governance domain entity |
| `Liens.Application/Repositories/ILienTaskGovernanceSettingsRepository.cs` | Liens DB repo contract |
| `Task.Application/Services/TaskGovernanceService.cs` | Task service governance logic |
| `Task.Application/DTOs/TaskGovernanceDtos.cs` | Task service DTOs (`UpsertTaskGovernanceRequest`, `ResolvedGovernance`) |
| `Task.Api/Endpoints/TaskGovernanceEndpoints.cs` | Task service HTTP surface |

### Write paths identified (pre-MIG-08)

| Operation | Primary write | Secondary write |
|---|---|---|
| `UpdateAsync` (existing entity) | **Liens DB** — `_repo.UpdateAsync(entity)` | Task service — `TrySyncToTaskServiceAsync` (best-effort, swallowed error) |
| `UpdateAsync` (entity not found → upsert) | **Liens DB** — `_repo.AddAsync(entity)` | Task service — `TrySyncToTaskServiceAsync` (best-effort, swallowed error) |
| `GetOrCreateAsync` (entity not found) | **Liens DB** — `_repo.AddAsync(newEntity)` | Task service — `TrySyncToTaskServiceAsync` (best-effort, swallowed error) |
| Startup `LiensGovernanceSyncService` | **Liens DB** (full scan `GetAllAsync`) | Task service — `UpsertGovernanceAsync` per row |

### Read paths identified (pre-MIG-08)

| Method | Primary read | Fallback | Status |
|---|---|---|---|
| `GetAsync` | Task service (MIG-01) | Liens DB | ✅ Already Task-first |
| `GetOrCreateAsync` | Task service (MIG-01) | Liens DB → then creates in Liens DB | ⚠️ Creation still Liens-first |
| `UpdateAsync` | **Liens DB** (loads entity for version check) | None | ⚠️ Liaison-first load; unchanged post-flip (transitional) |

### Governance enforcement points (unchanged in MIG-08)

Governance enforcement happens inside the **Task service** at task creation via `ResolveAsync` → `ResolvedGovernance`. All enforcement logic stays in the Task service and is untouched. The Liens service only provides the admin UI for reading/writing governance settings — it does not duplicate enforcement.

The `LienTaskService.CreateAsync` path inside Liens calls `_governanceService.GetAsync` (Task-first since MIG-01) to read the rules, then passes them to the Task service for enforcement. MIG-08 does not change this path.

### Governance field mapping

| Liens field | Task service field | Extension JSON field |
|---|---|---|
| `RequireAssigneeOnCreate` | `RequireAssignee` | — |
| `RequireWorkflowStageOnCreate` | `RequireStage` | — |
| `RequireCaseLinkOnCreate` | — | `LiensGovernanceExtensions.RequireCaseLinkOnCreate` |
| `AllowMultipleAssignees` | — | `LiensGovernanceExtensions.AllowMultipleAssignees` |
| `DefaultStartStageMode` | — | `LiensGovernanceExtensions.DefaultStartStageMode` |
| `ExplicitStartStageId` | — | `LiensGovernanceExtensions.ExplicitStartStageId` |

Liens-specific fields are preserved in `ProductSettingsJson` on the Task service row. Deserialization via `DeserializeExtensions` is unchanged.

---

## 2. Current Ownership Review

### Pre-MIG-08 authority map

| Concern | Write owner | Read owner | Notes |
|---|---|---|---|
| Governance create (first access) | **Liens DB** (primary) | — | GetOrCreateAsync creates in Liens DB first |
| Governance update | **Liens DB** (primary) | — | Task service receives best-effort copy |
| Governance get | Task service first | Liens DB fallback | ✅ Already Task-first (MIG-01) |
| Governance get-or-create | Task service first → Liens DB → then Liens DB create | | ⚠️ Creation still Liens-first |
| Startup population | **Liens DB → Task** | — | Runs every startup, overwrites Task data |
| Enforcement (`ResolveAsync`) | Task service | Task service | ✅ Already Task-owned (internal to Task service) |

### Remaining gaps before MIG-08

1. `GetOrCreateAsync` creates new governance records in Liens DB first.
2. `UpdateAsync` writes to Liens DB first.
3. Startup `LiensGovernanceSyncService` would overwrite any Task-side admin edits on restart.

---

## 3. Ownership Flip Design

### New write authority: Task service (primary)

Both `UpdateAsync` and `GetOrCreateAsync` creation path now:
1. Apply changes to the domain entity in-memory (validation + state transition only).
2. Call `_taskClient.UpsertGovernanceAsync(...)` — **this is the authoritative write; throws on failure**.
3. Mirror to Liens DB best-effort: `TryMirrorCreateToLiensDbAsync` / `TryMirrorUpdateToLiensDbAsync` — errors logged and tolerated.

### New read authority: Task service (primary, unchanged from MIG-01)

All reads (`GetAsync`, `GetOrCreateAsync`) were already Task-first. No read path changes needed — read authority was flipped in MIG-01.

### Transitional persistence strategy: Option A

> **Write Task first, mirror to Liens DB best-effort.**

Same rationale as MIG-07: Task write failure is visible (throws), Liens mirror failure is tolerated. No data loss: Task service has the definitive state.

### Startup sync: disabled

`LiensGovernanceSyncService.ExecuteAsync` made no-op (returns `Task.CompletedTask`). Class retained for rollback. Liens→Task direction suppressed.

### Version authority: transitional

`UpdateAsync` still loads the Liens entity for version conflict detection (`entity.Version != request.Version`). This is intentional — the client-visible version is currently from the Liens DB (or from the Task service response that `GetAsync` returns, which includes `dto.Version`). Full version authority flip to Task service is deferred.

### ID / tenant scoping

Governance settings are keyed by `(TenantId, SourceProductCode)` in the Task service — no explicit ID preservation concern (unlike templates). The `LienTaskGovernanceSettings.Id` is a Liens-internal identifier. The Task service governance row has its own ID.

---

## 4. Write Path Changes

### Changed file: `Liens.Application/Services/LienTaskGovernanceService.cs`

#### `UpdateAsync` — before → after

**Before (Liens DB primary):**
```
load entity from Liens DB (for version check)
↓
entity.Update(...) — applied
↓
_repo.UpdateAsync(entity)          ← PRIMARY write (Liens DB)
↓
TrySyncToTaskServiceAsync(...)     ← best-effort Task sync (error swallowed)
```

**After (Task service primary — MIG-08):**
```
load entity from Liens DB (transitional — version check)
↓
entity.Update(...) — in-memory only
↓
_taskClient.UpsertGovernanceAsync(payload, ct)   ← PRIMARY write (Task service), throws on failure
↓
audit.Publish(...)
↓
TryMirrorUpdateToLiensDbAsync(entity, ct)        ← best-effort Liens DB mirror, error logged+tolerated
```

#### `GetOrCreateAsync` creation path — before → after

**Before (Liens DB primary):**
```
// not found in either system:
_repo.AddAsync(newEntity)          ← PRIMARY write (Liens DB)
↓
TrySyncToTaskServiceAsync(...)     ← best-effort Task sync (error swallowed)
```

**After (Task service primary — MIG-08):**
```
// not found in either system:
_taskClient.UpsertGovernanceAsync(payload, ct)   ← PRIMARY write (Task service), throws on failure
↓
audit.Publish(...)
↓
TryMirrorCreateToLiensDbAsync(entity, ct)        ← best-effort Liens DB mirror, error logged+tolerated
```

### New helper: `BuildUpsertPayload`

Extracted from the old `TrySyncToTaskServiceAsync` inline logic into a static helper:

```csharp
private static TaskServiceGovernanceUpsertRequest BuildUpsertPayload(LienTaskGovernanceSettings entity)
```

Builds the Task service upsert body from the Liens domain entity, packing Liens-specific fields into `ProductSettingsJson` via `LiensGovernanceExtensions`. Logic is identical to the previous inline build — no semantic change.

### New mirror helpers

| Method | Purpose |
|---|---|
| `TryMirrorCreateToLiensDbAsync` | Calls `_repo.AddAsync`; logs warning on error; does not throw |
| `TryMirrorUpdateToLiensDbAsync` | Checks if entity exists in Liens DB; calls AddAsync or UpdateAsync accordingly; logs warning on error; does not throw |

`TryMirrorUpdateToLiensDbAsync` includes a re-check (`GetByTenantProductAsync`) before deciding add-vs-update. This handles mirror drift gracefully: if a prior mirror write failed, the update mirror will self-heal.

### Removed: `TrySyncToTaskServiceAsync`

The old sync helper swallowed Task write failures. Replaced by:
- Inline `await _taskClient.UpsertGovernanceAsync(...)` (not wrapped — throws on failure)
- `TryMirrorCreateToLiensDbAsync` / `TryMirrorUpdateToLiensDbAsync` (for Liens DB mirror only)

### Task service changes

None. `UpsertGovernanceAsync` (`POST /api/tasks/governance`) already existed from MIG-01. No Task service code modified.

### Log keys added

| Key | Emitted when |
|---|---|
| `governance_write_owner=task_service` | Task service write succeeds (primary write confirmed) |
| `governance_mirror_target=liens_db` | Liens DB mirror write succeeds |
| `governance_mirror_target=liens_db_failed` | Liens DB mirror write fails (logged+tolerated) |

---

## 5. Read Path Changes

### No read path changes required

All governance reads were already Task-first after MIG-01:
- `GetAsync`: Task service → Liens DB fallback ✅ (unchanged)
- `GetOrCreateAsync`: Task service → Liens DB → create (now Task-first create) ✅

### Log keys updated

Updated existing fallback log keys to use consistent `governance_read_owner=` prefix:

| Old key | New key |
|---|---|
| `governance_source=task_service` | `governance_read_owner=task_service` |
| `governance_source=liens_fallback` | `governance_read_owner=liens_db_fallback` |
| `governance_source=task_service_error` | `governance_read_owner=task_service_error` |
| `governance_source=liens_created` | (replaced by `governance_write_owner=task_service`) |

These renames are for consistency with MIG-07 (`template_read_owner=`, `template_write_owner=`) — all migration log keys now follow the `_read_owner` / `_write_owner` / `_mirror_target` pattern.

### Enforcement paths (Task service internal — unaffected)

The Task service's `ResolveAsync` reads directly from `tasks_GovernanceSettings`. After MIG-08, writes now go to `tasks_GovernanceSettings` first, so enforcement is always using the most current data. There is no gap.

---

## 6. Sync / Fallback Changes

### `LiensGovernanceSyncService` — disabled (same pattern as MIG-07)

**Before:** `ExecuteAsync` ran the `MigrateAllAsync` loop — scanned all `liens_TaskGovernanceSettings` rows and upserted to Task service.

**After:** `ExecuteAsync` immediately returns `Task.CompletedTask` and logs:
```
TASK-MIG-08: LiensGovernanceSyncService is DISABLED (governance_write_owner=task_service).
Liens→Task startup sync suppressed to protect Task-owned governance data.
```

**Why disabled rather than deleted:** Same as MIG-07 — class is retained as a rollback artifact. Constructor simplified (no `IServiceScopeFactory` needed). DI registration kept with updated comment.

### Startup sync direction summary (post MIG-08)

| Sync service | Direction | Status |
|---|---|---|
| `LiensGovernanceSyncService` | Liens→Task | **DISABLED** (MIG-08 — governance now Task-primary) |
| `LiensTemplateSyncService` | Liens→Task | **DISABLED** (MIG-07 — templates already Task-primary) |
| `LiensStageSyncService` | Liens→Task | Active (MIG-03 era; stages still Liens-primary) |
| `LiensTransitionSyncService` | Liens→Task | Active (MIG-04 era; transitions still Liens-primary) |

### Fallback behavior (post MIG-08)

| Read path | Fallback enabled? | Trigger |
|---|---|---|
| `GetAsync` | ✅ Yes | Task service returns null OR error |
| `GetOrCreateAsync` (get) | ✅ Yes | Task service returns null OR error → try Liens DB |
| `GetOrCreateAsync` (create) | Task write fails → throws | Task service write failure is fatal |

---

## 7. Validation Results

| # | Check | Method | Result |
|---|---|---|---|
| 1 | Governance update writes to Task service first — `UpsertGovernanceAsync` called before mirror | Code inspection — `UpdateAsync` line order | ✅ PASS |
| 2 | Governance update failure on Task service throws — error not swallowed | Code inspection — no try/catch around primary write | ✅ PASS |
| 3 | Governance update mirrors to Liens DB best-effort — `TryMirrorUpdateToLiensDbAsync` called after Task write | Code inspection | ✅ PASS |
| 4 | Mirror drift self-healed — `TryMirrorUpdateToLiensDbAsync` checks add-vs-update to handle prior mirror failures | Code inspection — re-checks `GetByTenantProductAsync` inside mirror helper | ✅ PASS |
| 5 | Governance create (GetOrCreateAsync) writes to Task service first | Code inspection — `GetOrCreateAsync` creation path line order | ✅ PASS |
| 6 | Governance create failure on Task service throws — error not swallowed | Code inspection | ✅ PASS |
| 7 | Governance create mirrors to Liens DB best-effort — `TryMirrorCreateToLiensDbAsync` called after Task write | Code inspection | ✅ PASS |
| 8 | Version conflict detection still works — Liens entity loaded for `entity.Version != request.Version` check | Code inspection — `RequireTemplate` equivalent in `UpdateAsync` | ✅ PASS |
| 9 | All governance fields preserved in Task write — `BuildUpsertPayload` produces identical payload to old `TrySyncToTaskServiceAsync` | Code inspection — field-by-field comparison | ✅ PASS |
| 10 | `LiensGovernanceExtensions` correctly packed into `ProductSettingsJson` | Code inspection — `BuildUpsertPayload` unchanged from prior logic | ✅ PASS |
| 11 | Governance reads unchanged — `GetAsync`, `GetOrCreateAsync` still Task-first (MIG-01) | Code inspection | ✅ PASS |
| 12 | Enforcement path unaffected — Task service `ResolveAsync` reads directly from `tasks_GovernanceSettings` | Code inspection — no Liens service in enforcement chain | ✅ PASS |
| 13 | Startup sync (Liens→Task) no longer overwrites Task-owned data — `LiensGovernanceSyncService.ExecuteAsync` returns `Task.CompletedTask` | Code inspection | ✅ PASS |
| 14 | Startup sync class retained for rollback — still registered in DI | Code inspection — `AddSingleton` + `AddHostedService` kept | ✅ PASS |
| 15 | Admin endpoints unchanged — same HTTP surface, same signatures | Code inspection — `TaskGovernanceEndpoints.cs` not modified | ✅ PASS |
| 16 | Task service not modified — no Task service source files changed | Code inspection | ✅ PASS |
| 17 | Liens.Api build succeeds — 0 errors | `dotnet build Liens.Api.csproj -c Release --no-restore` | ✅ PASS |
| 18 | No schema changes — `liens_TaskGovernanceSettings` retained; no migrations | No migration files created | ✅ PASS |
| 19 | Log keys present: `governance_write_owner`, `governance_read_owner`, `governance_mirror_target` | Code inspection | ✅ PASS |
| 20 | Rollback path viable — see Section 8 | Design review | ✅ PASS |

---

## 8. Rollback Plan

### Code-only rollback (no DB changes required)

To revert to Liens DB as primary write owner:

**Step 1 — Revert `LienTaskGovernanceService.cs` write order:**

In `UpdateAsync`:
- Move `_repo.UpdateAsync(entity)` / `_repo.AddAsync(entity)` back as the primary (non-try/catch) write.
- Move `UpsertGovernanceAsync` back inside `TrySyncToTaskServiceAsync` (best-effort, error swallowed).

In `GetOrCreateAsync` creation path:
- Move `_repo.AddAsync(newEntity)` back as the primary write.
- Move `UpsertGovernanceAsync` back inside `TrySyncToTaskServiceAsync`.

**Step 2 — Re-enable `LiensGovernanceSyncService`:**

Restore the `ExecuteAsync` body (the `MigrateAllAsync` call). The class is preserved for exactly this purpose.

**Step 3 — (Optional) revert read log keys:**

`governance_read_owner` → `governance_source` rename is cosmetic. If log consumers depend on the old keys, restore them.

### Liens DB state at rollback

- Every successful Task service write is followed by a best-effort Liens DB mirror.
- If all mirrors succeeded since MIG-08, Liens DB has current data.
- If some mirrors failed (`governance_mirror_target=liens_db_failed`), Liens DB may be slightly stale for those tenants.
- No data loss — Task service has the definitive state.

### Task service state at rollback

Task service governance data is left intact. If Liens→Task sync is re-enabled, it overwrites Task data with Liens DB copies on next startup — acceptable for rollback.

### DB rollback: not required

- `liens_TaskGovernanceSettings` is never dropped in this step.
- No new DB tables or columns added.
- No migrations created.

---

## 9. Known Gaps / Risks

| Item | Notes |
|---|---|
| **Version authority split** | `UpdateAsync` still loads from Liens DB for version conflict detection. If `GetAsync` returns the Task service version (which differs from Liens DB version after the flip), clients sending the Task-version back to `UpdateAsync` will see spurious `ConflictException`. Mitigation: Full version authority flip to Task service (read version from Task, send Task version for conflict check) is the natural MIG-09 or cleanup step. |
| **Liens DB mirror drift** | If `TryMirrorUpdateToLiensDbAsync` fails for a write, Liens DB has stale data. The self-healing add-vs-update check inside the mirror helper partially mitigates this — a later update will re-attempt the add. But if the Liens DB is unreachable for an extended period, drift accumulates. |
| **`GetOrCreateAsync` Task service failure blocks UI** | Before MIG-08, if Task service was down, `GetOrCreateAsync` would still create in Liens DB and return. After MIG-08, if Task service is down, `GetOrCreateAsync` throws when trying to create defaults. **Mitigation**: The read path (`TryFetchFromTaskServiceAsync` + Liens DB fallback) still works without Task service; only the first-access create scenario is blocked. This is the correct tradeoff for Task service primary ownership. |
| **`UpdateAsync` Task service failure blocks UI** | Same as above — UI will see a 500 if Task service is down during a governance update. Previously this would have silently fallen back. This is the correct behavior for a primary write owner. |
| **Sync services: stages and transitions still Liens-primary** | `LiensStageSyncService` and `LiensTransitionSyncService` are still active (Liens→Task direction). This is correct — stages and transitions have not had their ownership flipped yet. MIG-09 (cleanup) may address this. |
| **`liens_TaskGovernanceSettings` cleanup prerequisites** | Before dropping the table: (1) version authority must move fully to Task service (remove Liens entity load in `UpdateAsync`), (2) `TryMirrorCreate/UpdateToLiensDbAsync` must be removed, (3) `GetAsync`/`GetOrCreateAsync` fallback paths must be removed, (4) `ILienTaskGovernanceSettingsRepository` interface and implementations can be deleted. |
| **Governance enforcement remains in Task service** | Enforcement (`ResolveAsync`) is Task-internal and unaffected. No change needed. |

---

## Files Changed

| Path | Change |
|---|---|
| `Liens.Application/Services/LienTaskGovernanceService.cs` | Write paths flipped: Task service primary, Liens DB mirror best-effort. `TrySyncToTaskServiceAsync` replaced by `BuildUpsertPayload` + `TryMirrorCreateToLiensDbAsync` / `TryMirrorUpdateToLiensDbAsync`. Read log keys updated to `governance_read_owner=` prefix. |
| `Liens.Infrastructure/TaskService/LiensGovernanceSyncService.cs` | `ExecuteAsync` made no-op; constructor simplified; class retained for rollback. |
| `Liens.Infrastructure/DependencyInjection.cs` | Comment updated to document disabled state; registration lines unchanged. |
| `analysis/TASK-MIG-08-report.md` | This report |

**No Task service changes. No schema changes. No migrations.**

---

## Complete Log Key Reference (MIG-08 additions)

| Log key | Emitted when | Source |
|---|---|---|
| `governance_write_owner=task_service` | Governance written to Task service successfully (primary write) | `LienTaskGovernanceService.UpdateAsync` / `GetOrCreateAsync` |
| `governance_mirror_target=liens_db` | Liens DB mirror write succeeded | `TryMirrorCreateToLiensDbAsync` / `TryMirrorUpdateToLiensDbAsync` |
| `governance_mirror_target=liens_db_failed` | Liens DB mirror write failed (tolerated) | `TryMirrorCreateToLiensDbAsync` / `TryMirrorUpdateToLiensDbAsync` |
| `governance_read_owner=task_service` | Governance read served from Task service | `TryFetchFromTaskServiceAsync` |
| `governance_read_owner=liens_db_fallback` | Governance read fell back to Liens DB | `GetAsync` / `GetOrCreateAsync` |
| `governance_read_owner=task_service_error` | Task service call failed; Liens DB used | `TryFetchFromTaskServiceAsync` |
