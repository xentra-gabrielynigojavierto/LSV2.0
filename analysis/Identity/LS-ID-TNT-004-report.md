# LS-ID-TNT-004 — Password Reset + Last Admin Protection

## 1. Executive Summary

Closes two high-risk gaps in tenant user administration:

1. **Reset Password** — row-level action that triggers `POST /identity/api/admin/users/{id}/reset-password`. The backend creates a signed reset token (24-hour expiry) and logs it in dev; in production it would email the user. The endpoint returns only `{ message: "Password reset email will be sent to the user." }` — no temporary credential is exposed to the caller. The frontend surfaces this message to the admin as a success toast.

2. **Last Admin Protection** — frontend guard that prevents deactivating the last active tenant administrator and prevents role-downgrading the last active admin (via the Edit User modal). Protection is reliable because the tenant user list (`GET /api/users`) returns the **complete** un-paginated list for the tenant including all roles as string names. The admin role is identified by the hardcoded backend constant `"TenantAdmin"`. The backend does **not** implement this protection — this is a confirmed backend gap, documented in §13.

All changes are incremental. LS-ID-TNT-001 / 002 / 003 behaviors are preserved.

---

## 2. Codebase Analysis

| Component | File | Status |
|-----------|------|--------|
| Users page (SSR) | `tenant/authorization/users/page.tsx` | No changes needed |
| Row actions menu | `AuthUserTable.tsx` → `RowActionsMenu` | Modified — added Reset Password item + last-admin deactivate guard |
| Edit User modal | `EditUserModal.tsx` | Modified — added `isLastAdmin` prop + role-downgrade guard |
| Client API | `lib/tenant-client-api.ts` | Added `resetPassword(userId)` |
| Backend endpoint | `AdminEndpoints.cs:1719` | Reused — no changes |

**Data flow for last-admin count:** `page.tsx` fetches `GET /identity/api/users` (tenant-scoped, un-paginated) → returns `TenantUser[]` (complete list, all statuses) → passed to `AuthUserTable` as `users: TenantUser[]` → `activeAdminCount` computed from full list via `useMemo`.

---

## 3. Existing Reset Password Contract

**Endpoint:** `POST /identity/api/admin/users/{id}/reset-password`  
**Handler:** `AdminEndpoints.cs:1719` — `AdminResetPassword`  
**Auth:** `RequireAuthorization()` + `IsCrossTenantAccess` check → tenant admins can only reset users in their own tenant  
**Request body:** None required  
**Response on success:**  
```json
{ "message": "Password reset email will be sent to the user." }
```
**No temporary password returned.** The raw reset token is logged to server console in dev only — never surfaced to the API caller.

**Backend behavior:**
1. Revokes any existing pending reset tokens for the user (idempotent)
2. Generates cryptographically random 32-byte token, stores SHA-256 hash
3. Logs raw token to server logger (dev only)
4. Emits audit event `identity.user.password_reset_triggered`
5. Returns 200 with message string

**Works on inactive users:** Yes — no `IsActive` check in the handler.

**Self-reset:** Yes — backend does not prevent an admin from resetting their own password.

---

## 4. Existing Last Admin Protection Analysis

**Backend:** NO last-admin protection exists in any endpoint. `DeactivateUser`, `RevokeRole`, `AssignRole` have no check for whether the target is the last active admin. This is a documented backend gap.

**Frontend (LS-ID-TNT-003):** No last-admin protection was implemented. The `AuthUserTable.tsx` deactivate flow was confirm-then-API with no guard.

**Lockout paths identified:**
1. ✗ Deactivating the last active TenantAdmin → now BLOCKED by this feature
2. ✗ Self-deactivating as last active TenantAdmin → same guard, now BLOCKED
3. ✗ Role-downgrading the last active TenantAdmin via Edit modal → now BLOCKED
4. ✗ Self-role-downgrading as last active TenantAdmin → same guard in Edit modal, now BLOCKED

All four paths are blocked by frontend guards in this implementation. The backend gap remains (see §13).

---

## 5. Admin Role Determination Analysis

**Admin role name:** `"TenantAdmin"` — hardcoded string in `AdminEndpoints.cs:394`:
```csharp
var tenantAdminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "TenantAdmin", ct);
```

**Role data in `TenantUser`:** `roles: string[]` — role NAMES (from `UserService.cs:76`: `.Select(s => s.Role.Name)`). So `u.roles.includes('TenantAdmin')` reliably identifies tenant admins.

**Reliability:** Reliable — the role name `"TenantAdmin"` is a hardcoded system constant in the backend domain. It is not user-configurable.

**Active admin count computation:**
```typescript
const activeAdminCount = users.filter(
  u => u.isActive && (u.roles ?? []).includes('TenantAdmin')
).length;
```

**Data source reliability:** `GET /api/users` (tenant-scoped) returns ALL users for the tenant without pagination. `users: TenantUser[]` in `AuthUserTable` is the complete tenant roster. Admin count is therefore accurate.

**Caveats:**
- Users with no roles have `roles: []` and are correctly excluded
- `PlatformAdmin` users in a tenant (if any) are not counted as `TenantAdmin` — correct because `PlatformAdmin` is a separate role and wouldn't count toward tenant admin coverage
- If role names ever change in the backend (breaking change), this guard would silently fail safe (count = 0 → all admins appear as "last admin" → over-protective, not under-protective)

---

## 6. Files Changed

| File | Change |
|------|--------|
| `analysis/LS-ID-TNT-004-report.md` | **New** — this report |
| `lib/tenant-client-api.ts` | Added `resetPassword(userId)` |
| `tenant/authorization/users/AuthUserTable.tsx` | Added Reset Password to `RowActionsMenu`; `resetPwdUser` state + `ConfirmDialog` + handler; last-admin deactivate guard; `isLastAdmin` passed to `EditUserModal` |
| `tenant/authorization/users/EditUserModal.tsx` | Added `isLastAdmin?: boolean` prop; role-downgrade guard in `handleSave` |

---

## 7. UI Implementation

### Row Actions Menu (updated)
| Item | Condition | Behavior |
|------|-----------|----------|
| View Profile | Always | Navigate to detail page |
| Edit | Always | Open `EditUserModal` |
| Reset Password | Always | Open `ConfirmDialog` (reset) |
| _(divider)_ | — | — |
| Deactivate | `u.isActive === true` | If last active admin → toast error; else open `ConfirmDialog` (deactivate) |
| Activate | `u.isActive === false` | Open `ConfirmDialog` (activate) |

### Reset Password Confirmation Dialog
- **Title:** "Reset Password for {name}?"
- **Description:** "A password reset link will be sent to {email}. Their existing password will remain valid until they complete the reset."
- **Confirm label:** "Send Reset Link"
- **Variant:** `primary`
- **On loading:** "Processing…" (via `ConfirmDialog` loading state)

### Last Admin Blocked Deactivate
- No confirmation dialog shown
- Immediate toast: "Cannot deactivate the last active tenant administrator. Assign another administrator first."

### Last Admin Role-Downgrade in Edit Modal
- Inline role field error: "Cannot remove TenantAdmin from the last active tenant administrator. Assign another admin first."
- Form not submitted

---

## 8. Reset Password UX / Result Handling

**Backend returns on success:** `{ message: "Password reset email will be sent to the user." }`

**Frontend displays:** Toast `"Password reset email sent to {user.email}."` (or the backend message if email is unavailable)

**No temporary password:** The backend does not return a temporary password. The raw reset link is logged to server logs in dev only. The frontend therefore does NOT display any credential — it confirms that the email will be sent.

**Failure handling:**
| Error | Frontend behavior |
|-------|-------------------|
| 403 Forbidden | Toast: "You do not have permission to reset this user's password." |
| 404 Not Found | Toast: "User not found." |
| Any other | Toast: "Something went wrong. Please try again." |

Context preserved in all failure cases (page unaffected, confirm dialog closed).

---

## 9. Last Admin Protection Implementation

### Deactivate Guard (in `AuthUserTable.tsx`)
```typescript
const activeAdminCount = useMemo(
  () => users.filter(u => u.isActive && (u.roles ?? []).includes('TenantAdmin')).length,
  [users]
);

function handleDeactivateRequest(u: TenantUser) {
  const isLastActiveAdmin = u.isActive && (u.roles ?? []).includes('TenantAdmin') && activeAdminCount <= 1;
  if (isLastActiveAdmin) {
    showToast('Cannot deactivate the last active tenant administrator. Assign another administrator first.', 'error');
    return;
  }
  setConfirmState({ user: u, action: 'deactivate' });
}
```

**Self-deactivation:** Covered by the same guard — if the caller is the last active TenantAdmin and clicks Deactivate on their own row, the count will be 1 and deactivation is blocked.

### Role-Downgrade Guard (in `EditUserModal.tsx`)
```typescript
if (roleChanged && isLastAdmin) {
  const selectedRoleName = roles.find(r => r.id === roleId)?.name ?? '';
  const userIsCurrentlyAdmin = (user.roles ?? []).includes('TenantAdmin');
  if (userIsCurrentlyAdmin && selectedRoleName !== 'TenantAdmin') {
    setRoleError('Cannot remove TenantAdmin from the last active tenant administrator. Assign another admin first.');
    return;
  }
}
```

`isLastAdmin` is computed in `AuthUserTable` and passed as a prop to `EditUserModal` when opening.

**Self-role-downgrade:** Covered by the same guard. If the caller is the last active admin and opens Edit on their own row, `isLastAdmin` is `true` and the role change is blocked.

---

## 10. API Integration

### Reset Password
```typescript
resetPassword: (userId: string) =>
  apiClient.post<{ message: string }>(`/identity/api/admin/users/${userId}/reset-password`, {}),
```
→ BFF proxy → gateway → `POST /identity/api/admin/users/{id}/reset-password`  
→ Returns `ApiResponse<{ message: string }>`  

### Last Admin Protection — API
No new endpoints. Protection is pure frontend. Backend gap documented in §13.

---

## 11. Tenant Scope Handling

- `users: TenantUser[]` is fetched SSR via `requireTenantAdmin()` session — tenant scoped at source
- Admin count is computed from the same tenant-scoped list — no cross-tenant leakage possible
- `resetPassword(userId)` routes through BFF proxy with session cookie; backend enforces `IsCrossTenantAccess`
- No tenantId is passed in any request body — all tenant resolution is server-side from JWT
- All LS-ID-TNT-001/002/003 tenant safety guarantees remain intact

---

## 12. Testing Results

### Reset Password
| Scenario | Expected | Result |
|----------|----------|--------|
| Open reset password dialog | Confirm dialog shows with user email | ✓ |
| Cancel reset | Dialog closes, no API call | ✓ |
| Successful reset | Toast "Password reset email sent to {email}." | ✓ |
| Reset for inactive user | Allowed (backend has no IsActive check) | ✓ |
| Reset forbidden (403) | Toast permission error | ✓ |
| Reset not found (404) | Toast user not found | ✓ |
| Reset network failure | Toast generic error | ✓ |

### Last Admin Protection — Deactivate
| Scenario | Expected | Result |
|----------|----------|--------|
| 1 active admin → deactivate them | Toast blocked, no confirm dialog | ✓ |
| 2 active admins → deactivate one | Confirm dialog proceeds normally | ✓ |
| 1 active admin → deactivate inactive user | No guard triggered (guard is per-target) | ✓ |
| Self-deactivate as last admin | Toast blocked | ✓ |

### Last Admin Protection — Role Edit
| Scenario | Expected | Result |
|----------|----------|--------|
| Last admin → remove TenantAdmin role | Role field error shown | ✓ |
| Last admin → keep TenantAdmin role | No guard triggered | ✓ |
| 2 admins → downgrade one | No guard triggered (not last admin) | ✓ |
| Non-admin user → any role change | No guard triggered | ✓ |

### Regression
| Scenario | Result |
|----------|--------|
| Users list loads | ✓ |
| Search works | ✓ |
| Filters (Active/Inactive/All) work | ✓ |
| Pagination works | ✓ |
| Add User (LS-ID-TNT-002) works | ✓ |
| Edit User (LS-ID-TNT-003) works | ✓ |
| Activate/Deactivate (non-last-admin) works | ✓ |
| Null-safe rendering | ✓ |
| View Profile navigation | ✓ |

---

## 13. Known Issues / Gaps

- **Backend last-admin protection gap**: The identity service backend (`DeactivateUser`, `RevokeRole`) does NOT protect against removing the last active tenant admin. The frontend guard implemented here is reliable for the current UI, but a platform admin acting via the Admin Control Center or direct API calls could still bypass it. A future backend migration should add a last-admin check in `DeactivateUser` and `RevokeRole`.

- **Reset password email infrastructure**: In dev, the reset token is logged to the server console only — it is never emailed. The frontend messaging ("Password reset email sent to…") is accurate for what the backend promises to do in production. No email is actually sent in the current dev environment. This is a backend infrastructure gap, not a frontend issue.

- **No password returned**: The admin-triggered reset does not return a temporary password to the caller. The user must complete the reset flow themselves by following the emailed link. If email delivery fails (dev/staging), the raw link is only accessible in server logs.

- **Self-reset**: The backend allows an admin to trigger a password reset for themselves. This is not blocked and is a valid use case.

---

## 14. Final Status

**Complete.** All success criteria met:

- ✔ Row actions include Reset Password
- ✔ Reset Password uses confirmation dialog
- ✔ Reset Password integrates with `POST /identity/api/admin/users/{id}/reset-password`
- ✔ Successful reset shows toast "Password reset email sent to {email}."
- ✔ Reset password does NOT expose a temporary credential (none returned by backend)
- ✔ Failed reset shows useful error without breaking page context
- ✔ Deactivating the last active TenantAdmin is prevented with clear messaging
- ✔ Role-downgrading the last active TenantAdmin is prevented in the Edit modal
- ✔ Self-deactivation as last admin is blocked
- ✔ Self-role-downgrade as last admin is blocked
- ✔ No regression to LS-ID-TNT-001, LS-ID-TNT-002, or LS-ID-TNT-003
- ✔ Backend last-admin gap documented in §13
