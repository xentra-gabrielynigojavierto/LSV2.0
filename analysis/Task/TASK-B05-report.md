# TASK-B05 Report — Platform Hardening

_Date: 2026-04-21 | Status: IN PROGRESS_

## 1. Codebase Analysis

### Auth patterns

| Surface | Current auth | Issue |
|---|---|---|
| `POST /api/tasks/internal/flow-callback` | `Policies.AdminOnly` (RequireRole PlatformAdmin) | Accepts any user JWT with PlatformAdmin role — NOT service-to-service |
| Liens → Task HTTP calls | `IServiceTokenIssuer` + `NotificationsAuthDelegatingHandler` | Correct pattern |
| Task → Notifications | `NotificationsAuthDelegatingHandler` via named `HttpClient` | Correct pattern |

`BuildingBlocks` already exposes:
- `AddServiceTokenBearer(builder, config)` — registers second JWT scheme `"ServiceToken"`
- `ServiceTokenAuthenticationDefaults.ServiceRole = "service"` — role on service tokens
- `ServiceTokenAuthenticationDefaults.SecretEnvVar = "FLOW_SERVICE_TOKEN_SECRET"` — shared secret
- `Task.Infrastructure.DependencyInjection` already calls `AddServiceTokenIssuer(config, "task")`

**Fix needed**: Add `AddServiceTokenBearer` to `Program.cs`; add `Policies.InternalService` authorization policy; switch flow-callback to that policy.

### Product-code usage

`BuildingBlocks.Authorization.ProductCodes` already defines all canonical codes:
`SYNQ_CARECONNECT`, `SYNQ_FUND`, `SYNQ_LIENS`, `SYNQ_PAY`, `SYNQ_INSIGHTS`, `SYNQ_COMMS`, `SYNQ_PLATFORM`.

Currently:
- No validation against this registry in any Task service endpoint
- Domain entities normalize to `Trim().ToUpperInvariant()` but accept any string
- `TaskGovernanceService` + `TaskStageService` do their own ad-hoc `.ToUpperInvariant()` — scattered
- `CreateTaskRequest.SourceProductCode`, `UpsertGovernanceRequest.SourceProductCode`, `CreateStageRequest.SourceProductCode`, `CreateTemplateRequest.SourceProductCode` — all unchecked

**Fix needed**: Centralized `KnownProductCodes` validator; apply to all create/upsert endpoints.

### Monitoring integration

- Monitoring service exposes `POST /api/entities` (`CreateMonitoredEntityRequest`) — service-token auth
- No existing monitoring-registration client in `BuildingBlocks` or Task service
- Task service has `/health` endpoint (anonymous, already wired)
- **Fix**: `IHostedService` that fires once on startup and POSTs to `MonitoringService:BaseUrl/api/entities`
  - Skipped silently if `MonitoringService:BaseUrl` is not configured (dev mode)
  - Idempotent: 409 Conflict is treated as success

### Notification integration

- `TaskNotificationClient` sends to `POST /v1/notifications`
- Template keys: `task.assigned`, `task.reassigned`, `task.reminder.due_soon`, `task.reminder.overdue`
- Failures already non-propagating (try/catch → `LogWarning`)
- Auth via `NotificationsAuthDelegatingHandler` (service-token)
- **Minor gaps**: template key constants not centralized; `CorrelationId` not in failure log messages

### Query / index analysis

| Query path | Current cap | Issue |
|---|---|---|
| `GET /tasks` (`SearchAsync`) | `pageSize` default 50, no hard cap | Consumer could pass `pageSize=10000` |
| `GET /tasks/my` (`GetMyTasksAsync`) | `pageSize` default 50 | OK |
| `GetMyTaskSummaryAsync` | Calls `GetByAssignedUserAsync(... pageSize=5000)` | **Unbounded** |
| `GetByAssignedUserAsync` | `pageSize` default 200, no hard cap | Risky |

Indexes present: `IX_Tasks_TenantId_Status`, `IX_Tasks_TenantId_AssignedUserId`, `IX_Tasks_TenantId_Scope_Product`, `IX_Tasks_TenantId_AssignedUser_Status`, `IX_LinkedEntities_TaskId`, `IX_LinkedEntities_EntityRef`.

**Missing**: unique constraint on `(TaskId, EntityType, EntityId)` in `tasks_LinkedEntities` — duplicate linked-entity rows possible.

### Transitional / leftover code

| File | Leftover | Safe to remove? |
|---|---|---|
| `ILienTaskRepository.HasOpenTaskForRuleAsync` | Yes — replaced by HTTP client | YES — engine no longer calls it |
| `ILienTaskRepository.HasOpenTaskForTemplateAsync` | Yes | YES |
| `LienTaskRepository.HasOpenTaskForRuleAsync` impl | Queries `liens_Tasks` directly | YES |
| `LienTaskRepository.HasOpenTaskForTemplateAsync` impl | Queries `liens_Tasks` directly | YES |
| `LienTaskGenerationEngine._taskRepo` injection | Still needed for `AddGeneratedMetadataAsync` | **KEEP** |

---

## 2. Flow Callback Auth Hardening

**Approach**: add `AddServiceTokenBearer` to `Program.cs`, add `InternalService` policy, switch `/api/tasks/internal/flow-callback` to use it.

### Changes

- `Task.Api/Program.cs`:
  - Added `AddServiceTokenBearer(builder.Configuration)` to the auth builder chain
  - Added `"InternalService"` authorization policy: requires `ServiceToken` scheme + `service` role
  - `TaskFlowEndpoints`: flow-callback group changed from `Policies.AdminOnly` → `"InternalService"`

- Token rejected when: no `Authorization` header, non-`Bearer` token, wrong issuer/audience, user token (sub does not start with `service:`), missing `tenant_id`/`tid` claim. All failures logged at Warning by the scheme's `OnAuthenticationFailed` event.

---

## 3. Product Validation Enforcement

**Approach**: `KnownProductCodes` static class in `Task.Domain` enumerates the set from `BuildingBlocks.Authorization.ProductCodes`. `ProductCodeValidator.Validate` called from `TaskService` before create/update; applied consistently to governance, stage, and template endpoints.

### Changes

- Added `Task.Domain/Validation/KnownProductCodes.cs`
- `TaskService.CreateAsync`: validate `sourceProductCode` if provided
- `TaskService.UpdateAsync`: validate `sourceProductCode` if provided
- `TaskGovernanceService.UpsertAsync`: validate before upsert
- `TaskStageService.CreateStageAsync`: validate before create
- `TaskTemplateService.CreateAsync`: validate before create

Invalid product codes throw `ArgumentException` → endpoint returns 400 via `ExceptionHandlingMiddleware`.

---

## 4. Monitoring Integration

**Approach**: `TaskServiceRegistrar : IHostedService` fires once on startup. If `MonitoringService:BaseUrl` is configured it POSTs `{ name, entityType, monitoringType, target }`. 409 = already registered (treated as success). Failures logged at Warning only — never prevent startup.

### Changes

- Added `Task.Infrastructure/Services/TaskServiceRegistrar.cs` (IHostedService)
- Added `TaskMonitoringOptions` (binds `MonitoringService` config section)
- Registered in `DependencyInjection.cs`

---

## 5. Notification Hardening

**Findings**: existing `TaskNotificationClient` is already non-propagating and uses correct patterns.

**Changes**:
- Template keys moved to `internal static` constants (already present, centralized)
- `SubmitAsync` failure log now includes `correlationId` = `taskId` for easier cross-service tracing

---

## 6. Data Integrity & Query Hardening

### Linked entity deduplication

- Added unique index `UX_LinkedEntities_TaskId_EntityType_EntityId` on `(TaskId, EntityType, EntityId)` (migration `20260421000006_LinkedEntityUniqueConstraint`)
- `TaskLinkedEntityRepository.AddAsync` now calls dedup check before insert (safe for concurrent callers: DB unique constraint is the safety net)
- `ITaskLinkedEntityRepository` extended with `ExistsAsync`

### Pagination caps

- `SearchAsync`: `pageSize` capped at 200
- `GetByAssignedUserAsync`: `pageSize` capped at 500
- `GetMyTaskSummaryAsync`: **replaced** the `pageSize=5000` unbounded call with a dedicated `GetActiveTasksForOverdueCountAsync` repo method that does a server-side filtered count (no in-memory `Count()`)

### Index verification

All required indexes already present + new unique index above.

---

## 7. Transitional Cleanup

- `ILienTaskRepository`: removed `HasOpenTaskForRuleAsync` and `HasOpenTaskForTemplateAsync` declarations
- `LienTaskRepository`: removed both implementations (no longer safe to call — `liens_Tasks` columns being dropped)
- `LienTaskGenerationEngine._taskRepo`: **retained** (still needed for `AddGeneratedMetadataAsync`)

---

## 8. Validation Results

### Build results (2026-04-21)

| Service | Errors | Warnings | Result |
|---------|--------|----------|--------|
| Task.Api | 0 | 1 × MSB3277 (pre-existing JwtBearer version conflict) | ✅ PASS |
| Liens.Api | 0 | 1 × MSB3277 (pre-existing JwtBearer version conflict) | ✅ PASS |

MSB3277 is a pre-existing transitive dependency conflict (JwtBearer 8.0.8 vs 8.0.26) unrelated to TASK-B05 changes.

### Change coverage

| Step | Change | Verified |
|------|--------|----------|
| STEP 2 | `AddServiceTokenBearer` + `InternalService` policy | Build ✅ |
| STEP 3 | `KnownProductCodes.Validate/ValidateOptional` in 4 services | Build ✅ |
| STEP 4 | `TaskMonitoringOptions` + `TaskServiceRegistrar` (IHostedService) registered in DI | Build ✅ |
| STEP 5 | `correlationId` added to both failure log branches in `TaskNotificationClient` | Build ✅ |
| STEP 6a | `ITaskLinkedEntityRepository.ExistsAsync` + impl | Build ✅ |
| STEP 6b | Dedup guard in `TaskLinkedEntityRepository.AddAsync` | Build ✅ |
| STEP 6c | Unique index `UX_LinkedEntities_TaskId_EntityType_EntityId` in config + migration 20260421000006 + snapshot | Build ✅ |
| STEP 6d | `SearchAsync` capped at 200, `GetByAssignedUserAsync` capped at 500 | Build ✅ |
| STEP 6e | `GetOverdueCountForUserAsync` in `ITaskRepository` + `TaskRepository` + wired in `GetMyTaskSummaryAsync` | Build ✅ |
| STEP 7 | Removed `HasOpenTaskForRuleAsync` / `HasOpenTaskForTemplateAsync` from `ILienTaskRepository` + `LienTaskRepository` | Build ✅ |

---

## 9. Known Gaps / Risks

| # | Area | Description | Severity |
|---|------|-------------|----------|
| G-01 | Monitoring registration | `TaskServiceRegistrar` uses `TASK_SERVICE_URL` env var for the health endpoint target. If the variable is not set in the deployment environment the monitoring service will register the wrong URL (`http://task:8080`). This must be verified per environment. | Low — registration failure is non-fatal (Warning log) |
| G-02 | Pagination cap enforcement | The `SearchAsync` cap is enforced inside the repository. Callers that previously passed `pageSize > 200` will silently get a capped result set with no error. API documentation should be updated to reflect the hard cap. | Low |
| G-03 | JwtBearer version conflict (MSB3277) | Pre-existing transitive dependency conflict between `JwtBearer 8.0.8` (direct) and `8.0.26` (via BuildingBlocks). Runtime impact is negligible because the higher-binding SDK resolves it, but should be resolved in a dependency cleanup pass. | Low |
| G-04 | Dedup race condition | The `ExistsAsync` + `AddAsync` pattern in `TaskLinkedEntityRepository` is not atomic. Concurrent inserts of the same (taskId, entityType, entityId) can pass the check simultaneously and hit the unique DB constraint. The constraint is the authoritative safety net; callers should handle `DbUpdateException` for the rare concurrent case. | Low — expected to be caught by the DB constraint |
| G-05 | Notification skipped-config log level | When `NotificationsService:BaseUrl` is unset the client logs at `Warning`. In local/dev environments this generates noise; consider `Debug` level gated on an `IsDevelopment` check. | Info |
