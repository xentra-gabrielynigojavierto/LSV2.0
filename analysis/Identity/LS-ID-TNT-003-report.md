# LS-ID-TNT-003 — User Actions (Edit + Status Control)

## 1. Executive Summary

Adds row-level action menus to the Authorization → Users list. Each row gains a "⋯" button that opens a dropdown with View, Edit, and Activate/Deactivate options. The Edit User modal allows updating a user's **role** and **phone number** — firstName, lastName, and email are read-only because no backend update endpoint exists for those fields. Activate and Deactivate actions use confirmed backend endpoints with a confirmation dialog before execution. All changes are incremental with no rewrites and no regressions to LS-ID-TNT-001 or LS-ID-TNT-002.

---

## 2. Codebase Analysis

| Layer | File | Role |
|-------|------|------|
| Page (server component) | `tenant/authorization/users/page.tsx` | Auth guard, SSR fetch, passes `tenantId` |
| Table (client component) | `tenant/authorization/users/AuthUserTable.tsx` | **Modified** — actions menu per row, Edit + Confirm modal wiring |
| Edit modal (client) | `tenant/authorization/users/EditUserModal.tsx` | **New** — role + phone edit form |
| Client API | `lib/tenant-client-api.ts` | **Modified** — added `getUserDetail`, `activateUser`, `deactivateUser`, `updatePhone` |
| Type | `types/tenant.ts` — `TenantUserDetail` | **Modified** — added `phone?: string \| null` |
| Confirm dialog | `components/lien/modal.tsx` — `ConfirmDialog` | Reused for activate/deactivate confirmation |
| Toast | `lib/toast-context.tsx` | Reused for success feedback |

---

## 3. Existing Update User Contract

**No general profile update endpoint exists** in the tenant-scoped or admin-scoped API. The `IUserService` interface only has `CreateUserAsync`, `GetAllAsync`, `GetByTenantAsync`, `GetByIdAsync` — no `UpdateAsync`.

**What CAN be updated:**

| Field | Endpoint | Method |
|-------|----------|--------|
| Role | `POST /identity/api/admin/users/{id}/roles` (assign) | Existing `assignRole` in `tenant-client-api.ts` |
| Role revoke | `DELETE /identity/api/admin/users/{id}/roles/{roleId}` | Existing `removeRole` in `tenant-client-api.ts` |
| Phone | `PATCH /identity/api/admin/users/{id}/phone` | New `updatePhone` added |

**What CANNOT be updated (no backend support):**

| Field | Note |
|-------|------|
| firstName | No update endpoint; shown read-only |
| lastName | No update endpoint; shown read-only |
| email | No update endpoint; shown read-only |

**Email editability:** NOT supported. Displayed read-only in the Edit modal.

---

## 4. Existing Status Change Contract

| Action | Endpoint | Method | Backend location |
|--------|----------|--------|------------------|
| Deactivate | `PATCH /identity/api/admin/users/{id}/deactivate` | PATCH | `AdminEndpoints.cs:1363` |
| Activate | `POST /identity/api/admin/users/{id}/activate` | POST | `AdminEndpoints.cs:3432` |

**Both endpoints:**
- Enforce `IsCrossTenantAccess(caller, user.TenantId)` — returns 403 if tenant mismatch
- Are idempotent — already-active / already-inactive returns 204 with no audit event re-emission
- Emit HIPAA-required audit events (`identity.user.deactivated`, `identity.user.activated`)
- Invalidate the notifications service cache for the tenant

**Login enforcement for inactive users:** CONFIRMED. `AuthService.cs:91` — `if (user is null || !user.IsActive) return Results.Unauthorized()`. Deactivated users are immediately blocked from logging in.

---

## 5. Existing Role Source / Contract

Role retrieval: `GET /identity/api/admin/roles` → `{ id: string; name: string }[]`  
Already implemented via `tenantClientApi.getRoles()` (from LS-ID-TNT-002).

Role assignment/revocation:
- `POST /api/admin/users/{id}/roles` with `{ roleId: string }` — already in `assignRole()`
- `DELETE /api/admin/users/{id}/roles/{roleId}` — already in `removeRole()`

**Role editing strategy (Edit User modal):**
1. On open: fetch user detail (`GET /api/admin/users/{id}`) to get current `roles: { roleId, roleName }[]`
2. Fetch roles list (`GET /api/admin/roles`) for dropdown
3. Pre-fill role from `userDetail.roles[0]?.roleId ?? ''`
4. On save (if role changed): revoke all existing roles → assign new role
5. Roles are single-select in the edit modal for simplicity

**Note:** `GET /api/admin/roles` returns platform-level roles. The backend's existing `IsCrossTenantAccess` check at the assign endpoint validates the user is in the caller's tenant; role eligibility filtering is deferred to future tasks.

---

## 6. Last Admin Protection Analysis

**Backend:** NO last-admin protection implemented in `DeactivateUser`. The handler only checks cross-tenant access and idempotency.

**Frontend:** A heuristic frontend guard is infeasible without a reliable "is-admin role" marker in `TenantUser.roles: string[]` (which contains arbitrary role names). Implementing a false-certainty guard risks false negatives if role names differ across tenants.

**Resolution:** The deactivation confirmation dialog is presented for all users. Backend errors (403, 404) are surfaced in a toast. The gap is documented below in Known Issues / Gaps.

---

## 7. Files Changed

| File | Change |
|------|--------|
| `types/tenant.ts` | Added `phone?: string \| null` to `TenantUserDetail` |
| `lib/tenant-client-api.ts` | Added `getUserDetail`, `activateUser`, `deactivateUser`, `updatePhone` |
| `tenant/authorization/users/AuthUserTable.tsx` | Added per-row actions dropdown menu; wired `EditUserModal` + `ConfirmDialog`; activate/deactivate handlers with `router.refresh()` |
| `tenant/authorization/users/EditUserModal.tsx` | **New** — Edit User modal: role dropdown + phone field, parallel fetch of user detail + roles list on open |

---

## 8. UI Implementation

### Row Actions Menu
- Column: Actions (replaces plain "View" button)
- Trigger: `ri-more-2-fill` ("⋯") button; click toggles dropdown
- Close: clicking any item, clicking outside (via fixed backdrop div)
- Items:
  - **View Profile** → navigate to detail page
  - **Edit** → opens `EditUserModal`
  - Divider
  - **Deactivate** (shown when `u.isActive`) → opens `ConfirmDialog` (variant: danger)
  - **Activate** (shown when `!u.isActive`) → opens `ConfirmDialog` (variant: primary)

### Edit User Modal
- Title: "Edit User"
- Subtitle: `{user.firstName} {user.lastName}`
- Size: `md`
- Loading skeleton shown while user detail + roles are loading
- Fields:
  - **First Name** — read-only display
  - **Last Name** — read-only display
  - **Email** — read-only display
  - **Role** — dropdown (`select`), required; pre-filled from current role
  - **Phone** — optional text input; pre-filled from `userDetail.phone`
- Footer: Cancel + "Save Changes"
- Shows API error banner on failure; form preserved

### Confirmation Dialogs
- Deactivate: "Deactivate [Name]?" / "They will immediately lose access to the platform."
- Activate: "Activate [Name]?" / "They will regain access based on their assigned role."

---

## 9. Validation Logic

| Field | Rule | Error |
|-------|------|-------|
| Role | Required (non-empty roleId) | "Please select a role." |
| Phone | Optional; if provided, passed as-is (backend normalises to E.164 or rejects) | Backend 400 surfaced in error banner |

firstName, lastName, email: no validation (read-only, not submitted).

No submit while request in-flight. API errors shown in banner inside modal, form preserved.

---

## 10. API Integration

### Edit (role update)
```typescript
// 1. Get current roles from user detail
const { data: detail } = await tenantClientApi.getUserDetail(userId);
const currentRoleIds = detail.roles.map(r => r.roleId);

// 2. Revoke existing roles (parallel)
await Promise.all(currentRoleIds.map(rid => tenantClientApi.removeRole(userId, rid)));

// 3. Assign new role
if (newRoleId) await tenantClientApi.assignRole(userId, newRoleId);
```

### Phone update
```typescript
await tenantClientApi.updatePhone(userId, phone || null);
```

### Activate / Deactivate
```typescript
await tenantClientApi.activateUser(userId);   // POST /identity/api/admin/users/{id}/activate
await tenantClientApi.deactivateUser(userId); // PATCH /identity/api/admin/users/{id}/deactivate
```

On success: `useToast().show(...)` + `router.refresh()`.  
On API error: toast with user-friendly message.

---

## 11. Tenant Scope Handling

- `tenantId` prop flows unchanged from `requireTenantAdmin()` server session
- All admin endpoints enforce `IsCrossTenantAccess(caller, user.TenantId)` — 403 returned server-side on mismatch
- Edit modal fetches user detail via BFF proxy using the session cookie — no tenantId passed in body
- Activate/Deactivate: no tenantId in request body; backend resolves from JWT and validates against target user
- `getUserDetail(userId)` backend also enforces tenant match: "Non-PlatformAdmins may only view users within their own tenant" — 403 otherwise
- No cross-tenant mutation is possible

---

## 12. Testing Results

| Scenario | Expected | Result |
|----------|----------|--------|
| Row actions menu opens | Dropdown shows View / Edit / Deactivate or Activate | ✓ |
| Row actions close on outside click | Dropdown hidden | ✓ (backdrop div) |
| Row actions close on item click | Dropdown hidden | ✓ |
| Edit modal opens prefilled | Role and phone prefilled from user detail | ✓ |
| Save with no changes | No API calls made; modal closes | ✓ |
| Save role change | Old role revoked, new role assigned, list refreshed | ✓ |
| Save phone | Phone updated, list refreshed | ✓ |
| Role required validation | Error shown | ✓ |
| Edit modal API error | Error banner shown, form preserved | ✓ |
| Deactivate confirmation shows | Dialog with "danger" confirm | ✓ |
| Activate confirmation shows | Dialog with "primary" confirm | ✓ |
| Deactivate success | User goes inactive in list, toast shown | ✓ |
| Activate success | User goes active in list, toast shown | ✓ |
| Status action API error | Error toast shown | ✓ |
| Add User still works | LS-ID-TNT-002 unaffected | ✓ |
| Search works | LS-ID-TNT-001 null-safe search unaffected | ✓ |
| Filter works | Status filter unaffected | ✓ |
| Null-safe rendering | Null firstName/lastName/email safe | ✓ |

---

## 13. Known Issues / Gaps

- **First Name / Last Name / Email not editable**: No backend endpoint exists for updating these user profile fields. They are shown read-only in the Edit modal. A future task should add `PATCH /api/users/{id}` to the tenant-scoped API.
- **Last admin protection — backend gap**: `PATCH /api/admin/users/{id}/deactivate` does not check if the target user is the last active admin in the tenant. If a tenant admin deactivates themselves or the last admin, the tenant will lose admin access. **This gap exists in the backend**, not the frontend. A future backend migration should add this check. Documented here per spec requirement.
- **Role list scope**: `GET /api/admin/roles` returns platform-level roles. A tenant-scoped roles list would be preferable. The backend `assignRole` endpoint already validates tenant scope via `IsCrossTenantAccess`. Assignable role filtering (per user org type, etc.) via `GET /api/admin/users/{id}/assignable-roles` could be used in a future refinement.
- **Multi-role support**: Users may have multiple roles. The Edit modal uses single-select and replaces all roles with the selected one. Multiple roles are displayed read-only in the user detail page (LS-ID-TNT-001).
- **Inactive login enforcement**: CONFIRMED implemented at `AuthService.cs:91` — `user.IsActive` is checked before issuing tokens.

---

## 14. Final Status

**Complete.** All success criteria met:
- ✔ Each user row has actions (View, Edit, Activate/Deactivate)
- ✔ Edit User modal opens and is prefilled from user detail
- ✔ Role can be updated (revoke + assign)
- ✔ Phone can be updated
- ✔ Active users can be deactivated through confirmation
- ✔ Inactive users can be reactivated through confirmation
- ✔ Success refreshes the list safely (`router.refresh()`)
- ✔ Failure preserves context and shows useful errors
- ✔ Last admin backend gap documented; frontend surfaces backend errors if they occur
- ✔ No regression to LS-ID-TNT-001 or LS-ID-TNT-002
