# Flow Service — Architecture Document

## Purpose

Flow is a generic, reusable workflow and task orchestration service. It provides the foundation for defining workflows, managing task lifecycles, processing events, and supporting audit and notification integrations.

Flow is designed to be a **detachable, standalone service** that can operate independently or integrate with other systems (such as LegalSynq v2) via APIs and adapters — never through shared database coupling.

## Detachable Service Principle

- Flow owns its own database (`flow_db`) and does not share schema or connections with any consuming application.
- Integration with external systems is accomplished through:
  - REST APIs exposed by Flow
  - Adapter interfaces implemented by consuming applications
  - Event-based communication (future)
  - Generic `ContextReference` for external entity linking without tight coupling
- No external system-specific entities (e.g., caseId, lienId, referralId) exist in Flow's core model.

## Stack

### Backend
- **Runtime**: .NET 8.0 (ASP.NET Core Web API)
- **Language**: C#
- **ORM**: Entity Framework Core with Pomelo MySQL provider
- **Database**: MySQL (`flow_db`)
- **Architecture**: Clean Architecture (layered)

### Frontend
- **Framework**: Next.js (React)
- **Language**: TypeScript
- **Styling**: Tailwind CSS
- **Brand**: SynqFlow (frontend brand; backend remains generic "Flow")

## Layered Backend Architecture

```
Flow.Api            → HTTP layer: controllers, DI, middleware, CORS, health checks
Flow.Application    → Business logic: services, DTOs, validation, state transitions
Flow.Domain         → Core: entities, enums, value objects, domain rules
Flow.Infrastructure → Persistence: EF Core, MySQL, repository implementations, DI registration
```

### Dependency Rules
- **Flow.Api** → references Flow.Application, Flow.Infrastructure
- **Flow.Application** → references Flow.Domain only
- **Flow.Infrastructure** → references Flow.Domain, Flow.Application
- **Flow.Domain** → no project references (pure domain)

## flow_db Ownership Principle

- The `flow_db` database is exclusively owned by the Flow service.
- No other service or application should read from or write to `flow_db` directly.
- Schema migrations are managed solely through Flow's EF Core migration pipeline.
- External systems interact with Flow data only through Flow's public API surface.

## Database Configuration

Connection string resolution order:
1. `ConnectionStrings:FlowDb` from appsettings.json / appsettings.{Environment}.json
2. `FLOW_DB_CONNECTION_STRING` environment variable
3. Default local development fallback

Production deployments should use environment variables or secrets management. Never commit production credentials to appsettings files.

## Modular Engine Architecture

Flow is structured around logical engine modules that will be developed incrementally:

| Module | Purpose | Status |
|--------|---------|--------|
| **Task Engine** | Task lifecycle management, assignment, status transitions | Foundation implemented (LS-FLOW-002) |
| **Workflow Engine** | Flow definition execution, step orchestration, state management | Foundation implemented (LS-FLOW-008) |
| **Event Processor** | Internal and external event handling, triggers | Placeholder |
| **Audit Adapter** | Audit trail recording, compliance logging | Placeholder |
| **Notification Adapter** | Internal notification persistence + event generation | Foundation implemented (LS-FLOW-015-A1) |

Each module has a dedicated namespace under `Flow.Application` with its own interfaces, allowing independent development and testing.

## Task Engine

### Task Entity (TaskItem)
The core task entity supports:
- Identity: `Id` (GUID)
- Content: `Title`, `Description`
- Lifecycle: `Status` (enum), `DueDate`
- Assignment: `AssignedToUserId`, `AssignedToRoleKey`, `AssignedToOrgId`
- Linking: `FlowDefinitionId` (nullable), `WorkflowStageId` (nullable), `ContextReference` (owned entity)
- Audit: `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`

### Task State Model

States: `Open`, `InProgress`, `Blocked`, `Done`, `Cancelled`

Allowed transitions:

| From | Allowed To |
|------|------------|
| Open | InProgress, Blocked, Cancelled |
| InProgress | Blocked, Done, Cancelled |
| Blocked | Open, InProgress, Cancelled |
| Done | Open (reopen) |
| Cancelled | Open (reopen) |

Transition validation is enforced in the application layer (`TaskStateTransitions` rule class). Invalid transitions return HTTP 422 with a descriptive error.

**Deferred**: Full workflow-driven state machine, conditional transitions, automated triggers. These will be part of the Workflow Engine.

### Assignment Model

Basic assignment fields:
- `AssignedToUserId` — specific user assignment (nullable)
- `AssignedToRoleKey` — role-based assignment (nullable)
- `AssignedToOrgId` — organization-scoped assignment (nullable)

All fields are nullable and independently settable. Assignment is updated via `PATCH /api/v1/tasks/{id}/assign`.

**Deferred**: Permission enforcement, dynamic assignment rules, assignment history, role resolution.

### Task API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/tasks` | List tasks with optional filters |
| GET | `/api/v1/tasks/{id}` | Get task by ID |
| POST | `/api/v1/tasks` | Create a new task |
| PUT | `/api/v1/tasks/{id}` | Update task details |
| PATCH | `/api/v1/tasks/{id}/status` | Update task status (validated transitions) |
| PATCH | `/api/v1/tasks/{id}/assign` | Update task assignment |

Filtering supported on list endpoint: `status`, `assignedToUserId`, `assignedToRoleKey`, `assignedToOrgId`, `contextType`, `contextId`.

Pagination and sorting supported on list endpoint via query parameters: `page`, `pageSize`, `sortBy`, `sortDirection`.

Paginated response shape: `{ items, totalCount, page, pageSize }`.

Supported sort fields: `createdAt`, `dueDate`, `title`, `status`, `updatedAt`. Default: `createdAt desc`.

**Not implemented yet**: DELETE (intentional — tasks should transition to Cancelled rather than be deleted; soft delete may be added later), full-text search.

## Error Handling

Global exception handling middleware (`ExceptionHandlingMiddleware`) provides consistent JSON error responses across all API endpoints:

| Exception Type | HTTP Status | Response |
|---------------|-------------|----------|
| `NotFoundException` | 404 | `{ error: "..." }` |
| `ValidationException` | 400 | `{ error: "Validation failed.", errors: [...] }` |
| `InvalidStateTransitionException` | 422 | `{ error: "..." }` |
| Unhandled | 500 | `{ error: "An unexpected error occurred." }` |

Controllers do not contain per-action try/catch blocks — all exception-to-HTTP-status mapping is centralized in the middleware.

## Frontend Architecture

### Structure
```
src/
  app/                    → Next.js App Router pages
    page.tsx              → Landing/dashboard page
    tasks/page.tsx        → Task list/board view with toggle + drawer
  components/
    tasks/                → Task-specific components
      TaskFilterBar.tsx   → Structured filter controls
      TaskTable.tsx       → Sortable task table
      TaskDetailDrawer.tsx → Detail view with editing, status, assignment, activity
      CreateTaskDrawer.tsx → Create task form with full field set
      StatusChangeDialog.tsx → Quick status transition modal (from list)
      activity/           → Activity timeline components
        ActivityTimeline.tsx → Event list with subscribe pattern
        ActivityItem.tsx     → Individual event row with icon
      board/              → Board view components
        BoardView.tsx     → Kanban container with drag-drop + error handling
        BoardColumn.tsx   → Single status column with drop target
        TaskBoardCard.tsx → Compact task card with quick actions
    ui/                   → Shared UI primitives
      Drawer.tsx          → Reusable right-side drawer shell
      Pagination.tsx      → Page navigation controls
      StatusBadge.tsx     → Color-coded status indicator
      ViewToggle.tsx      → List/Board view switcher
  lib/
    api/tasks.ts          → Centralized API client (fetch-based)
    api/workflows.ts      → Workflow API client (listWorkflows, getWorkflow)
    activityStore.ts      → In-memory activity event store (singleton)
    config.ts             → Environment configuration
  types/
    task.ts               → TypeScript types matching API contracts (includes workflow fields)
    activity.ts           → Activity event model types
    workflow.ts           → Workflow definition, stage, transition types
```

### API Integration Approach
- All API calls are centralized in `src/lib/api/tasks.ts`
- Base URL configured via `NEXT_PUBLIC_FLOW_API_URL` environment variable
- Typed request/response models in `src/types/task.ts` match backend DTOs
- Generic `apiFetch` wrapper handles error parsing and content-type headers
- No fetch logic scattered in components — components call typed API functions
- API client functions: `listTasks`, `getTask`, `createTask`, `updateTask`, `updateTaskStatus`, `assignTask`

### View Switching
The `/tasks` page supports two view modes, toggled via `ViewToggle`:
- **List View**: Sortable table with pagination (default)
- **Board View**: Kanban-style columns grouped by status

Filters are preserved when switching views. Board mode fetches all tasks (pageSize=100) since pagination breaks column grouping. The view toggle is in the page header, always accessible.

### Create Task
The "+ New Task" button is always visible in the page header. It opens a `CreateTaskDrawer` with:
- **Required**: Title
- **Optional**: Description, Status (default Open), Due Date, Assigned User/Role/Org, Context Type/ID
- Calls `POST /api/v1/tasks` when backend is available
- Falls back to in-memory local mode if API fails

### Local Fallback Mode
If the backend is unavailable (API fetch fails), the UI switches to local mode:
- Tasks are stored in-memory via `useRef`
- A yellow banner indicates "Local mode — backend unavailable"
- Create task, status changes, view switching, and filters all work locally
- Local tasks are prefixed with `local-` IDs
- When backend becomes available, local tasks persist alongside API results until page reload

### Task List View
The list view is the default operational screen. It provides:
- Sortable table with configurable columns
- Structured filter bar (status, user, role, org, context type/id)
- Server-side pagination with page/pageSize controls
- Loading, empty, and error states
- Inline status change action via modal dialog
- Row click opens task detail drawer

### Board View (Kanban)
The board view presents tasks grouped by status in horizontal columns:
- **Columns**: Open, InProgress, Blocked, Done, Cancelled — each with colored header border and item count badge
- **Cards**: Compact task cards showing title, description preview, assignment badges, context, and due date
- **Drag-and-drop**: Native HTML5 drag-and-drop between columns — no external library
- **Quick actions**: Hover menu on each card to move to any other status
- **Move interaction**: Calls `PATCH /api/v1/tasks/{id}/status` — on failure, shows error banner and refetches to revert
- **Drop target feedback**: Columns highlight with blue ring when a card is dragged over
- **Empty columns**: Render with dashed border placeholder to maintain layout and accept drops
- **Detail drawer**: Card click opens the same `TaskDetailDrawer` used in list view; drawer mutations trigger board refresh
- **Data source**: Uses existing `listTasks` API with large page size — no board-specific backend endpoint

### Task Detail Drawer
The detail drawer slides in from the right when a row is clicked. It provides:
- **Header**: editable title, status badge, close button
- **Details section**: editable description, due date, read-only context
- **Status section**: dropdown to change status (calls PATCH /status)
- **Assignment section**: editable user/role/org fields (calls PATCH /assign)
- **Metadata section**: created/updated timestamps, created/updated by
- **Activity Timeline**: Live event log showing task history (see Activity System below)
- **Placeholder sections**: Comments (coming soon)
- Closable via close button, clicking outside, or Escape key
- Does not navigate away from `/tasks` — list state is preserved
- On save/update, refreshes the list to reflect changes

## Workflow Engine (LS-FLOW-008) — COMPLETED

### Domain Model
The workflow engine introduces three new entities:

**FlowDefinition** (existing, extended):
- `Name`, `Description`, `Version`, `Status` (Draft/Active/Paused/Completed/Cancelled)
- Navigation: `Stages`, `Transitions` collections

**WorkflowStage**:
- `WorkflowDefinitionId` — parent workflow
- `Key` — stable internal identifier (e.g., "open", "in-progress")
- `Name` — display label
- `MappedStatus` — maps to `TaskItemStatus` enum
- `Order` — display/sequence ordering
- `IsInitial` / `IsTerminal` — stage lifecycle markers

**WorkflowTransition**:
- `WorkflowDefinitionId` — parent workflow
- `FromStageId` / `ToStageId` — the transition path
- `Name` — human-readable transition label (e.g., "Start Work", "Complete")
- `IsActive` — toggle for enabling/disabling transitions

### Task-to-Workflow Linkage
`TaskItem` has two optional fields for workflow governance:
- `FlowDefinitionId` — which workflow this task belongs to (nullable)
- `WorkflowStageId` — which stage the task is currently in (nullable)

When a task is created with a `FlowDefinitionId`, it's automatically placed in the workflow's initial stage.

### Transition Validation Strategy
When a status change is requested via `PATCH /api/v1/tasks/{id}/status`:
1. **Workflow-governed task** (has `WorkflowStageId`): Look up the current stage, find active transitions from that stage, check if any transition leads to a stage with the requested status. If valid, update both `Status` and `WorkflowStageId`. If invalid, return HTTP 422.
2. **Non-workflow task** (no `WorkflowStageId`): Fall back to the existing generic `TaskStateTransitions` rule map.

This dual-path design ensures backward compatibility — all existing tasks continue to work without modification.

### Workflow APIs

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/workflows` | List all workflow definitions (summary) |
| GET | `/api/v1/workflows/{id}` | Get workflow with stages and transitions |
| POST | `/api/v1/workflows` | Create a new workflow definition |
| PUT | `/api/v1/workflows/{id}` | Update workflow name/description/status |
| DELETE | `/api/v1/workflows/{id}` | Delete workflow (blocked if tasks assigned) |
| POST | `/api/v1/workflows/{id}/stages` | Add a stage to workflow |
| PUT | `/api/v1/workflows/{id}/stages/{stageId}` | Update stage details |
| DELETE | `/api/v1/workflows/{id}/stages/{stageId}` | Delete stage (blocked if used in transitions) |
| POST | `/api/v1/workflows/{id}/transitions` | Add a transition between stages |
| PUT | `/api/v1/workflows/{id}/transitions/{transitionId}` | Update transition name/active |
| DELETE | `/api/v1/workflows/{id}/transitions/{transitionId}` | Delete transition |

### TaskResponse Enhancement
The `TaskResponse` DTO now includes:
- `workflowStageId` — current stage ID (if workflow-governed)
- `workflowName` — name of the linked workflow
- `workflowStageName` — name of the current stage
- `allowedNextStatuses` — list of valid next statuses based on workflow transitions (or generic fallback)

### Seed Data
A "Standard Task Flow" workflow is seeded with 5 stages (Open, In Progress, Blocked, Done, Cancelled) and 9 transitions matching the existing generic transition rules. This serves as the default workflow for development and testing.

### Database Tables
- `flow_definitions` — workflow definitions (existing, unchanged)
- `workflow_stages` — stages within workflows
- `workflow_transitions` — allowed transitions between stages
- `task_items` — added `WorkflowStageId` column with FK and index

### Frontend Workflow Visibility
The `TaskDetailDrawer` shows workflow context when available:
- Workflow name with icon
- Current stage label
- Status dropdown filtered to only show allowed next statuses
- Transition count indicator

The `TaskBoardCard` quick-action menu also uses `allowedNextStatuses` when available, falling back to all statuses when not.

The `BoardView` drag-and-drop handler performs a client-side check before attempting a move — if the task has `allowedNextStatuses` and the target column is not in that list, the move is rejected with a user-visible error message. This is a UX guardrail; the backend remains the authoritative source of truth for transition validation.

Local mode continues to use generic task statuses — workflow governance is purely additive and requires no frontend simulation.

### Local Mode Sync
The `TaskDetailDrawer` passes updated task objects back to the parent via `onUpdated(updatedTask)` for all local mutations (edits, status changes, assignment changes). The parent page syncs the updated task into `localTasksRef` and refreshes the data view, ensuring the board and list views reflect drawer changes immediately without a round-trip to the API.

### Key Files (Backend)
| File | Purpose |
|------|---------|
| `Flow.Domain/Entities/WorkflowStage.cs` | Stage entity with key, mapped status, ordering, terminal flags |
| `Flow.Domain/Entities/WorkflowTransition.cs` | Transition entity linking two stages |
| `Flow.Domain/Entities/TaskItem.cs` | Extended with `WorkflowStageId` FK |
| `Flow.Domain/Entities/FlowDefinition.cs` | Extended with `Stages` and `Transitions` nav props |
| `Flow.Application/DTOs/WorkflowDtos.cs` | Response DTOs for workflow, stage, transition |
| `Flow.Application/Interfaces/IWorkflowService.cs` | Workflow service contract |
| `Flow.Application/Services/WorkflowService.cs` | Workflow query implementation |
| `Flow.Application/Services/TaskService.cs` | Updated with workflow-aware status transitions |
| `Flow.Infrastructure/Persistence/FlowDbContext.cs` | EF mappings for stages, transitions, seed data |
| `Flow.Infrastructure/Persistence/WorkflowSeedData.cs` | Standard Task Flow seed (5 stages, 9 transitions) |
| `Flow.Api/Controllers/WorkflowsController.cs` | REST endpoints for workflow queries |

### Key Files (Frontend)
| File | Purpose |
|------|---------|
| `types/workflow.ts` | WorkflowDefinition, WorkflowStage, WorkflowTransition types |
| `types/task.ts` | TaskResponse extended with workflow fields |
| `lib/api/workflows.ts` | API client for workflow endpoints |
| `components/tasks/TaskDetailDrawer.tsx` | Workflow badge, stage label, filtered status dropdown |
| `components/tasks/board/TaskBoardCard.tsx` | Quick-move menu respects allowedNextStatuses |
| `components/tasks/board/BoardView.tsx` | Drag-and-drop validates allowed transitions |
| `app/tasks/page.tsx` | Local mode sync for drawer mutations |

## Workflow Assignment UI (LS-FLOW-009) — COMPLETED

### Backend Adjustments
- `TaskService.CreateAsync`: When `FlowDefinitionId` is provided, resolves initial stage and sets both `WorkflowStageId` and `Status` to the initial stage's `MappedStatus` (previously hardcoded to `Open`)
- `TaskService.UpdateAsync`: When `FlowDefinitionId` changes, re-resolves initial stage, updates `WorkflowStageId` and synchronizes `Status`. Clearing workflow unlinks both `FlowDefinitionId` and `WorkflowStageId`

### Workflow Selector Component
Reusable `WorkflowSelector` at `components/tasks/workflow/WorkflowSelector.tsx`:
- Fetches available workflows from `GET /api/v1/workflows`
- Handles loading, error, empty, and local mode states gracefully
- In local mode: disabled with "Workflow selection requires backend connection"
- On API failure: shows "None (generic transitions)" as clearable option with warning text
- On empty list: shows "No workflows available"
- On success: dropdown with workflow names and versions, "None" as default

### Create Task Drawer Integration
- `CreateTaskFormData` includes `flowDefinitionId` (optional)
- Workflow selector added between Description and Status fields
- When workflow selected, status dropdown is disabled with helper text explaining status is set by workflow's initial stage
- `localMode` prop passed from parent to disable selector in local mode
- `flowDefinitionId` sent in `createTask` API call when present

### Task Detail Drawer Integration
- Workflow section always visible (not just when workflow present)
- View mode: shows workflow name + stage + "Transition model: Workflow" / "Generic"
- Edit mode: "Change"/"Assign" button opens WorkflowSelector dropdown
- Save/Cancel flow for workflow changes via existing `PUT /tasks/{id}` endpoint
- On save: refreshes task state from API response, emits activity event, updates parent
- Local tasks: shows "Workflow requires backend connection", no edit controls

### Workflow Assignment Behavior
- **Assign workflow**: Select workflow → Save → backend assigns initial stage + status
- **Change workflow**: New workflow → Save → backend re-initializes to new workflow's initial stage/status
- **Clear workflow**: Select "None" → Save → backend unlinks workflow/stage, task falls back to generic transitions

### Local Mode Compatibility
- Workflow selector disabled with explanatory text in local mode
- Forms remain fully usable without workflow selection
- Detail drawer shows informational message instead of edit controls
- No simulation of workflow definitions locally

### Key Files
| File | Purpose |
|------|---------|
| `components/tasks/workflow/WorkflowSelector.tsx` | Reusable workflow selector with loading/error/empty states |
| `components/tasks/CreateTaskDrawer.tsx` | Extended with workflow field and localMode prop |
| `components/tasks/TaskDetailDrawer.tsx` | Workflow assign/change/clear with edit mode |
| `app/tasks/page.tsx` | Passes localMode to CreateTaskDrawer, sends flowDefinitionId on create |
| `types/task.ts` | CreateTaskRequest includes flowDefinitionId |
| `Flow.Application/Services/TaskService.cs` | Create/Update sync workflow stage and status |

## Workflow Transition Enhancements (LS-FLOW-011) — COMPLETED

### Transition Validation Rules
Configurable validation system in `lib/transitionRules.ts`:

| Target Status | Rules |
|---------------|-------|
| Done | Title required, Description required, Confirmation dialog |
| Cancelled | Confirmation dialog |
| InProgress | Title required |

Rules are defined as a simple `TRANSITION_RULES` record — easily extended without code changes. Validation runs on both workflow action buttons and generic dropdown paths.

### Confirmation Dialogs
`ConfirmDialog` (`components/ui/ConfirmDialog.tsx`) — reusable modal:
- Triggered for Done and Cancelled transitions
- Escape key to dismiss, backdrop click to close
- Loading state on confirm button
- Danger variant for Cancelled (red confirm button)
- Standard variant for Done (blue confirm button)
- Focus automatically placed on Cancel button for safety

### Validation UX
- **Pre-transition validation**: Runs before confirmation dialog or API call
- **Inline amber warning**: Shows validation errors (e.g., "Description is required before moving to Done")
- **Auto-dismiss**: Validation errors clear after 5 seconds
- **Non-blocking**: Validation guides but never blocks rendering

### Error Handling UX
- **Styled error banners**: Red background with warning icon instead of plain text
- **API errors**: Shown inline near action buttons, cleared on next attempt
- **No success-on-failure**: Errors rethrown to prevent false success messages
- **No alert popups**: All feedback is inline

### Success Feedback
- **Subtle inline banner**: Green "Complete — done" message after successful transition
- **Auto-dismiss**: Clears after 3 seconds
- **Only on actual success**: Error path prevents success message from showing

### Fallback Behavior
- **Workflow tasks**: TransitionActions with validation + confirmation + success feedback
- **Non-workflow/local tasks**: Generic dropdown with same validation + confirmation
- **Both paths**: Same validation rules, same confirmation dialogs, same error UX
- **State cleanup**: All validation/confirm state resets on task switch

### Key Files
| File | Purpose |
|------|---------|
| `components/ui/ConfirmDialog.tsx` | Reusable confirmation modal |
| `lib/transitionRules.ts` | Configurable validation rules + confirmation triggers |
| `components/tasks/workflow/TransitionActions.tsx` | Enhanced with validation + confirm + success |
| `components/tasks/TaskDetailDrawer.tsx` | Both paths get validation + confirmation |

## Workflow Transition UX (LS-FLOW-010) — COMPLETED

### Transition Label Strategy
A configurable mapping system (`lib/transitionLabels.ts`) converts raw status-to-status transitions into human-readable action labels:

| Transition | Default Label |
|------------|--------------|
| Open → InProgress | Start Work |
| InProgress → Done | Complete |
| InProgress → Blocked | Block |
| Blocked → InProgress | Resume |
| Any → Cancelled | Cancel |
| Done/Cancelled → Open | Reopen |

When a workflow is assigned, the system fetches the full workflow definition and maps transition names from the `WorkflowTransition.name` field. If workflow transition names are unavailable, the fallback label map is used.

### Transition Actions Component
`TransitionActions` renders allowed transitions as styled action buttons instead of a generic dropdown:
- Each button has a color variant based on the target status (primary/success/warning/danger/neutral)
- Buttons include icons and show a spinner while processing
- Only rendered when `allowedNextStatuses` is present (workflow-governed tasks)
- Error messages displayed inline above buttons

### Fallback Behavior
- **Workflow tasks** (have `allowedNextStatuses`): Render `TransitionActions` button bar with labeled actions
- **Non-workflow tasks** (no `allowedNextStatuses`): Render the original generic status dropdown
- **Local mode tasks**: Render the original generic status dropdown (no workflow data available locally)
- UI never goes blank — one of the two UX modes is always rendered

### Stage Progress Indicator
`StageIndicator` renders a horizontal progress track when a task has a workflow:
- Numbered circles for each stage, connected by lines
- Past stages: green checkmarks
- Current stage: blue ring highlight
- Future stages: gray
- Stage names shown below each circle
- Fetches workflow definition on mount to get ordered stage list

### Activity Message Enhancement
Activity timeline messages use friendly transition names instead of raw status labels:
- "Started work" instead of "Status changed from Open to In Progress"
- "Completed task" instead of "Status changed from In Progress to Done"
- Workflow transition names used when available (e.g., custom "Submit for Review")
- Fallback to friendly labels when workflow names unavailable

### Board Card Enhancement
Quick-move menu on board cards uses transition labels:
- "Start Work" instead of "Move to In Progress"
- "Complete" instead of "Move to Done"
- Uses fallback label map (no workflow transition names in board context)

### Key Files
| File | Purpose |
|------|---------|
| `lib/transitionLabels.ts` | Configurable transition label/activity message mapping |
| `components/tasks/workflow/TransitionActions.tsx` | Action button bar for workflow-governed transitions |
| `components/tasks/workflow/StageIndicator.tsx` | Visual stage progress indicator |
| `components/tasks/TaskDetailDrawer.tsx` | Integrated TransitionActions + StageIndicator + fallback dropdown |
| `components/tasks/board/TaskBoardCard.tsx` | Quick-move uses transition labels |
| `app/tasks/page.tsx` | Board move handler uses friendly activity messages |

## Activity System (LS-FLOW-007) — COMPLETED

### Event Model
The `ActivityEvent` type (`types/activity.ts`) represents a generic activity entry:
- `id` — unique event identifier (generated client-side)
- `taskId` — the task this event belongs to
- `type` — one of: `CREATED`, `STATUS_CHANGED`, `ASSIGNED`, `UPDATED`
- `message` — human-readable description (e.g., "Status changed from Open to In Progress")
- `metadata` — optional key-value pairs for structured data (e.g., `{ from: "Open", to: "InProgress" }`)
- `createdAt` — ISO timestamp
- `createdBy` — optional user identifier

The model is intentionally generic — not LegalSynq-specific — and designed to be replaceable by a backend Audit service in the future.

### Local Event Store
Events are stored in a module-level singleton (`lib/activityStore.ts`) using a `Map<string, ActivityEvent[]>` keyed by taskId. The store provides:
- `addActivityEvent()` — creates and stores an event, notifies subscribers
- `getActivityEvents()` — retrieves events for a given taskId
- `subscribe()` — registers a listener for real-time updates (returns unsubscribe function)

This is an in-memory store that works regardless of backend availability. Events are generated by the page (on create, board moves) and the drawer (on edit, status change, assignment change).

### Future Audit Integration
When a backend audit endpoint is available (`GET /api/v1/tasks/{id}/activity`), the `ActivityTimeline` component can be extended to:
1. Fetch API events on mount
2. Merge with local events by `createdAt` timestamp
3. Deduplicate by event type + timestamp proximity
4. Fall back silently to local-only events if the API call fails

The store's modular design means swapping to API-backed events requires changes only in the timeline component, not in event emitters.

### Timeline UI
The timeline is rendered inside the `TaskDetailDrawer` via two components:
- `ActivityTimeline` — subscribes to the store, renders event list with empty state
- `ActivityItem` — individual event row with type-specific colored icon, message, timestamp, and optional author

Events display in reverse chronological order (newest first) in a scrollable area (max 256px). Each event type has a distinct icon and color: green (created), blue (status changed), purple (assigned), amber (updated).

### Event Generation Points
| Action | Location | Event Type | Example Message |
|--------|----------|------------|-----------------|
| Create task | `tasks/page.tsx` | CREATED | "Task created: Review contract" |
| Board drag-drop | `tasks/page.tsx` | STATUS_CHANGED | "Status changed from Open to In Progress" |
| Status dropdown | `TaskDetailDrawer.tsx` | STATUS_CHANGED | "Status changed from Open to Done" |
| Edit save | `TaskDetailDrawer.tsx` | UPDATED | "Task updated: title, description" |
| Assignment save | `TaskDetailDrawer.tsx` | ASSIGNED | "Assigned to user: john, role: reviewer" |

## Context Model

The `ContextReference` is an owned entity type stored inline on `task_items`:
- `context_type` — identifies the external entity type (e.g., "case", "referral")
- `context_id` — the external entity's identifier
- `context_label` — optional human-readable label

**Current**: Single primary context per task, stored as owned entity columns.

**Future expansion**: Support for multiple/related context references per entity will be introduced in a later bundle. This may involve a separate `context_references` junction table, allowing tasks to be linked to multiple external entities. The current single-context pattern is preserved for simplicity at this stage.

## CORS Configuration

CORS is configured via `appsettings.json` under `Cors:AllowedOrigins`. The default allows `http://localhost:3000` and `http://localhost:3001` for local development. Production deployments should override this with the appropriate frontend domain(s) via environment-specific configuration.

## Integration with LegalSynq (Future)

When Flow integrates with LegalSynq v2, the integration will follow these principles:

1. **API-First**: LegalSynq calls Flow's REST APIs to create/manage workflows and tasks.
2. **Context References**: LegalSynq passes its entity identifiers (case IDs, lien IDs, etc.) as generic `ContextReference` values — Flow stores but does not interpret them.
3. **Adapter Pattern**: LegalSynq implements Flow's adapter interfaces (audit, notification) to receive callbacks.
4. **No Shared Database**: LegalSynq and Flow maintain separate databases with no cross-database queries or foreign keys.
5. **Event-Driven** (future): Flow publishes domain events that LegalSynq can subscribe to for real-time updates.

## Workflow Designer (LS-FLOW-012) — COMPLETED

### Overview
The Workflow Designer provides a full CRUD UI for creating and configuring custom workflows. Users can define workflows, add stages, and create transitions — all through form-based list interfaces (no drag-and-drop, no diagrams).

### Frontend Routes
- `/workflows` — List all workflows with name, status, stage/transition counts, and create/edit/delete actions
- `/workflows/[id]` — Edit screen with three sections: workflow details, stages, and transitions

### Components
- `WorkflowList` — Table showing all workflows with action links
- `CreateWorkflowDialog` — Modal form for creating a new workflow (name + description)
- `StageEditor` — Inline list editor for managing stages (add/edit/delete/reorder with up/down buttons)
- `TransitionEditor` — Inline list editor for managing transitions between stages

### Backend CRUD API
Full CRUD endpoints for workflows, stages, and transitions (see Workflow APIs table above). Validation rules:
- Exactly one initial stage per workflow
- Unique stage keys within a workflow
- Transitions must reference valid stages within the same workflow
- Cannot delete a stage used in transitions
- Cannot delete a workflow assigned to tasks
- Duplicate transitions between same stages are rejected

### Local Mode / Backend Unavailable
When the backend is unreachable, the `/workflows` page renders with a prominent amber banner: "Workflow management requires backend connection" — create and edit actions are disabled. No local simulation of workflow CRUD.

### Integration with Task System
Created workflows automatically appear in the Create Task Drawer and Task Detail Drawer workflow selectors — no additional wiring required beyond the existing `listWorkflows()` API.

### Navigation
- Home page: "Workflow Designer" button added alongside "Open Task Queue"
- Tasks page: "Workflows" navigation link in header
- Workflows page: "Tasks" navigation link in header
- Workflow editor: Breadcrumb navigation (Flow / Workflows / [name])

### Future Extensibility
- Workflow versioning (draft → active → archived lifecycle)
- Role-based transition permissions
- Conditional transition rules
- Visual workflow diagram view
- Workflow templates/cloning

## Notifications Engine (LS-FLOW-015-A1) — COMPLETED

### Notification Entity
The `Notification` entity stores persisted internal notifications for key workflow, task, and automation events.

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | Auto-generated (BaseEntity) |
| TaskId | Guid? | FK → TaskItems (SetNull on delete) |
| WorkflowDefinitionId | Guid? | FK → FlowDefinitions (SetNull on delete) |
| Type | string(64) | Strongly typed constant |
| Title | string(512) | Human-readable notification title |
| Message | string(2048) | Detailed notification message |
| TargetUserId | string(256)? | Target user identifier |
| TargetRoleKey | string(128)? | Target role key |
| TargetOrgId | string(256)? | Target org identifier |
| Status | string(16) | Unread / Read |
| SourceType | string(64) | Strongly typed constant |
| CreatedAt | DateTime | UTC timestamp |
| ReadAt | DateTime? | Set when marked read |

Database table: `notifications` with indexes on Status, TargetUserId, TargetRoleKey, TargetOrgId, CreatedAt, TaskId.

### Notification Types and Constants

**NotificationType**: `TASK_ASSIGNED`, `TASK_REASSIGNED`, `TASK_TRANSITIONED`, `AUTOMATION_SUCCEEDED`, `AUTOMATION_FAILED`, `WORKFLOW_ASSIGNED`

**NotificationStatus**: `Unread`, `Read`

**NotificationSourceType**: `WorkflowTransition`, `AutomationHook`, `Assignment`, `System`

### Notification APIs

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/notifications` | List with filters + pagination |
| GET | `/api/v1/notifications/{id}` | Get by ID |
| PATCH | `/api/v1/notifications/{id}/read` | Mark as read |
| PATCH | `/api/v1/notifications/{id}/unread` | Mark as unread |
| PATCH | `/api/v1/notifications/read-all` | Bulk mark read |
| GET | `/api/v1/notifications/summary` | Unread count |

List filters: `status`, `targetUserId`, `targetRoleKey`, `targetOrgId`, `taskId`, `page`, `pageSize`.

### Event Sources

Notifications are created automatically from three event sources:

1. **Task Assignment** (`TaskService.AssignAsync`): Fires `TASK_ASSIGNED` on first assignment, `TASK_REASSIGNED` when changing existing assignment. Source: `Assignment`.
2. **Workflow Transition** (`TaskService.UpdateStatusAsync`): Fires `TASK_TRANSITIONED` after a successful workflow-governed status change. Source: `WorkflowTransition`.
3. **Automation Hook Result** (`TaskService.UpdateStatusAsync`): Fires `AUTOMATION_SUCCEEDED` or `AUTOMATION_FAILED` per hook execution result. Source: `AutomationHook`.

### Target Resolution

Target fields are populated directly from the task's assignment fields at the time of the event:
- `TargetUserId` = task's `AssignedToUserId`
- `TargetRoleKey` = task's `AssignedToRoleKey`
- `TargetOrgId` = task's `AssignedToOrgId`

No recipient resolution engine, no fan-out, no preference lookup. Simple and predictable.

### Failure Isolation

`NotificationService.CreateNotificationAsync` wraps all persistence in try/catch:
- On failure: logs `LogWarning` with notification type and task ID
- Never rethrows — the primary business action (transition, assignment) always succeeds
- Notification creation is best-effort, not transactional with the business operation

### Future Extensibility

The notification model is designed for future extension to real channels:
- **External dispatch**: Add a `NotificationDispatcher` that reads persisted notifications and sends via email/SMS/webhook
- **Provider integration**: Add `NotificationChannel` entity linking notification types to delivery channels
- **Preference center**: Add user preference entity to control which types/channels a user receives
- **Digest scheduling**: Add background job that batches recent notifications into digest emails
- **Realtime streaming**: Add SignalR hub that pushes new notifications to connected clients

None of these require changes to the existing notification entity or event source integration.

### Key Files

| File | Purpose |
|------|---------|
| `Flow.Domain/Entities/Notification.cs` | Entity + type/status/source constants |
| `Flow.Application/DTOs/NotificationDtos.cs` | Response/query DTOs |
| `Flow.Application/Services/NotificationService.cs` | Service interface + implementation |
| `Flow.Api/Controllers/V1/NotificationsController.cs` | REST API controller |
| `Flow.Application/Services/TaskService.cs` | Event source integration |

## Merge Compatibility Notes

### Frontend
The frontend currently uses npm as its package manager (standard Next.js scaffold). When merging into the LegalSynq v2 monorepo:
- Convert `flow/frontend/package.json` to use the monorepo's package manager (pnpm)
- Add it as a workspace package with `@workspace/flow-frontend` or equivalent naming
- Align shared dependencies (React, TypeScript) with the monorepo catalog
- Component structure (`components/tasks/`, `components/ui/`, `lib/api/`, `types/`) is designed for direct lift into monorepo
- API client uses environment-variable-based URL — no hardcoded origins
- The frontend code itself (components, pages, styles) requires no changes

### Backend
The .NET backend is fully standalone and merge-compatible:
- Solution file and project structure are self-contained under `flow/backend/`
- No shared references to LegalSynq projects
- Database is isolated (`flow_db`)
- Integration via REST APIs only

## Notifications UX (LS-FLOW-018 Series) — COMPLETED

Three incremental bundles built atop the Notifications Engine (LS-FLOW-015-A1):

### 018-A — Deep Linking & Navigation
- `?taskId=<id>` query param on `/tasks` opens the Task Detail Drawer directly.
- Notification rows whose `taskId` is set are clickable and route to that task.
- Tenant-aware unread badge (`useUnreadCount`) caches per `cachedTenantId` and is invalidated on tenant switch and on `/notifications` mount.

### 018-B — Task-Level Panel & Advanced Filtering
- Backend: `NotificationListQuery` accepts optional `Type` and `SourceType` filters in addition to the existing `TaskId` and `Status`. No DB migration; both columns already exist on the entity.
- Frontend: `/notifications` gained two `<select>` dropdowns (type, source) plus a Reset shortcut.
- New `TaskNotificationsPanel` is rendered inside the Task Detail Drawer (below the activity timeline). It calls `listNotifications({ taskId })` and uses the compact `NotificationItem` variant with `disableNavigation` so rows don't re-route.
- Stale-response guard (`fetchIdRef` + `activeTaskIdRef`) prevents leakage when the user switches tasks before a fetch resolves.
- Panel exposes `totalCount`; if more than the rendered window exists, shows "Showing latest N of M — View all" linking to `/notifications`.

### 018-C — Bulk Actions & UX Polish

**Bulk selection (full variant only)**
- Per-row checkbox via `NotificationItem` props `isSelectable` / `isSelected` / `onSelect`.
- "Select all on this page" header checkbox with native indeterminate state when the selection is partial.
- Selection is **local to the current page** and is reset when the filter, page, or returned result-set identity changes (per-page identity = sorted ID list).
- Checkbox click is wrapped in a `stopPropagation` cell so it never triggers the row's deep-link navigation.

**Bulk action bar (`NotificationBulkActionBar`)**
- Renders only when `selectedCount > 0`.
- Buttons: **Mark as Read**, **Mark as Unread**, **Clear**.
- Each button is independently enabled based on whether the selection actually contains items in the relevant state (e.g. Mark as Read is disabled if all selected items are already Read).

**Bulk execution strategy: best-effort partial success**
- Loops the existing per-id endpoints (`PATCH /notifications/{id}/read|unread`) via `Promise.allSettled`.
- No new backend endpoint introduced; the existing failure isolation in `NotificationService.CreateNotificationAsync` is unaffected.
- Successful items are updated optimistically in local state and removed from the selection set; failed items remain selected so the user can retry.
- If any operation fails, surfaces a summary message ("3 updated, 1 failed to mark as read"); on full failure, surfaces a single-line error.
- After every successful run, calls `refreshUnreadCount()` to keep the global header badge in sync.

**Notification item polish**
- Leading icon avatar keyed by `sourceType` (Assignment → user; WorkflowTransition → arrows; AutomationHook → gear/check/alert depending on type; System → bell). All icons are inline SVG — no new dependency.
- Stronger read/unread differentiation: bold title + tinted background + accent bar for unread.
- Selected rows get a distinct blue tint that wins over the unread tint, so selection state is always visible.
- Compact variant (used by `TaskNotificationsPanel`) defaults `showIcon = false` and ignores selection props, preserving the existing minimalist look in the drawer.

**Degraded mode**
- When the backend is unreachable, `NotificationList` short-circuits to the existing amber "Notifications require backend connection" card. Selection controls and the bulk bar never render in this state, so there is no risk of clicking actions against an unavailable API.
- The task-level compact panel (`TaskNotificationsPanel`) has its own independent fetch + error states and is unaffected by selection logic.

**Backward compatibility**
- All new `NotificationItem` props (`isSelectable`, `isSelected`, `onSelect`, `showIcon`) are optional with safe defaults; existing callers continue to compile and behave identically.
- No backend changes in 018-C — the bulk bar reuses `markRead` / `markUnread`.

### Key Frontend Files

| File | Purpose |
|------|---------|
| `src/components/notifications/NotificationList.tsx` | Page-level list, filters, selection, bulk handlers |
| `src/components/notifications/NotificationItem.tsx` | Row renderer for both default + compact variants |
| `src/components/notifications/NotificationBulkActionBar.tsx` | Selection-aware action bar |
| `src/components/notifications/TaskNotificationsPanel.tsx` | Task-scoped panel inside Task Detail Drawer |
| `src/components/notifications/notificationIcons.tsx` | Source/type → icon + color mapping |
| `src/lib/api/notifications.ts` | Typed REST client (`taskId`, `type`, `sourceType` params supported) |
| `src/lib/useUnreadCount.ts` | Tenant-aware unread badge singleton |

## Automation Hooks — Multiple Actions (LS-FLOW-019-A)

As of LS-FLOW-019-A, a workflow automation hook owns an **ordered list of actions** instead of a single action. When the hook fires, its actions execute **sequentially** in `Order` ascending, each within its own `try`/`catch` (best-effort). A failure in one action does not skip subsequent actions; the overall hook status is `Succeeded` only if every action succeeded.

### Data model
- `WorkflowAutomationHook` 1 — ∞ `AutomationAction` (cascade delete).
- `AutomationAction`: `{ Id, HookId, ActionType, ConfigJson?, Order, TenantId }`.
- Unique index on `(HookId, Order)` enforces no duplicate orders within a hook.
- `AutomationExecutionLog.ActionId` is a nullable FK-like column populated with the specific action that produced the log row (or `null` for the legacy fallback path).

### Backward compatibility
- Legacy columns `WorkflowAutomationHook.ActionType`/`ConfigJson` are retained and kept in sync with `Actions[0]`, so pre-019-A API clients that read or write the old shape continue to work unchanged.
- EF migration `AddAutomationActions` backfills one `AutomationAction` row per existing hook so `GET` responses immediately carry a populated `actions[]` after upgrade.
- If a hook ever has zero actions (e.g., direct DB edit), the executor still runs it via a runtime fallback built from the legacy columns and emits one log row with `ActionId = null`.

### API surface
- `POST`/`PUT /api/v1/workflows/{id}/automation-hooks[/{hookId}]` accepts either the legacy `{ actionType, configJson }` shape or the new `{ actions: [{ actionType, configJson?, order? }, ...] }` shape. If `actions` is present it wins; otherwise the legacy fields are normalized to a one-item list. Omitted `order` values are auto-assigned sequentially starting at `0`, preserving the input order.
- All hook responses always include `actions[]` (ordered) and still include top-level `actionType`/`configJson` mirroring `actions[0]`.
- `GET /api/v1/tasks/{id}/automation-logs` rows now include a nullable `actionId`.

### Validation
- At least one action is required.
- Action type must be whitelisted (`ADD_ACTIVITY_EVENT`, `SET_DUE_DATE_OFFSET_DAYS`, `ASSIGN_ROLE`, `ASSIGN_USER`, `ASSIGN_ORG`).
- Config JSON is schema-checked per action type (same rules as the legacy single-action config).
- `Order` values must be non-negative and unique within the hook.

### Key files
| File | Purpose |
|------|---------|
| `src/Flow.Domain/Entities/AutomationAction.cs` | Action entity |
| `src/Flow.Application/Services/AutomationExecutor.cs` | Sequential per-action execution, per-action logging, legacy fallback |
| `src/Flow.Application/Services/WorkflowService.cs` | Multi-action CRUD + normalization/validation |
| `src/Flow.Infrastructure/Persistence/Migrations/20260416194337_AddAutomationActions.cs` | Schema + data backfill |

## Automation Actions — Conditional Execution (LS-FLOW-019-B)

As of LS-FLOW-019-B, each `AutomationAction` may carry an optional `ConditionJson` describing a single `{ field, operator, value }` predicate evaluated against the current task at execution time. Non-matching conditions cause the action to be **skipped**, not failed.

### Condition shape
```json
{ "field": "<whitelist>", "operator": "<equals|not_equals|in|not_in>", "value": <scalar | array> }
```
- **Allowed fields:** `status`, `assignedToUserId`, `assignedToRoleKey`, `assignedToOrgId`, `workflowStageId`, `flowDefinitionId`.
- **Allowed operators:** `equals`, `not_equals`, `in`, `not_in`.
- `equals`/`not_equals` require a scalar `value`; `in`/`not_in` require a non-empty array `value`.
- Comparisons are string-based (case-sensitive `Ordinal`); GUID/enum task fields are stringified before comparison.

### Validation (up-front, at create/update)
- `WorkflowService` calls `AutomationConditionEvaluator.Parse` for every action with a condition. Any malformed JSON, unknown field/operator, or shape mismatch returns HTTP 400 with a descriptive message — the hook is not persisted.

### Runtime evaluation
- `AutomationExecutor` evaluates the condition (when present) before invoking the handler.
  - **Match** → action runs; logged `Succeeded`/`Failed` per handler outcome.
  - **No match** → action skipped; log entry `Status=Skipped`, message `"Condition not met: <human description>"`. Handler is not invoked.
  - **Null task field** → treated as not matched (`equals`, `not_equals`, `in`, `not_in` all return false for null). The action is skipped.
  - **Malformed persisted condition** (e.g., direct DB tampering) → action logged `Failed` with parse error; execution continues with the next action.

### Hook aggregation
- Any `Failed` action → hook summary `Failed`.
- Otherwise any `Succeeded` action → hook summary `Succeeded`.
- Otherwise (every action `Skipped`) → hook summary `Succeeded` with note "All actions skipped due to unmet conditions".

### API surface
- `AutomationActionDto.ConditionJson` (optional string) added to all hook create/update/read payloads. Omitting it preserves prior behavior exactly.

### Backward compatibility
- `ConditionJson` is nullable in entity, DB column (`varchar(2048)`), and DTO.
- Pre-019-B hooks have `ConditionJson = NULL`; the executor short-circuits the gate and runs the action unchanged.

### Key files
| File | Purpose |
|------|---------|
| `src/Flow.Application/Services/AutomationConditionEvaluator.cs` | Parse / Evaluate / Describe + whitelists |
| `src/Flow.Application/Services/AutomationExecutor.cs` | Condition gate + `Skipped` logging + aggregation |
| `src/Flow.Application/Services/WorkflowService.cs` | Up-front condition validation + persistence |
| `src/Flow.Domain/Entities/AutomationAction.cs` | `ConditionJson` property |
| `src/Flow.Infrastructure/Persistence/Migrations/20260416205848_AddActionConditionJson.cs` | Schema |

## Automation Actions — Retry & Failure Handling (LS-FLOW-019-C)

As of LS-FLOW-019-C, each `AutomationAction` may declare a per-action retry/failure policy:
- `RetryCount` (int, default `0`): number of **additional** attempts after the first failure. Total attempts = `1 + RetryCount`.
- `RetryDelaySeconds` (nullable int, default `null`): synchronous wait between attempts (`Task.Delay`). Applied **only between** failed attempts — never before the first, never after the last.
- `StopOnFailure` (bool, default `false`): when an action ultimately fails (after exhausting its retries), halt processing of the remaining actions in the same hook.

`AutomationExecutionLog` carries `Attempts` (int) so the UI/operator can see exactly how many tries each action consumed.

### Validation (up-front, at create/update)
- `WorkflowService` rejects `RetryCount < 0` and `RetryDelaySeconds < 0` with HTTP 400. `StopOnFailure` is a plain bool (no constraint).

### Runtime semantics
- Per action, the executor enters a retry loop bounded by `1 + RetryCount`. The loop catches any exception from the handler, sleeps `RetryDelaySeconds` (if > 0) between attempts, and exits early on the first success.
- **Attempts logging** in `AutomationExecutionLog`:
  - **Succeeded**: `Attempts = <attempt index that succeeded>` (1-based).
  - **Failed**: `Attempts = 1 + RetryCount` (final attempt count).
  - **Skipped** (condition not met) and **malformed condition Failed**: `Attempts = 0`. Skipping is *not* an attempt; malformed conditions are policy errors that bypass execution entirely.
- **Stop-on-failure**:
  - Only takes effect *after* retries are exhausted (`StopOnFailure` is checked once per action, post-retry).
  - On trigger, the executor `break`s the action loop. Remaining actions produce **no log rows** (no fake-skipped entries).
  - The hook summary message appends a note: `"execution stopped after [N:TYPE] (stop-on-failure); X actions not executed"` (X is singularized correctly).
  - Skipped actions never trigger stop-on-failure.
  - Malformed conditions never trigger stop-on-failure (they are not subject to the retry/stop policy).

### Hook aggregation (unchanged from 019-B)
- Any `Failed` action → hook summary `Failed` (with optional stop-on-failure note when applicable).
- Otherwise any `Succeeded` action → hook summary `Succeeded`.
- Otherwise (every action `Skipped`) → hook summary `Succeeded` with note "(all actions skipped — no work to do)".

The hook-level summary message is propagated to the per-hook notification body (`AutomationSucceeded` / `AutomationFailed`).

### API surface
- `AutomationActionDto` adds `retryCount`, `retryDelaySeconds`, `stopOnFailure` (all optional on input; always returned on read).
- `AutomationExecutionLogResponse` adds `attempts` (int).
- All fields are optional in input payloads; omitting them preserves prior behavior exactly.

### Backward compatibility
- New columns are nullable / defaulted (`RetryCount=0`, `StopOnFailure=0`, `RetryDelaySeconds=NULL`, `Attempts=1`).
- Pre-019-C hooks: `RetryCount=0` ⇒ single attempt; `StopOnFailure=false` ⇒ executor processes the full action list as before. Legacy log rows backfill `Attempts=1` (matching the historical "one attempt, success or fail" semantics).

### Key files
| File | Purpose |
|------|---------|
| `src/Flow.Domain/Entities/AutomationAction.cs` | `RetryCount`, `RetryDelaySeconds`, `StopOnFailure` |
| `src/Flow.Domain/Entities/AutomationExecutionLog.cs` | `Attempts` |
| `src/Flow.Infrastructure/Persistence/FlowDbContext.cs` | Column mappings + defaults |
| `src/Flow.Application/Services/AutomationExecutor.cs` | Retry loop + stop-on-failure + Attempts logging + hook summary |
| `src/Flow.Application/Services/WorkflowService.cs` | Validate `>=0` + persist + map |
| `src/Flow.Application/DTOs/AutomationDtos.cs` | Request/response DTO extensions |
| `src/Flow.Infrastructure/Persistence/Migrations/20260416230823_AddActionRetryAndLogAttempts.cs` | Schema |

## Automation Configuration UI (LS-FLOW-019-D)

LS-FLOW-019-D exposes the multi-action automation engine (019-A/B/C) through a structured admin UI in the workflow designer. Users can create, edit, reorder, and delete actions, configure per-action conditions, and tune retry/failure policy entirely through form controls — no raw JSON is shown.

### Component Layout

| Component | Responsibility |
|---|---|
| `AutomationEditor.tsx` | Hook list, create/edit forms, hydration/serialization, degraded-mode handling |
| `AutomationActionEditor.tsx` | Single action card: type select, type-specific config inputs, reorder/remove buttons, retry/stop-on-failure controls |
| `AutomationConditionEditor.tsx` | Optional condition block: field/operator/value inputs and JSON (de)serialization |

### UI → Backend Field Mapping

Per action, the form state serializes to the following payload fields (see `AutomationActionDto`):

| UI Control | Payload Field |
|---|---|
| Action type dropdown | `actionType` |
| Type-specific structured inputs | `configJson` (built internally per type) |
| Up/Down reorder buttons | `order` (normalized to 0-based array index on save) |
| Condition enabled + field/operator/value | `conditionJson` (`null` when disabled or value empty) |
| Retry count number input | `retryCount` (default `0`) |
| Retry delay number input | `retryDelaySeconds` (`null` when blank) |
| Stop-on-failure checkbox | `stopOnFailure` (default `false`) |

### Type-specific config builders

| Action type | Field(s) → `configJson` |
|---|---|
| `ADD_ACTIVITY_EVENT` | `{ messageTemplate }` (or `null` if blank) |
| `SET_DUE_DATE_OFFSET_DAYS` | `{ days }` (positive integer) |
| `ASSIGN_ROLE` | `{ roleKey }` |
| `ASSIGN_USER` | `{ userId }` |
| `ASSIGN_ORG` | `{ orgId }` |

### Condition serialization

For `equals` / `not_equals` the value is sent as a string; for `in` / `not_in` the value is split on commas, trimmed, and sent as a string array — matching the contract from `AutomationConditionEvaluator`. When the condition checkbox is unchecked or the value is blank, `conditionJson` is sent as `null` and the executor runs the action unconditionally.

### Hydration & backward compatibility

On load, the editor reads `actions[]` from `AutomationHookResponse` (always populated by the backend, including legacy single-action hooks where `actions[0]` mirrors the legacy root fields). Each entry's `configJson` and `conditionJson` is parsed into structured form state; missing retry fields default to `0` / `null` / `false`. If a response is missing `actions[]` entirely, the UI falls back to a single synthetic action built from the legacy root `actionType` / `configJson` so older hooks remain editable.

### Validation

Client-side validation blocks save when:

- name or transition is missing
- the action list is empty
- a type-specific required field is blank or invalid (e.g. `days <= 0`)
- `retryCount` or `retryDelaySeconds` is negative
- a condition is enabled but the value is blank

Server-side errors from the existing API surface as a single error banner above the editor and do not discard user input.

### Degraded mode

If the initial `GET /workflows/{id}/automation-hooks` call fails, the editor renders an amber warning banner ("Automation configuration is unavailable …") with a Retry button. The rest of the workflow designer (stages, transitions, canvas) remains functional.

### Backend transactional fix (incidental)

`UpdateAutomationHookAsync` was updated to invoke its multi-step transaction through `IFlowDbContext.CreateExecutionStrategy().ExecuteAsync(...)`. The previous direct `BeginTransactionAsync` call was incompatible with the MySQL retrying execution strategy and produced a runtime 500 the moment the new UI tried to save edited actions. `IFlowDbContext` gained a `CreateExecutionStrategy()` member, implemented in `FlowDbContext` by delegating to `Database.CreateExecutionStrategy()`.

### Files

| File | Purpose |
|---|---|
| `src/components/workflows/AutomationEditor.tsx` | Rewritten multi-action editor + hook list + degraded mode |
| `src/components/workflows/AutomationActionEditor.tsx` | New per-action card |
| `src/components/workflows/AutomationConditionEditor.tsx` | New condition editor with serialization helpers |
| `src/types/workflow.ts` | Added `AutomationAction`, condition enums/labels, updated `AutomationHook`/`Create*`/`Update*` DTOs |
| `src/Flow.Application/Interfaces/IFlowDbContext.cs` | Added `CreateExecutionStrategy()` |
| `src/Flow.Infrastructure/Persistence/FlowDbContext.cs` | Implemented `CreateExecutionStrategy()` |
| `src/Flow.Application/Services/WorkflowService.cs` | Wrap `UpdateAutomationHookAsync` transaction in execution strategy |

## Frontend Product Context Layer (LS-FLOW-020-B)

### Model

Source of truth: `flow/frontend/src/lib/productKeys.ts`.

- Allowed product keys: `FLOW_GENERIC` (default), `SYNQ_LIENS`, `SYNQ_FUND`,
  `CARE_CONNECT`. Keys mirror backend exactly.
- Helpers: `PRODUCT_KEY_LABELS`, `PRODUCT_KEY_BADGE_CLASSES`,
  `DEFAULT_PRODUCT_KEY`, `isValidProductKey`, `normalizeProductKey`,
  `productLabel`.
- Reusable UI: `ProductKeyBadge` (label badge) and `ProductFilter` (single
  select dropdown with optional "All products" option).

### Workflow context

- `Workflow*` DTOs and summaries carry an optional `productKey`.
- `/workflows`: ProductFilter synced with URL `?productKey=`. Default is "All".
- `/workflows/[id]`: header badge plus editable product field. Backend rejection
  of product changes (when tasks/hooks reference the workflow) is surfaced
  inline via the existing save-error path; other form state is preserved.
- `CreateWorkflowDialog`: requires a product (default = active filter or
  `FLOW_GENERIC`).
- `WorkflowList` adds a Product column with `ProductKeyBadge`.
- `WorkflowSelector` is product-scoped: it requires a `productKey` prop, gates
  workflow loading until set, and only loads workflows for the chosen product.
  Signature: `onChange(workflowId, workflow?)`.

### Task context

- Task DTOs carry an optional `productKey`.
- `/tasks`: ProductFilter synced with URL `?productKey=`. Default is "All".
  Filter is forwarded both to the API list call and to the local-mode in-memory
  filter, and used as the default for the Create Task drawer.
- `CreateTaskDrawer`: explicit product selector. Selecting a workflow
  auto-aligns the product to that workflow's product. Changing the product
  clears any incompatible workflow selection with a visible notice.
- `TaskDetailDrawer`: shows the product badge in the header and passes the
  task's `productKey` to the workflow selector when re-assigning, preventing
  cross-product reassignment.
- `TaskTable` and board cards (via badge) display the product.
- Local-mode tasks default product to (form value > active filter >
  `FLOW_GENERIC`).

### FLOW_GENERIC transition

- FLOW_GENERIC records remain visible by default and selectable in every
  filter. They render with a neutral grey badge.
- No automatic migration. New records created without an explicit product use
  the backend default (FLOW_GENERIC).

### Degraded mode

- The product UI continues to work when the backend is unreachable. Filtering
  applies to local in-memory tasks; the workflow selector keeps its existing
  local-mode notice.

### Key files

| File | Purpose |
| --- | --- |
| `src/lib/productKeys.ts` | Product key constants, labels, helpers |
| `src/components/ui/ProductKeyBadge.tsx` | Badge for any product key |
| `src/components/ui/ProductFilter.tsx` | Single-select filter dropdown |
| `src/components/tasks/workflow/WorkflowSelector.tsx` | Product-scoped workflow chooser |
| `src/components/tasks/CreateTaskDrawer.tsx` | Product/workflow sync at create |
| `src/components/tasks/TaskDetailDrawer.tsx` | Header product badge, scoped re-assign |
| `src/components/tasks/TaskTable.tsx` | Product column |
| `src/components/workflows/CreateWorkflowDialog.tsx` | Product on workflow creation |
| `src/components/workflows/WorkflowList.tsx` | Product column |
| `src/app/workflows/page.tsx` | URL-synced product filter |
| `src/app/workflows/[id]/page.tsx` | Editable product + header badge |
| `src/app/tasks/page.tsx` | URL-synced product filter, local-mode default |

---

## Phase 2 Update (2026-04-17)

### Authentication & Authorization
- `Microsoft.AspNetCore.Authentication.JwtBearer` configured against the shared
  LegalSynq Identity v2 signing key.
- `BuildingBlocks.Authorization.Policies` registered: `AuthenticatedUser`,
  `AdminOnly`, `PlatformOrTenantAdmin`.
- `BuildingBlocks.Context.ICurrentRequestContext` exposes the current user /
  tenant / org claims to all application code.

### Tenant Model
- Tenant is **always** derived from the JWT `tenant_id` claim by
  `ClaimsTenantProvider`. The legacy `X-Tenant-Id` header is no longer trusted.
- `TenantValidationMiddleware` returns 403 when a request body or query string
  carries a `tenantId` that disagrees with the JWT claim.

### Platform Adapters
- `IAuditAdapter` / `LoggingAuditAdapter` / `HttpAuditAdapter`
- `INotificationAdapter` / `LoggingNotificationAdapter` / `HttpNotificationAdapter`
- `IFlowEventDispatcher` / `FlowEventDispatcher` — internal in-process fan-out
  to the audit + notification seams.

### Gateway
- Cluster `flow-cluster` → `http://localhost:5012`
- Routes: `/flow/health`, `/flow/info`, `/flow/api/v1/status` (anonymous),
  `/flow/{**catch-all}` (protected).

### Product consumption (Phase 3)
- `IFlowUserContext` (Flow.Domain) — small abstraction giving the application
  layer access to the current tenant + user id without referencing
  BuildingBlocks. Implemented in `Flow.Api/Services/FlowUserContext.cs`.
- `ProductWorkflowMapping` — explicit link between a product-side entity and a
  Flow workflow instance (table `flow_product_workflow_mappings`, tenant query
  filter applied).
- `IProductWorkflowService` — product-facing service that creates a Flow task
  (workflow-instance grain) plus a mapping row, validating that the workflow's
  `ProductKey` matches the route's product.
- `ProductWorkflowsController` — `/api/v1/product-workflows/{product}` with
  per-product `[Authorize(Policy=...)]` (`CanSellLien` / `CanReferCareConnect`
  / `CanReferFund`).

### Operational hardening (Phase 4)
- `WorkflowInstance` (Flow.Domain) — first-class workflow-instance grain
  (table `flow_workflow_instances`, tenant query filter applied). Replaces
  the Phase-3 use of `TaskItem` as the instance grain.
- `ProductWorkflowMapping.WorkflowInstanceId` — canonical FK to the new
  instance row; `WorkflowInstanceTaskId` is kept for back-compat.
- `BuildingBlocks/FlowClient/IFlowClient` — typed `HttpClient` consumed by
  product services. Retries, timeouts, structured logging, and bearer
  pass-through via `IHttpContextAccessor`. Registered with
  `services.AddFlowClient(configuration)` (binds `Flow:BaseUrl`).
- Each product (`Liens.Api`, `CareConnect.Api`, `Fund.Api`) exposes a
  minimal `POST/GET .../workflows` endpoint pair that calls Flow on behalf
  of the user and returns **HTTP 503** with `{ error, code: "flow_unavailable" }`
  if Flow is unreachable, instead of bubbling exceptions.
- Capability policies in `Flow.Api/Program.cs` now reference
  `BuildingBlocks.Authorization.PermissionCodes` constants
  (`LienSell` / `ReferralCreate` / `ApplicationRefer`) instead of inline
  strings, keeping permission codes single-sourced.

## Phase 5 (LS-FLOW-MERGE-P5) — execution authority

- `WorkflowInstance` carries execution state directly (`Status`, `CurrentStageId`,
  `CurrentStepKey`, `StartedAt`, `CompletedAt`, `AssignedToUserId`,
  `LastErrorMessage`); `CurrentStepKey` mirrors `WorkflowStage.Key`.
- `Flow.Application/Engines/WorkflowEngine` is the single mutator of execution
  state. `StartAsync` resolves the initial stage by `Order`; `AdvanceAsync`
  validates an `expectedCurrentStepKey` (optimistic concurrency) and then
  follows the existing `WorkflowTransition` graph (or honours an explicit
  `toStepKey`). `Complete/Cancel/Fail` are terminal. Invalid transitions
  surface as `InvalidWorkflowTransitionException` (with stable `Code`),
  mapped by the controller to **HTTP 409**.
- `WorkflowInstancesController` (`/api/v1/workflow-instances/...`) is the public
  execution surface; product services call it via `IFlowClient`.
- Authentication: `Flow.Api` registers two JwtBearer schemes (user `Bearer` and
  `ServiceToken`) behind a `MultiAuth` PolicyScheme that dispatches by `aud`
  (`flow-service` ⇒ `ServiceToken`). Service tokens are minted by
  `BuildingBlocks.Authentication.ServiceTokens.IServiceTokenIssuer` (HS256,
  5-min TTL, claims `sub=service:<name>`, `tid`, optional `actor=user:<id>`).
  Shared secret via env `FLOW_SERVICE_TOKEN_SECRET`.
- Product apps register `AddFlowClient(configuration, serviceName: "...")` so
  the client prefers an M2M token (with the caller's user as `actor`) and
  falls back to bearer pass-through when the secret is unconfigured. Each
  product calls `group.MapFlowExecutionPassthrough()` to expose
  `GET/POST .../workflows/{wfId}{,/advance,/complete}`.
- The transactional outbox for transition events is intentionally deferred
  (Phase 6); today `WorkflowEvent` rows are written in the same DbContext
  save as the instance update, atomic in MySQL but not yet relayed.

## Phase A1 — Atomic Ownership Layer

- The Phase-5 product-side ownership pre-check has moved fully inside Flow. `ProductWorkflowExecutionController` (Flow.Api/Controllers/V1) accepts `/api/v1/product-workflows/{product}/{sourceEntityType}/{sourceEntityId}/{workflowInstanceId:guid}` and resolves the instance through ONE EF join over `ProductWorkflowMappings` and `WorkflowInstances`. The tenant query filter applies to both tables, so cross-tenant access is impossible by construction; mismatch on product / parent / instance produces the uniform `404 workflow_instance_not_owned`.
- Capability policies (`CanSellLien` / `CanReferCareConnect` / `CanReferFund`) are checked only for end-user callers. Service tokens skip per-product capability (the originating product service has already enforced it) but cannot bypass tenant scoping or parent ownership.
- `BuildingBlocks/FlowClient` now ships `FlowErrorCodes` (machine-readable codes used by Flow, the passthrough layer, and tests) and `ICallerContextAccessor` (per-request projection of the principal into `{ Type, TenantId, Subject, Actor }`).
- `AddServiceTokenBearer` is hardened (signed-tokens required, semantic claims required) and gains `failFastIfMissingSecret`, which Flow.Api uses to crash-on-startup outside Development if the shared HS256 secret is missing or shorter than 32 chars.


## Phase A1.1 — Validation Track

- A1.1 adds no runtime behaviour; the only production-tree change is `public partial class Program { }` appended to `Flow.Api/Program.cs` so `WebApplicationFactory<Program>` can host the real entry point.
- The integration suite (`Flow.IntegrationTests`) replaces only the auth scheme (header-driven `TestAuth`) and the EF provider (SQLite in-memory with a shared keep-alive connection). `ClaimsTenantProvider`, `CallerContextAccessor`, `TenantValidationMiddleware`, the capability policies, and `WorkflowEngine` all run unmodified.
- The factory runs the host as `Development` so the service-token startup guard (already covered by `Flow.UnitTests.ServiceTokenStartupGuardTests`) does not require a real secret. Capability tests rely on the explicit permission / product-role claims they carry; the "no permissions at all" dev-fallback never engages because every authenticated test caller carries at least one permission claim.
