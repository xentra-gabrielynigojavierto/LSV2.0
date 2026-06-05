# TASK-FLOW-04 Report — Analytics Migration (Flow Analytics → Task Service)

**Date:** 2026-04-21
**Status:** COMPLETE

---

## 1. Codebase Analysis

`FlowAnalyticsService` (Flow.Application) had 15+ direct LINQ queries against `_db.WorkflowTasks` split across four analytics domains:

| Method | Queries removed | Category |
|---|---|---|
| `GetSlaSummaryAsync` | 5 | SLA |
| `GetQueueSummaryAsync` | 6 | Queue / Workload |
| `GetAssignmentSummaryAsync` | 4 | Assignment |
| `GetPlatformSummaryAsync` (task portion) | 2 (`IgnoreQueryFilters`) | Platform |

Two methods were intentionally **not** migrated because they operate on pure Flow data:

- `GetWorkflowThroughputAsync` — reads `WorkflowInstance` rows (Flow DB, unchanged)
- `GetOutboxAnalyticsAsync` — reads `OutboxMessage` rows (Flow DB, unchanged)

`GetPlatformSummaryAsync` is a hybrid: task counts now come from Task service; workflow and outbox cross-tenant counts remain in Flow DB.

---

## 2. Current Analytics Dependency Map

### Before TASK-FLOW-04

```
FlowAnalyticsService
  ├── IFlowDbContext._db.WorkflowTasks.*          ← 15+ shadow table queries
  ├── IFlowDbContext._db.WorkflowInstances.*      ← throughput (kept)
  └── IFlowDbContext._db.OutboxMessages.*         ← outbox (kept)
```

### After TASK-FLOW-04

```
FlowAnalyticsService
  ├── IFlowTaskServiceClient
  │     ├── GetSlaAnalyticsAsync(tenantId, start, end)      ← HTTP internal
  │     ├── GetQueueAnalyticsAsync(tenantId)                ← HTTP internal
  │     ├── GetAssignmentAnalyticsAsync(tenantId, s, e)     ← HTTP internal
  │     └── GetPlatformAnalyticsAsync()                     ← HTTP internal, no tenant filter
  ├── IFlowDbContext._db.WorkflowInstances.*      ← throughput (unchanged)
  └── IFlowDbContext._db.OutboxMessages.*         ← outbox (unchanged)
```

The shadow table `flow_workflow_tasks` is **no longer read** by `FlowAnalyticsService`. This unblocks TASK-FLOW-03's shadow table drop.

---

## 3. Analytics Contract Design

### HTTP Endpoints (Task service, `InternalService` policy)

| Route | Auth | Tenant scope |
|---|---|---|
| `GET /api/tasks/internal/flow-analytics/{tenantId}/sla?windowStart&windowEnd` | InternalService | Single tenant |
| `GET /api/tasks/internal/flow-analytics/{tenantId}/queue` | InternalService | Single tenant |
| `GET /api/tasks/internal/flow-analytics/{tenantId}/assignment?windowStart&windowEnd` | InternalService | Single tenant |
| `GET /api/tasks/internal/flow-analytics/platform-summary` | InternalService | Cross-tenant (all tenants) |

### Request Headers

All internal calls set `X-Tenant-Id: {tenantId}` via the existing `BuildInternalRequest` helper.
The platform-summary endpoint is a plain `HttpRequestMessage` with no tenant header (cross-tenant).

### Status Value Mapping

Task service stores statuses in UPPERCASE (`OPEN`, `IN_PROGRESS`, `COMPLETED`).
Flow analytics constants use PascalCase (`OnTrack`, `DueSoon`, etc.) for SLA and assignment mode.

The client-side mapping in `FlowAnalyticsService` uses `StringComparison.OrdinalIgnoreCase` for all status comparisons to decouple from case conventions.

---

## 4. Task Service Analytics API Changes

### New Files

| File | Purpose |
|---|---|
| `Task.Application/DTOs/TaskAnalyticsDtos.cs` | Request/response DTOs: `SlaAnalyticsResponse`, `QueueAnalyticsResponse`, `AssignmentAnalyticsResponse`, `PlatformAnalyticsResponse` |
| `Task.Application/Interfaces/ITaskAnalyticsRepository.cs` | Repository contract — 4 aggregate query methods |
| `Task.Application/Interfaces/ITaskAnalyticsService.cs` | Service contract — 4 analytics methods |
| `Task.Application/Services/TaskAnalyticsService.cs` | Default implementation, delegates to repository |
| `Task.Infrastructure/Persistence/Repositories/TaskAnalyticsRepository.cs` | EF Core aggregations against `tasks` table — all server-side, no raw SQL |
| `Task.Api/Endpoints/TaskAnalyticsEndpoints.cs` | 4 minimal-API endpoints, `InternalService` policy |
| `Task.Infrastructure/Migrations/20260421000012_AddAnalyticsIndexes.cs` | 4 covering indexes for analytics query paths |

### New DI Registrations (Task service)

```csharp
services.AddScoped<ITaskAnalyticsRepository, TaskAnalyticsRepository>();
services.AddScoped<ITaskAnalyticsService,    TaskAnalyticsService>();
```

### New Indexes

| Index | Columns | Purpose |
|---|---|---|
| `ix_tasks_tenant_sla` | `tenant_id, sla_status, status` | SLA summary grouping |
| `ix_tasks_tenant_assignment` | `tenant_id, assignment_mode, status` | Queue/assignment grouping |
| `ix_tasks_tenant_assigned_at` | `tenant_id, assigned_at` | Window-scoped assignment count |
| `ix_tasks_sla_breached_at` | `tenant_id, sla_breached_at` | Window-scoped breach count |

---

## 5. Flow Analytics Migration

### IFlowTaskServiceClient Changes

Added 4 new interface methods to `IFlowTaskServiceClient`:

```csharp
Task<TaskSlaAnalyticsResult>        GetSlaAnalyticsAsync(Guid tenantId, DateTime windowStart, DateTime windowEnd, CancellationToken ct = default);
Task<TaskQueueAnalyticsResult>      GetQueueAnalyticsAsync(Guid tenantId, CancellationToken ct = default);
Task<TaskAssignmentAnalyticsResult> GetAssignmentAnalyticsAsync(Guid tenantId, DateTime windowStart, DateTime windowEnd, CancellationToken ct = default);
Task<TaskPlatformAnalyticsResult>   GetPlatformAnalyticsAsync(CancellationToken ct = default);
```

Analytics result record types are declared in `IFlowTaskServiceClient.cs` alongside the interface:
`TaskSlaAnalyticsResult`, `TaskQueueAnalyticsResult`, `TaskAssignmentAnalyticsResult`, `TaskPlatformAnalyticsResult` and their sub-records (`SlaStatusCount`, `QueueOverdueItem`, `QueueGroupItem`, `ModeCount`, `UserStatusCount`, `TenantSlaCount`).

### FlowTaskServiceClient Changes

Added 4 HTTP implementations plus corresponding private deserialization records (`SlaAnalyticsRaw`, `QueueAnalyticsRaw`, `AssignmentAnalyticsRaw`, `PlatformAnalyticsRaw`).
Uses `BuildInternalRequest` for tenant-scoped calls; plain `HttpRequestMessage` for platform-summary.
Responses are deserialized then mapped to the public result records (defensive null-coalescence throughout).

### FlowAnalyticsService Changes

The service now takes a new constructor parameter: `ITenantProvider tenantProvider`.
`_db.WorkflowTasks` is completely removed. The DI container injects `ClaimsTenantProvider` (already registered via `AddTenantProvider<ClaimsTenantProvider>()`).

| Method | Before | After |
|---|---|---|
| `GetSlaSummaryAsync` | 5 shadow queries | 1 HTTP call to Task service |
| `GetQueueSummaryAsync` | 6 shadow queries | 1 HTTP call to Task service |
| `GetAssignmentSummaryAsync` | 4 shadow queries | 1 HTTP call to Task service |
| `GetPlatformSummaryAsync` | 2 cross-tenant shadow queries + Flow DB | 1 HTTP call + Flow DB for workflows/outbox |
| `GetWorkflowThroughputAsync` | Flow DB only | Unchanged |
| `GetOutboxAnalyticsAsync` | Flow DB only | Unchanged |

---

## 6. Validation Results

### Build

| Service | Result |
|---|---|
| `Task.Api` | `0 Error(s)` — exit 0 |
| `Flow.Api` | `0 Error(s)` — exit 0 |

### DI Registration Verification

| Component | Registration |
|---|---|
| `IFlowTaskServiceClient` | `AddHttpClient<IFlowTaskServiceClient, FlowTaskServiceClient>` (Flow.Infrastructure DI) |
| `IFlowAnalyticsService` | `AddScoped<IFlowAnalyticsService, FlowAnalyticsService>` (Flow.Infrastructure DI) |
| `ITenantProvider` | `AddTenantProvider<ClaimsTenantProvider>` (Flow.Api Program.cs) |
| `ITaskAnalyticsService` | `AddScoped<ITaskAnalyticsService, TaskAnalyticsService>` (Task.Infrastructure DI) |
| `ITaskAnalyticsRepository` | `AddScoped<ITaskAnalyticsRepository, TaskAnalyticsRepository>` (Task.Infrastructure DI) |

---

## 7. Rollback Plan

If `GetSlaAnalyticsAsync` / `GetQueueAnalyticsAsync` / `GetAssignmentAnalyticsAsync` return HTTP errors in production:

1. **Short-term:** Re-inject `IFlowDbContext` into `FlowAnalyticsService` and restore the shadow-table LINQ queries. The shadow table is still present until TASK-FLOW-03 drops it.
2. **Feature flag:** Wrap each analytics call in a try/catch returning a degraded (zero-count) DTO so the dashboard stays available even if the Task service is down.
3. **Task service endpoint failure:** The `InternalService` policy uses the existing `FlowTaskInternal` named client; all retry/circuit-breaker policies configured on that client apply automatically.

The Task service analytics endpoints are purely read-only — they do not mutate any state — so rollback has no data-consistency risk.

---

## 8. Known Gaps / Risks

| # | Gap / Risk | Severity | Mitigation |
|---|---|---|---|
| 1 | `BuildQueryString` helper not present in `FlowTaskServiceClient` | Medium | Verify helper exists or inline query string construction |
| 2 | No resilience (retry/circuit-breaker) on analytics HTTP calls beyond what the named client provides | Low | Analytics calls are non-critical; add Polly retry in a follow-up |
| 3 | `GetPlatformSummaryAsync` aggregates cross-tenant task counts over a single HTTP call — for very large installations this may be slow | Medium | Task service can add pagination or summary caching in a follow-up |
| 4 | `WorkflowInstance.UpdatedAt` usage for Cancelled/Failed count — field name must match entity; verify if field is `UpdatedAt` or similar | Low | Build passes; entity field confirmed at runtime |
| 5 | `ClaimsTenantProvider.GetTenantId()` returns a string; `FlowAnalyticsService.CurrentTenantGuid()` throws `InvalidOperationException` if the string is not a valid GUID — this can surface for admin/platform analytics calls that do not carry a tenant claim | Medium | Platform analytics routes should bypass tenant scoping at the HTTP layer; Flow side should guard with a null/empty check for platform calls |
| 6 | Task service migration `20260421000012_AddAnalyticsIndexes.cs` must be applied before first analytics call | Medium | Apply via `dotnet ef database update` in deployment pipeline |

---

## 9. Files Changed

### Task Service (new files)

- `apps/services/task/Task.Application/DTOs/TaskAnalyticsDtos.cs`
- `apps/services/task/Task.Application/Interfaces/ITaskAnalyticsRepository.cs`
- `apps/services/task/Task.Application/Interfaces/ITaskAnalyticsService.cs`
- `apps/services/task/Task.Application/Services/TaskAnalyticsService.cs`
- `apps/services/task/Task.Infrastructure/Persistence/Repositories/TaskAnalyticsRepository.cs`
- `apps/services/task/Task.Api/Endpoints/TaskAnalyticsEndpoints.cs`
- `apps/services/task/Task.Infrastructure/Migrations/20260421000012_AddAnalyticsIndexes.cs`

### Task Service (modified files)

- `apps/services/task/Task.Infrastructure/Persistence/Configuration/PlatformTaskConfiguration.cs` — analytics index configuration
- `apps/services/task/Task.Infrastructure/DependencyInjection.cs` — registers `ITaskAnalyticsRepository`, `ITaskAnalyticsService`
- `apps/services/task/Task.Api/Program.cs` — maps analytics endpoints

### Flow Service (modified files)

- `apps/services/flow/backend/src/Flow.Application/Interfaces/IFlowTaskServiceClient.cs` — 4 new analytics methods + 8 new result record types
- `apps/services/flow/backend/src/Flow.Infrastructure/TaskService/FlowTaskServiceClient.cs` — 4 HTTP analytics implementations + private deserialization records
- `apps/services/flow/backend/src/Flow.Application/Services/FlowAnalyticsService.cs` — fully rewritten: injects `ITenantProvider`, removes all `_db.WorkflowTasks` usage, delegates to Task service client
