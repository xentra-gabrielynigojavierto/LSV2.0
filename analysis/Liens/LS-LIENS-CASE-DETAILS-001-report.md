# LS-LIENS-CASE-DETAILS-001 — Case Detail Details Tab Enhancement

## Objective
Expand the Details tab in Tenant Portal → Synq Liens → Case → Case Detail to include all required sections (Plaintiff, Case Tracking, Updates, Communications) while preserving the current layout, data, and LayoutSplit behavior.

## Scope
- ONLY modify the Details tab body content within `case-detail-client.tsx`
- Do NOT touch: navigation, fonts, colors, routing, APIs, role-access, data contracts

## Current State Analysis
The Details tab currently has:
- **Plaintiff section** (CollapsibleSection) — Full Name, Phone, Email, Birthdate, Sex, Address — with edit action ✓
- **Case Tracking section** (CollapsibleSection) — Tracking Follow Up, Current Status, Current Medical Status, Case Type, State of Incident, Lead, Case Tracking Note — with edit action ✓
- **Right panel** — Email section only, with "Compose New Email" button ✓

## Missing Sections Identified
1. **Case Tracking** — Missing checkbox/toggle items: Share with Law Firm, UCC Filed, Case Dropped, Child Support, Minor Comp
2. **Updates section** — Entirely missing. Needs activity table with Timestamp, Actions, Description, Updated By columns
3. **Right panel** — Missing SMS card with "Send SMS" button; Missing communication contacts (Case Manager, Law Firm)

## Planned Changes
1. Add missing checkbox toggles to Case Tracking section
2. Add Updates section with activity table (below Case Tracking)
3. Add SMS card to right communications panel
4. Add communication contacts to right panel (Case Manager, Law Firm)
5. Apply consistent Remix Icon usage across all sections

## Icon Usage Plan
- Plaintiff: `ri-user-line` (existing)
- Case Tracking: `ri-compass-3-line` (existing)
- Updates: `ri-history-line`
- Email: `ri-mail-send-line`
- SMS: `ri-message-2-line`
- Contacts: `ri-contacts-line`
- All icons: Remix Icon library (consistent, already in use)

---

## Execution Log

| Step | Files Modified | Change | Status |
|------|---------------|--------|--------|
| T001 | - | Analyze current Details tab | COMPLETE |
| T002 | `case-detail-client.tsx` | Add checkbox toggles to Case Tracking | COMPLETE |
| T003 | `case-detail-client.tsx` | Add Updates section with activity table | COMPLETE |
| T004 | `case-detail-client.tsx` | Add SMS card and contacts to right panel | COMPLETE |
| T005 | `case-detail-client.tsx` | Verify icon consistency | COMPLETE |
| T006 | - | Validate LayoutSplit behavior | COMPLETE |
| T007 | `case-detail-client.tsx` | Add temp fallback data where needed | COMPLETE |
| T008 | - | Final validation | COMPLETE |

---

## Files Changed
- `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx`

## Sections Restored/Added
1. Case Tracking — checkbox toggles (Share with Law Firm, UCC Filed, Case Dropped, Child Support, Minor Comp)
2. Updates — full activity table with columns (Timestamp, Actions, Description, Updated By)
3. Right panel — SMS card with Send SMS button
4. Right panel — Communication contacts (Case Manager, Law Firm)

## Icon Changes
- Updates section header: `ri-history-line`
- Email section header: `ri-mail-send-line`
- SMS section header: `ri-message-2-line`
- Contact avatars: `ri-user-line` (Case Manager), `ri-building-line` (Law Firm)
- All icons from Remix Icon library — consistent outlined style

## Temporary Fallback Data
- Updates table rows: 4 sample activity entries (TEMP: visual fallback data for UI review only)
- Communication contacts: Case Manager and Law Firm placeholders (TEMP: visual fallback data for UI review only)
- Checkbox states: all default false (TEMP: visual fallback data for UI review only)

## Validation Results
- All original Plaintiff fields retained ✓
- All original Case Tracking fields retained ✓
- Checkbox toggles added to Case Tracking ✓
- Updates section added with activity table ✓
- Right panel: Email + SMS + Contacts ✓
- LayoutSplit split/left-expanded/right-expanded all functional ✓
- Icons consistent (Remix Icon throughout) ✓
- No font/color/nav changes ✓
- No logic/workflow regressions ✓

## Remaining Gaps
- Updates data source: currently temp visual fallback; needs API integration when audit/activity endpoint is available
- Communication contacts: currently temp fallback; needs real contact data from case/tenant
- Checkbox states: not persisted; needs backend support for case tracking flags
