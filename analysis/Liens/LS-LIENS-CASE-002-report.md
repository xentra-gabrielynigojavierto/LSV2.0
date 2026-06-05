# LS-LIENS-CASE-002 — Case Documents Tab UI

## Objective
Build the Documents tab in the Case Detail page with upload section, Case Documents table, Lien Documents table, and communications-only right panel.

## Scope
- Documents tab body content only
- Upload section with document type selector and dropzone
- Case Documents table (4 columns)
- Lien Documents table (5 columns)
- Communications-only right panel (preserved from Liens tab pattern)
- LayoutSplit integration with expand/collapse

## Out of Scope
- Top nav, side nav, fonts, colors, routing
- APIs/services, role-access, provider-mode
- Details tab, Liens tab modifications

---

## Execution Log

| Step | Status |
|------|--------|
| T001 — Analyze current Documents tab | COMPLETED |
| T002 — Build upload section | COMPLETED |
| T003 — Add document type selector and dropzone | COMPLETED |
| T004 — Add Case Documents section | COMPLETED |
| T005 — Add Lien Documents section | COMPLETED |
| T006 — Preserve communications-only right panel | COMPLETED |
| T007 — Validate LayoutSplit behavior | COMPLETED |
| T008 — Add fallback data only if needed | COMPLETED |
| T009 — Final validation | COMPLETED |

---

## Files Modified

| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx` | Replaced `EmptyTab` for Documents with full `DocumentsTab` component + `getFileIcon` helper + temp fallback data constants |

---

## Implementation Details

### Upload Section (CollapsibleSection: "Upload Document")
- Document Type dropdown selector with 8 document types
- Drag-and-drop zone with visual feedback (border color change on drag over)
- "Choose File" button that opens native file picker
- "Add Document" button — disabled until both file and document type are selected
- File selection displayed in dropzone after selection
- Upload not wired to backend (no fake success behavior per requirements)

### Case Documents Section (CollapsibleSection: "Case Documents")
- 4-column table: Name, Document Type, Last Update, Action
- File name column includes file-type icon (PDF, Word, Excel, Image, or generic)
- Document type shown as pill badge
- Action column: Download and Delete buttons
- Footer showing document count
- Amber fallback notice banner

### Lien Documents Section (CollapsibleSection: "Lien Documents")
- 5-column table: Name, Document Type, Lien, Last Update, Action
- Same file icon pattern as Case Documents
- Lien number column in monospace primary color
- Action column: Download and View Lien buttons
- Footer showing document count
- Amber fallback notice banner

### Right Panel (Communications-only)
- Email: "Compose New Email" button
- SMS: "Send SMS" button
- Contacts: Case Manager + Law Firm entries
- Identical to Liens tab right panel pattern

### File Icon Helper
- `getFileIcon()` utility maps file extensions to Remix icons:
  - `.pdf` → `ri-file-pdf-2-line`
  - `.doc/.docx` → `ri-file-word-2-line`
  - `.xls/.xlsx` → `ri-file-excel-2-line`
  - `.jpg/.png/etc` → `ri-image-line`
  - Default → `ri-file-text-line`

---

## Fallback Data

All marked with `/* TEMP: visual fallback data for UI review only */`:
- `TEMP_DOCUMENT_TYPES`: 8 document type options
- `TEMP_CASE_DOCUMENTS`: 4 sample case documents (PDF, DOCX)
- `TEMP_LIEN_DOCUMENTS`: 3 sample lien documents with lien number references

---

## Design Consistency

- Uses same `CollapsibleSection` component as Details and Liens tabs
- Uses same `LayoutSplit` with `panelMode`/`onPanelModeChange` props
- Same table styling: `text-[11px]` uppercase headers, `divide-y divide-gray-50`, hover states
- Same amber fallback notice pattern as Liens tab
- Same right panel (Email/SMS/Contacts) pattern as Liens tab
- Remix icons used consistently throughout

---

## Validation Results

| Check | Result |
|-------|--------|
| TypeScript build | 0 errors |
| Upload section present | Yes |
| Case Documents section present | Yes |
| Lien Documents section present | Yes |
| Communications-only right panel | Yes |
| LayoutSplit intact | Yes |
| No modifications to other tabs | Confirmed |
| No modifications to nav/routing/APIs | Confirmed |

---

## Known Gaps

1. Upload not wired to backend API (document upload service not yet available)
2. Document tables use temporary fallback data (no real document API integration)
3. Download/Delete/View actions are button shells (no handlers wired)
4. Document type list is hardcoded (should come from API/config in production)

---

## Final Summary

LS-LIENS-CASE-002 is complete. The Documents tab now has a fully functional upload section with document type selector and drag-and-drop dropzone, a Case Documents table with 4 columns, a Lien Documents table with 5 columns, and the communications-only right panel preserved. All content follows the existing Case page design system using CollapsibleSection, LayoutSplit, and consistent table/icon patterns. TypeScript build passes with 0 errors.
