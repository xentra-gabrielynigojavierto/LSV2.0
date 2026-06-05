# LS-ID-TNT-015 — Permission-Aware Product UI

## 1. Executive Summary

LS-ID-TNT-015 makes the product UI permission-aware by closing the gap between backend authorization enforcement (LS-ID-TNT-012) and frontend UX. The backend was already issuing `permissions` claims in JWT tokens and enforcing them — but the frontend had no access to those permission codes, so it could not hide or disable unavailable actions before users attempted them.

This feature:
1. **Surfaces permission codes to the frontend session** via a minimal, non-breaking backend addition: `GetCurrentUserAsync` now extracts the `permissions` JWT claims and includes them in `AuthMeResponse`. `PlatformSession` and the session provider are updated to carry `permissions: string[]`.
2. **Introduces a shared `usePermission(code)` hook** (fail-open, admin-bypass) that product UIs can use for UX-layer gating. Companion hooks `usePermissions()`, `useAllPermissions()`, and `useAnyPermission()` are also provided.
3. **Introduces a canonical `PermissionCodes` constant** in the frontend (mirroring `BuildingBlocks.Authorization.PermissionCodes`) to prevent magic-string usage.
4. **Introduces a shared `ForbiddenBanner` component** for standardized permission-denied messaging.
5. **Applies permission-aware UI** to CareConnect (`ReferralStatusActions`) and SynqFund (`ReviewDecisionPanel`) — the two highest-value product action surfaces identified during codebase analysis.
6. **Fixes a pre-existing TypeScript error** in `PermissionsClient.tsx` (destructuring `ApiResponse` wrapper incorrectly) discovered during the compilation check.

Backend enforcement (LS-ID-TNT-012) remains authoritative. Frontend checks are UX-layer only: fail-open when permissions are absent (stale token) so authorized users are never falsely blocked.

---

## 2. Codebase Analysis

### App structure
- **Tenant Portal** (`apps/web/`) — all product UIs live here under `src/app/(platform)/`
  - `lien/` — SynqLien: cases, liens, servicing, contacts, documents, marketplace, portfolio, BOS
  - `fund/` — SynqFund: applications, underwriting, processing, payouts, reports
  - `careconnect/` — CareConnect: referrals, appointments, providers, admin
  - `insights/` — SynqInsights: dashboard, reports, schedules
  - `tenant/` — platform admin: users, groups, roles, permissions, audit
- **Control Center** (`apps/control-center/`) — platform admin only; NOT a product UI

### Session data (before LS-ID-TNT-015)
`PlatformSession` (from `apps/web/src/types/index.ts`) contained:
- `productRoles: ProductRoleValue[]` — e.g. `["SYNQ_CARECONNECT:CARECONNECT_REFERRER"]`
- `systemRoles: SystemRoleValue[]` — e.g. `["TenantAdmin"]`
- `isPlatformAdmin`, `isTenantAdmin` — boolean flags
- `userProducts: string[]` — frontend-friendly product codes
- **MISSING: `permissions?: string[]`** — effective permission code list NOT in session

### JWT
The backend `JwtTokenService` adds `permissions` claims to the JWT (one per permission code, e.g. `SYNQ_CARECONNECT.referral:accept`). However:
- `GetCurrentUserAsync` (the `/auth/me` handler) did NOT extract `permissions` from JWT claims
- `AuthMeResponse` did NOT have a `Permissions` field
- Therefore the frontend session had no access to permission codes

### Existing role-access system
- `useRoleAccess()` hook — returns a `RoleAccessInfo` with a `can(action: LienAction)` utility for SynqLien, based on product roles
- `careconnect-access.ts` — role-based readiness check for receiver path only
- No `usePermission(code)` hook existed anywhere
- No `<RequirePermission />` or `ForbiddenBanner` existed

### Existing 403 handling
- `ApiError` class with `.isForbidden` getter — consistent across all API calls
- `ApiError.isForbidden` already caught in: `ReferralStatusActions`, `ReviewDecisionPanel`, `BookingPanel`, `PermissionsClient`
- No standard shared "ForbiddenBanner" component — handling was scattered with inconsistent phrasing

---

## 3. Existing Session / Permission Surface Analysis

### What was available before this feature
| Surface | Available in frontend |
|---|---|
| Product roles (`productRoles`) | ✅ In session |
| System roles (`systemRoles`) | ✅ In session |
| `isPlatformAdmin` / `isTenantAdmin` | ✅ Derived flags in session |
| Effective product codes (`userProducts`) | ✅ In session |
| Permission codes (`permissions`) | ❌ NOT in session — added by this feature |

### Permission codes in the JWT
The JWT contained `permissions` claims (e.g., `SYNQ_CARECONNECT.referral:accept`). They were put in by `JwtTokenService` at login time from the role→permission assignments resolved by `EffectiveAccessService`.

### What this feature added to the session pipeline
```
JWT (permissions claim) → GetCurrentUserAsync → AuthMeResponse.Permissions
                        → SessionProvider maps me.permissions
                        → PlatformSession.permissions: string[]
                        → usePermission(code) hook
                        → Component permission checks
```

---

## 4. Existing Product UI Action Analysis

### CareConnect — `ReferralStatusActions`
**File**: `apps/web/src/components/careconnect/referral-status-actions.tsx`

Current action gating was purely role-based via `isReceiver`/`isReferrer` props passed from the server component.
| Action | Previous gate | Permission gate added |
|---|---|---|
| Accept Referral | `isReceiver + status ∈ [New, Received, Contacted]` | `SYNQ_CARECONNECT.referral:accept` |
| Mark In Progress | `isReceiver + status = Accepted` | `SYNQ_CARECONNECT.referral:update_status` |
| Decline | `isReceiver + status ∈ [...]` | `SYNQ_CARECONNECT.referral:decline` |
| Cancel | `isReferrer + non-terminal` | `SYNQ_CARECONNECT.referral:cancel` |

**403 handling**: already caught `err.isForbidden` → inline error ✓

### SynqFund — `ReviewDecisionPanel`
**File**: `apps/web/src/components/fund/review-decision-panel.tsx`

Panel was only rendered when `isFunder = session.productRoles.includes(SynqFundFunder)` (in parent). No granular permission check.
| Action | Previous gate | Permission gate added |
|---|---|---|
| Begin Review | `isFunder + status=Submitted` | `SYNQ_FUND.application:evaluate` |
| Approve | `isFunder + status=InReview` | `SYNQ_FUND.application:approve` |
| Deny | `isFunder + status=InReview` | `SYNQ_FUND.application:decline` |

**403 handling**: already caught `err.isForbidden` → inline error ✓

### SynqLien — `useRoleAccess()` / `can()`
Already has a mature role-based `can()` utility. Permission-code enrichment is possible but the role-based guards are comprehensive and accurate. No reported dead-end 403 issues. Deferred.

### Insights — reports/schedules
No matching action→permission code mappings in the current catalog. Deferred.

---

## 5. Coverage Scope Selection

### Selected scope
**CareConnect** (ReferralStatusActions) + **SynqFund** (ReviewDecisionPanel)

**Rationale:**
- Highest-value: these are the primary mutation action points in both products
- Clear permission semantics: direct 1:1 mapping from action → permission code in the backend catalog
- Both components already handle 403 defensively — permission-aware UI reduces 403 frequency, not replaces 403 handling
- Both are client components that can consume a hook directly without prop-drilling

### Out of scope (this iteration)
- SynqLien: role-based `can()` is already comprehensive
- Insights: no matching action→permission mappings in current catalog
- Tenant admin: tenant-level permissions (TENANT.*) are governed separately; admin guards are server-side
- CareConnect appointments: `BookingPanel` already has `isForbidden` handling; permission gate is provider-provisioning status

---

## 6. Permission-Aware UI Design Rules

### When to hide vs disable vs read-only

| Rule | When to apply |
|---|---|
| **Hide** | The user's role means they should never see this action (e.g., Referrer seeing Accept button). Already handled by `isReceiver`/`isReferrer`. |
| **Hide** | The user lacks the specific permission AND the discoverability of the button is not helpful. Applied for: Accept, Decline, Cancel, Begin Review, Approve, Deny — when permission is absent the button is absent. |
| **ForbiddenBanner** | The user has the role (panel is shown) but lacks all permissions for the visible state — shows an amber notice explaining the restriction. |
| **Read-only + notice** | Fund panel: if user has FUNDER role and panel is rendered by parent, but lacks `evaluate`/`approve`/`decline` → shows `ForbiddenBanner` inline with specific action context. |
| **Disable** | NOT used — to avoid confusion about whether the status or the permission is the blocker. |

### Permission-denied UX
- **Inline `ForbiddenBanner`**: amber notice shown when a user has role access to a panel but lacks the specific permission. Standard phrasing: "You do not have permission to [action]. Contact your administrator if you believe this is incorrect."
- **Action-error inline banner**: existing red error div, preserved for 403 responses from the backend (stale token, direct API call). Standard phrasing already present: "You do not have permission to update this referral." / "You do not have permission to perform this action."

### `usePermission` design
- **Fail-open**: when `permissions` is empty (missing claim, old token), returns `true` → UI shows actions → backend denies if needed
- **Admin bypass**: `isPlatformAdmin || isTenantAdmin` → always returns `true`
- **No session**: returns `false` (unauthenticated)

---

## 7. Files Changed

| File | Type | Description |
|---|---|---|
| `apps/services/identity/Identity.Application/DTOs/AuthMeResponse.cs` | Modified | Added `List<string>? Permissions = null` parameter |
| `apps/services/identity/Identity.Application/Services/AuthService.cs` | Modified | `GetCurrentUserAsync`: extract `permissions` from JWT claims; pass to `AuthMeResponse` |
| `apps/web/src/types/index.ts` | Modified | Added `permissions?: string[]` to `PlatformSession` |
| `apps/web/src/providers/session-provider.tsx` | Modified | Map `me.permissions ?? []` → `session.permissions` |
| `apps/web/src/lib/permission-codes.ts` | **New** | Frontend mirror of `PermissionCodes.cs`; canonical permission code strings for CC, Lien, Fund, Tenant |
| `apps/web/src/hooks/use-permission.ts` | **New** | `usePermissions()`, `usePermission(code)`, `useAllPermissions(...codes)`, `useAnyPermission(...codes)` |
| `apps/web/src/components/ui/forbidden-banner.tsx` | **New** | Shared amber inline permission-denied notice |
| `apps/web/src/components/careconnect/referral-status-actions.tsx` | Modified | Permission-gate Accept/Decline/Cancel/InProgress; `ForbiddenBanner` when role ok but all perms missing |
| `apps/web/src/components/fund/review-decision-panel.tsx` | Modified | Permission-gate Begin Review/Approve/Deny; `ForbiddenBanner` when funder role ok but action perms missing |
| `apps/web/src/app/(platform)/tenant/authorization/permissions/PermissionsClient.tsx` | Modified | **Bug fix**: `data.permissions` → `data.data.permissions` (pre-existing destructuring error on `ApiResponse`) |

---

## 8. Frontend Implementation

### `lib/permission-codes.ts`
```typescript
export const PermissionCodes = {
  CC: { ReferralAccept: 'SYNQ_CARECONNECT.referral:accept', ... },
  Lien: { LienCreate: 'SYNQ_LIENS.lien:create', ... },
  Fund: { ApplicationEvaluate: 'SYNQ_FUND.application:evaluate', ... },
  Tenant: { UsersManage: 'TENANT.users:manage', ... },
} as const;
```
Exported as `const` objects so values are narrowed to string literals. Provides a `PermissionCode` union type for type-safe usage.

### `hooks/use-permission.ts`
```typescript
// Check a single permission
const canAccept = usePermission(PermissionCodes.CC.ReferralAccept);

// Get all permissions (for custom logic)
const perms = usePermissions();
```

**`usePermission(code)` logic:**
1. `session === null` → return `false` (unauthenticated)
2. `isPlatformAdmin || isTenantAdmin` → return `true` (admin bypass)
3. `permissions.length === 0` → return `true` (fail-open for stale/old tokens)
4. `permissions.includes(code)` → return the result

### `components/ui/forbidden-banner.tsx`
Amber inline notice. Props: `action?: string` (used in default sentence) or `message?: string` (custom text). Uses `ri-lock-line` icon from Remix Icons (consistent with the rest of the app). Includes `role="status"` for accessibility.

### `ReferralStatusActions` changes
```typescript
// Role + status gates (unchanged)
const roleCanAccept = isReceiver && statusInList;
// ...

// Permission gates layered on top (NEW)
const canAccept = roleCanAccept && usePermission(PermissionCodes.CC.ReferralAccept);
// ...

// If user has role access but no permission access at all → ForbiddenBanner
if (!hasAnyPermAccess) {
  return (
    <div ...>
      <h3>Actions</h3>
      <ForbiddenBanner action="manage this referral" />
    </div>
  );
}
```

### `ReviewDecisionPanel` changes
```typescript
const canEvaluate = usePermission(PermissionCodes.Fund.ApplicationEvaluate);
const canApprove  = usePermission(PermissionCodes.Fund.ApplicationApprove);
const canDecline  = usePermission(PermissionCodes.Fund.ApplicationDecline);

// Panel renders ForbiddenBanner if status is actionable but no permissions
const neitherActionAvailable = (status === 'Submitted' && !canEvaluate) ||
                               (status === 'InReview'  && !canApprove && !canDecline);
```

---

## 9. Backend Alignment / Error Handling

### Backend changes
- `AuthMeResponse`: added optional `Permissions` parameter (default `null` → non-breaking for any other callers)
- `GetCurrentUserAsync`: reads `permissions` multi-value claims from `ClaimsPrincipal` using `principal.FindAll("permissions")` — same pattern used for `product_roles`, `product_codes`
- No new endpoints, no schema migration, no behavior change to existing permission enforcement

### Why this is safe and non-breaking
- The `permissions` claim was already in the JWT — this only surfaces it via `/auth/me`
- `AuthMeResponse.Permissions` is optional (`null`-default) so no existing code that constructs `AuthMeResponse` is broken
- The frontend `permissions` field on `PlatformSession` is optional too
- Backend enforcement (LS-ID-TNT-012 middleware + endpoint policies) is completely untouched

### 403 handling standard after this feature
| Scenario | UX before | UX after |
|---|---|---|
| User lacks permission — action in same status | Button shown, click → 403 → inline error | Button hidden (permission check prevents render) |
| User has role, lacks all action permissions | Panel shown with only status-gated logic | Panel shows `ForbiddenBanner` with clear message |
| Stale token: permission expired mid-session | Button shows, click → 403 → inline error | Button shows (fail-open), click → 403 → inline error (same) |
| Backend rejects despite permission check passing | — | Same inline error handling already in place |

### Session freshness note
Permissions in the JWT are stamped at login time and refreshed on `access_version` bump (LS-COR-AUT-003). Between login and an access_version bump:
- Frontend shows actions based on the session permissions
- If a TenantAdmin revokes a role, `access_version` bumps → next `/auth/me` returns 401 → user is redirected to login → new JWT has updated permissions
- During the window between revocation and session refresh, the backend enforces correctly

---

## 10. Testing Results

### Build validation
- ✅ .NET services: `Build succeeded. 0 Error(s)` (13 warnings — all pre-existing)
- ✅ Next.js (web + control-center): TypeScript clean, Fast Refresh clean, no compilation errors
- ✅ `pnpm exec tsc --noEmit` in `apps/web`: zero output (zero errors)
- ✅ Pre-existing TS bug fixed: `PermissionsClient.tsx` destructuring error resolved

### Scenario validation (by inspection / logic)

**Permission codes in session**
- Backend extracts `permissions` from JWT → `AuthMeResponse.Permissions` → session provider maps → `PlatformSession.permissions`
- `usePermission()` reads from `session.permissions`

**`usePermission()` fail-open**
- `session.permissions = []` (old token, no claim) → `usePermission()` returns `true` → UI shows → backend enforces ✓
- `isPlatformAdmin = true` → `usePermission()` returns `true` regardless ✓
- `session = null` (loading) → `usePermission()` returns `false` → actions hidden until session loads ✓

**CareConnect — authorized user (has role + has permission)**
- `roleCanAccept = true`, `canAcceptPerm = true` → `canAccept = true` → Accept button visible ✓
- Backend call succeeds, referral updated ✓

**CareConnect — user with role, lacking `referral:accept` permission**
- `roleCanAccept = true`, `canAcceptPerm = false` → `canAccept = false` → Accept button hidden
- `ForbiddenBanner` shows "You do not have permission to manage this referral." ✓
- Direct API call still rejected by backend ✓

**Fund — authorized funder (has role + has evaluate permission)**
- `canEvaluate = true` → Begin Review button visible ✓
- `canApprove = true`, `canDecline = true` → both buttons visible in InReview state ✓

**Fund — funder lacking `application:approve` permission but having `application:decline`**
- `canApprove = false` → Approve button hidden
- `canDecline = true` → Deny button visible ✓
- Partial permission state handled correctly ✓

**Fund — funder lacking both approve + decline**
- `neitherActionAvailable = true` → `ForbiddenBanner` shown ✓

### Regression validation
- `useRoleAccess()` / `can()` for SynqLien — unchanged, no imports modified ✓
- `ReferralStatusActions` 403 handler — preserved (`err.isForbidden → setError(...)`) ✓
- `ReviewDecisionPanel` 403 handler — preserved ✓
- Tenant Portal permission management tab — `PermissionsClient.tsx` bug fix improves behavior ✓
- Control Center — no changes made ✓
- Auth guards (`requireAdmin`, `requirePlatformAdmin`, `requireOrg`) — no changes ✓
- Invite/activate/password reset flows — no changes ✓

---

## 11. Known Issues / Gaps

1. **Token freshness window**: Between a role/permission change and the user's next login (or `access_version` refresh), the frontend may show or hide actions based on stale permissions. This is by design — the backend enforces correctly, and the next tab-focus `/auth/me` check (LS-ID-TNT-010) catches stale `access_version` tokens promptly.

2. **SynqLien permission codes**: The `useRoleAccess()` / `can()` system is role-based, not code-based. A future iteration could layer permission codes on top (e.g., `SYNQ_LIENS.lien:create` → `can('lien:create')`) but this would require aligning the role-access action vocabulary with the backend permission codes. Deferred — no dead-end 403 issues reported for Liens.

3. **Insights actions**: No CareConnect/Fund/Lien-style action→permission code mappings exist for Insights report builder / schedule actions in the current backend catalog. Deferred until catalog is extended.

4. **`useAllPermissions` / `useAnyPermission` rest-param stability**: These hooks take a rest parameter `...codes`. The array reference changes on each render but since the codes are string literals passed inline, React memo dependencies may not trigger correctly in edge cases. These hooks are not currently used in any component; they are provided as a utility. If adopted, callers should pass a stable reference (useMemo/useCallback-wrapped array) or use `usePermission` once per code.

5. **CareConnect appointment booking**: `BookingPanel` already handles `isForbidden`. Permission-aware pre-gating of appointment booking buttons is not implemented yet — deferred to a future iteration alongside appointment permission model review.

6. **New user with no role→permission assignments**: If a user has a product role but the administrator has not assigned any permissions to that role (unusual given seeded defaults), `permissions` will be empty → fail-open → UI shows buttons → backend enforces. This is correct behavior.

---

## 12. Final Status

**COMPLETE**

### Shared frontend permission guard pattern
- `usePermission(code)` hook with fail-open and admin-bypass semantics
- `PermissionCodes` constant object (frontend mirror of `PermissionCodes.cs`)
- `ForbiddenBanner` shared component for standardized permission-denied UI

### Products/pages/actions now permission-aware
| Product | Component | Actions gated |
|---|---|---|
| CareConnect | `ReferralStatusActions` | Accept, Decline, Cancel, Mark In Progress |
| SynqFund | `ReviewDecisionPanel` | Begin Review, Approve, Deny |

### Hide / disable / read-only rules chosen
- **Hide** individual buttons when the permission is absent
- **ForbiddenBanner** (amber notice) when role qualifies the panel but all permissions for the current status are absent
- **No disable** (avoids ambiguity between status and permission as the blocker)
- Existing red error banner preserved for actual 403 backend responses

### Permission-denied UX improvement
- Before: unauthorized users clicked a button → 403 → generic red inline error
- After: unauthorized users see no action button; if role grants panel visibility, they see a clear amber notice explaining the restriction

### Backend authority preserved
- All 403-based error handling in both components is preserved
- Backend enforcement (`LS-ID-TNT-012`) is completely untouched
- Frontend guards are UX-only and fail-open

### Remaining deferred
- SynqLien permission code gating (role-based `can()` is already accurate)
- Insights action permissions (catalog not yet extended for report/schedule actions)
- CareConnect appointment booking permission pre-gating
- `useAllPermissions`/`useAnyPermission` rest-param stability (documented; not currently used)
