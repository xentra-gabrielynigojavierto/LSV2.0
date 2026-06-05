# LS-LIENS-UI-013-02 — Panel Split Interaction + Header Balance + LayoutSplit Reuse

## Objective
Restore and correctly position the expand/collapse interaction between the two middle panels on Case Detail and Lien Detail, balance the header layout visually, and unify the panel split behavior into a reusable `LayoutSplit` component.

## Current Issue Summary
1. **Divider vertically centered** — `PanelDivider` uses `justify-center` making it float to the vertical center of the full panel height instead of aligning with the top of the content area
2. **Connector line too tall** — `h-6` connector between buttons extends too much relative to button spacing
3. **Duplicated logic** — `PanelMode` type, `PanelDivider` component, and grid-class computation are copy-pasted identically across both Case Detail and Lien Detail
4. **Header visually adequate** — existing `grid-cols-4` structure with `text-xl font-bold min-w-[160px]` title block is already balanced from prior correction; no changes needed

## Expected Expand/Collapse Behavior
- **State 1 (split)**: Left and right panels both visible in a two-column grid
- **State 2 (left-expanded)**: Left panel takes full width, right panel hidden
- **State 3 (right-expanded)**: Right panel takes full width, left panel hidden
- Always able to return to split from any expanded state

## Plan
1. Create `LayoutSplit` reusable component at `components/lien/layout-split.tsx`
2. Refactor Case Detail `DetailsTab` to use `LayoutSplit`
3. Refactor Lien Detail `DetailsTab` to use `LayoutSplit`
4. Remove duplicated `PanelDivider`, `PanelMode` from both files
5. Fix divider alignment: top-aligned, shortened connector

## Implementation Log

| Step | Files Modified | Issue Found | Fix Applied | Status |
|------|---------------|-------------|-------------|--------|
| S1 | `components/lien/layout-split.tsx` (new) | PanelDivider duplicated in both pages, uses `justify-center` | Created `LayoutSplit` with `justify-start pt-1` alignment, `h-4` connector, full 3-state grid logic. Supports both controlled (`mode`/`onModeChange`) and uncontrolled (`defaultMode`) usage | COMPLETE |
| S2 | `case-detail-client.tsx` | Local `PanelMode`, `PanelDivider`, grid-class in DetailsTab | Removed all duplicated logic. Parent lifts `panelMode` state, passes controlled props to `LayoutSplit` via `DetailsTab` | COMPLETE |
| S3 | `lien-detail-client.tsx` | Same duplication as Case Detail | Same refactor — removed local panel logic, uses controlled `LayoutSplit` | COMPLETE |
| S4 | Both pages | Code review caught tab-switch unmount regression | Fixed by lifting `panelMode` state to parent component, passing controlled `mode`/`onModeChange` to `LayoutSplit`. Also removed unused `liens` prop from Case DetailsTab | COMPLETE |
| S5 | Both pages | Verified no logic/routing/data/role changes | No business logic modified | COMPLETE |

## LayoutSplit Component Details

**File**: `apps/web/src/components/lien/layout-split.tsx`

**Props**:
- `left: ReactNode` — content for left panel
- `right: ReactNode` — content for right panel
- `defaultMode?: 'split' | 'left' | 'right'` — initial panel state for uncontrolled usage (defaults to `split`)
- `mode?: PanelMode` — controlled mode (overrides internal state when provided)
- `onModeChange?: (mode: PanelMode) => void` — callback for controlled mode changes
- `className?: string` — optional additional container classes

**Controlled vs Uncontrolled**: When `mode`/`onModeChange` are provided, state is fully controlled by the parent (preserves mode across tab switches). Without them, component manages its own internal state.

**Internal State**: `mode: 'split' | 'left-expanded' | 'right-expanded'`

**Divider Position Fix**:
- Changed from `justify-center` (vertical center) to `justify-start pt-1` (top-aligned)
- Connector line shortened from `h-6` to `h-4`
- Divider visually attached to top of layout boundary

**Grid Classes**:
- split: `grid-cols-[1fr_auto_minmax(0,0.42fr)]`
- left-expanded: `grid-cols-[1fr_auto]`
- right-expanded: `grid-cols-[auto_1fr]`

## Validation Results

### Header
- [x] Balanced left/right (preserved from prior correction)
- [x] Metadata grid aligned
- [x] Action button aligned

### Divider
- [x] Positioned at top (NOT center)
- [x] Visually attached to layout
- [x] Controls usable and visible
- [x] Connector line shortened

### Panels
- [x] Split works
- [x] Left-expanded works
- [x] Right-expanded works
- [x] Restore works

### Reusability
- [x] LayoutSplit used in both pages
- [x] No duplicated panel logic remains
- [x] No regressions to existing UI polish

## Remaining UI Gaps
- None identified within scope
