# TASK-B04-01 Report — Duplicate Check Migration Fix

_Date: 2026-04-21_

## 1. Objective

Remove `LienTaskGenerationEngine`'s direct SQL queries against `liens_Tasks`
(`HasOpenTaskForRuleAsync` / `HasOpenTaskForTemplateAsync`) so the
TASK-B04 drop migration can be safely applied.  Replace with HTTP calls to
the canonical Task service using `GenerationRuleId` / `GeneratingTemplateId`
filtering.

---

## 2. Existing Duplicate-Check Paths (pre-TASK-B04-01)

### Engine entry points

| Location | Method | Table touched |
|---|---|---|
| `LienTaskGenerationEngine.cs:111` | `_taskRepo.HasOpenTaskForRuleAsync(tenantId, ruleId, caseId, lienId)` | `liens_Tasks` |
| `LienTaskGenerationEngine.cs:130` | `_taskRepo.HasOpenTaskForTemplateAsync(tenantId, templateId, caseId, lienId)` | `liens_Tasks` |

### Repository implementation

`ILienTaskRepository` / `LienTaskRepository` contained two raw SQL helpers
that queried `liens_Tasks.GenerationRuleId` and `liens_Tasks.GeneratingTemplateId`
with a status NOT IN ('COMPLETED','CANCELLED') filter.  These columns were
being dropped by Liens migration `20260421000001_DropLegacyTaskColumns`.

### Duplicate-prevention modes

| Enum value | Dup-check used |
|---|---|
| `SameRuleSameEntityOpenTask` | `HasOpenTaskForRuleAsync` |
| `SameTemplateSameEntityOpenTask` | `HasOpenTaskForTemplateAsync` |
| `None` | no check |

---

## 3. Task Service Capability Review

### New columns added to `tasks_Tasks` (migration 20260421000005)

| Column | Type | Purpose |
|---|---|---|
| `GenerationRuleId` | `char(36) NULL` | Stores the Liens `LienTaskGenerationRule.Id` that produced the task |
| `GeneratingTemplateId` | `char(36) NULL` | Stores the Liens `LienTaskTemplate.Id` that was used |

### New composite indexes

| Index name | Columns | Purpose |
|---|---|---|
| `IX_Tasks_TenantId_Product_GenerationRule` | `(TenantId, SourceProductCode, GenerationRuleId)` | Fast lookup by rule across a tenant's Liens tasks |
| `IX_Tasks_TenantId_Product_GeneratingTemplate` | `(TenantId, SourceProductCode, GeneratingTemplateId)` | Fast lookup by template |

### Query pipeline changes

The full `SearchAsync` chain was extended with three new parameters threaded
from endpoint to repository:

```
GET /api/tasks?generationRuleId={g}&generatingTemplateId={t}&excludeTerminal=true
```

| Layer | File | Change |
|---|---|---|
| Repository interface | `ITaskRepository.cs` | `+generationRuleId`, `+generatingTemplateId`, `+excludeTerminal` |
| Repository impl | `TaskRepository.cs` | 3 filter clauses added before `CountAsync` |
| Service interface | `ITaskService.cs` | matching parameters threaded |
| Service impl | `TaskService.cs` | passes new params to repository |
| Endpoint | `TaskEndpoints.cs` | 3 new query params on `ListTasks` |

### `excludeTerminal` semantics

```csharp
if (excludeTerminal)
    q = q.Where(t => t.Status != "COMPLETED" && t.Status != "CANCELLED");
```

Covers statuses: `NEW`, `OPEN`, `IN_PROGRESS`, `WAITING_BLOCKED` (all active).

### `CreateTaskAsync` body extended

`LiensTaskServiceClient.CreateTaskAsync` now forwards
`generationRuleId` and `generatingTemplateId` in the POST body so the Task
service persists them on create.

---

## 4. Liens Client / Service Changes

### `ILiensTaskServiceClient` additions

```csharp
Task<bool> HasOpenTaskForRuleAsync(
    Guid tenantId, Guid ruleId, Guid? caseId, Guid? lienId, CancellationToken ct);

Task<bool> HasOpenTaskForTemplateAsync(
    Guid tenantId, Guid templateId, Guid? caseId, Guid? lienId, CancellationToken ct);
```

### `LiensTaskServiceClient` implementation

Both methods call a private `BuildDupCheckQuery` helper that builds:

```
GET /api/tasks?sourceProductCode=SYNQ_LIENS
              &generationRuleId={id}        (or generatingTemplateId={id})
              &sourceEntityType=LIEN_CASE&sourceEntityId={caseId}   (case scope)
              &linkedEntityType=LIEN&linkedEntityId={lienId}         (lien scope)
              &excludeTerminal=true
              &pageSize=1&page=1
```

Returns `list.Total > 0`.  Returns `false` (safe default) if neither
`caseId` nor `lienId` is provided.

---

## 5. Generation Engine Changes

### Constructor injection

`ILiensTaskServiceClient _taskServiceClient` added as constructor parameter.

### Dup-check call sites

```csharp
// Before (direct liens DB query)
var hasDup = await _taskRepo.HasOpenTaskForRuleAsync(...);

// After (HTTP to Task service)
var hasDup = await _taskServiceClient.HasOpenTaskForRuleAsync(...);
```

`_taskRepo` is **retained** for `AddGeneratedMetadataAsync` (line 193) —
that call saves Liens-side metadata and was not in scope for this task.

---

## 6. Task Service API / Query Extensions — Summary

| Endpoint | New query params |
|---|---|
| `GET /api/tasks` | `generationRuleId` (Guid?), `generatingTemplateId` (Guid?), `excludeTerminal` (bool, default false) |

These params are additive and backward-compatible — existing callers are
unaffected.

---

## 7. Validation Results

### Build — Task service

```
Task.Infrastructure → Build succeeded. 0 Warning(s) 0 Error(s)
Task.Application    → Build succeeded. 0 Warning(s) 0 Error(s)
Task.Api            → Build succeeded. 1 Warning(s) 0 Error(s)   ← pre-existing JWT version conflict
```

### Build — Liens service

```
Liens.Application    → Build succeeded. 0 Warning(s) 0 Error(s)
Liens.Infrastructure → Build succeeded. 0 Warning(s) 0 Error(s)
```

All errors: **0**.  The single warning is a pre-existing NuGet version
conflict in `Task.Api` unrelated to this task.

---

## 8. Migration Runbook

### Pre-requisites

TASK-B04 must be applied first (creates the Task service Liens proxy layer and
the backfill endpoint).

### Step order

1. Deploy updated Task service (migration `20260421000005_GenerationMetadataColumns`
   runs on startup — adds 2 nullable columns and 2 indexes, zero downtime).
2. Run backfill script to populate `GenerationRuleId` / `GeneratingTemplateId`
   on migrated tasks (see TASK-B04 backfill endpoint).
3. Deploy updated Liens service (the engine now calls the Task service for
   dup checks; Liens migration `20260421000001` drops the old columns).

### Rollback

If the Liens service needs to be rolled back before the Task service drop
migration is reversed:
- The old `HasOpenTask*` repo methods can be temporarily restored.
- The Task service columns are nullable so no data loss if re-deployed.

---

## 9. Known Gaps / Risks

| Item | Severity | Notes |
|---|---|---|
| Backfill gap window | Low | Tasks created between deploy step 1 and backfill run won't have `GenerationRuleId` populated. Dup checks may miss them until backfill completes. Window is typically <1 min. |
| No entity scope | Low | If both `caseId` and `lienId` are null, `BuildDupCheckQuery` returns null and `HasOpenTask*` returns `false` (allows creation). This matches the prior behaviour of the SQL queries which used the same scoping. |
| `_taskRepo` still injected | Informational | `ILienTaskRepository` remains injected for `AddGeneratedMetadataAsync`. A follow-up task can remove the dependency once that method is also migrated to the Task service. |
| No integration test | Informational | HTTP dup-check path is not covered by automated tests in this PR. Integration tests against a running Task service can be added as a follow-up. |
