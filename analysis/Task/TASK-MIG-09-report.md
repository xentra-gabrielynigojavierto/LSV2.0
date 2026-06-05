# TASK-MIG-09 Report — Final Cleanup (Remove Liens Config Authority)
**Date:** 2026-04-21
**Status:** ✅ Complete

---

## 1. Codebase Analysis

### Files inspected

| File | Role |
|---|---|
| `Liens.Application/Services/LienTaskTemplateService.cs` | Template admin + generation reads/writes |
| `Liens.Application/Services/LienTaskGovernanceService.cs` | Governance reads/writes |
| `Liens.Application/Repositories/ILienTaskTemplateRepository.cs` | Template Liens DB repo contract |
| `Liens.Application/Repositories/ILienTaskGovernanceSettingsRepository.cs` | Governance Liens DB repo contract |
| `Liens.Infrastructure/Repositories/LienTaskTemplateRepository.cs` | EF repo implementation (`liens_TaskTemplates`) |
| `Liens.Infrastructure/Repositories/LienTaskGovernanceSettingsRepository.cs` | EF repo implementation (`liens_TaskGovernanceSettings`) |
| `Liens.Infrastructure/Persistence/Configurations/LienTaskTemplateConfiguration.cs` | EF entity→table mapping |
| `Liens.Infrastructure/Persistence/Configurations/LienTaskGovernanceSettingsConfiguration.cs` | EF entity→table mapping |
| `Liens.Infrastructure/TaskService/LiensTemplateSyncService.cs` | No-op disabled sync (MIG-07) |
| `Liens.Infrastructure/TaskService/LiensGovernanceSyncService.cs` | No-op disabled sync (MIG-08) |
| `Liens.Infrastructure/Persistence/LiensDbContext.cs` | DbSets for both entities |
| `Liens.Infrastructure/DependencyInjection.cs` | All repo + sync registrations |
| `Liens.Infrastructure/Migrations/LiensDbContextModelSnapshot.cs` | EF model snapshot (LienTaskTemplate present; LienTaskGovernanceSettings absent) |
| `Liens.Infrastructure/Migrations/20260420000001_AddTaskGovernanceSettings.cs` | Migration that created `liens_TaskGovernanceSettings` (table physically present but not in snapshot) |
| `Liens.Api/Program.cs` | `EnsureLiensSchemaTablesAsync` — creates `liens_TaskGovernanceSettings` on startup |

### Service method × dependency matrix (pre-MIG-09)

#### Templates

| Method | Liens DB repo call | Notes |
|---|---|---|
| `GetByTenantAsync` | `_repo.GetByTenantAsync` (fallback after Task call) | Fallback read |
| `GetByIdAsync` | `_repo.GetByIdAsync` (fallback) | Fallback read |
| `GetContextualAsync` | `_repo.GetActiveByTenantAsync` (fallback) | Fallback read |
| `GetForGenerationAsync` | `_repo.GetByIdAsync` (fallback) | Fallback read |
| `UpdateAsync` | `RequireTemplate` → `_repo.GetByIdAsync` (version check + entity mutation) | Primary entity load |
| `ActivateAsync` | `RequireTemplate` → `_repo.GetByIdAsync` | Primary entity load |
| `DeactivateAsync` | `RequireTemplate` → `_repo.GetByIdAsync` | Primary entity load |
| `CreateAsync` | `TryMirrorCreateToLiensDbAsync` → `_repo.AddAsync` | Mirror write |
| `UpdateAsync` | `TryMirrorUpdateToLiensDbAsync` → `_repo.UpdateAsync` | Mirror write |
| `ActivateAsync` | `TryMirrorUpdateToLiensDbAsync` → `_repo.UpdateAsync` | Mirror write |
| `DeactivateAsync` | `TryMirrorUpdateToLiensDbAsync` → `_repo.UpdateAsync` | Mirror write |

#### Governance

| Method | Liens DB repo call | Notes |
|---|---|---|
| `GetAsync` | `_repo.GetByTenantProductAsync` (fallback) | Fallback read |
| `GetOrCreateAsync` | `_repo.GetByTenantProductAsync` (fallback) + `_repo.AddAsync` (create path) | Fallback + create |
| `UpdateAsync` | `_repo.GetByTenantProductAsync` (version check) + `TryMirrorUpdateToLiensDbAsync` | Load + mirror |
| `GetOrCreateAsync` (create path) | `TryMirrorCreateToLiensDbAsync` → `_repo.AddAsync` | Mirror write |

### EF model state

| Entity | In snapshot? | In DbContext? | Config class? | Physical table? |
|---|---|---|---|---|
| `LienTaskTemplate` | ✅ Yes (lines 1061–1155) | ✅ Yes (`LienTaskTemplates`) | ✅ Yes | ✅ Yes (`liens_TaskTemplates`) |
| `LienTaskGovernanceSettings` | ❌ No (snapshot predates governance migration) | ✅ Yes (`LienTaskGovernanceSettings`) | ✅ Yes | ✅ Yes (`liens_TaskGovernanceSettings`) |

### Stage/transition scope assessment

- `liens_WorkflowStages` and `liens_WorkflowTransitions` are still Liens-authoritative.
- `LiensStageSyncService` and `LiensTransitionSyncService` are still active.
- These are **out of scope** for MIG-09 — they are not config-domain owned tables.
- Stage/transition cleanup is a separate future migration lane.

### Generation rule scope assessment

- `liens_TaskGenerationRules` and `liens_GeneratedTaskMetadata` are still Liens-owned.
- These are NOT template config — they are generation policy tables.
- Out of scope for MIG-09. Not touched.

---

## 2. Current Legacy Dependency Review

### References safe to remove now

| Reference | Type | Safe to remove |
|---|---|---|
| `ILienTaskTemplateRepository` interface | App layer contract | ✅ After service rewrite |
| `ILienTaskGovernanceSettingsRepository` interface | App layer contract | ✅ After service rewrite |
| `LienTaskTemplateRepository` implementation | EF repo | ✅ After service rewrite |
| `LienTaskGovernanceSettingsRepository` implementation | EF repo | ✅ After service rewrite |
| `LienTaskTemplateConfiguration` EF config | EF mapping | ✅ With migration |
| `LienTaskGovernanceSettingsConfiguration` EF config | EF mapping | ✅ With migration |
| `LienTaskTemplates` DbSet | LiensDbContext | ✅ With EF config removal |
| `LienTaskGovernanceSettings` DbSet | LiensDbContext | ✅ With EF config removal |
| `LiensTemplateSyncService` | No-op class | ✅ Now (was only for rollback) |
| `LiensGovernanceSyncService` | No-op class | ✅ Now (was only for rollback) |
| DI registrations for above repos and sync services | DI | ✅ With file deletions |
| `liens_TaskGovernanceSettings` in `EnsureLiensSchemaTablesAsync` | Program.cs | ✅ Table being dropped |
| All Liens DB fallback blocks in template service | LienTaskTemplateService | ✅ After service rewrite |
| All Liens DB mirror blocks in template service | LienTaskTemplateService | ✅ After service rewrite |
| All Liens DB fallback blocks in governance service | LienTaskGovernanceService | ✅ After service rewrite |
| All Liens DB mirror blocks in governance service | LienTaskGovernanceService | ✅ After service rewrite |

### References that are rollback artifacts only

| Reference | After MIG-09 |
|---|---|
| Old migration Designer files referencing `LienTaskTemplate` | Kept (historical migration files are immutable) |
| Domain entity classes (`LienTaskTemplate.cs`, `LienTaskGovernanceSettings.cs`) | Kept as in-memory value objects — used for ID generation + field validation in `CreateAsync` / `GetOrCreateAsync` paths. No EF dependency. |

### Still-active Liens config tables (NOT removed in MIG-09)

| Table | Why not removed |
|---|---|
| `liens_TaskGenerationRules` | Still Liens-authoritative (generation policy, separate from config ownership migration) |
| `liens_GeneratedTaskMetadata` | Still Liens-authoritative |
| `liens_WorkflowConfigs` | Stage/transition parent — Liens-primary (MIG-03/04 active) |
| `liens_WorkflowStages` | Liens-primary — `LiensStageSyncService` still active |
| `liens_WorkflowTransitions` | Liens-primary — `LiensTransitionSyncService` still active |

---

## 3. Cleanup Design

### Template cleanup

1. **Remove fallback reads**: `GetByTenantAsync`, `GetByIdAsync`, `GetContextualAsync`, `GetForGenerationAsync` → Task service calls only. On error, let exceptions propagate (no Liens DB rescue path).
2. **Remove mirror writes**: `CreateAsync`, `UpdateAsync`, `ActivateAsync`, `DeactivateAsync` → delete `TryMirrorCreateToLiensDbAsync`, `TryMirrorUpdateToLiensDbAsync`.
3. **Version authority**: `UpdateAsync`, `ActivateAsync`, `DeactivateAsync` currently load entity from Liens DB via `RequireTemplate`. Replace with `RequireFromTaskServiceAsync` → calls `_taskClient.GetTemplateAsync`. Version check is now against Task service's `dto.Version`.
4. **Payload construction for Update/Activate/Deactivate**: Build `TaskServiceTemplateUpsertRequest` directly from request fields (Update) or from `TaskServiceTemplateResponse` fields (Activate/Deactivate). No domain entity mutation needed.
5. **`LienTaskTemplate` domain entity**: Retained as in-memory value object for `CreateAsync` only (ID generation + field validation → `MapToUpsertPayload`). No repo reference.
6. **`liens_TaskTemplates` table**: DROPPED via new EF migration.
7. **Disabled sync service**: `LiensTemplateSyncService` deleted (class + DI registration).
8. **Repositories**: `ILienTaskTemplateRepository` + `LienTaskTemplateRepository` deleted.
9. **EF config**: `LienTaskTemplateConfiguration` deleted.
10. **DbSet**: `LienTaskTemplates` removed from `LiensDbContext`.

### Governance cleanup

1. **Remove fallback reads**: `GetAsync`, `GetOrCreateAsync` → Task service calls only. On error, let exceptions propagate.
2. **Remove mirror writes**: `GetOrCreateAsync` create path, `UpdateAsync` → delete `TryMirrorCreateToLiensDbAsync`, `TryMirrorUpdateToLiensDbAsync`.
3. **Version authority**: `UpdateAsync` currently loads entity from Liens DB. Replace with `_taskClient.GetGovernanceAsync`. Version check is now against Task service's `dto.Version`.
4. **`GetOrCreateAsync` create path**: After removing Liens repo, build default payload directly from `LienTaskGovernanceSettings.CreateDefault(...)` (domain entity retained as in-memory value object for defaults factory + mapping).
5. **`LienTaskGovernanceSettings` domain entity**: Retained as in-memory value object for default-create paths only. No repo reference.
6. **`liens_TaskGovernanceSettings` table**: DROPPED via new EF migration (even though not in snapshot — the table physically exists).
7. **Disabled sync service**: `LiensGovernanceSyncService` deleted.
8. **Repositories**: `ILienTaskGovernanceSettingsRepository` + `LienTaskGovernanceSettingsRepository` deleted.
9. **EF config**: `LienTaskGovernanceSettingsConfiguration` deleted.
10. **DbSet**: `LienTaskGovernanceSettings` removed from `LiensDbContext`.

### Repository / DI cleanup

Remove from `DependencyInjection.cs`:
- `services.AddScoped<ILienTaskTemplateRepository, LienTaskTemplateRepository>()`
- `services.AddScoped<ILienTaskGovernanceSettingsRepository, LienTaskGovernanceSettingsRepository>()`
- Both `LiensTemplateSyncService` singleton + hosted service registrations
- Both `LiensGovernanceSyncService` singleton + hosted service registrations

Remove `using` directives if they become orphaned.

### Version / conflict handling

**Templates:**

Before: Load Liens entity (`RequireTemplate`) → check `entity.Version == request.Version`
After: Load Task service DTO (`RequireFromTaskServiceAsync`) → check `dto.Version == request.Version`

The client UI sends back the version it received from the last GET. Since reads already go to Task service first (since MIG-01), the client's version is already the Task service version. This is a clean resolution with no observable change to the UI.

**Governance:**

Before: `_repo.GetByTenantProductAsync` → check `entity.Version == request.Version`
After: `_taskClient.GetGovernanceAsync` → check `dto.Version == request.Version`

Same reasoning — client version already comes from Task service reads.

### Scope exclusions

Explicitly NOT removed in MIG-09:
- `liens_WorkflowStages`, `liens_WorkflowConfigs`, `liens_WorkflowTransitions` — stage/transition ownership not yet flipped
- `liens_TaskGenerationRules`, `liens_GeneratedTaskMetadata` — generation policy, not config ownership
- `LiensStageSyncService`, `LiensTransitionSyncService` — still active Liens→Task sync services
- Domain entity classes `LienTaskTemplate.cs`, `LienTaskGovernanceSettings.cs` — retained as in-memory value objects

---

## 4. Read/Write Path Removal

### Template service — after MIG-09

**Read paths (all Task-service-only now):**

| Method | Before | After |
|---|---|---|
| `GetByTenantAsync` | Task service → Liens DB fallback | Task service only (no catch/fallback) |
| `GetByIdAsync` | Task service → Liens DB fallback | Task service only |
| `GetContextualAsync` | Task service → Liens DB fallback | Task service only |
| `GetForGenerationAsync` | Task service → Liens DB fallback | Task service only |

**Write paths (all Task-service-only now):**

| Method | Load from | Version check | Primary write | Mirror | Response |
|---|---|---|---|---|---|
| `CreateAsync` | — | — | Task service | **None** | Built from in-memory entity |
| `UpdateAsync` | Task service (`RequireFromTaskServiceAsync`) | `dto.Version == request.Version` | Task service | **None** | Built from request + existing |
| `ActivateAsync` | Task service (`RequireFromTaskServiceAsync`) | none (idempotent) | Task service | **None** | Built from existing + IsActive=true |
| `DeactivateAsync` | Task service (`RequireFromTaskServiceAsync`) | none (idempotent) | Task service | **None** | Built from existing + IsActive=false |

**Removed helpers:**
- `RequireTemplate(tenantId, id, ct)` → replaced by `RequireFromTaskServiceAsync(tenantId, id, ct)` (Task service)
- `TryFetchFromTaskServiceAsync(tenantId, id, ct)` → inlined into read methods (just direct `_taskClient.GetTemplateAsync` call)
- `TryMirrorCreateToLiensDbAsync` → deleted
- `TryMirrorUpdateToLiensDbAsync` → deleted
- `TryGetContextualFromTaskServiceAsync` → simplified into `GetContextualAsync` body directly

**Constructor change:** `_repo` parameter and field removed.

### Governance service — after MIG-09

**Read paths (all Task-service-only now):**

| Method | Before | After |
|---|---|---|
| `GetAsync` | Task service → Liens DB fallback | Task service only |
| `GetOrCreateAsync` (read path) | Task service → Liens DB → create in Liens | Task service → create in Task service |

**Write paths (all Task-service-only now):**

| Method | Load from | Version check | Primary write | Mirror | Response |
|---|---|---|---|---|---|
| `GetOrCreateAsync` (create) | — | — | Task service | **None** | Built from in-memory defaults |
| `UpdateAsync` | Task service (version check) | `dto.Version == request.Version` | Task service | **None** | Built from request + existing |

**Removed helpers:**
- `TryMirrorCreateToLiensDbAsync` → deleted
- `TryMirrorUpdateToLiensDbAsync` → deleted

**Constructor change:** `_repo` parameter and field removed.

---

## 5. Legacy Table Removal / Decommissioning

### `liens_TaskTemplates` — DROPPED

**Readiness check:**
- ✅ No remaining runtime reads from Liens DB (MIG-07 flipped reads to Task service)
- ✅ No remaining runtime writes to Liens DB (MIG-07 flipped writes to Task service; MIG-09 removes mirrors)
- ✅ No FK constraints from other Liens tables pointing to `liens_TaskTemplates`
- ✅ EF configuration + DbSet removed → EF no longer tracks this entity
- **Decision: DROP TABLE**

### `liens_TaskGovernanceSettings` — DROPPED

**Readiness check:**
- ✅ No remaining runtime reads from Liens DB (MIG-08 flipped reads to Task service)
- ✅ No remaining runtime writes to Liens DB (MIG-08 flipped writes to Task service; MIG-09 removes mirrors)
- ✅ No FK constraints from other Liens tables pointing to `liens_TaskGovernanceSettings`
- ✅ EF configuration + DbSet removed → EF no longer tracks this entity
- **Decision: DROP TABLE**

### EF migration

New migration: `20260421000002_DropLiensConfigTables`

```
Up()  → DROP TABLE liens_TaskTemplates
        DROP TABLE liens_TaskGovernanceSettings
Down() → CREATE TABLE liens_TaskTemplates (...)
         CREATE TABLE liens_TaskGovernanceSettings (...)
```

### Model snapshot

- Remove `modelBuilder.Entity("Liens.Domain.Entities.LienTaskTemplate", ...)` block (lines 1061–1155)
- `LienTaskGovernanceSettings` was never in snapshot (governance migration predates this) — no change needed there

### Disabled sync services — DELETED

- `LiensTemplateSyncService.cs` — deleted (no-op since MIG-07; no longer needed for rollback)
- `LiensGovernanceSyncService.cs` — deleted (no-op since MIG-08; no longer needed for rollback)

### Repository classes — DELETED

- `Liens.Application/Repositories/ILienTaskTemplateRepository.cs`
- `Liens.Application/Repositories/ILienTaskGovernanceSettingsRepository.cs`
- `Liens.Infrastructure/Repositories/LienTaskTemplateRepository.cs`
- `Liens.Infrastructure/Repositories/LienTaskGovernanceSettingsRepository.cs`

### EF configuration classes — DELETED

- `Liens.Infrastructure/Persistence/Configurations/LienTaskTemplateConfiguration.cs`
- `Liens.Infrastructure/Persistence/Configurations/LienTaskGovernanceSettingsConfiguration.cs`

### Program.cs cleanup

Remove `createTaskGovernanceSettings` DDL and its execution from `EnsureLiensSchemaTablesAsync`. The table is being dropped — the idempotent CREATE TABLE IF NOT EXISTS guard is no longer needed (and would recreate a dropped table on startup if left).

---

## 6. Validation Results

| # | Check | Method | Result |
|---|---|---|---|
| 1 | Admin template list uses Task service only — no Liens DB fallback code | Code inspection | ✅ PASS |
| 2 | Admin template get-by-id uses Task service only | Code inspection | ✅ PASS |
| 3 | Contextual template read uses Task service only | Code inspection | ✅ PASS |
| 4 | Generation engine template read uses Task service only | Code inspection | ✅ PASS |
| 5 | Template create writes to Task service only — no mirror | Code inspection | ✅ PASS |
| 6 | Template update uses Task service version for conflict check | Code inspection | ✅ PASS |
| 7 | Template update writes to Task service only — no mirror | Code inspection | ✅ PASS |
| 8 | Template activate/deactivate writes to Task service only | Code inspection | ✅ PASS |
| 9 | Governance get uses Task service only | Code inspection | ✅ PASS |
| 10 | Governance get-or-create uses Task service only | Code inspection | ✅ PASS |
| 11 | Governance update uses Task service version for conflict check | Code inspection | ✅ PASS |
| 12 | Governance update writes to Task service only | Code inspection | ✅ PASS |
| 13 | ILienTaskTemplateRepository no longer referenced | Code inspection | ✅ PASS |
| 14 | ILienTaskGovernanceSettingsRepository no longer referenced | Code inspection | ✅ PASS |
| 15 | LiensTemplateSyncService deleted | File system | ✅ PASS |
| 16 | LiensGovernanceSyncService deleted | File system | ✅ PASS |
| 17 | DI registrations for repos and sync services removed | Code inspection | ✅ PASS |
| 18 | EF configurations deleted — EF model no longer tracks template/governance entities | Code inspection | ✅ PASS |
| 19 | LiensDbContext DbSets removed | Code inspection | ✅ PASS |
| 20 | liens_TaskGovernanceSettings removed from EnsureLiensSchemaTablesAsync | Code inspection | ✅ PASS |
| 21 | New migration `20260421000002_DropLiensConfigTables` created | File system | ✅ PASS |
| 22 | Model snapshot updated — LienTaskTemplate entity block removed | Code inspection | ✅ PASS |
| 23 | Liens.Api builds 0 errors | `dotnet build` | ✅ PASS |
| 24 | No Task service changes | Code inspection | ✅ PASS |
| 25 | Stage/transition services untouched | Code inspection | ✅ PASS |

---

## 7. Rollback Plan

### Code-only rollback (if tables NOT yet physically dropped)

If the migration `20260421000002_DropLiensConfigTables` has NOT been applied to production yet, rollback is code-only:

1. Restore `ILienTaskTemplateRepository` + `LienTaskTemplateRepository`
2. Restore `ILienTaskGovernanceSettingsRepository` + `LienTaskGovernanceSettingsRepository`
3. Restore `LienTaskTemplateConfiguration` + `LienTaskGovernanceSettingsConfiguration`
4. Restore `LienTaskTemplates` + `LienTaskGovernanceSettings` DbSets in `LiensDbContext`
5. Restore DI registrations for above repos
6. Restore sync service classes + DI registrations (or use MIG-07/MIG-08 state where they were no-ops)
7. Restore Liens DB fallback blocks in `LienTaskTemplateService` + `LienTaskGovernanceService`
8. Restore Liens DB mirror blocks in both services
9. Restore `RequireTemplate` (Liens DB load) in template service
10. Restore Liens DB version check in governance `UpdateAsync`
11. Restore `liens_TaskGovernanceSettings` in `EnsureLiensSchemaTablesAsync`

All tables still exist — data is intact. No DB migration needed for code-only rollback.

### Rollback after physical table drop (migration applied)

If `20260421000002_DropLiensConfigTables.Up()` has been applied:

1. Run `20260421000002_DropLiensConfigTables.Down()` via `dotnet ef database update <prior-migration>` — this recreates both tables.
2. The recreated tables will be empty (no historical data).
3. Perform all code-only rollback steps above.
4. Re-enable startup sync services (`LiensTemplateSyncService`, `LiensGovernanceSyncService`) to repopulate from Task service if needed — or swap direction and sync from Task service back to Liens.

**Risk:** Production data in the dropped tables is lost when the migration runs. The Task service has all authoritative data. Rollback creates empty tables which will repopulate via sync or mirrors as writes occur.

**Rollback complexity:** Medium. Code rollback is easy. DB rollback requires migration down + data repopulation.

---

## 8. Known Gaps / Risks

| Item | Notes |
|---|---|
| **Stage/transition ownership not flipped** | `liens_WorkflowStages`, `liens_WorkflowTransitions`, `liens_WorkflowConfigs` are still Liens-primary. `LiensStageSyncService` and `LiensTransitionSyncService` remain active. This is correct — out of scope for MIG-09. |
| **Generation rule ownership not touched** | `liens_TaskGenerationRules` and `liens_GeneratedTaskMetadata` are still Liens-owned. Generation rule admin is not part of this migration lane. |
| **Domain entity classes retained** | `LienTaskTemplate.cs` and `LienTaskGovernanceSettings.cs` remain in `Liens.Domain` as in-memory value objects (no EF dependency). They could be cleaned up in a future step if generation rules are ever migrated, but are harmless to keep. |
| **`liens_TaskGovernanceSettings` not in model snapshot** | The governance migration (`20260420000001`) created the table but the snapshot was not regenerated at that time. The new `20260421000002_DropLiensConfigTables` migration explicitly drops this table in its `Up()`, ensuring it is removed from the live DB even though EF's snapshot doesn't reference it. |
| **Task service availability** | After removing all fallback paths, any Task service outage will cause template and governance UI operations to fail with HTTP 502/503 errors. This is the correct behavior — Task service is the authority. For resilience, the Task service should have its own HA strategy. |
| **Historical migration Designer files** | Migration Designer files from `20260418152345_AddTaskTemplates.Designer.cs` etc. still reference `LienTaskTemplate` entity. These are immutable historical migration snapshots and MUST NOT be edited. EF uses only the latest snapshot for forward migration planning. |
| **Flow convergence** | Flow orchestration tables remain a separate future project — out of scope. |
| **No `ILienTaskTemplateService.MapToUpsertPayload` exposure** | `MapToUpsertPayload` is a `public static` method on `LienTaskTemplateService`. It may be used by backfill or other services. After MIG-09, the signature and behavior are unchanged — it still takes a `LienTaskTemplate` entity and returns `TaskServiceTemplateUpsertRequest`. |

---

## Files Changed

| Path | Change |
|---|---|
| `Liens.Application/Services/LienTaskTemplateService.cs` | Removed `_repo`, all fallback reads, all mirror writes, `RequireTemplate`, `TryFetchFromTaskServiceAsync`, `TryGetContextualFromTaskServiceAsync`, `TryMirrorCreate/UpdateToLiensDbAsync`. Added `RequireFromTaskServiceAsync`. Version check now from Task service. |
| `Liens.Application/Services/LienTaskGovernanceService.cs` | Removed `_repo`, all fallback reads, all mirror writes, `TryFetchFromTaskServiceAsync` (replaced with direct call), `TryMirrorCreate/UpdateToLiensDbAsync`. Version check now from Task service. |
| `Liens.Infrastructure/Persistence/LiensDbContext.cs` | Removed `LienTaskTemplates` and `LienTaskGovernanceSettings` DbSets |
| `Liens.Infrastructure/DependencyInjection.cs` | Removed 4 repo+sync registrations, cleaned up comments |
| `Liens.Infrastructure/Persistence/Migrations/20260421000002_DropLiensConfigTables.cs` | New migration: drops `liens_TaskTemplates` and `liens_TaskGovernanceSettings` |
| `Liens.Infrastructure/Persistence/Migrations/LiensDbContextModelSnapshot.cs` | Removed `LienTaskTemplate` entity block |
| `Liens.Api/Program.cs` | Removed `liens_TaskGovernanceSettings` DDL and execution from `EnsureLiensSchemaTablesAsync` |
| **DELETED** `Liens.Application/Repositories/ILienTaskTemplateRepository.cs` | — |
| **DELETED** `Liens.Application/Repositories/ILienTaskGovernanceSettingsRepository.cs` | — |
| **DELETED** `Liens.Infrastructure/Repositories/LienTaskTemplateRepository.cs` | — |
| **DELETED** `Liens.Infrastructure/Repositories/LienTaskGovernanceSettingsRepository.cs` | — |
| **DELETED** `Liens.Infrastructure/Persistence/Configurations/LienTaskTemplateConfiguration.cs` | — |
| **DELETED** `Liens.Infrastructure/Persistence/Configurations/LienTaskGovernanceSettingsConfiguration.cs` | — |
| **DELETED** `Liens.Infrastructure/TaskService/LiensTemplateSyncService.cs` | — |
| **DELETED** `Liens.Infrastructure/TaskService/LiensGovernanceSyncService.cs` | — |

**No Task service changes. No domain entity changes. Liens.Api builds 0 errors.**
