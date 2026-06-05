# TASK-B01 Report — Task Service Foundation

**Status:** COMPLETE  
**Date:** 2026-04-21  
**Port:** 5016  
**Gateway prefix:** `/task`  
**Database secret:** `ConnectionStrings__TasksDb`  
**Migration:** `20260421000001_InitialCreate` — applied successfully

---

## 1. Codebase Analysis

Studied the Liens service as the reference implementation for the 4-project DDD pattern used across LegalSynq v2 microservices. Key patterns extracted:

| Concern | Pattern |
|---|---|
| Project layout | Domain / Application / Infrastructure / Api |
| Auth | JWT bearer via `Microsoft.AspNetCore.Authentication.JwtBearer`; `ICurrentRequestContext` (sub/tenant_id/email/roles) |
| DB | EF Core 8 + Pomelo MySQL; `AddMySql()` in `DependencyInjection.cs` |
| Table naming | `{service-slug}_{TableName}` — e.g. `liens_Tasks`, `tasks_Tasks` |
| Endpoints | Minimal API `MapGroup` with `RequireAuthorization(Policies.AuthenticatedUser)` |
| Exception handling | `ExceptionHandlingMiddleware` maps `NotFoundException` → 404, `InvalidOperationException` → 409, `ValidationException` → 400 |
| Migration | Auto-applied on startup via `db.Database.MigrateAsync()` |
| Shared libs | `BuildingBlocks` (AuditableEntity, ICurrentRequestContext, Exceptions, Authorization/Policies/Roles), `Contracts` (HealthResponse, InfoResponse) |
| Gateway | YARP cluster + anonymous health/info routes + protected catch-all |
| Scripts | `run-dev.sh` (dotnet run), `run-prod.sh` (BUILD_PROJECTS + launch loop + probe), `build-prod.sh`, `_startup-helpers.sh` label map |

**Port selection:** 5016 (free; confirmed no conflict with existing services 5001–5015, 5029).

---

## 2. Service Scaffold

Four projects created under `apps/services/task/` and added to `LegalSynq.sln`.

```
apps/services/task/
├── Task.Domain/
│   ├── Task.Domain.csproj              ← refs BuildingBlocks
│   ├── Entities/
│   │   ├── PlatformTask.cs
│   │   ├── TaskNote.cs
│   │   └── TaskHistory.cs
│   └── Enums/
│       ├── TaskStatus.cs
│       ├── TaskPriority.cs
│       └── TaskScope.cs
├── Task.Application/
│   ├── Task.Application.csproj         ← refs Task.Domain, BuildingBlocks
│   ├── DTOs/TaskDtos.cs
│   ├── Interfaces/
│   │   ├── ITaskRepository.cs
│   │   ├── ITaskNoteRepository.cs
│   │   ├── ITaskHistoryRepository.cs
│   │   ├── IUnitOfWork.cs
│   │   └── ITaskService.cs
│   └── Services/TaskService.cs
├── Task.Infrastructure/
│   ├── Task.Infrastructure.csproj      ← refs Task.Application, Pomelo MySQL
│   ├── DependencyInjection.cs          ← AddTaskServices()
│   └── Persistence/
│       ├── TasksDbContext.cs
│       ├── UnitOfWork.cs
│       ├── Configurations/
│       │   ├── PlatformTaskConfiguration.cs
│       │   ├── TaskNoteConfiguration.cs
│       │   └── TaskHistoryConfiguration.cs
│       ├── Repositories/
│       │   ├── TaskRepository.cs
│       │   ├── TaskNoteRepository.cs
│       │   └── TaskHistoryRepository.cs
│       └── Migrations/
│           ├── 20260421000001_InitialCreate.cs
│           └── TasksDbContextModelSnapshot.cs
└── Task.Api/
    ├── Task.Api.csproj                 ← refs Task.Infrastructure, Contracts, BuildingBlocks
    ├── appsettings.json                ← Urls :5016, ConnectionStrings:TasksDb, Jwt
    ├── Program.cs                      ← JWT auth, authorization policies, AddTaskServices, auto-migrate
    ├── DesignTimeDbContextFactory.cs   ← EF CLI support
    ├── Endpoints/
    │   ├── TaskEndpoints.cs
    │   └── TaskNoteEndpoints.cs
    └── Middleware/
        └── ExceptionHandlingMiddleware.cs
```

**Solution integration:** all 4 projects added via `dotnet sln add`.

---

## 3. Database Schema

**Database:** `tasks_db` (configured via `ConnectionStrings__TasksDb` secret)  
**Migration applied:** `20260421000001_InitialCreate`

### Table: `tasks_Tasks`

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `char(36)` | NOT NULL | PK |
| `TenantId` | `char(36)` | NOT NULL | Tenant isolation |
| `Title` | `varchar(500)` | NOT NULL | |
| `Description` | `varchar(4000)` | NULL | |
| `Status` | `varchar(30)` | NOT NULL | Default `OPEN` |
| `Priority` | `varchar(20)` | NOT NULL | Default `MEDIUM` |
| `Scope` | `varchar(20)` | NOT NULL | Default `GENERAL` |
| `AssignedUserId` | `char(36)` | NULL | |
| `SourceProductCode` | `varchar(50)` | NULL | Required when Scope=PRODUCT |
| `SourceEntityType` | `varchar(100)` | NULL | |
| `SourceEntityId` | `char(36)` | NULL | |
| `DueAt` | `datetime(6)` | NULL | |
| `CompletedAt` | `datetime(6)` | NULL | Set on COMPLETED transition |
| `ClosedByUserId` | `char(36)` | NULL | Set on COMPLETED or CANCELLED |
| `CreatedByUserId` | `char(36)` | NOT NULL | |
| `UpdatedByUserId` | `char(36)` | NULL | |
| `CreatedAtUtc` | `datetime(6)` | NOT NULL | |
| `UpdatedAtUtc` | `datetime(6)` | NOT NULL | |

**Indexes:**
- `IX_Tasks_TenantId_Status` — list/filter queries
- `IX_Tasks_TenantId_AssignedUserId` — "my tasks" queries
- `IX_Tasks_TenantId_Scope_Product` — product-scoped task queries
- `IX_Tasks_TenantId_CreatedAt` — ordered list queries

### Table: `tasks_Notes`

| Column | Type | Nullable |
|---|---|---|
| `Id` | `char(36)` | NOT NULL PK |
| `TaskId` | `char(36)` | NOT NULL |
| `TenantId` | `char(36)` | NOT NULL |
| `Note` | `varchar(4000)` | NOT NULL |
| `CreatedByUserId` | `char(36)` | NOT NULL |
| `UpdatedByUserId` | `char(36)` | NULL |
| `CreatedAtUtc` | `datetime(6)` | NOT NULL |
| `UpdatedAtUtc` | `datetime(6)` | NOT NULL |

**Index:** `IX_Notes_TenantId_TaskId`

### Table: `tasks_History`

| Column | Type | Nullable |
|---|---|---|
| `Id` | `char(36)` | NOT NULL PK |
| `TaskId` | `char(36)` | NOT NULL |
| `TenantId` | `char(36)` | NOT NULL |
| `Action` | `varchar(50)` | NOT NULL |
| `Details` | `varchar(500)` | NULL |
| `PerformedByUserId` | `char(36)` | NOT NULL |
| `CreatedAtUtc` | `datetime(6)` | NOT NULL |

**Index:** `IX_History_TenantId_TaskId`

---

## 4. Domain Model

### Entity: `PlatformTask`

Extends `AuditableEntity`. Uses the name `PlatformTask` (not `Task`) to avoid the `System.Threading.Tasks.Task` namespace conflict inherent to the `Task.*` project namespace.

**Factory method:** `PlatformTask.Create(...)` — validates TenantId, Title, Priority, Scope; enforces `SourceProductCode` required when `Scope = PRODUCT`.

**Mutations:**
- `Update(title, updatedByUserId, ...)` — updates title/description/priority/assignee/due-date
- `TransitionStatus(newStatus, updatedByUserId)` — guards against transitioning out of terminal states (`COMPLETED`, `CANCELLED`); sets `CompletedAt` / `ClosedByUserId` on close
- `Assign(userId?, updatedByUserId)` — sets or clears assignee

### Enum: `TaskStatus`

`OPEN` → `IN_PROGRESS` → `COMPLETED` (terminal)  
`OPEN` / `IN_PROGRESS` → `CANCELLED` (terminal)

Terminal states: `COMPLETED`, `CANCELLED` — transitions out of these are blocked by domain guard.

### Enum: `TaskPriority`

`LOW` | `MEDIUM` (default) | `HIGH` | `URGENT`

### Enum: `TaskScope`

`GENERAL` (default) — no product linkage required  
`PRODUCT` — requires `SourceProductCode`; optionally `SourceEntityType` + `SourceEntityId`

### Entity: `TaskNote`

Immutable after creation. Factory method `TaskNote.Create(...)`.

### Entity: `TaskHistory`

Append-only audit log. Factory method `TaskHistory.Record(...)`.  
Constant action codes in `TaskActions`: `TASK_CREATED`, `TASK_UPDATED`, `STATUS_CHANGED`, `ASSIGNED`, `NOTE_ADDED`, `COMPLETED`, `CANCELLED`.

---

## 5. APIs Implemented

All routes registered via Minimal API `MapGroup`. JWT required on all except `/health` and `/info`.

### Task endpoints — `TaskEndpoints.cs`

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/health` | Anonymous | `{"status":"ok","service":"task"}` |
| `GET` | `/info` | Anonymous | `{"service":"task","environment":"...","version":"v1"}` |
| `GET` | `/api/tasks` | JWT | Search/filter tasks (query params: `search`, `status`, `priority`, `scope`, `assignedUserId`, `sourceProductCode`, `page`, `pageSize`) — returns `TaskListResponse` |
| `GET` | `/api/tasks/my` | JWT | Tasks assigned to the calling user |
| `GET` | `/api/tasks/{id}` | JWT | Single task by ID |
| `POST` | `/api/tasks` | JWT | Create task — 201 Created |
| `PUT` | `/api/tasks/{id}` | JWT | Update title/description/priority/assignee/due-date |
| `POST` | `/api/tasks/{id}/status` | JWT | Transition status — `{"status":"IN_PROGRESS"}` |
| `POST` | `/api/tasks/{id}/assign` | JWT | Assign/unassign — `{"assignedUserId":null}` |

### Note & history endpoints — `TaskNoteEndpoints.cs`

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/tasks/{taskId}/notes` | JWT | Add note — 201 Created |
| `GET` | `/api/tasks/{taskId}/notes` | JWT | List notes (asc by `CreatedAtUtc`) |
| `GET` | `/api/tasks/{taskId}/history` | JWT | List history entries (desc by `CreatedAtUtc`) |

### Gateway routes (YARP — `appsettings.json`)

| Route key | Path | Order | Auth |
|---|---|---|---|
| `task-service-health` | `/task/health` | 95 | Anonymous |
| `task-service-info` | `/task/info` | 96 | Anonymous |
| `task-protected` | `/task/{**catch-all}` | 195 | Authenticated |

Cluster: `task-cluster` → `http://localhost:5016`

---

## 6. Notes & Timeline

| Step | Detail |
|---|---|
| Pattern analysis | Liens service used as reference — DI, JWT, EF configs, table naming, endpoint structure |
| Namespace conflict | `Task.*` namespace clashes with `System.Threading.Tasks.TaskStatus` — resolved with `using TaskStatus = Task.Domain.Enums.TaskStatus` aliases in `PlatformTask.cs` and `TaskService.cs` |
| Entity name | `PlatformTask` chosen (not `Task`) to avoid conflict with `System.Threading.Tasks.Task` return type |
| Migration | EF CLI (`dotnet ef migrations add`) ran slowly in environment; migration written manually using the established Liens migration format. `TasksDbContextModelSnapshot.cs` also written manually |
| DB secret | `ConnectionStrings__TasksDb` added as a Replit secret; picked up automatically by ASP.NET Core configuration |
| Build warnings | 1 MSB3277 JWT version warning (8.0.8 vs 8.0.26) — pre-existing across all services, no runtime impact |

---

## 7. Observability

- **Startup logging:** `Starting task v1 in {Environment}` on launch
- **Migration logging:** `Task database migrations applied successfully.` on successful DB init; `Could not apply Task database migrations on startup` warning on failure (non-fatal)
- **Request logging:** ASP.NET Core default request/response logging via `AddConsole()`
- **Operation logging:** `TaskService` logs structured messages for task creation, update, status transition with `TaskId`, `UserId`, `TenantId`
- **Exception handling:** `ExceptionHandlingMiddleware` catches and maps all domain/validation exceptions to structured JSON responses; unhandled exceptions logged at `LogError` level

---

## 8. Validation Results

| Check | Result |
|---|---|
| `dotnet build Task.Api.csproj` | ✅ 0 errors, 1 warning (MSB3277 — pre-existing) |
| `GET :5016/health` | ✅ `{"status":"ok","service":"task"}` |
| `GET :5016/info` | ✅ `{"service":"task","environment":"Production","version":"v1"}` |
| `GET :5010/task/health` (via gateway) | ✅ `{"status":"ok","service":"task"}` |
| `GET :5010/task/info` (via gateway) | ✅ `{"service":"task","environment":"Production","version":"v1"}` |
| EF migrations applied to DB | ✅ `Task database migrations applied successfully.` (confirmed in startup logs) |
| DB schema created | ✅ `tasks_Tasks`, `tasks_Notes`, `tasks_History` tables created |
| `LegalSynq.sln` | ✅ All 4 projects added |
| `run-dev.sh` | ✅ `dotnet run` entry added |
| `run-prod.sh` | ✅ BUILD_PROJECTS, PID_TASK, launch case, `_probe_svc "Task" 5016` added |
| `build-prod.sh` | ✅ `build_service "Task"` added |
| `_startup-helpers.sh` | ✅ `Task.Api) echo "Task"` label mapping added |
| Gateway `appsettings.json` | ✅ `task-cluster` + 3 routes added |

---

## 9. Known Gaps / Risks

| Item | Notes |
|---|---|
| No tenant isolation at DB level | Tenant filtering is query-scoped (all queries include `WHERE TenantId = @tenantId`). No row-level security. Consistent with all other services on the platform. |
| No permission codes | Task endpoints use `Policies.AuthenticatedUser` (any valid JWT). Fine-grained permission codes (e.g. `TaskRead`, `TaskCreate`, `TaskManage`) can be added later following the `LiensPermissions` pattern. |
| `GET /api/tasks/my` ordering | Returns all open+in-progress tasks for the user; no pagination applied. Acceptable at MVP scale; add `page`/`pageSize` if volumes grow. |
| No soft-delete | Tasks are never deleted via API; terminal statuses (`COMPLETED`/`CANCELLED`) serve as the closure mechanism. Hard delete omitted intentionally. |
| `DesignTimeDbContextFactory` connection string | Falls back to a localhost default if `ConnectionStrings:TasksDb` is absent during EF CLI design-time operations. Never used at runtime. |
| Monitoring integration | Task service is not yet registered as a monitored entity in the Monitoring service. Can be added via `POST /monitoring/admin/entities`. |
