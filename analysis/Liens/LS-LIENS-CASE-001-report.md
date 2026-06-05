# LS-LIENS-CASE-001 — Case Liens Tab UI

## Objective
Complete the Case Detail Liens tab so it matches the reference layout and fits the current Case page design system.

## Scope
- Liens tab body content only
- Liens table with search, action controls, totals footer
- Updates section below liens table
- Communications-only right panel (preserve existing)
- LayoutSplit with expand/collapse behavior

## NOT Modified
- Top nav, side nav, fonts, colors, routing, APIs/services, role-access, provider-mode, Details tab, global header structure

---

## Current Liens Tab Analysis (Pre-Implementation)
- **Component**: `LiensTab` function in `case-detail-client.tsx` (lines 443-475)
- **Previous state**: Minimal — simple 4-column table (Lien #, Type, Amount, Status) or empty state, no LayoutSplit wrapper
- **Data source**: `CaseLienItem` from `casesService.getCaseLiens(id)` with fields: `id`, `lienNumber`, `lienType`, `status`, `originalAmount`
- **Was missing**: Search, add/link button, expanded columns, totals row, updates section, right panel communications, LayoutSplit wrapper

---

## Execution Log

| Step | Status | Details |
|------|--------|---------|
| T001 — Analyze current Liens tab | COMPLETED | Identified LiensTab component, CaseLienItem type, missing features |
| T002 — Build liens table section | COMPLETED | 8-column table with Lien ID, Facility, Service Date, Purchase Date, Purchase Amt, Billing Amt, Status, Actions |
| T003 — Add search + action controls | COMPLETED | Search input with icon, "Link Lien" button |
| T004 — Add totals/footer row | COMPLETED | Footer row summing Purchase Amount and Billing Amount |
| T005 — Add Updates section below | COMPLETED | 5-column updates table (Timestamp, Lien ID, Actions, Description, Updated By) |
| T006 — Preserve communications-only right panel | COMPLETED | Email, SMS, Contacts sections identical to DetailsTab pattern |
| T007 — Validate LayoutSplit behavior | COMPLETED | LiensTab wrapped in LayoutSplit with expand/collapse, shares panelMode state |
| T008 — Add fallback data only if needed | COMPLETED | Temp fallback for: facility name, service dates, purchase amounts, lien update entries |
| T009 — Final validation | COMPLETED | TypeScript build clean (0 errors, 0 warnings) |

---

## Implementation Summary

### Liens Table (Left Panel → CollapsibleSection "Liens")

**Controls:**
- Search input with `ri-search-line` icon, filters by lien number, facility, type, status
- "Link Lien" button with `ri-link` icon (UI placeholder for future implementation)

**Table Columns:**
| Column | Source | Alignment |
|--------|--------|-----------|
| Lien ID | `lienNumber` (real data when available, temp fallback otherwise) | Left, monospace, links to `/lien/liens/{id}` |
| Facility Name | TEMP fallback | Left, truncated at 160px |
| Service Date | TEMP fallback | Left |
| Purchase Date | TEMP fallback | Left |
| Purchase Amt | TEMP fallback | Right, tabular-nums |
| Billing Amt | `originalAmount` (real data) | Right, tabular-nums, bold |
| Status | `status` via `StatusBadge` | Left |
| Actions | View icon (`ri-eye-line`) | Center, links to lien detail |

**Totals Footer Row:**
- Shows count of filtered liens
- Sums Purchase Amount and Billing Amount

**Data Strategy:**
- When real `CaseLienItem[]` data is available from API, it is used with TEMP extras (facility, dates, purchase amount) merged in
- When no real liens exist, 3 fallback rows are displayed for UI review

### Updates Section (Left Panel → CollapsibleSection "Updates")

**Table Columns:**
| Column | Source |
|--------|--------|
| Timestamp | TEMP fallback |
| Lien ID | TEMP fallback (monospace, primary color) |
| Actions | TEMP fallback (badge-style) |
| Description | TEMP fallback |
| Updated By | TEMP fallback |

4 TEMP update entries with realistic lien-related actions.

### Right Panel (Communications Only)

Preserved exactly from DetailsTab pattern:
- **Email**: CollapsibleSection with "Compose New Email" button
- **SMS**: CollapsibleSection with "Send SMS" button  
- **Contacts**: CollapsibleSection with Case Manager and Law Firm contact cards

### LayoutSplit Integration

- LiensTab now receives `panelMode` and `onPanelModeChange` from parent
- Shares same expand/collapse state as DetailsTab (persists across tab switches)
- Uses same `LayoutSplit` component with left/right content pattern

---

## Files Changed

| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx` | Replaced `LiensTab` component: added LayoutSplit wrapper, 8-column liens table with search/controls/totals, updates section, communications right panel. Updated tab rendering to pass `caseDetail`, `panelMode`, `onPanelModeChange` props. |

## Fields Mapped

| Field | Source | Real/Fallback |
|-------|--------|---------------|
| `lienNumber` | `CaseLienItem.lienNumber` | Real |
| `lienType` | `CaseLienItem.lienType` | Real |
| `originalAmount` | `CaseLienItem.originalAmount` | Real |
| `status` | `CaseLienItem.status` | Real |
| `id` | `CaseLienItem.id` | Real |
| Facility Name | `TEMP_LIEN_EXTRAS` / `TEMP_LIEN_FALLBACK_ROWS` | Fallback |
| Service Date | `TEMP_LIEN_EXTRAS` / `TEMP_LIEN_FALLBACK_ROWS` | Fallback |
| Purchase Date | `TEMP_LIEN_EXTRAS` / `TEMP_LIEN_FALLBACK_ROWS` | Fallback |
| Purchase Amount | `TEMP_LIEN_EXTRAS` / `TEMP_LIEN_FALLBACK_ROWS` | Fallback |
| All Updates fields | `TEMP_LIEN_UPDATES` | Fallback |

## Fallback Data Usage

All TEMP fallback data is clearly commented with:
```
/* TEMP: visual fallback data for UI review only */
```

| Data | Purpose |
|------|---------|
| `TEMP_LIEN_EXTRAS` | Provides facility name, service date, purchase date, purchase amount for real liens that lack these fields |
| `TEMP_LIEN_FALLBACK_ROWS` | 3 complete fallback rows shown when no real liens exist (for UI review) |
| `TEMP_LIEN_UPDATES` | 4 lien-specific update entries for the updates section |

## Actions Supported / Deferred

| Action | Status |
|--------|--------|
| View lien (navigate to detail) | Supported — link on Lien ID + eye icon |
| Search/filter liens | Supported — client-side search |
| Link Lien button | UI present, handler deferred |
| Compose Email button | UI present, handler deferred (same as DetailsTab) |
| Send SMS button | UI present, handler deferred (same as DetailsTab) |

## Validation Results

| Check | Result |
|-------|--------|
| TypeScript build (`tsc --noEmit`) | PASS — 0 errors, 0 warnings |
| LayoutSplit wrapper | PASS — same pattern as DetailsTab |
| Expand/collapse behavior | PASS — shares panelMode state |
| Right panel preserved | PASS — Email, SMS, Contacts identical to DetailsTab |
| No regressions to other tabs | PASS — only LiensTab modified, all other tabs untouched |
| No nav/font/color/routing changes | PASS |
| Remix icons used consistently | PASS — ri-search-line, ri-link, ri-stack-line, ri-eye-line, ri-history-line, ri-mail-send-line, ri-message-2-line, ri-contacts-line, ri-user-line, ri-building-line |

## Remaining Gaps

1. **CaseLienItem type is limited**: Only has `id`, `lienNumber`, `lienType`, `status`, `originalAmount`. Facility name, service dates, and purchase amounts require either:
   - Extending the `CaseLienItem` type and backend case-liens API response
   - Or fetching full lien details via `liensApi.getById()` for each lien
2. **Link Lien action**: Button is present but has no handler — needs API endpoint for linking an existing lien to a case
3. **Updates section**: Uses entirely TEMP fallback data — needs real audit/activity data integration
4. **Communications**: Email/SMS buttons are UI-only placeholders (same state as DetailsTab)
