# LS-LIENS-FLOW-009-01 — Task Manager Layout Compression & Unified UX Refresh

**Status:** COMPLETE  
**Date:** 2026-04-21  
**Scope:** Global Task Manager page + Case-level Task Manager component

---

## Executive Summary

Redesign of the Task Manager UI to compress vertical chrome, increase task density, and unify layout logic between the global Task Manager page (`/lien/task-manager`) and the Case-scoped Task Manager (`CaseTaskManager` component). The `PageHeader` component is replaced with a compact inline header row. Large KPI cards are replaced with a horizontal stat-chip strip. Filter toolbar is reduced in height and padding. Board columns are tightened. Task cards are made more compact. Four new shared components are extracted and used by both views to eliminate duplicate layout logic.

---

## Codebase Assessment

### Files analyzed

| File | Lines | Role |
|---|---|---|
| `apps/web/src/app/(platform)/lien/task-manager/page.tsx` | 393 | Global Task Manager page |
| `apps/web/src/components/lien/case-task-manager.tsx` | 411 | Case-scoped Task Manager |
| `apps/web/src/components/lien/task-card.tsx` | ~120 | Reusable task card |
| `apps/web/src/components/lien/page-header.tsx` | ~40 | Shared page-level header |

### Issues identified

| Issue | Location | Impact |
|---|---|---|
| `PageHeader` wraps entire header in `bg-white border rounded-xl px-6 py-5` | `page.tsx` L132 | ~80px wasted vertical height |
| KPI cards use `text-2xl font-bold` values + `px-4 py-3 rounded-xl` per card | Both pages | ~110px wasted per row |
| Filter bar has its own `bg-white border rounded-xl px-4 py-3` wrapper | Both pages | ~60px wasted |
| Board column headers: `px-4 py-3` | Both pages | Extra column header height |
| Board gaps: `gap-4` | Both pages | Wider than necessary |
| Task card: `p-3 mb-2` spacing | `task-card.tsx` | Each card taller than needed |
| Duplicate layout logic | Both pages | ~300 lines of near-identical board + list + filter code |
| Utility functions duplicated | `page.tsx`, `case-task-manager.tsx`, `task-card.tsx` | `avatarColor`, `getInitials`, `formatDate`, `isOverdue`, `shortCaseId` all defined 2–3 times |
| Loading spinner: `p-10` centered block | Both pages | Consumes full board height during load |

---

## Files Changed

| File | Change type |
|---|---|
| `apps/web/src/components/lien/task-manager-header.tsx` | **NEW** — compact inline header row |
| `apps/web/src/components/lien/task-manager-stats-strip.tsx` | **NEW** — horizontal stat chip strip |
| `apps/web/src/components/lien/task-manager-toolbar.tsx` | **NEW** — single-line filter toolbar |
| `apps/web/src/components/lien/task-board.tsx` | **NEW** — compact kanban board container |
| `apps/web/src/app/(platform)/lien/task-manager/page.tsx` | **UPDATED** — uses shared components |
| `apps/web/src/components/lien/case-task-manager.tsx` | **UPDATED** — uses shared components |
| `apps/web/src/components/lien/task-card.tsx` | **UPDATED** — reduced padding/spacing |

---

## UI Changes (Before vs After)

### Header

| | Before | After |
|---|---|---|
| Container | `bg-white border rounded-xl px-6 py-5` (full card) | No container — flex row only |
| Title size | `text-xl font-semibold` inside card | `text-base font-semibold` inline |
| Subtitle | Separate `<p>` row below title | Task count badge inline next to title |
| Vertical height | ~85px | ~34px |

### KPI Row

| | Before | After |
|---|---|---|
| Layout | Grid of cards (`rounded-xl px-4 py-3 border`) | Single flex row of chips |
| Value typography | `text-2xl font-bold` | `text-xs font-semibold` inline with label |
| Card shadow/border | `bg-gray-50 border border-gray-100 rounded-xl` per card | `bg-gray-50 border border-gray-100 rounded-lg` per chip, compact |
| Vertical height | ~100px | ~30px |
| Space saved | — | ~70px |

### Filter Toolbar

| | Before | After |
|---|---|---|
| Wrapper | `bg-white border rounded-xl px-4 py-3` | No wrapper — bare flex row |
| Input/select height | `py-2 text-sm` | `py-1.5 text-xs` |
| Vertical height | ~60px | ~34px |
| Space saved | — | ~26px |

### Board Columns

| | Before | After |
|---|---|---|
| Column header padding | `px-4 py-3` | `px-3 py-2` |
| Column body padding | `p-3 space-y-2` | `p-2 space-y-1.5` |
| Column gap | `gap-4` | `gap-3` |
| Corner radius | `rounded-xl` | `rounded-lg` |
| Min height | `min-h-[200px]` | `min-h-[120px]` |
| Empty state | `py-6` centered | `py-4` + optional Add Task link |

### Task Card

| | Before | After |
|---|---|---|
| Padding | `p-3` | `p-2` |
| Priority → title spacing | `mb-2` | `mb-1.5` |
| Priority pill margin | `mb-1.5` | `mb-1` |
| Footer border | `pt-1.5` | `pt-1` |

### Container spacing

| | Before | After |
|---|---|---|
| Global page gap | `space-y-5` (20px) | `space-y-3` (12px) |
| Case view gap | `space-y-4` (16px) | `space-y-2` (8px) |

---

## Layout Strategy

```
ROW 1 — COMPACT HEADER (34px)
  [Task Manager • 47 tasks]         [Board][List]  [+ New Task]

ROW 2 — STAT CHIPS (30px)
  [◉ Total 47]  [○ New 12]  [⟳ In Progress 8]  [⏸ Blocked 3]  [⚠ Overdue 5]

ROW 3 — TOOLBAR (34px)
  [🔍 Search...]  [Assignee ▾]  [Status ▾]  [Priority ▾]  [Clear (n)]

ROW 4 — BOARD (remaining viewport)
  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐
  │ New     │  │ In Prog │  │ Blocked │  │  Done   │
  │  card   │  │  card   │  │         │  │  card   │
  │  card   │  │         │  │         │  │         │
  └─────────┘  └─────────┘  └─────────┘  └─────────┘
```

Total chrome above board: ~98px (down from ~265px). Board gains ~167px of vertical screen space.

---

## Component Refactors

### `TaskManagerHeader`

- Props: `title`, `taskCount`, `viewMode`, `onViewModeChange`, `onNewTask`
- No outer container/card — plain flex row
- Board/List toggle at `text-xs`, button height `py-1`
- New Task button at `text-xs px-3 py-1.5`

### `TaskManagerStatsStrip`

- Props: `stats: { label, value, icon, color }[]`
- Renders flex-row of chip pills: `bg-gray-50 border rounded-lg px-2.5 py-1.5`
- Each chip: icon + label text + bold value, all at `text-xs`

### `TaskManagerToolbar`

- Props: search, onSearch, statusFilter, onStatusFilter, priorityFilter, onPriorityFilter, `assigneeSlot?: ReactNode`, activeFilterCount, onClearFilters
- No outer wrapper card — plain flex row
- Input/select: `py-1.5 text-xs`
- `assigneeSlot` allows caller to inject different assignee filters (global scope selector vs case user picker)

### `TaskBoard`

- Props: `columns`, `usersById`, `onTaskClick`, `onNewTask?`
- Internally renders grid of column cards using `TaskCard`
- Column header: `px-3 py-2`, count badge at `text-[10px]`
- Body: `p-2 space-y-1.5`
- Gap: `gap-3`

---

## Reusability (Global vs Case)

| Component | Global Task Manager | Case Task Manager |
|---|---|---|
| `TaskManagerHeader` | title="Task Manager", taskCount=totalCount | title="Tasks", taskCount=tasks.length |
| `TaskManagerStatsStrip` | 5 stats (Total, New, InProgress, Blocked, Overdue) | 4 stats (Total, InProgress, Blocked, Overdue) |
| `TaskManagerToolbar` | assigneeSlot = scope dropdown (all/me/others/unassigned) | assigneeSlot = user picker (from users list) |
| `TaskBoard` | full board, no prefill | onNewTask prefills caseId |
| `CreateEditTaskForm` | no prefill | prefillCaseId + prefillWorkflowStageId |
| `TaskDetailDrawer` | shared | shared |

---

## Validation Results

| Check | Result |
|---|---|
| Build passes (0 errors) | ✅ `tsc --noEmit` — 0 errors, 0 warnings |
| Fast refresh picks up all changes | ✅ All 7 modules rebuilt by Next.js fast refresh |
| Header is compact (no full-card wrapper) | ✅ Replaced `PageHeader` card with plain flex row |
| KPI row is single horizontal line of chips | ✅ `TaskManagerStatsStrip` — flex-row of `px-2.5 py-1.5` chips |
| Toolbar is single-line at desktop width | ✅ `TaskManagerToolbar` — bare flex row, `py-1.5 text-xs` inputs |
| Board uses majority of viewport | ✅ ~167px of vertical space reclaimed above the board |
| Cards are visually tighter | ✅ `p-2`, `mb-1.5`, `mb-1`, `pt-1` — all internal spacing reduced |
| Filters still work | ✅ All filter state/callbacks preserved, no logic changed |
| View mode toggle (board/list) still works | ✅ `onViewModeChange` wired in `TaskManagerHeader` |
| New Task form opens | ✅ `onNewTask` → `setShowCreate(true)` unchanged |
| Task detail drawer opens | ✅ `onTaskClick` → `setDetailTask` unchanged |
| Case view matches global view | ✅ Both use same 4 shared components |
| No feature regression | ✅ Business logic, API calls, state shape untouched |

---

## Known Gaps

| Item | Notes |
|---|---|
| List view not extracted to shared component | List view HTML is duplicated between the two pages; functional but not DRY. Can be extracted in a follow-up. |
| Utility function duplication | `avatarColor`, `getInitials`, `formatDate`, `isOverdue` still exist in both pages and task-card. Could be moved to a shared `task-utils.ts` file in a follow-up. |
| No drag-and-drop | Neither page had drag-and-drop implemented; not removed. |
| Mobile toolbar wrapping | On mobile the toolbar wraps via `flex-wrap`; a dedicated mobile layout could be added later. |

---

## Run Instructions

```
# Dev server
bash scripts/run-dev.sh

# Navigate to
/lien/task-manager            — Global Task Manager
/lien/cases/[id]  → Tasks tab — Case Task Manager
```
