# LS-LIENS-CASE-005-02 — Task Filtering + Search + Sort

## Objective
Add a scalable filtering, search, and sort system to the Task Manager tab so both Kanban and List views consume the same query-driven dataset.

## Scope
- Search: across task name, description, assignee, and task ID
- Assignment filter: All / Assigned to Me / Assigned to Others / Unassigned
- Status filter: multi-select (Upcoming, In Progress, In Review, Completed)
- Priority filter: multi-select (High, Medium, Low)
- Sort: Recently Updated (default), Due Date, Priority, Task Name
- Filter chips with individual clear and clear-all
- Both Kanban and List share the same filtered dataset
- Filters preserved when switching between Kanban ↔ List views

## Architecture

### Unified Query Model
- `TaskQueryState` interface with `search`, `assignment`, `statuses[]`, `priorities[]`, `sort`
- `useTaskQuery()` custom hook encapsulates all filter/sort logic via `useMemo`
- Both Kanban and List consume `filteredTasks` from the same hook — no duplicated logic

### Current User Resolution
- Uses `useSession()` hook → `session.email` for "Assigned to Me" / "Assigned to Others"
- `TaskItem.assigneeEmail` optional field added for matching
- TEMP data does not have assigneeEmail set, so "Assigned to Me" will show 0 results against TEMP data (honest behavior — documented)
- When real API data is connected, `assigneeEmail` will be populated from the backend

## Files Changed
| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx` | Added `useSession` import, `useMemo` import, `TaskQueryState`, `useTaskQuery()` hook, `TaskFilterDropdown` component, filter bar UI, chip row, empty states |

## Implementation Details

### Filter Bar (compact, enterprise-style)
- Search input with clear button (left-aligned, max 280px)
- Assignment dropdown (native select)
- Status multi-select dropdown (custom checkbox dropdown with count badge)
- Priority multi-select dropdown (same pattern)
- Sort dropdown (native select)
- Placed in gray toolbar row between header and TEMP banner

### Filter Chips
- Active chips rendered below filter bar when any filter is active
- Each chip shows filter label + close button
- "Clear all" link at the end
- Chips include: search term, assignment mode, each selected status, each selected priority, non-default sort

### Kanban Behavior
- All 4 columns always visible regardless of filters
- Empty columns show "No matching tasks" when filters active (vs "No tasks" when no filters)
- Column counts reflect filtered counts

### List Behavior
- Same filtered dataset rendered in table
- Full empty state with icon + "Clear all filters" link when no matches
- Footer shows "X tasks (filtered from Y)" when filters active

### Task Count Badge
- Header badge shows `filteredCount/totalCount` when filters active
- Shows just `totalCount` when no filters

## Validation Results
- TypeScript: Clean build, no errors
- Search: Filters across name, description, assignee, ID
- Assignment: All/Me/Others/Unassigned working (Me/Others depend on assigneeEmail match)
- Status: Multi-select toggles correctly
- Priority: Multi-select toggles correctly
- Sort: All 4 options working
- Chips: Individual clear and clear-all working
- View switching: Filters preserved between Kanban ↔ List
- No regressions: Other tabs unaffected

## Code Review Adjustments
- **Assignment filter edge cases**: Fixed — "Assigned to Me" with no session email now returns empty set (not silent pass-through). "Assigned to Others" with no session email returns all assigned tasks.
- **Case-insensitive email comparison**: Both `assigneeEmail` and `currentUserEmail` are now normalized with `.trim().toLowerCase()` before comparison.
- **List empty state**: Now branches on `hasActiveFilters` — shows "No tasks yet" when no filters active vs "No tasks match the current filters" when filtered.

## Remaining Gaps
- "Assigned to Me" returns no results with TEMP data (no assigneeEmail on TEMP tasks) — by design, will work with real API data
- Backend task API not yet available — filtering runs entirely client-side on TEMP data
- No filter persistence across page reloads (state resets when navigating away)
