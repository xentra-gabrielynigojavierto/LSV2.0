# LS-LIENS-FLOW-004 — Task Notes & Collaboration + Generated Task Visibility

**Status:** Complete  
**Date:** 2026-04-18  
**Spec:** Task Notes/Comments + Generated Task Visibility Enhancements  
**Depends on:** LS-LIENS-FLOW-001 (Tasks), LS-LIENS-FLOW-002 (Templates), LS-LIENS-FLOW-003 (Event-Driven Generation)

---

## 1. Executive Summary

Extends the Synq Liens task layer (FLOW-001/002/003) with two capabilities:

1. **Task Notes** — Text-only comments stored per task in the `liens_TaskNotes` table. Users create, edit, and soft-delete their own notes. The Task Manager UI surfaces notes in a per-task activity thread inside a new slide-in `TaskDetailDrawer`. Notes integrate with the audit pipeline and optionally post a case-timeline event when the task is linked to a case.

2. **Generated Task Visibility** — The `TaskDetailDrawer` exposes a second tab, "Automation Details", for any task where `isSystemGenerated = true`. The panel shows the generation rule ID, template ID, linked case, and a human-readable 3-step explanation of the rules-engine flow. Manual tasks show a plain "Details" tab with task metadata.

Both features are fully wired end-to-end: domain → persistence → application → API → frontend.

---

## 2. Codebase Assessment

### Prior state

| Area | State before FLOW-004 |
|---|---|
| Task entity (`LienTask`) | Had `isSystemGenerated`, `sourceType`, `generationRuleId`, `generatingTemplateId` fields from FLOW-003 |
| Task Manager page | Clicking any card immediately opened `CreateEditTaskForm` — no detail/notes surface |
| Notes infrastructure | `NotesPanel` component existed for cases (generic `Note` interface); no task-level note entity existed |
| Audit | `IAuditPublisher` pattern in use across all services |
| Case timeline | `liens.case.*` audit events published by `CaseService` |

### Key integration points used

- `LienTask` repository — `GetByIdAsync(tenantId, taskId)` used to validate task ownership before note creation
- `IAuditPublisher` — used for `liens.task_note.{created,updated,deleted}` and `liens.case.task_note_added`
- `TASK_STATUS_COLORS` / `TASK_PRIORITY_ICONS` — consumed by the drawer header without duplication
- `formatDateTime` from `lien-utils` — used in the note thread timestamps
- `apiClient` pattern — notes API/service follows the same shape as `lien-tasks.api.ts` / `lien-tasks.service.ts`

---

## 3. Files Changed

### New — Backend

| File | Purpose |
|---|---|
| `Liens.Domain/Entities/LienTaskNote.cs` | Domain entity with `Create`, `Edit`, `SoftDelete` factory methods |
| `Liens.Infrastructure/Persistence/Configurations/LienTaskNoteConfiguration.cs` | EF config: table `liens_TaskNotes`, two composite indexes |
| `Liens.Application/Repositories/ILienTaskNoteRepository.cs` | Repository interface: `GetByTaskAsync`, `GetByIdAsync`, `Add`, `Remove` |
| `Liens.Infrastructure/Repositories/LienTaskNoteRepository.cs` | EF implementation; filters out soft-deleted notes on list query |
| `Liens.Application/DTOs/TaskNoteDTOs.cs` | `TaskNoteResponse`, `CreateTaskNoteRequest`, `UpdateTaskNoteRequest` |
| `Liens.Application/Interfaces/ILienTaskNoteService.cs` | Service interface: `GetNotesAsync`, `CreateNoteAsync`, `UpdateNoteAsync`, `DeleteNoteAsync` |
| `Liens.Application/Services/LienTaskNoteService.cs` | Full service implementation |
| `Liens.Api/Endpoints/TaskNoteEndpoints.cs` | 4 minimal-API endpoints; `MapTaskNoteEndpoints` extension method |
| `Liens.Infrastructure/Persistence/Migrations/20260418000004_AddTaskNotes.cs` | EF migration creating `liens_TaskNotes` |

### Modified — Backend

| File | Change |
|---|---|
| `Liens.Domain/LiensPermissions.cs` | Added `TaskNoteManage = "SYNQ_LIENS.task_note:manage"` |
| `Liens.Infrastructure/Persistence/LiensDbContext.cs` | Added `DbSet<LienTaskNote> LienTaskNotes` |
| `Liens.Infrastructure/DependencyInjection.cs` | Registered `ILienTaskNoteRepository` → `LienTaskNoteRepository` and `ILienTaskNoteService` → `LienTaskNoteService` |
| `Liens.Api/Program.cs` | Called `app.MapTaskNoteEndpoints()` |

### New — Frontend

| File | Purpose |
|---|---|
| `apps/web/src/lib/liens/lien-task-notes.types.ts` | `TaskNoteResponse`, `CreateTaskNoteRequest`, `UpdateTaskNoteRequest` |
| `apps/web/src/lib/liens/lien-task-notes.api.ts` | REST layer: list / create / update / delete |
| `apps/web/src/lib/liens/lien-task-notes.service.ts` | Service wrapper |
| `apps/web/src/components/lien/task-detail-drawer.tsx` | Full slide-in drawer component (see §6) |

### Modified — Frontend

| File | Change |
|---|---|
| `apps/web/src/app/(platform)/lien/task-manager/page.tsx` | Imported `TaskDetailDrawer`; replaced `onClick → setEditTask` with `onClick → setDetailTask`; added `onEdit` path back to `CreateEditTaskForm`; wired `<TaskDetailDrawer>` at bottom of JSX |

---

## 4. Database / Schema Changes

### Table: `liens_TaskNotes`

| Column | Type | Notes |
|---|---|---|
| `Id` | `char(36)` | PK, GUID |
| `TaskId` | `char(36)` | FK → `liens_Tasks.Id` (restricted delete) |
| `TenantId` | `char(36)` | Tenant isolation |
| `Content` | `varchar(5000)` | Note text; max 5000 characters enforced in service |
| `CreatedByUserId` | `char(36)` | Author; used for ownership enforcement |
| `CreatedByUserName` | `varchar(200)` | Denormalized display name |
| `IsEdited` | `bit` | Set to `true` on first edit |
| `IsDeleted` | `bit` | Soft-delete flag; default `false` |
| `CreatedAtUtc` | `datetime(6)` | Set by `SaveChangesAsync` audit hook |
| `UpdatedAtUtc` | `datetime(6)` | Updated by `SaveChangesAsync` audit hook |

### Indexes

| Name | Columns | Purpose |
|---|---|---|
| `IX_TaskNotes_TenantTask` | `(TenantId, TaskId)` | Efficient notes list per task |
| `IX_TaskNotes_TenantUser` | `(TenantId, CreatedByUserId)` | Notes-by-author queries |

### Migration

`20260418000004_AddTaskNotes` — applied to DB. No data migrations needed (additive only).

---

## 5. API Changes

All endpoints under `/api/liens/tasks/{taskId}/notes`.  
Auth: `AuthenticatedUser` policy + product access guard (`SYNQ_LIENS`).

| Method | Path | Permission | Description |
|---|---|---|---|
| `GET` | `/api/liens/tasks/{taskId}/notes` | `TaskRead` | Returns all non-deleted notes for a task, ordered by `CreatedAtUtc` ascending |
| `POST` | `/api/liens/tasks/{taskId}/notes` | `TaskNoteManage` | Creates a note; returns 201 with `Location` header |
| `PUT` | `/api/liens/tasks/{taskId}/notes/{noteId}` | `TaskNoteManage` | Edits own note; sets `IsEdited = true` |
| `DELETE` | `/api/liens/tasks/{taskId}/notes/{noteId}` | `TaskNoteManage` | Soft-deletes own note; returns 204 |

### Response shape (`TaskNoteResponse`)

```json
{
  "id": "guid",
  "taskId": "guid",
  "tenantId": "guid",
  "content": "string (≤5000 chars)",
  "createdByUserId": "guid",
  "createdByUserName": "string|null",
  "isEdited": false,
  "createdAtUtc": "2026-04-18T16:51:00Z",
  "updatedAtUtc": "2026-04-18T16:51:00Z"
}
```

---

## 6. UI Changes

### TaskDetailDrawer (`components/lien/task-detail-drawer.tsx`)

A full-height slide-in panel (max-width `xl`) fixed to the right side with a dark backdrop. Opens when any task card (board or list) is clicked. Closes on backdrop click or the × button.

**Header**
- Task title (2-line clamp), status badge, System Generated badge (violet, robot icon) when applicable
- Edit button (pencil icon) — closes drawer and opens `CreateEditTaskForm` for editable tasks
- Priority icon + due date + linked lien count metadata row
- Full description if present

**Notes tab** (default)

| Element | Behaviour |
|---|---|
| Activity thread | Chronological list of notes; scrolls to bottom on load and on new post |
| Note item | Avatar initials circle, author name, "edited" italic label, relative timestamp |
| Edit/delete controls | Appear on hover over the note bubble (own notes only); edit expands inline textarea; delete calls soft-delete API |
| Inline edit form | 3-row textarea, char counter, Save / Cancel buttons; `savingEdit` spinner state |
| Loading state | Centered spinner while notes load |
| Empty state | Large icon + prompt text |
| Error banner | Dismissible error with Retry button |
| Compose box | 3-row textarea, `maxLength=5000`, char counter, Post button with spinner; auto-appends new note to thread without refetch |

**Details / Automation Details tab**

For **manual tasks** (`isSystemGenerated = false`):
- Task ID, Created by user ID, Created at, Last updated, Completed at (if present), Workflow Stage ID

For **system-generated tasks** (`isSystemGenerated = true`):
- Violet info banner explaining the task was generated automatically
- Generation Rule ID (mono)
- Task Template ID (mono)
- Linked Case ID (if set)
- 3-step numbered explanation: event emitted → rule matched → task created from template
- Followed by the same base task metadata rows

### Task Manager page changes

- Board view: `onClick={(t) => setDetailTask(t)}` (was `setEditTask`)
- List view: row `onClick={() => setDetailTask(task)}` (was `setEditTask`)
- Edit form still reachable via the drawer's Edit button → `onEdit` callback
- `<TaskDetailDrawer>` rendered once at page level; receives `task={detailTask}` (null = closed)

---

## 7. Permissions / Security

| Permission | Constant | Usage |
|---|---|---|
| `SYNQ_LIENS.task:read` | `TaskRead` | Reading notes list (GET) |
| `SYNQ_LIENS.task_note:manage` | `TaskNoteManage` | Creating, editing, deleting notes (POST/PUT/DELETE) |

**Ownership enforcement** in `LienTaskNoteService`:
- `UpdateNoteAsync` — throws `UnauthorizedAccessException` if `note.CreatedByUserId != actorUserId`
- `DeleteNoteAsync` — same check
- Tenant isolation enforced at repository query level; cross-tenant access returns 404 (not 403) to avoid information disclosure

**Validation:**
- Empty content → `ValidationException` with `Content` error key
- Content > 5000 chars → `ValidationException` with `Content` error key
- Edit/delete of soft-deleted note → `ValidationException`
- Note not belonging to the supplied `taskId` → `NotFoundException`

---

## 8. Audit Integration

All events published via `IAuditPublisher` using the standard `AuditEvent` structure.

| Event Name | Trigger | Payload highlights |
|---|---|---|
| `liens.task_note.created` | Note successfully saved | `noteId`, `taskId`, `actorUserId`, `tenantId` |
| `liens.task_note.updated` | Note content changed | `noteId`, `taskId`, `actorUserId` |
| `liens.task_note.deleted` | Note soft-deleted | `noteId`, `taskId`, `actorUserId` |
| `liens.case.task_note_added` | Note created on task that has `CaseId` set | `caseId`, `taskId`, `noteId` — surfaces in case activity feed |

Audit calls are fire-and-forget (logged on failure, do not surface to caller).

---

## 9. Case Timeline Integration

When a note is created on a task that has a `CaseId` set, `LienTaskNoteService.CreateNoteAsync` publishes a secondary audit event `liens.case.task_note_added` in addition to `liens.task_note.created`. This event carries `caseId`, `taskId`, and `noteId` so any case-level activity feed can display "Note added to linked task".

The service does not directly modify the `Case` entity — the integration is purely event-based, keeping the note domain decoupled from the case domain.

---

## 10. Validation Results

| Check | Result |
|---|---|
| `dotnet build Liens.Api/Liens.Api.csproj` | **0 errors**, pre-existing MSB3277 warnings only (unchanged from FLOW-003) |
| EF migration `20260418000004_AddTaskNotes` generated | ✓ |
| EF migration applied to DB (`dotnet ef database update`) | ✓ |
| Next.js frontend compiled (Fast Refresh) | ✓ — 0 TypeScript errors |
| Application workflow restarted and serving | ✓ — proxy ready, login page loads |

---

## 11. Known Gaps / Risks

| Item | Severity | Notes |
|---|---|---|
| `createdByUserName` is caller-supplied | Low | The service accepts whatever name is passed from `ICurrentRequestContext`; no Identity service lookup performed. If the user's display name changes, historical notes retain the old name. Acceptable for v1. |
| Edit/delete controls visible only to all authenticated users on the frontend | Low | Ownership is enforced server-side; the UI shows the controls speculatively and the API returns 401 on mismatch. A future improvement would resolve the current user's ID client-side and hide controls for others' notes. |
| No pagination on notes list | Low | `GetByTaskAsync` returns all non-deleted notes for a task. Acceptable while note counts per task are small; add cursor pagination if volumes grow. |
| No real-time push (no WebSocket/SSE) | Low | Notes refresh only on drawer open or Retry. Multi-user simultaneous editing is not addressed. |
| Soft-deleted notes not surfaced to admins | Info | Deleted notes are excluded from all queries. There is no admin recovery UI yet. |

---

## 12. Run Instructions

### Apply the migration (already applied in dev)

```bash
cd apps/services/liens
dotnet ef database update \
  --project Liens.Infrastructure/Liens.Infrastructure.csproj \
  --startup-project Liens.Api/Liens.Api.csproj
```

### Build check

```bash
cd apps/services/liens
dotnet build Liens.Api/Liens.Api.csproj
```

### Test the notes flow

1. Navigate to **Liens → Task Manager**
2. Click any task card (board) or row (list view) — the **TaskDetailDrawer** slides in from the right
3. **Notes tab** (default): compose a note and click **Post** — the note appears immediately in the thread
4. Hover over your note — edit (pencil) and delete (bin) controls appear
5. Click the pencil to edit inline; click Save
6. Click the bin to soft-delete; note disappears from the thread
7. Switch to **Details** tab — for manual tasks shows task metadata; for system-generated tasks shows the violet Automation Details panel with rule/template IDs and the 3-step engine explanation
8. Click **Edit** (pencil in header) for editable tasks — drawer closes and `CreateEditTaskForm` opens

### Create a test note via API

```bash
curl -X POST https://<host>/api/liens/tasks/<taskId>/notes \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"content": "Test note from API"}'
```
