# LS-LIENS-UI-DESIGN-001 — Synq Liens Case Section Body Redesign

## Objective
Implement a UI body-only redesign for the Tenant Portal → Synq Liens → Case section. Replicate the reference design's layout, spacing, and composition while preserving existing top navigation, side navigation, fonts, colors, and all business logic.

## Scope
- **App:** Tenant Portal (`apps/web`)
- **Product:** Synq Liens
- **Section:** Case (list + detail pages)
- **Constraint:** Body content only — no shell, nav, font, or color changes

## Screen Replication Priority
1. Case Detail page body — default split-panel state (70/30)
2. Case Detail page body — expanded/collapsed panel behavior
3. Case List page body — same design language

## Design Interpretation
- Clean enterprise canvas: white card sections, subtle borders, compact spacing
- Split-panel detail layout: LEFT dominant (details/working content), RIGHT narrower (contextual/summary)
- Card-based section organization with consistent headers
- Compact metadata label/value pairs
- Enterprise-density tables with restrained borders
- Toggle controls at panel divider for expand/collapse

## Unchanged Elements — VERIFIED
- Top navigation bar (navy `#0f1928`, h-14) — UNCHANGED ✓
- Side navigation (collapsible sidebar) — UNCHANGED ✓
- Font family (system sans-serif stack) — UNCHANGED ✓
- Color scheme (primary via `--color-primary`, gray palette) — UNCHANGED ✓
- Global shell layout (`AppShell` component) — UNCHANGED ✓

## Temporary Mock Display Data Plan
- Isolated mock module at `apps/web/src/components/lien/case-mock-data.ts`
- Used ONLY as visual fallback for empty/sparse UI sections
- Covers: activity timeline, notes preview, documents count, tasks count, related contacts
- Real data always takes precedence
- Clearly labeled in code comments
- **Action required:** Remove `case-mock-data.ts` once real data is wired

---

## Implementation Steps

### T001 — Analysis & Foundation Components
- **Status:** COMPLETED ✓
- **Files:**
  - `analysis/LS-LIENS-UI-DESIGN-001-report.md` (this file)
  - `apps/web/src/components/lien/case-mock-data.ts`
  - `apps/web/src/components/lien/split-panel-layout.tsx`
- **Decisions:**
  - SplitPanelLayout uses local state with 3 modes: `split`, `left-expanded`, `right-expanded`
  - Default split ratio: ~70/30 using flex-[7]/flex-[3]
  - Smooth CSS transitions for expand/collapse
  - Divider toggle strip with subtle controls
  - Mock data module exports typed objects for Case detail visual review
- **Components created:**
  - `SplitPanelLayout` — 3-mode split panel (split, left-expanded, right-expanded)
  - `SectionCard` — Consistent card wrapper with icon header and optional actions
  - `MetadataGrid` — Responsive grid for label/value metadata pairs
  - `MetadataItem` — Individual metadata label+value pair

### T002 — Case Detail Body Redesign
- **Status:** COMPLETED ✓
- **Files:** `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx`
- **Changes:**
  - Compact page header with status badge, key stats, and advance button
  - Tab bar: Details, Liens, Documents, Servicing, Notes, Task Manager
  - Details tab uses SplitPanelLayout:
    - **Left panel (70%):** Plaintiff Info, Case Details, Related Liens table, Recent Activity (mock)
    - **Right panel (30%):** Case Summary stats, Key Dates, Contacts (mock), Notes preview (mock), Tasks (mock)
  - Liens tab: Full-width enterprise-density table
  - All other tabs: Clean empty state placeholders
  - Breadcrumb navigation at top
  - Real data (liens, case info) always takes precedence over mock fallbacks

### T003 — Case List Body Redesign
- **Status:** COMPLETED ✓
- **Files:** `apps/web/src/app/(platform)/lien/cases/page.tsx`
- **Changes:**
  - Enterprise-density table with compact row spacing (py-2.5)
  - Columns: Case ID, Plaintiff Name, Law Firm, Case Manager, Accident Type, Date of Loss, DOB, Status
  - Rounded card container with subtle border
  - Improved header with 11px uppercase tracking labels
  - Row hover states, click-to-preview, selection checkboxes
  - Pagination with page info + prev/next controls
  - Side drawer preview panel
  - Bulk action bar and confirmation modal
  - Error state with retry button

### T004 — Final Validation
- **Status:** COMPLETED ✓
- **Validation results:**
  - **Build:** 0 compilation errors across all services and frontends
  - **Changed files:** Only `case-detail-client.tsx` and `page.tsx` modified (plus new foundation files)
  - **Shell integrity:** Top nav, side nav, AppShell, fonts, colors — ALL UNCHANGED (verified via git diff)
  - **No regressions:** No new warnings or errors introduced
  - **Mock data:** Isolated in `case-mock-data.ts`, clearly labeled as temporary

---

## Summary
All four implementation tasks completed successfully. The Synq Liens Case section now uses a clean enterprise design language with:
- A split-panel detail layout (70/30 default, expandable/collapsible)
- Tabbed navigation for case detail sections
- Enterprise-density table for the case list
- Consistent card-based sections with icon headers
- Compact metadata display and summary statistics
- Temporary mock data for visual review (clearly labeled for removal)

No shell, navigation, font, or color changes were made.
