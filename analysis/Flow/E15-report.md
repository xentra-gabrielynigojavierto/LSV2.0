# E15 Report

## Scope Executed

Built the user-facing **Work** surface that exposes the assignment engine shipped in E14.1/E14.2:

- A consolidated tabbed work area at `/my-work` with three tabs: **My Tasks**, **Role Queue**, **Org Queue**.
- A task detail **drawer** that opens from any list and shows full assignment context.
- A **Claim** action on queue rows + the drawer for queue tasks the caller is eligible for.
- A **Reassign** modal in the drawer, visible only to platform / tenant admins.
- Backend: additive read surface (queue list endpoints + task detail + widened DTO) and the BFF pass-through is reused (no new BFF route).

## Assumptions

1. **Single work area pattern.** The portal already has one `/my-work` route (`apps/web/src/app/(platform)/my-work/page.tsx`). Per the spec's "prefer a single consolidated Work area with tabs", we replace that single-list page with a tabbed shell rather than introducing three sibling routes.
2. **BFF pass-through is sufficient.** The catch-all proxy at `apps/web/src/app/api/flow/[...path]/route.ts` already forwards the platform_session cookie as a Bearer to the gateway. Every new endpoint we add to Flow is reachable through this proxy with no new BFF route required.
3. **Frontend role/org info is not authoritative — it is a hint.** `PlatformSession` already exposes `productRoles`, `systemRoles`, `orgId`, `isPlatformAdmin`, `isTenantAdmin`. We use these only to decide which **tabs and buttons** to show. The backend remains authoritative: the queue endpoints filter by the caller's roles / org server-side, and `Reassign` is gated by `Policies.PlatformOrTenantAdmin` plus a defensive re-check in the assignment service.
4. **MyTaskDto can be widened additively.** Adding new fields with default `null` does not break existing JSON consumers because deserialisers ignore unknown / extra fields. We do this rather than introduce a parallel "TaskDetailDto" so the same row shape is reused for list and detail surfaces.
5. **No Flow direct-from-browser calls.** All UI traffic goes through `/api/flow/...` → Gateway → Flow. We add no exception to this pattern.

## Repository / Architecture Notes

- **Tenant portal**: `apps/web` (Next.js App Router). Layouts under `(platform)` already handle auth/org guards.
- **BFF proxy**: `apps/web/src/app/api/flow/[...path]/route.ts`. Reused as-is.
- **Gateway**: C# YARP, route `flow-protected` strips `/flow` prefix to Flow.Api on :5012.
- **Flow service**: `apps/services/flow/backend/`. Existing surface that was reused: `MyTasksController` (`/api/v1/tasks`), `WorkflowTasksController` (`/api/v1/workflow-tasks`), `IMyTasksService`, `IWorkflowTaskAssignmentService` (E14.2).
- **Identity / session**: `apps/web/src/lib/session.ts` resolves `PlatformSession` from `/identity/api/auth/me`. `getServerSession()` is called in the page component to compute tab/button visibility.

## My Tasks View Notes

- Filter: `AssignmentMode = DirectUser` AND `AssignedUserId = current user`. Server-side; backed by the existing E11.5 endpoint, no change needed.
- Columns: title, workflow context, product, status, priority, createdAt, assignedAt (new — surfaced from the widened DTO).
- Filters: status (Open/InProgress/Completed/Cancelled), inherited from E11.6.
- Empty state: "No tasks assigned to you".

## Role Queue Notes

- New endpoint **GET `/api/v1/tasks/role-queue`** returns tasks where `AssignmentMode = RoleQueue` AND `AssignedRole ∈ {caller's roles}` AND `Status = Open` (only Open is claimable; in-progress queue items aren't a real state in our model).
- The server-side filter uses the same `IFlowUserContext.Roles` (E14.2) used by `EnsureCallerHoldsRole`, so a user is shown exactly the queues they can actually claim from. Platform admins see all role-queue rows.
- Each row carries an inline **Claim** button.
- Tab is hidden when the caller holds zero roles **and** is not a platform admin (the API would return an empty list anyway, but this avoids a misleading empty surface).

## Org Queue Notes

- New endpoint **GET `/api/v1/tasks/org-queue`** returns tasks where `AssignmentMode = OrgQueue` AND `AssignedOrgId = caller's orgId` AND `Status = Open`. Platform admins see all org queues.
- Inline **Claim** button per row.
- Tab is hidden when the caller has no `orgId` and is not a platform admin.

## Task Detail Panel Notes

- Drawer opens from any row in any tab. Owns its own load via the new **GET `/api/v1/workflow-tasks/{id}`** endpoint (returns the same widened `MyTaskDto`).
- Shows: title, status, priority, assignment mode + target (user/role/org), workflow name, product, step, createdAt, assignedAt, assignedBy, assignmentReason, and any lifecycle timestamps that are set.
- Action buttons (visibility rules):
  - **Claim** — when row is `Open` AND mode is `RoleQueue`/`OrgQueue` AND caller looks eligible (best-effort hint; backend authoritative).
  - **Reassign** — when caller is `isPlatformAdmin` or `isTenantAdmin`.
  - Lifecycle (Start/Complete/Cancel) is intentionally **not** added in this drawer — the existing inline row buttons remain in My Tasks. Lifecycle in queue rows is out of scope here.

## Claim UI Notes

- Lightweight: confirm-on-click, no modal. Reason field is not exposed in the UI — the backend stamps a deterministic default ("claimed from queue") when omitted, which keeps audit rows non-blank.
- Calls **POST `/api/v1/workflow-tasks/{id}/claim`**. After 200, the calling list refetches and the task disappears from the queue and appears in My Tasks.
- Errors are translated to friendly text:
  - `403` → "You aren't eligible to claim this task."
  - `422 task_already_assigned` → "This task was already claimed."
  - `422 task_not_claimable` → "This task isn't claimable. Ask an admin to reassign it."
  - `409` → "Someone else just claimed this task. Refreshing."
  - `404` → "This task no longer exists."

## Reassign UI Notes

- Modal opened from the drawer's Reassign button. Visible only to admins.
- Form:
  - **Target mode**: select (DirectUser / RoleQueue / OrgQueue / Unassigned).
  - **Target field**: contextual text input — labelled "User ID", "Role", or "Org ID" depending on mode; hidden for Unassigned.
  - **Reason**: required textarea, max 500 chars (matches backend limit; we reject locally before round-trip but the backend re-validates).
  - Confirm + Cancel.
- Calls **POST `/api/v1/workflow-tasks/{id}/reassign`**. After 200, the drawer reloads its details and the underlying list refetches.
- Error mapping mirrors Claim, plus:
  - `403` → "You don't have permission to reassign tasks."
  - `422 assignment_target_invalid` → backend message surfaced verbatim (it already explains which target field is wrong).

## API / BFF Notes

**New backend endpoints (Flow):**

| Method | Path                                          | Auth                       | Purpose                                  |
| ------ | --------------------------------------------- | -------------------------- | ---------------------------------------- |
| GET    | `/api/v1/tasks/role-queue`                    | AuthenticatedUser          | Open `RoleQueue` tasks the caller can claim. |
| GET    | `/api/v1/tasks/org-queue`                     | AuthenticatedUser          | Open `OrgQueue` tasks the caller can claim.  |
| GET    | `/api/v1/workflow-tasks/{id}`                 | AuthenticatedUser          | Single task detail (widened DTO).         |

**Widened DTO** (`MyTaskDto`): added `AssignmentMode`, `AssignedRole`, `AssignedOrgId`, `AssignedAt`, `AssignedBy`, `AssignmentReason`. All optional/nullable; existing consumers are not broken.

**No new BFF route.** The catch-all proxy already covers any path under `/api/flow/...`.

## Permission / Visibility Notes

- **My Tasks**: visible to any authenticated user with org access.
- **Role Queue tab**: visible only when `productRoles.length > 0 || isPlatformAdmin`.
- **Org Queue tab**: visible only when `orgId != null || isPlatformAdmin`.
- **Reassign button**: visible only when `isPlatformAdmin || isTenantAdmin`. Backend re-asserts via the controller's `PlatformOrTenantAdmin` policy + the service-level `EnsureCallerIsAdmin`.
- The frontend never decides eligibility itself — it only **hides empty / unauthorized affordances** to reduce noise. The backend remains the only authority for actual data filtering and action authorization.

## Validation Results

### Build
- **Flow.sln**: 0 errors, 1 pre-existing warning (`AutomationConditionEvaluator.cs` CS0108 from an earlier phase).
- **Web app type-check (`tsc --noEmit`)**: 0 errors.
- **Web app lint**: clean for the new files.

### Functional (tracing through code paths)
- My Tasks list path (existing E11.5/E11.6) is unchanged and still passes all of its prior contract.
- New role-queue and org-queue endpoints reuse the same paginated query shape and ordering as `MyTasksService` so the UI's pagination/loading semantics are consistent.
- Claim flow: the new claim button calls the existing E14.2 endpoint; UI refetches the list it was called from.
- Reassign flow: modal posts to the existing E14.2 endpoint; on 200, drawer reloads detail and the host list refetches.
- Empty / loading / error states implemented per tab (mirroring the My Work component pattern).

### Security
- Cross-tenant rows cannot be returned: every new query reuses the global `WorkflowTask` query filter, so a cross-tenant id surfaces as 404. Same as every other Flow read.
- Cross-role / cross-org leakage prevented server-side: the queue endpoints filter on `AssignedRole ∈ Roles` / `AssignedOrgId == OrgId` (with platform-admin bypass) BEFORE pagination.
- **Intra-tenant IDOR closed on `GET /workflow-tasks/{id}`** (architect finding, fixed). `GetTaskDetailAsync` now requires the caller to be platform-admin OR the direct assignee OR a holder of the task's role-queue role OR a member of the task's org. Not-allowed rows return 404 (existence-leak avoided).
- Reassign: backend `Policies.PlatformOrTenantAdmin` + `EnsureCallerIsAdmin`; the frontend hide of the button is cosmetic only.
- BFF cookie → Bearer rewrite is unchanged; no token leaks to the browser.

### UX correctness
- **Error-message extraction widened in `apiClient`** (architect finding, fixed). Flow / Identity / most LegalSynq services return `{ error: "..." }`; the previous client only read `message` / `title`, so 422 / 403 / 409 / 404 paths degraded to "HTTP xxx". Now reads `error → message → detail → title`, so the friendly-text mapping in claim / reassign actually fires.

### Data integrity
- All assignment mutations still flow through `IWorkflowTaskAssignmentService` (the spec's "single entry point" contract). The UI never writes assignment columns by any other route.
- After a claim, the task transitions `RoleQueue|OrgQueue → DirectUser` with the audit row that E14.2 already emits. No frontend duplication of mode logic.

## Known Issues / Gaps

1. **Reassign target picker is a free-text field** (User ID / Role / Org ID), not a typeahead. Out of scope for E15 — picker UX would need an identity service / org-directory query that doesn't exist in the current portal. Documented as a follow-up.
2. **No inline lifecycle (Start/Complete/Cancel) buttons in the drawer.** Existing My Tasks rows still carry them; the drawer is intentionally read+claim+reassign only. A unified "row + drawer share the same actions" iteration is a follow-up.
3. **Pre-existing dev-sandbox migration drift** (carried from E14.1/E14.2): the local Flow database may not have `flow_workflow_tasks` applied. Doesn't affect compiled artefacts; only affects this sandbox's local end-to-end smoke. Migration files are correct.
4. **Bulk claim/reassign, SLA, history, notifications, prioritisation, analytics** — all explicitly deferred per spec.

## Recommendation

**Ready for the SLA / history / queue-optimization phase.** The assignment engine is now operationally usable in the UI: users can see and claim from queues they're eligible for; admins can reassign through a governed flow; and the surface stays additive to E14.x without weakening any tenant or governance boundary. The follow-up picker UX is a quality-of-life improvement, not a blocker.

## Files Created

### Backend
- (none — all backend changes are additive on existing files)

### Frontend
- `apps/web/src/components/my-work/work-area-client.tsx` — tabbed shell.
- `apps/web/src/components/my-work/role-queue-client.tsx`
- `apps/web/src/components/my-work/org-queue-client.tsx`
- `apps/web/src/components/my-work/queue-task-row.tsx` — shared row component for queue tabs.
- `apps/web/src/components/my-work/task-detail-drawer.tsx`
- `apps/web/src/components/my-work/reassign-modal.tsx`

## Files Modified

### Backend
- `apps/services/flow/backend/src/Flow.Application/DTOs/MyTaskDtos.cs` — widened `MyTaskDto`; added `RoleQueueQuery`, `OrgQueueQuery`.
- `apps/services/flow/backend/src/Flow.Application/Interfaces/IMyTasksService.cs` — added `ListRoleQueueAsync`, `ListOrgQueueAsync`, `GetTaskDetailAsync`.
- `apps/services/flow/backend/src/Flow.Application/Services/MyTasksService.cs` — implemented the three new queries; refactored DTO projection into a shared expression so widened fields are populated everywhere.
- `apps/services/flow/backend/src/Flow.Api/Controllers/V1/MyTasksController.cs` — added `GET /role-queue`, `GET /org-queue`.
- `apps/services/flow/backend/src/Flow.Api/Controllers/V1/WorkflowTasksController.cs` — added `GET /{id}` task detail.

### Frontend
- `apps/web/src/lib/tasks/tasks.types.ts` — added assignment fields, assignment-mode constants, queue-list params type.
- `apps/web/src/lib/tasks/tasks.api.ts` — added `listRoleQueue`, `listOrgQueue`, `getTaskDetail`, `claim`, `reassign`.
- `apps/web/src/lib/tasks/index.ts` — re-exports.
- `apps/web/src/app/(platform)/my-work/page.tsx` — switches `MyWorkClient` for `WorkAreaClient`; passes session-derived capability flags.
- `apps/web/src/components/my-work/my-work-client.tsx` — minor refactor: row click now opens the drawer (lifecycle buttons preserved).
- `apps/web/src/components/my-work/task-row.tsx` — adds row-click pass-through for drawer opening (no behaviour change to existing buttons).
