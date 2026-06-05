# LIENS-TASK-MODAL-001 — Task Manager Modal Improvements

**Date:** 2026-04-20  
**Status:** Implementing  

## Scope

Enhance the Create Task modal in Synq Liens Task Manager with five improvements:
1. Auto-skip template picker (pre-select first available template)
2. Move Case field below Description, make always optional
3. Replace Case text input with case autocomplete (search by name/number)
4. Add read-only Reporter field (auto-filled from session)
5. Send email notification to reporter on task creation (backend)

---

## Current State

### Frontend — `create-edit-task-form.tsx`
- Two-step flow: `pick-template` → `fill-form`
- Template picker always shown first; user must click a template OR "Start from Scratch"
- Case field rendered before Assigned To (above the assignee dropdown)
- Case field is a plain text input for a UUID string
- No Reporter field exists
- Governance flag `requireCaseLinkOnCreate` can mark Case as required client-side

### Backend — `LienTaskService.CreateAsync`
- `INotificationPublisher` already injected and used
- On create-with-assignee: fires `liens.task.created_assigned` event with assignee ID in data
- Reporter = `actingUserId` (the authenticated creator); **no separate reporter notification fires today**
- `CreateTaskRequest` DTO has no `ReporterId`; reporter is derived from JWT claim on backend

---

## Changes Required

### 1. Frontend — skip template picker
- On modal open (non-edit), default `step` to `'fill-form'` immediately
- Load templates in background; when loaded, auto-apply first template's defaults if none yet selected
- Show "Use a template" link in form header so user can still navigate to template picker

### 2. Frontend — Case field position & optionality
- Move Case field JSX block to immediately after the Description textarea
- Remove `requireCase` asterisk and client-side required validation for Case
- Keep field hidden when `prefillCaseId` is set (no change there)

### 3. Frontend — Case autocomplete
- Add `caseQuery` (display text), `caseSuggestions` (CaseResponseDto[]), `caseDropdownOpen` state
- Debounce 300ms; search `casesApi.list({ search: caseQuery, pageSize: 10 })`
- Show dropdown with `caseNumber — clientDisplayName` options
- On select: store UUID in `caseId`, display `caseNumber — clientDisplayName` in input
- Clear `caseId` when user clears the input

### 4. Frontend — Reporter field
- Import `useSession` from `@/hooks/use-session`
- Display read-only "Reporter" field showing session email + lookup in `users[]` for full name
- Cosmetic only — reporter = actingUserId on backend already; no DTO change needed

### 5. Backend — reporter notification
- In `LienTaskService.CreateAsync`, after existing `created_assigned` fire:
- Always fire `liens.task.created_reporter` with `reporterId = actingUserId.ToString()`
- Only fire if `actingUserId != effectiveAssignedUserId` (avoid duplicate if same person assigned to themselves)
- Data payload includes: taskId, taskTitle, caseId, priority, dueDate, taskUrl stub

---

## Files Touched

| File | Change |
|---|---|
| `apps/web/src/components/lien/forms/create-edit-task-form.tsx` | All 4 frontend changes |
| `apps/services/liens/Liens.Application/Services/LienTaskService.cs` | Reporter notification |

---

## Validation Plan
1. `pnpm tsc --noEmit` — zero type errors
2. `dotnet build apps/services/liens/Liens.sln` — clean build
3. Manual smoke: open Task Manager → Create Task → form opens directly (no template picker) → first template pre-fills → Case autocomplete works → Reporter shown → save → check logs for notification events
