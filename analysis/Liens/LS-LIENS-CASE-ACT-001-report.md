# LS-LIENS-CASE-ACT-001 — Case Details Action Enablement

## Objective
Enable real, usable edit functionality for Plaintiff Info and Case Tracking sections within Case Detail → Details tab.

## Scope
- Plaintiff Info: inline edit with real API persistence
- Case Tracking: inline edit with real API persistence
- No fake persistence — only fields supported by backend are editable
- No visual redesign — action enablement only

## API / Service Discovery

### Update Path
- Single unified update: `casesService.updateCase(caseId, UpdateCaseRequestDto)`
- Full PUT replacement — must send all fields including unchanged ones
- Endpoint: `PUT /lien/api/liens/cases/{caseId}` via BFF → Gateway → SynqLiens service

### UpdateCaseRequestDto — Supported Fields
| Field | Type | Notes |
|-------|------|-------|
| `clientFirstName` | string (required) | Plaintiff first name |
| `clientLastName` | string (required) | Plaintiff last name |
| `clientPhone` | string? | Phone number |
| `clientEmail` | string? | Email address |
| `clientDob` | string? | Date of birth |
| `clientAddress` | string? | Full address (single field) |
| `status` | string? | Case status |
| `title` | string? | Case type |
| `description` | string? | Case tracking note |
| `dateOfIncident` | string? | Date of incident |
| `externalReference` | string? | External reference |
| `insuranceCarrier` | string? | Insurance carrier |
| `policyNumber` | string? | Policy number |
| `claimNumber` | string? | Claim number |
| `notes` | string? | Notes |
| `demandAmount` | number? | Demand amount |
| `settlementAmount` | number? | Settlement amount |

### Unsupported Fields (Not in Backend Contract)
| Field | Category | UI Treatment |
|-------|----------|--------------|
| Sex | Plaintiff | Disabled input "Not yet supported" in edit; "---" in read |
| City/State/Zip (separate) | Plaintiff | Backend uses single `clientAddress`; not split |
| Tracking Follow Up | Case Tracking | Disabled input "Not yet supported" |
| Current Medical Status | Case Tracking | Disabled input "Not yet supported" in edit; "---" in read |
| State of Incident | Case Tracking | Disabled input "Not yet supported" in edit; "---" in read |
| Lead | Case Tracking | Disabled input "Not yet supported" in edit; "---" in read |
| Case Flags (5 toggles) | Case Tracking | All disabled checkboxes with "Not yet supported" label |

## Implementation

### T001 — Plaintiff Info Edit Flow
- **Edit trigger**: Pencil icon on CollapsibleSection header, role-gated by `canEdit` (requires `case:edit` permission)
- **Prefill**: All editable fields populated from current `CaseDetail`
- **Editable fields**: First Name, Last Name, Phone, Email, Birthdate, Address
- **Disabled fields**: Sex (shown in edit mode as disabled placeholder)
- **Save**: Calls `casesService.updateCase()` with full DTO (all unchanged fields echoed)
- **Cancel**: Resets form via `resetPlaintiffForm()`, clears errors
- **Files**: `case-detail-client.tsx` (DetailsTab component)

### T002 — Plaintiff Info Validation
- First Name: required (blocks save if empty)
- Last Name: required (blocks save if empty)
- Email: format validation (`/^[^\s@]+@[^\s@]+\.[^\s@]+$/`)
- Phone: format validation (`/^[\d\s()+-]{7,20}$/`)
- Inline error messages shown below each field

### T003 — Case Tracking Edit Flow
- **Edit trigger**: Same pattern — pencil icon, role-gated
- **Prefill**: Status, Case Type (title), Date of Incident, Case Tracking Note (description)
- **Editable fields**: Case Status (dropdown), Case Type, Date of Incident, Case Tracking Note (textarea)
- **Disabled fields**: Tracking Follow Up, Current Medical Status, State of Incident, Lead
- **Save**: Same unified API call
- **Cancel**: Resets form via `resetTrackingForm()`, clears errors

### T004 — Case Tracking Validation
- Date of Incident: format validation (accepts MM/DD/YYYY and MMM D, YYYY)
- Inline error message shown below field

### T005 — Loading, Success, Error Behavior
- **Loading**: Save button shows spinner + "Saving..." text
- **Duplicate submit prevention**: Save button disabled while `pSaving`/`tSaving` is true
- **Success**: Toast notification ("Plaintiff Updated" / "Case Tracking Updated"), edit mode closed, data refreshed
- **Error**: Toast notification with error message from `ApiError`, edit mode stays open, user input preserved

### T006 — Data Refresh Strategy
- `onCaseUpdated(updated)` callback passes the fresh `CaseDetail` from API response directly to parent state
- No page reload, no stale data — immediate in-place update
- All read-only display fields reflect new values instantly

### T007 — Role / Permission
- Edit pencil only rendered when `canEdit` is true (via `useRoleAccess().can('case:edit')`)
- Backend remains authoritative — unauthorized API calls will fail with proper error toast

### T008 — Case Flags
- 5 flags shown as disabled checkboxes: Share with Law Firm, UCC Filed, Case Dropped, Child Support, Minor Comp
- All have `opacity-50 cursor-not-allowed` styling
- Section header includes "Not yet supported" label
- These are read-only placeholders until backend schema supports them

## Files Changed
| File | Change |
|------|--------|
| `apps/web/src/app/(platform)/lien/cases/[id]/case-detail-client.tsx` | Full edit flows for Plaintiff Info and Case Tracking with validation, save/cancel, loading states, error handling, role gating, and unsupported field placeholders |

## Validation Results
- TypeScript: Clean build, no errors
- Plaintiff Info save: end-to-end via real API
- Case Tracking save: end-to-end via real API
- Validation blocks save on invalid input
- Save/Cancel behavior correct
- Success/error toasts shown
- Updated values appear immediately after save
- Role gating works (no edit buttons without `case:edit`)
- No regressions to other tabs
- Unsupported fields honestly shown as disabled/"---"

## Code Review Adjustments
- **DOB date validation**: Added client-side date format validation for Plaintiff Birthdate (`pDob`) — accepts MM/DD/YYYY and MMM D, YYYY formats, with inline error message and save blocking
- **State of Incident**: Added as read-only "---" field in read view and disabled "Not yet supported" placeholder in edit view
- **Current Medical Status / Lead**: Added as disabled "Not yet supported" placeholders in edit view (already present in read view)
- **Sex field in edit mode**: Added as disabled "Not yet supported" placeholder in Plaintiff edit form

## Backend Field Support Summary
- **Fully wired**: clientFirstName, clientLastName, clientPhone, clientEmail, clientDob, clientAddress, status, title, description, dateOfIncident
- **Not yet supported**: sex, city/state/zip (split), trackingFollowUp, currentMedicalStatus, stateOfIncident, lead, caseFlags (5 toggles)
