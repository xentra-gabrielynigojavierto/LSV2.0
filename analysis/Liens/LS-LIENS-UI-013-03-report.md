# LS-LIENS-UI-013-03 — Split Divider Arrow Direction Fix

## Bug Summary
The LayoutSplit divider had two issues:
1. In expanded modes (left-expanded / right-expanded), both arrow buttons showed the same direction icon, making them visually indistinguishable
2. Clicking the non-active button in an expanded mode would jump directly to the opposite expanded state (e.g., from left-expanded to right-expanded) instead of restoring to split view first — this felt inverted/unpredictable

## Root Cause
The click handlers used simple toggle logic per button: `mode === 'left-expanded' ? 'split' : 'left-expanded'`. This meant when in `right-expanded` mode and clicking the left-expand button, it would set `left-expanded` (skipping `split`), creating unexpected jumps between expanded states.

## Handler Logic Changed

### Before
- Top button: toggle between `split` ↔ `left-expanded` (if already left-expanded → split, otherwise → left-expanded)
- Bottom button: toggle between `split` ↔ `right-expanded` (if already right-expanded → split, otherwise → right-expanded)
- In left-expanded: clicking bottom button → right-expanded (jumps between expanded states)
- In right-expanded: clicking top button → left-expanded (jumps between expanded states)

### After
- Top button: `split` → `left-expanded`; ANY other mode → `split`
- Bottom button: `split` → `right-expanded`; ANY other mode → `split`
- In any expanded mode, clicking either button restores to split view first
- No more direct jumps between expanded states

## Icon Behavior (unchanged)
- Top button: "<" in split/right-expanded; ">" in left-expanded (active, restore indicator)
- Bottom button: ">" in split/left-expanded; "<" in right-expanded (active, restore indicator)

## File Modified
- `apps/web/src/components/lien/layout-split.tsx`

## Validation Results

### Split mode
- "<" (top button) → left-expanded ✓
- ">" (bottom button) → right-expanded ✓

### Left-expanded mode
- ">" (top button, active/primary) → restores to split ✓
- ">" (bottom button, gray) → restores to split ✓

### Right-expanded mode
- "<" (top button, gray) → restores to split ✓
- "<" (bottom button, active/primary) → restores to split ✓

### Cross-page consistency
- Fix is in shared `LayoutSplit` component
- Case Detail inherits fix automatically ✓
- Lien Detail inherits fix automatically ✓

## No Regressions
- Layout unchanged
- Spacing unchanged
- Panel styling unchanged
- Header/tabs unchanged
- Data/routing unchanged
- Services/role logic unchanged
