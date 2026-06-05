# LS-LIENS-UI-DESIGN-002 — Lien Detail Page Body Redesign

## Objective
High-fidelity UI body redesign of the Lien Detail page (Tenant Portal → Synq Liens → Liens → Lien Detail) to match the Case Detail page's visual language: rounded header panel, tab bar, two-column body with expand/collapse, and a communications-focused right panel.

## Scope
- Page: `apps/web/src/app/(platform)/lien/liens/[id]/lien-detail-client.tsx`
- ONLY the body content area
- No changes to: top nav, side nav, fonts, colors, routing, services, APIs, role-access, provider-mode, sell/manage behavior

## Design Interpretation
Following the Case Detail pattern (`case-detail-client.tsx`):
1. Rounded header card with person name as primary title, lien number secondary, inline metadata grid
2. Tab bar directly under header (Details, Documents, Servicing, Notes, History, Tasks)
3. Two-column body layout (65-70% left / 30-35% right) with expand/collapse divider
4. Left: lien record content in collapsible sections
5. Right: status/financial summary + important dates + communications

## Header Alignment Plan
- Primary title: `subjectName` (person name) with fallback to mock "John Doe"
- Secondary: lien number
- Metadata grid: Lien Type, Status (badge), Incident Date, Jurisdiction, Case link, Created date
- Actions button: existing Submit Offer button (sell mode only)
- Rounded card container matching lower panels

## Right Panel — Status + Communications Plan
1. Financial Summary card: Original Amount, Current Balance, Offer Price, Purchase Price
2. Important Dates card: Incident Date, Opened, Created, Updated, Closed
3. Communications card: contacts placeholder + Compose Email button

## Expand/Collapse Interaction Plan
- Centered divider buttons between left and right columns
- Three states: split (default), left-expanded, right-expanded
- Local `useState` only, no global store

## Confirmation
- Top nav: UNCHANGED
- Side nav: UNCHANGED
- Fonts: UNCHANGED
- Colors: UNCHANGED
- Routing: UNCHANGED
- Services/APIs: UNCHANGED
- Role access: UNCHANGED
- Provider mode: UNCHANGED

## Implementation Log

### T001 — Analyze Existing Lien Detail Page
- Current header: `DetailHeader` component with `lienNumber` as title, `lienTypeLabel` as subtitle
- Current sections: Lien Lifecycle stepper, financial cards (Original/Offer/Purchase), Lien Summary + Subject Info (2-col grid), Offers list, Entity Timeline
- Person name available: `subjectName`, `subjectFirstName`, `subjectLastName`
- Communication content: none currently present
- LienDetail fields: id, lienNumber, externalReference, lienType, lienTypeLabel, status, caseId, originalAmount, currentBalance, offerPrice, purchasePrice, payoffAmount, jurisdiction, isConfidential, subjectName, subjectFirstName, subjectLastName, orgId, sellingOrgId, buyingOrgId, holdingOrgId, incidentDate, description, openedAt, closedAt, createdAt, updatedAt
- Status: COMPLETE

### T002-T010 — Full Implementation
- Rebuilt entire component with inline subcomponents matching Case Detail patterns
- HeaderMeta, CollapsibleSection, FieldGrid, FieldItem reused as inline components
- Tab system with 6 tabs
- Two-column layout with expand/collapse divider
- Status: COMPLETE

## Temporary Mock Data Usage
1. Header "State of Incident" — uses jurisdiction field which is real data, no mock needed
2. Header "Law Firm" — mock fallback "Smith & Associates" when no linked case (TEMP)
3. Header "Case Manager" — mock fallback "Sarah Mitchell" (TEMP)
4. Right panel Communications section — placeholder contacts (TEMP)
5. Servicing/Tasks tabs — empty state placeholders (matches Case Detail pattern)

## Files Modified
1. `apps/web/src/app/(platform)/lien/liens/[id]/lien-detail-client.tsx` — complete rewrite

## Files NOT Modified
- No shared components changed
- No services/APIs changed
- No routing changed
- No nav changed

## Code Review Fixes Applied
1. Restored "Submit Offer" button label (was changed to generic "Actions")
2. Removed no-op `onEdit` handlers from Lien Information and Subject Information sections (edit functionality not yet wired)
3. Fixed divider vertical centering: grid uses `items-stretch`, divider uses sticky centering
4. Notes tab: confirmed correct — original page had no notes data source either; EMPTY_NOTES is intentional empty state

## Validation
- TypeScript: PASS (0 errors, verified twice)
- App startup: PASS (login page renders, no compilation errors)
- Business logic preserved: offers, modals, sell/manage mode, role access all intact

## Final Summary
Complete redesign of Lien Detail body following Case Detail visual language. All existing business logic (offers, modals, sell/manage mode, role access) preserved. New structure: rounded header → tabs → two-column body with expand/collapse.

Key structural changes:
1. Header: person name primary title, lien number secondary, metadata grid with 7 items + actions button
2. Tab bar: 6 tabs (Details, Documents, Servicing, Notes, History, Tasks)
3. Details tab: two-column with expand/collapse divider
4. Left column: Lien Lifecycle stepper, Lien Information (collapsible), Subject Information (collapsible), Offers (sell mode, collapsible)
5. Right column: Financial Summary, Important Dates, Communications (contacts + compose), Status Summary
6. Grid ratio: ~70/30 split with auto-sizing divider column

Communication-focused right panel:
- Communications section with placeholder contacts (case manager, law firm)
- Compose New Email button
- Status summary card
- Financial overview for quick operational reference

Expand/collapse between panels:
- Two directional buttons centered vertically between columns
- Three states: split (default), left-expanded, right-expanded
- Active button highlighted with primary color
- Directional arrows flip to show restore action
