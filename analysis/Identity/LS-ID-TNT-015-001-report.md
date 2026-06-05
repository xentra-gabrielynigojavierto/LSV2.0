# LS-ID-TNT-015-001 — Full UI Coverage for Permission-Aware Product UX

## 1. Executive Summary

Completed. Permission gates have been added to the four remaining high-value client-component action surfaces — two in CareConnect (appointment status management, appointment booking) and two in Fund (submit-to-funder). All share the primitives delivered in LS-ID-TNT-015-004: `usePermission`, `PermissionTooltip`, `DisabledReasons`, and `ForbiddenBanner`. TypeScript compiles clean; no runtime console errors.

## 2. Codebase Analysis

### Products and their current permission model
| Product | Model | Where |
|---|---|---|
| CareConnect | `usePermission` + role flags from `requireOrg()` | referral and appointment surfaces |
| Fund | `usePermission` + role flags from `requireOrg()` / `session.productRoles` | application surfaces |
| Lien | `useRoleAccess().can()` — role-only, self-consistent | all lien/case surfaces |
| Insights | No permission codes in `PermissionCodes` enum | reports, schedules |

### Pattern from LS-ID-TNT-015-004 (unchanged)
- **Role absent** → component hidden (returns null or route-level redirect)
- **Role present + ALL perms absent** → `ForbiddenBanner` in place of action buttons
- **Role present + SOME perms absent** → disabled-with-tooltip (via `PermissionTooltip`) for blocked actions; enabled actions remain clickable
- **Role present + ALL perms present** → fully interactive

## 3. Existing Coverage Baseline (before this ticket)

| Surface | Permission gate |
|---|---|
| `ReferralStatusActions` | `CC.ReferralAccept`, `CC.ReferralDecline`, `CC.ReferralUpdate` |
| `ReviewDecisionPanel` (Fund) | `Fund.ApplicationApprove`, `Fund.ApplicationDeny` |
| Provider detail "Create Referral" CTA | `CC.ReferralCreate` (PermissionTooltip) |
| `ActionMenu` | `disabledReason` field (infrastructure, not permission-specific) |

## 4. Remaining Action Surface Inventory (pre-implementation)

### CareConnect — NOT permission-gated before this ticket
| Surface | Role gate | Permission gap |
|---|---|---|
| `AppointmentActions` (Confirm/Complete/NoShow/Reschedule) | `isReceiver + status` | `CC.AppointmentUpdate` / `CC.AppointmentManage` unused |
| `AppointmentCancelButton` | `(isReferrerOfAppt || isReceiverOfAppt)` in parent page | `CC.AppointmentUpdate` unused |
| `BookingPanel` "Confirm Booking" submit | `isReferrer` (caller-side) | `CC.AppointmentCreate` unused |
| Appointments list "Book Appointment" link | `isReferrer` server-side | Server component — role gate is sufficient |

### Fund — NOT permission-gated before this ticket
| Surface | Role gate | Permission gap |
|---|---|---|
| `SubmitApplicationPanel` "Submit to Funder" | `isReferrer + status === Draft` (caller) | `Fund.ApplicationRefer` unused |
| Applications list "New Application" header | `isReferrer` server-side | Server component — role gate is sufficient |

### Lien — Role model is already self-consistent
`useRoleAccess().can()` gates all lien/case mutations (`lien:create`, `lien:edit`, `case:create`, `case:edit`). These are role-based rules defined centrally and are already granular. Adding `usePermission` on top would create a dual-gating system that is redundant and could diverge — intentionally NOT changed.

### Insights — No permission codes defined
No entries in `PermissionCodes` for Insights (Run/Export/Customize/Schedule). Documented as gap; not fabricated. Requires separate ticket to define and wire permission codes before UI gating is possible.

## 5. Coverage Scope Selection

| Surface | Action | Permission code | Pattern |
|---|---|---|---|
| `AppointmentActions` | Confirm, Complete, NoShow | `CC.AppointmentUpdate` | ForbiddenBanner / disabled-with-tooltip |
| `AppointmentActions` | Reschedule | `CC.AppointmentManage` | disabled-with-tooltip |
| `AppointmentCancelButton` | Cancel | `CC.AppointmentUpdate` | disabled-with-tooltip |
| `BookingPanel` | Confirm Booking (submit) | `CC.AppointmentCreate` | disabled-with-tooltip |
| `SubmitApplicationPanel` | Submit to Funder | `Fund.ApplicationRefer` | disabled-with-tooltip |

## 6. Shared Pattern Reuse Strategy

All four surfaces import and reuse the existing primitives from 015-004:

```typescript
import { usePermission }     from '@/hooks/use-permission';
import { PermissionCodes }   from '@/lib/permission-codes';
import { PermissionTooltip } from '@/components/ui/permission-tooltip';
import { DisabledReasons }   from '@/lib/disabled-reasons';
import { ForbiddenBanner }   from '@/components/ui/forbidden-banner';
```

No new primitives were needed.

## 7. Files Changed

| File | Change type | Description |
|---|---|---|
| `apps/web/src/components/careconnect/appointment-actions.tsx` | Modified | Added `usePermission(CC.AppointmentUpdate/AppointmentManage)`, role-split variables, `ForbiddenBanner` for all-perm-absent, `PermissionTooltip` per button |
| `apps/web/src/components/careconnect/appointment-cancel-button.tsx` | Modified | Added `usePermission(CC.AppointmentUpdate)`, `PermissionTooltip` on cancel button |
| `apps/web/src/components/careconnect/booking-panel.tsx` | Modified | Added `usePermission(CC.AppointmentCreate)`, `PermissionTooltip` on submit button |
| `apps/web/src/components/fund/submit-application-panel.tsx` | Modified | Added `usePermission(Fund.ApplicationRefer)`, `PermissionTooltip` on submit button |

## 8. Frontend Implementation

### AppointmentActions

**New role-split logic:**
```typescript
const canApptUpdatePerm = usePermission(PermissionCodes.CC.AppointmentUpdate);
const canApptManagePerm = usePermission(PermissionCodes.CC.AppointmentManage);

const roleCanConfirm    = isReceiver && ['Scheduled', 'Pending', 'Rescheduled'].includes(status);
const roleCanComplete   = isReceiver && status === 'Confirmed';
const roleCanNoShow     = isReceiver && status === 'Confirmed';
const roleCanReschedule = isReceiver && ['Scheduled', 'Pending', 'Confirmed'].includes(status);

const canConfirm    = roleCanConfirm    && canApptUpdatePerm;
const canComplete   = roleCanComplete   && canApptUpdatePerm;
const canNoShow     = roleCanNoShow     && canApptUpdatePerm;
const canReschedule = roleCanReschedule && canApptManagePerm;

const hasAnyRoleAccess = roleCanConfirm || roleCanComplete || roleCanNoShow || roleCanReschedule;
const hasAnyPermAccess = canConfirm     || canComplete     || canNoShow     || canReschedule;
```

**Rendering:**
- `!hasAnyRoleAccess` → `return null` (unchanged from before)
- `hasAnyRoleAccess && !hasAnyPermAccess` → `<ForbiddenBanner action="manage this appointment" />`
- Otherwise → each `roleCanXxx` button shown, `disabled={!!loading || !canXxx}`, wrapped in `PermissionTooltip show={!canXxx}`
- Reschedule modal guarded with `{showReschedule && canReschedule && ...}` so it cannot open when perm absent

### AppointmentCancelButton

```typescript
const canCancelPerm = usePermission(PermissionCodes.CC.AppointmentUpdate);
```
- Cancel section always visible (not hidden) — feature discovery UX
- "Cancel Appointment" button: `disabled={!canCancelPerm}`, wrapped in `PermissionTooltip`
- `onClick` guard: `if (canCancelPerm) setConfirming(true)` prevents opening confirmation dialog via keyboard

### BookingPanel

```typescript
const canBookPerm = usePermission(PermissionCodes.CC.AppointmentCreate);
```
- Form remains fully visible and fillable (slot details review)
- "Confirm Booking" submit button: `disabled={loading || !canBookPerm}`, wrapped in `PermissionTooltip`

### SubmitApplicationPanel

```typescript
const canReferPerm = usePermission(PermissionCodes.Fund.ApplicationRefer);
```
- Panel and funder-ID form remain visible
- "Submit to Funder" button: `disabled={loading || !canReferPerm}`, wrapped in `PermissionTooltip`

## 9. Backend Alignment / Error Handling

Each component already handled `ApiError.isForbidden` for the case where the backend 403s (race condition where frontend perm state is stale). These error paths are preserved unchanged, providing a second safety net.

## 10. Testing Results

- `npx tsc --noEmit` — 0 errors
- `Start application` workflow — running, all services up
- Browser console — 0 errors; only Fast Refresh rebuild confirmations

## 11. Known Issues / Gaps

### Not covered in this ticket (intentional)

| Gap | Reason |
|---|---|
| Insights Run/Export/Customize/Schedule | No `PermissionCodes.Insights` entries defined; cannot gate without backend permission definitions |
| Lien/Case mutation buttons (`lien:create`, `case:edit`, etc.) | Role-based model via `useRoleAccess().can()` is self-consistent and already granular; dual-gating would diverge and add no value |
| Server-rendered list page CTAs (Appointments "Book Appointment", Applications "New Application") | Server components cannot call `usePermission`; role gate from `requireOrg()` is the correct layer |
| Fund payouts/processing/underwriting | `BlankPage` stubs — no action surfaces to gate |
| `AppointmentActions` ForbiddenBanner placement | Inline in the actions card; not a full-page banner. Intentional: actions card is not the only content on the page |

## 12. Final Status

**COMPLETE.** All in-scope client-component action surfaces have been permission-gated using the shared primitives from LS-ID-TNT-015-004. Zero regressions to existing coverage. Gaps documented with clear reasoning.
