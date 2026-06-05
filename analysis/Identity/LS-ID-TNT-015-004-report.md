# LS-ID-TNT-015-004 — Disabled State Explainability

## 1. Executive Summary

LS-ID-TNT-015-004 introduces shared UX primitives and a standard disabled-reason model so that product users understand why an action is unavailable and what to do next, while preserving backend enforcement as the sole authority for access control.

**Problem addressed:** After LS-ID-TNT-015 added permission-aware product UI, users in partial-permission scenarios (role grants access to a panel but only some action-level permissions are held) saw some buttons silently disappear with no explanation. Fully-blocked scenarios received a `ForbiddenBanner`, but the gap between "all allowed" and "none allowed" was invisible.

**What was built:**
- `disabled-reasons.ts` — shared reason model with typed categories, a builder utility, and pre-built factory functions for the most common product-UI disabled scenarios
- `PermissionTooltip` — CSS tooltip wrapper that activates on hover and keyboard focus-within, renders with zero DOM overhead when not needed
- `ActionMenu` — extended with `disabledReason?: string` on menu items; disabled items render an inline hint below their label
- `ReviewDecisionPanel` (Fund) — partial-permission Approve/Deny now shows both buttons; the blocked one is disabled-with-tooltip instead of hidden
- `ReferralStatusActions` (CareConnect) — all role-applicable action buttons now visible; blocked ones are disabled-with-tooltip; ForbiddenBanner retained for the fully-blocked case
- `ProviderDetailPage` (CareConnect) — existing `title`-based tooltip upgraded to the shared `PermissionTooltip` for consistency and keyboard access

**Backend enforcement is unchanged.** The frontend changes are purely UX-layer explainers.

---

## 2. Codebase Analysis

### Frontend permission surfaces (pre-existing from LS-ID-TNT-015)

| Hook / Utility | Location | Behaviour |
|---|---|---|
| `usePermission(code)` | `hooks/use-permission.ts` | Returns `true` when code is in the session's `permissions` array; fail-open when array is empty (old token); admin bypass for `isPlatformAdmin` / `isTenantAdmin` |
| `useRoleAccess()` → `ra.can(action)` | `hooks/use-role-access.ts` + `lib/role-access/` | Role-level access for Liens product; predates permission model |
| `PermissionCodes` | `lib/permission-codes.ts` | Canonical frontend catalog mirroring backend `BuildingBlocks.Authorization.PermissionCodes` |

### Key components

| Component | Product | Pattern (pre-015-004) |
|---|---|---|
| `ForbiddenBanner` | Shared | Read-only notice; shown when ALL actions blocked |
| `ReferralStatusActions` | CareConnect | Hide when perm missing; ForbiddenBanner when all missing |
| `ReviewDecisionPanel` | SynqFund | Hide when perm missing; ForbiddenBanner when all missing |
| `ActionMenu` | Liens | Hide item when role check fails |
| Provider detail page CTA | CareConnect | Native `title` attribute for workflow-state block |

---

## 3. Existing Permission-Aware UI Analysis

### Identified gaps (pre-015-004)

**Partial permission scenario (the core gap):**
When a user's role grants access to an action panel (`hasAnyRoleAccess = true`) but only *some* of the individual action permissions are held, the buttons for the missing permissions are silently omitted. The user sees a subset of buttons with no explanation that others exist but are blocked.

Examples:
- Fund funder with `application:decline` but not `application:approve` → Approve button invisible
- CareConnect receiver with `referral:decline` but not `referral:accept` → Accept button invisible
- Lien case list: `Advance Status` item omitted for users without `case:edit`; they don't know the workflow step exists

**Already adequate:**
- Fully-blocked scenario: `ForbiddenBanner` provides a clear notice — retained as-is
- Non-applicable role scenario: panel hidden entirely (`return null`) — correct, no change needed
- Workflow-state block on provider CTA: `title` tooltip existed but was not keyboard-accessible

---

## 4. Existing Tooltip / Banner / Action Component Analysis

| Component | Location | Verdict |
|---|---|---|
| `ForbiddenBanner` | `components/ui/forbidden-banner.tsx` | Adequate for fully-blocked case; retained |
| `ActionMenu` | `components/lien/action-menu.tsx` | Had `disabled?: boolean` but no explanation surface |
| Button `title` (provider page) | `app/(platform)/careconnect/providers/[id]/page.tsx` | Works for mouse users only; inconsistent styling |

No shared tooltip component existed prior to this ticket. All existing tooltip usage was native browser `title` or ad-hoc.

---

## 5. Coverage Scope Selection

### Included in this rollout

| Surface | Product | Scenario addressed |
|---|---|---|
| `ReviewDecisionPanel` | SynqFund | Partial Approve/Deny perm — disabled-with-tooltip for the blocked action |
| `ReferralStatusActions` | CareConnect | Partial Accept/Decline/InProgress/Cancel perm — disabled-with-tooltip |
| Provider CTA | CareConnect | Workflow-state block (not accepting referrals) — upgraded to PermissionTooltip |
| `ActionMenu` | Liens (shared) | Infrastructure: `disabledReason` field added; call sites can opt in |

### Rationale for scope

- **Fund + CareConnect:** These were the surfaces modified in LS-ID-TNT-015 and have clearly decomposed role + permission gates, making the partial-permission scenario concrete and high-value.
- **ActionMenu:** Adding `disabledReason` to the interface is low-risk infrastructure. The Liens-specific pages use role-only gates (`ra.can`) which are binary — a user will never gain that role via a permission grant — so they are not converted to disabled-with-reason in this ticket (hiding still adds appropriate UX friction for role changes).
- **Tenant Portal / Control Center:** Out of scope per the hard scope boundary in the build instructions.

### Explicitly excluded

- Tenant Portal permission-management UI
- Control Center governance UI
- Route-level access gates (these are enforced server-side and do not produce action buttons)
- Any backend permission logic

---

## 6. Explainability Design Rules

### When to hide

Hide an action when its visibility adds **no user value**:
- The user's role does not grant access to the workflow at all (e.g., a buyer in the lien product seeing seller-only actions)
- The record is in a terminal state and no further action is possible for any user
- The panel itself is not rendered for this product/role combination

### When to show as disabled-with-tooltip

Disable and explain when **role qualifies but a specific permission is missing**:
- The user's role gives them access to the action surface (panel renders)
- At least one other action in the same surface is enabled (`hasAnyPermAccess = true`)
- Showing the disabled action helps the user understand that the feature exists and who to contact

### When to show ForbiddenBanner

Show the read-only notice when **the user has the role but ALL action permissions are absent**:
- The panel renders (role qualifies) but every action in it is blocked
- A group of disabled buttons without any enabled action would be more confusing than a single clear notice
- The ForbiddenBanner provides a single scannable message and next-step guidance

### Consistency rule

A surface must not mix hide and disable-with-tooltip for the same class of action. Within each surface, the rule is applied uniformly once the partial-permission condition is identified.

---

## 7. Files Changed

| File | Change type | Notes |
|---|---|---|
| `apps/web/src/lib/disabled-reasons.ts` | **Created** | Reason model, builder, factory functions |
| `apps/web/src/components/ui/permission-tooltip.tsx` | **Created** | CSS tooltip wrapper; zero overhead when `show=false` |
| `apps/web/src/components/lien/action-menu.tsx` | **Updated** | Added `disabledReason?: string` to `ActionMenuItem`; inline hint rendered below disabled items |
| `apps/web/src/components/fund/review-decision-panel.tsx` | **Updated** | Approve/Deny always rendered in InReview state; disabled-with-tooltip for blocked action |
| `apps/web/src/components/careconnect/referral-status-actions.tsx` | **Updated** | All role-applicable buttons rendered; disabled-with-tooltip for blocked ones |
| `apps/web/src/app/(platform)/careconnect/providers/[id]/page.tsx` | **Updated** | Provider CTA tooltip upgraded from native `title` to `PermissionTooltip` |

---

## 8. Shared Component / Utility Implementation

### `apps/web/src/lib/disabled-reasons.ts`

```
DisabledReasonCode  (union type)
  'missing_permission' | 'workflow_state' | 'missing_data'
  | 'external_dependency' | 'system_managed' | 'temporary_unavailable'

DisabledReason  (interface)
  code:      DisabledReasonCode
  message:   string         — human-readable, non-technical
  detail?:   string
  nextStep?: string

buildDisabledReason(code, overrides?)  → DisabledReason
  Builds a reason with default message for the given code, allowing overrides.

DisabledReasons  (const factory object)
  .noPermission(action?)    — "You do not have permission to {action}."
  .wrongStatus(message?)    — "This action is not available in the current status."
  .externalBlock(message?)  — "This action depends on an external process completing first."
  .missingData(message?)    — "Complete the required information before continuing."
```

All messages are intentionally non-technical and safe to display to any product user. Internal permission codes and role names are never surfaced.

### `apps/web/src/components/ui/permission-tooltip.tsx`

```
Props:
  show:      boolean   — true activates the wrapper; false renders children with no overhead
  message:   string    — tooltip copy
  children:  ReactNode
  className?: string

Accessibility:
  - Outer <span> is focusable (tabIndex=0), labelled with aria-label={message}
  - <span role="tooltip"> appears on group-hover and group-focus-within
  - Works for both pointer (mouse/touch) and keyboard users
  - Tailwind: group-hover:opacity-100 group-focus-within:opacity-100
  - z-index 50; bottom-positioned with a downward arrow; max-width 220px
```

### ActionMenu `disabledReason`

```
ActionMenuItem extended with:
  disabledReason?: string

When disabled=true and disabledReason is set:
  - Renders a <span> below the label with text-xs text-gray-400
  - No tooltip (the dropdown itself is the visible layer; inline text is cleaner)
  - Clicking a disabled item does nothing (onClick guarded by !item.disabled check)
  - aria-disabled={item.disabled} for assistive technology
```

---

## 9. Product UI Implementation

### SynqFund — ReviewDecisionPanel

**Before:** `showDecisions = status === 'InReview' && (canApprove || canDecline)` — blocked action hidden.

**After:**
- `showDecisions = status === 'InReview'` — always rendered when status matches
- `decisionsFullyBlocked = status === 'InReview' && !canApprove && !canDecline` — ForbiddenBanner only when both absent
- Approve wrapped in `<PermissionTooltip show={!canApprove}>` + `disabled={!canApprove}`
- Deny wrapped in `<PermissionTooltip show={!canDecline}>` + `disabled={!canDecline}`

**Scenarios:**
| canApprove | canDecline | UI |
|---|---|---|
| ✓ | ✓ | Both buttons enabled (unchanged from LS-ID-TNT-015) |
| ✓ | ✗ | Approve enabled; Deny disabled-with-tooltip (**new**) |
| ✗ | ✓ | Approve disabled-with-tooltip (**new**); Deny enabled |
| ✗ | ✗ | ForbiddenBanner (unchanged from LS-ID-TNT-015) |

### CareConnect — ReferralStatusActions

**Before:** Each button rendered only when `canXxx` (role AND perm). Partial-perm buttons silently absent.

**After:**
- Section renders when `roleCanXxx` (role + status gate, no perm check)
- Button enabled when `canXxx` (role AND perm)
- Button disabled-with-tooltip when `roleCanXxx && !canXxx`
- ForbiddenBanner retained for `!hasAnyPermAccess` (all perms missing)
- Decline notes flow (`showDeclineNotes`) only reachable when `canDecline` (perm check in button `onClick`)

**Covered actions:**
| Action | Role gate | Perm gate | Disabled-with-tooltip when |
|---|---|---|---|
| Accept Referral | `isReceiver + status in [New/NewOpened/Received/Contacted]` | `CC.ReferralAccept` | role✓, perm✗ |
| Mark In Progress | `isReceiver + status == Accepted` | `CC.ReferralUpdateStatus` | role✓, perm✗ |
| Decline | `isReceiver + status in [New…Accepted]` | `CC.ReferralDecline` | role✓, perm✗ |
| Cancel Referral | `(isReferrer or isReceiver) + !terminal` | `CC.ReferralCancel` | role✓, perm✗ |

### CareConnect — Provider Detail Page (CTA)

**Before:** `<button title="This provider is not currently accepting referrals">` (mouse-only; inconsistent).

**After:**
```jsx
<PermissionTooltip
  show={!provider.acceptingReferrals}
  message={DisabledReasons.externalBlock('This provider is not currently accepting referrals.').message}
>
  <button disabled={!provider.acceptingReferrals}>Create Referral</button>
</PermissionTooltip>
```

This is a `workflow_state` / `external_dependency` block rather than a permission block, demonstrating that `PermissionTooltip` applies broadly to any disabled-state explanation.

---

## 10. Backend Alignment / Error Handling

### Backend authority is unchanged

All permission checks in the UI are UX-only. The backend (LS-ID-TNT-012) enforces permissions authoritatively on every API call regardless of what the UI shows or hides.

### 403 handling preserved

`doUpdate` in `ReferralStatusActions` and `handleApiError` in `ReviewDecisionPanel` continue to catch `ApiError.isForbidden` and display `"You do not have permission to perform this action."` — this covers the stale-session case where a user's permissions are downgraded mid-session and they attempt an action the UI still shows as enabled (fail-open edge case with old tokens).

### No duplicate/conflicting messages

- When a button is disabled-with-tooltip, it cannot be clicked, so no API error is possible from that button.
- When a user has a valid session and permission, the button is enabled, and any backend denial surfaces through the existing `error` state in the component.
- The ForbiddenBanner and disabled-with-tooltip are mutually exclusive: ForbiddenBanner shows only when `!hasAnyPermAccess`, and disabled-with-tooltip buttons appear only within the `hasAnyPermAccess` render path.

---

## 11. Testing Results

### TypeScript
`npx tsc --noEmit` — **zero errors** across all changed files and their dependents.

### Shared primitives
- `PermissionTooltip` with `show=false` renders `<>{children}</>` — verified zero DOM overhead.
- `PermissionTooltip` with `show=true` renders `<span class="relative inline-flex group">` with inner tooltip span.
- `DisabledReasons.noPermission('approve applications')` → `"You do not have permission to approve applications."`
- `DisabledReasons.externalBlock(msg)` → correct message and `external_dependency` code.
- `buildDisabledReason('missing_permission', { message: 'Custom' })` → overrides default correctly.

### ReviewDecisionPanel scenarios (manual trace)
- `status=InReview, canApprove=true, canDecline=true` → both buttons enabled — same as pre-015-004 ✓
- `status=InReview, canApprove=true, canDecline=false` → Approve enabled; Deny disabled-with-tooltip ✓ (new)
- `status=InReview, canApprove=false, canDecline=false` → `decisionsFullyBlocked=true`; ForbiddenBanner shown; decision buttons hidden ✓
- `status=Submitted, canEvaluate=true` → Begin Review shown ✓
- `status=Submitted, canEvaluate=false` → `beginReviewFullyBlocked=true`; ForbiddenBanner shown ✓

### ReferralStatusActions scenarios (manual trace)
- `roleCanAccept=true, canAcceptPerm=true` → Accept enabled ✓
- `roleCanAccept=true, canAcceptPerm=false, hasAnyPermAccess=true` → Accept disabled-with-tooltip ✓ (new)
- `!hasAnyRoleAccess` → `return null` (panel not rendered) ✓
- `hasAnyRoleAccess=true, !hasAnyPermAccess` → ForbiddenBanner shown ✓
- `roleCanCancel=true, canCancelPerm=false` → Cancel Referral disabled-with-tooltip; confirmation dialog unreachable ✓

### ActionMenu regression
- Existing `disabled` items still show `opacity-40 cursor-not-allowed` ✓
- Items without `disabledReason` show only the label (no empty hint line) ✓
- `disabledReason` present on a disabled item renders hint text below the label ✓
- Clicking a disabled item no longer invokes `onClick` (guard added) ✓

### Provider CTA
- `acceptingReferrals=false` → tooltip renders on hover/focus ✓
- `acceptingReferrals=true` → `PermissionTooltip show=false`; no wrapper; button enabled ✓

---

## 12. Known Issues / Gaps

### Not covered in this rollout

| Surface | Why deferred |
|---|---|
| Lien case / lien pages — `Advance Status` disabled reason | Lien access is role-only (`ra.can`); role is binary and cannot be granted via a permission; hiding is appropriate UX for role absence |
| Tenant Portal permission UI | Out of scope per hard boundary |
| Control Center governance UI | Out of scope per hard boundary |
| Bulk action surfaces | No bulk actions are permission-gated in the current product scope |
| Read-only page/section banners (e.g., case detail, application detail) | No current surface in scope has the "view allowed but edit blocked" pattern at the full-page level; can be added in a follow-on ticket |

### Tailwind named-group caveat

`PermissionTooltip` uses the unnamed `group` / `group-hover` / `group-focus-within` Tailwind classes (not Tailwind v3.2 named groups like `group/tooltip`). This is consistent with existing usage in the codebase and works correctly in all current usages. If a future consumer nests `PermissionTooltip` inside another `group`-bearing element, the inner `group` will override the outer for hover, but the tooltip will still work correctly since the wrapper is the innermost `group`.

### Fail-open edge case

`usePermission` is fail-open when `session.permissions` is empty (old token). In that state all permission checks return `true` and buttons are enabled. If the backend denies the call, the existing `isForbidden` error path surfaces the denial. This is documented and intentional from LS-ID-TNT-015.

---

## 13. Final Status

**Complete.**

### Shared explainability primitives
- ✅ `disabled-reasons.ts` — reason model (`DisabledReasonCode`), builder (`buildDisabledReason`), factories (`DisabledReasons.*`)
- ✅ `PermissionTooltip` — CSS tooltip wrapper; hover + focus-within; zero overhead when inactive
- ✅ `ActionMenu.disabledReason` — inline hint text below disabled menu items

### Hide / disable / read-only rules
- ✅ Documented and applied consistently: hide when role absent, disable-with-tooltip when role✓ + perm✗ + other perms exist, ForbiddenBanner when role✓ + all perms✗

### Product UI explainability
- ✅ SynqFund `ReviewDecisionPanel` — partial Approve/Deny perm scenario resolved
- ✅ CareConnect `ReferralStatusActions` — partial Accept/Decline/InProgress/Cancel perm scenario resolved
- ✅ CareConnect provider detail — workflow-state CTA block upgraded to shared tooltip

### Backend alignment
- ✅ Backend 403 handling preserved in all modified components
- ✅ No duplicate or conflicting messages; ForbiddenBanner and disabled-with-tooltip are mutually exclusive

### Backward compatibility
- ✅ TypeScript: zero errors
- ✅ LS-ID-TNT-015 permission-aware UI behavior unchanged for authorized users
- ✅ ForbiddenBanner retained for fully-blocked scenarios
- ✅ Route-level product access enforcement unchanged
- ✅ Tenant Portal and Control Center UIs untouched
- ✅ Invite / activation / password reset flows untouched
