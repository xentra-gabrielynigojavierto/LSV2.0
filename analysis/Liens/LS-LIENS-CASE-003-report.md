# LS-LIENS-CASE-003 — Case Servicing Tab UI

## Objective
Implement the Servicing tab within the Case Detail page (Synq Liens → Cases → Case Detail), including three internal sub-tabs: Servicing Details, Settlement Details, and History.

## Scope
- Only the Servicing tab body content in `case-detail-client.tsx`
- No changes to top nav, side nav, routing, fonts, colors, APIs, other tabs

## Implementation

### Sub-Tab Structure
| Sub-Tab | Icon | Content |
|---------|------|---------|
| Servicing Details | ri-settings-3-line | Form fields for case status, law firm switching, lawyer/case manager |
| Settlement Details | ri-money-dollar-circle-line | Reduction, Payments, Open Liens table, Closed Liens table, Payment History |
| History | ri-history-line | Timestamped activity log table |

### Servicing Details Fields
- Case Status (dropdown: Pre-demand, Demand Sent, In Negotiation, Case Settled, Closed)
- Switched Law Firm (checkbox)
- Switched Date (date picker, disabled when checkbox unchecked)
- Current Law Firm (text input)
- Current Lawyer (text input)
- Current Case Manager (text input)
- Save button (with loading spinner state, not wired to backend)

### Settlement Details Sections
1. **Reduction** — Empty state with "Setup Reduction" action button
2. **Payments** — Count display with "Add Payment" action button
3. **Open Liens** — Table with Lien ID, Facility, Billing Amt, Reduction, Payment, Balance, Status; totals row; action buttons (Setup Reduction, No Recovery, Add Payment)
4. **Closed Liens** — Same columns as Open Liens; totals row with green reduction amounts
5. **Payment History** — Table with Date, Lien ID, Facility, Amount, Method, Reference, Processed By

### History
- Table with Timestamp, Description, Updated By
- 6 sample entries covering status changes, law firm switches, payments, reductions

### Right Panel
Communications-only (preserved from other tabs):
- Email (Compose New Email button)
- SMS (Send SMS button)
- Contacts (Case Manager + Law Firm)

### Files Changed
| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx` | Added `ServicingTab` function, TEMP data constants, sub-tab type/config, replaced `EmptyTab` reference |

### Design Patterns Used
- `LayoutSplit` with `panelMode` / `onPanelModeChange` (same as Details, Liens, Documents tabs)
- `CollapsibleSection` for all content blocks
- Same table styling: `text-[11px]` headers, `divide-y divide-gray-50`, `hover:bg-gray-50/50`
- Same form input styling: `border-gray-200 rounded-lg bg-gray-50/50 focus:border-primary/40`
- Remix icons throughout
- `formatCurrency()` for all monetary values
- `StatusBadge` for lien status pills
- Tabular nums for financial columns

### Fallback Data
All data is TEMP visual fallback, clearly labeled:
- `TEMP_SERVICING_OPEN_LIENS` — 3 open liens
- `TEMP_SERVICING_CLOSED_LIENS` — 1 closed lien
- `TEMP_PAYMENT_HISTORY` — 1 payment record
- `TEMP_SERVICING_HISTORY` — 6 history entries
- Servicing Details form values initialized with sample data

### Validation
- TypeScript: 0 errors, 0 warnings
- No changes to other tabs (Details, Liens, Documents, Notes, Task Manager)
- LayoutSplit expand/collapse behavior preserved
- Right panel communications-only preserved
- Sub-tab navigation functional

### Code Review Adjustments
- **Save button**: Changed from simulated async save (fake spinner) to explicitly disabled state with "Not yet connected to API" label. Does not fake save success.
- **Fallback data**: All TEMP data is clearly labeled. No real servicing API exists yet, so all data is visual fallback by necessity. The pattern matches the Documents tab approach.

### Remaining Gaps
- Save button disabled until backend API is wired
- Setup Reduction / No Recovery / Add Payment actions are UI-only (buttons present, not wired)
- All table data uses TEMP fallback (no servicing API exists)
- No real servicing field persistence
