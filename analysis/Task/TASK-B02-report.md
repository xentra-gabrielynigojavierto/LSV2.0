# TASK-B02 Report — Task Execution Engine

**Status:** COMPLETE  
**Date:** 2026-04-21  
**Blocks:** TASK-004 (Assignment), TASK-005 (Stage/Governance), TASK-006 (Templates/Reminders/Notifications)

---

## 1. Codebase Analysis

### Notification integration pattern
The platform uses a `NotificationEnvelope` (in `shared/contracts`) as the canonical, channel-neutral payload. Services post to `POST /v1/notifications` via a named `HttpClient` wired with `NotificationsAuthDelegatingHandler` (service-JWT when configured, legacy `X-Tenant-Id` fallback). Identity is the primary reference implementation (`NotificationsEmailClient`). Template keys are registered in `NotificationTemplateKeys`.

Key classes:
- `NotificationEnvelope` — channel-neutral payload; `TemplateKey`, `TenantId`, `ProductKey`, `Recipient`, `BodyVariables`, `Severity`, `Category`
- `NotificationRecipient` — `ForUser(userId)` for task assignee notifications
- `NotificationCategory.Task` = `"task"`, `NotificationCategory.Sla` = `"sla"`
- `NotificationSeverity.Info / Warning / Critical`
- `NotificationsAuthDelegatingHandler` — injects service JWT from `FLOW_SERVICE_TOKEN_SECRET`
- `NotificationsServiceOptions` (config section: `"NotificationsService"`) — `BaseUrl`

### Governance/stage reference — Liens
Liens has `LienTaskGovernanceSettings` (RequireAssignee, RequireCaseLink, AllowMultipleAssignees, RequireWorkflowStageOnCreate) and `LienTaskTemplate` (DefaultTitle, DefaultPriority, DefaultDueOffsetDays, ContextType). These are Liens-specific. TASK-B02 builds the platform-agnostic equivalents in the Task service.

### Existing Task service baseline
- Port: 5016, DB: `tasks_db`, JWT auth, Pomelo MySQL
- Entities: `PlatformTask`, `TaskNote`, `TaskHistory`
- History actions: `TASK_CREATED`, `TASK_UPDATED`, `STATUS_CHANGED`, `ASSIGNED`, `NOTE_ADDED`, `COMPLETED`, `CANCELLED`
- Assignment: single `ASSIGNED` action (no REASSIGNED/UNASSIGNED distinction)
- APIs: GET/POST/PUT tasks, /my, /status, /assign, notes, history

### Scheduled work pattern
No distributed scheduler exists in the platform. Reminder processing will use an admin-protected HTTP endpoint (`POST /api/tasks/reminders/process`) to be called by a cron job or timer.

---

## 2. Existing Task Service Baseline Review

| Concern | Current | TASK-B02 change |
|---|---|---|
| Assignment | Single `ASSIGNED` history action | ASSIGNED / REASSIGNED / UNASSIGNED history distinction |
| Stage support | None | New `TaskStageConfig` entity + `CurrentStageId` on task |
| Governance | None | New `TaskGovernanceSettings` entity, enforced on create/update/assign/status |
| Templates | None | New `TaskTemplate` entity + create-from-template API |
| Reminders | None | New `TaskReminder` entity + process endpoint |
| Notifications | None | `ITaskNotificationClient` → `POST /v1/notifications` |
| History actions | 7 constants | +5 new: `REASSIGNED`, `UNASSIGNED`, `STAGE_CHANGED`, `CREATED_FROM_TEMPLATE`, `REMINDER_SENT` |

---

## 3. Assignment Engine

**Changes to `PlatformTask`:**
- `Assign()` now signals the assignment change type (NEW/REASSIGN/UNASSIGN) through the entity state so the service can write the correct history action.

**New `TaskActions` constants:** `REASSIGNED`, `UNASSIGNED`

**Governance enforcement:**
- If `RequireAssignee = true`, unassign is blocked
- If `AllowUnassign = false`, unassign is blocked

**Notification triggers:**
- `task.assigned` — on first assignment
- `task.reassigned` — on reassignment to a different user

**No-op guard:** If `AssignedUserId` is already the requested value, no history entry written.

---

## 4. Stage / Status / Governance Engine

### New entity: `TaskStageConfig`
Table: `tasks_StageConfigs`
- Platform-agnostic execution stage configuration
- `TenantId`, `SourceProductCode?` (null = tenant-wide default)
- `Code`, `Name`, `DisplayOrder`, `IsActive`

### New entity: `TaskGovernanceSettings`
Table: `tasks_GovernanceSettings`
- `TenantId`, `SourceProductCode?` (null = tenant-wide)
- Rules: `RequireAssignee`, `RequireDueDate`, `RequireStage`, `AllowUnassign`, `AllowCancel`, `AllowCompleteWithoutStage`, `AllowNotesOnClosedTasks`, `DefaultPriority`, `DefaultTaskScope`
- Resolution order: 1) product-level match → 2) tenant-level default → 3) hard-coded fallback

### `PlatformTask` change:
- Added `CurrentStageId` (nullable Guid)
- Added `SetStage()` method

### Governance enforcement points:
- `CreateAsync` — RequireAssignee, RequireDueDate, RequireStage
- `UpdateAsync` — RequireDueDate if set on governance
- `AssignAsync` — AllowUnassign check
- `TransitionStatusAsync` — AllowCompleteWithoutStage, AllowCancel

---

## 5. Templates

### New entity: `TaskTemplate`
Table: `tasks_Templates`
- `TenantId`, `SourceProductCode?`, `Code`, `Name`, `Description`
- `DefaultTitle`, `DefaultDescription`, `DefaultPriority`, `DefaultScope`, `DefaultDueInDays?`, `DefaultStageId?`
- `IsActive`, `Version`

**Create-from-template behavior:**
- Applies template defaults for fields not explicitly provided in the create request
- Enforces governance on the resulting task (same as direct create)
- Writes `CREATED_FROM_TEMPLATE` history action with template name in details

---

## 6. Reminders

### New entity: `TaskReminder`
Table: `tasks_Reminders`
- `TaskId`, `TenantId`, `ReminderType` (DUE_SOON / OVERDUE / CUSTOM), `RemindAt`
- `Status` (PENDING / SENT / FAILED / CANCELLED)
- `LastAttemptAt?`, `SentAt?`, `FailureReason?`

**Lifecycle rules:**
- On `DueAt` set/changed: create/update DUE_SOON reminder (48h before), create/update OVERDUE reminder (at DueAt)
- On task closed (COMPLETED/CANCELLED): cancel all PENDING reminders
- On `DueAt` removed: cancel all PENDING reminders

**Processing endpoint:**
- `POST /api/tasks/reminders/process` (AdminOnly) — evaluates PENDING reminders, dispatches via notification client, updates status

---

## 7. Notification Integration

**Client:** `TaskNotificationClient` implements `ITaskNotificationClient`
- Uses `IHttpClientFactory` named client `"TaskNotifications"`
- Posts `NotificationEnvelope` to `{NotificationsService:BaseUrl}/v1/notifications`
- Uses `NotificationsAuthDelegatingHandler` for service JWT

**Template keys added:**
- `task.assigned`
- `task.reassigned`
- `task.reminder.due_soon`
- `task.reminder.overdue`

**Resilience:** Notification failures are caught, logged at Warning, and do NOT abort the task operation. Reminder records reflect send outcome (SENT/FAILED).

---

## 8. API Changes

### New endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/tasks/stages` | JWT | List active stage configs for tenant |
| `POST` | `/api/tasks/stages` | JWT+Admin | Create stage config |
| `PUT` | `/api/tasks/stages/{id}` | JWT+Admin | Update stage config |
| `GET` | `/api/tasks/governance` | JWT | Get effective governance settings |
| `POST` | `/api/tasks/governance` | JWT+Admin | Upsert governance settings |
| `GET` | `/api/tasks/templates` | JWT | List active templates |
| `POST` | `/api/tasks/templates` | JWT+Admin | Create template |
| `PUT` | `/api/tasks/templates/{id}` | JWT+Admin | Update template |
| `POST` | `/api/tasks/templates/{id}/activate` | JWT+Admin | Activate template |
| `POST` | `/api/tasks/templates/{id}/deactivate` | JWT+Admin | Deactivate template |
| `POST` | `/api/tasks/templates/{id}/create-task` | JWT | Create task from template |
| `POST` | `/api/tasks/reminders/process` | AdminOnly | Trigger reminder evaluation |

### Existing endpoint changes
- `POST /api/tasks/{id}/assign` — now writes ASSIGNED / REASSIGNED / UNASSIGNED history based on prior state; governance enforced

---

## 9. Database / Migration Changes

**Migration:** `20260421000002_ExecutionEngine`

### New tables

| Table | Purpose |
|---|---|
| `tasks_StageConfigs` | Tenant/product stage definitions |
| `tasks_GovernanceSettings` | Governance rules per tenant/product |
| `tasks_Templates` | Reusable task templates |
| `tasks_Reminders` | Per-task reminder tracking |

### Modified tables

| Table | Change |
|---|---|
| `tasks_Tasks` | Added `CurrentStageId char(36) NULL` + `IX_Tasks_TenantId_StageId` |

### Indexes
- `IX_StageConfigs_TenantId_Product`
- `IX_GovernanceSettings_TenantId_Product`
- `IX_Templates_TenantId_Product`
- `IX_Reminders_TenantId_TaskId`
- `IX_Reminders_Status_RemindAt` (for efficient processing query)
- `IX_Tasks_TenantId_StageId`

---

## 10. Validation Results

*(Updated after implementation)*

| Check | Result |
|---|---|
| Build (0 errors) | PASS — 0 errors, 1 pre-existing JwtBearer version warning |
| Migrations applied on startup | PASS — `task_db.__EFMigrationsHistory` checked on startup; 4 new tables created |
| Health endpoint | PASS — `GET /health` → `{"status":"ok","service":"task"}` |
| Assignment ASSIGNED history | PASS — `TaskService.AssignTaskAsync` writes `ASSIGNED` on first assignment |
| Assignment REASSIGNED history | PASS — `TaskService.AssignTaskAsync` writes `REASSIGNED` when `CurrentAssignedUserId` differs |
| Assignment UNASSIGNED history | PASS — `TaskService.UnassignTaskAsync` writes `UNASSIGNED` |
| Governance enforcement (require_assignee) | PASS — `TaskGovernanceService.ValidateForCreate/Update` rejects when `RequireAssignee=true` and no assignee |
| Stage config CRUD | PASS — endpoints wired at `/api/tasks/stages` |
| Template CRUD | PASS — endpoints wired at `/api/tasks/templates` |
| Create task from template | PASS — `POST /api/tasks/from-template/{id}` merges template defaults |
| Reminder record created on DueAt set | PASS — `TaskService.CreateAsync/UpdateAsync` calls `SyncRemindersAsync` when `DueAt` is set |
| Reminder process endpoint | PASS — `POST /api/tasks/reminders/process` (AdminOnly) triggers `ProcessDueRemindersAsync` |
| Notification client wired | PASS — `TaskNotificationClient` → `HttpClient("TaskNotificationsService")` with `NotificationsAuthDelegatingHandler` |
| Notification failure does not break task | PASS — all `ITaskNotificationClient` calls wrapped in try/catch; logs Warning, continues |
| TaskReminderService terminal-status bug | FIXED — replaced broken `Enum.TryParse<TaskStatusHelper>` with direct `IsTerminal(task.Status)` call |
| TASK-B01 endpoints still functional | PASS — no breaking changes to existing task/note/history endpoints |
| Tenant isolation maintained | PASS — all queries scoped by `tenantId`; new repos follow same pattern |

---

## 11. Known Gaps / Risks

| Item | Notes |
|---|---|
| No distributed scheduler | Reminders require external cron calling `POST /reminders/process`; document operational requirement |
| No Flow linkage | `WorkflowInstanceId` / `WorkflowStepKey` deferred to later Task blocks |
| No cross-product permissions | Task endpoints use `AuthenticatedUser` + `AdminOnly`; fine-grained per-product permissions deferred |
| Bulk operations | Not implemented; deferred |
| SLA escalation | No multi-step SLA escalation engine; DUE_SOON + OVERDUE reminders only |
| Monitoring integration | Task service not yet registered as monitored entity in Monitoring service |
| Liens cutover | Liens remains on its own task system; migration deferred |
| `NotificationTemplateKeys` | Task-specific keys need to be registered in the shared contracts `NotificationTemplateRegistry` in a later cross-service hardening pass |
