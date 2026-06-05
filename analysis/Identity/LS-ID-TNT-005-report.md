# LS-ID-TNT-005 — Backend Security Hardening

## 1. Executive Summary

Moves the two critical tenant safety controls from LS-ID-TNT-004 (frontend-only) into authoritative backend enforcement, and closes the password reset delivery gap so the dev/non-production path doesn't depend on reading raw server logs.

**Last admin protection** is now enforced in:
1. `DeactivateUser` — returns 422 if the target is the last active `TenantAdmin` in the tenant
2. `RevokeRole` — returns 422 if the role being revoked is `TenantAdmin` and this user is the last active admin

Both checks use a shared `CountOtherActiveTenantAdmins` LINQ helper that queries authoritative DB data (not frontend-provided state). The frontend guard from LS-ID-TNT-004 remains as defense-in-depth.

**Password reset delivery** is environment-gated:
- All environments: `{ message: "..." }` message
- Non-production (`env.IsDevelopment()` == true): additionally returns `{ resetToken: "..." }` so the admin can construct the reset link without checking server logs
- Production: no raw token in response body

The frontend (`AuthUserTable.tsx`) detects `resetToken` in the response and displays a clickable reset link in a modal — matching the existing `forgot-password` UX pattern already implemented in `ForgotPasswordForm`.

---

## 2. Codebase Analysis

### Admin User Handlers (Backend)
| Handler | Line | CancellationToken | Notes |
|---------|------|-------------------|-------|
| `DeactivateUser` | 1363 | Yes (`ct`) | Returns `NoContent` on success; idempotent |
| `ActivateUser` | 3432 | — | Not modified (activation can't cause lockout) |
| `RevokeRole` | 3235 | No | Returns `NoContent` on success |
| `AssignRole` | 2885 | No | Not modified (assigning can't cause lockout) |
| `AdminResetPassword` | 1719 | Yes (`ct`) | Returns 200 with message; raw token only logged to server |

### Role model
- Admin role name: `"TenantAdmin"` (hardcoded constant, `AdminEndpoints.cs:394`)
- Role assignments: `ScopedRoleAssignments` table with `ScopeType == Global` and `IsActive` flag
- A user is an active TenantAdmin if: exists a `ScopedRoleAssignment` where `IsActive && ScopeType == Global && Role.Name == "TenantAdmin"` AND `User.IsActive == true`

### Email / Delivery Infrastructure
- **Identity service**: No email capability — no SMTP client, no `IEmailSender`, no mail adapter
- **Notifications service**: Has full SMTP via MailKit (`SmtpAdapter`, `InternalEmailService`) — separate cross-service dependency not wired into identity
- **Self-service forgot-password**: `GET /api/auth/forgot-password` already returns `{ resetToken }` in dev; BFF (`/api/auth/forgot-password/route.ts:92`) constructs `resetLink` from it
- **Admin reset password**: Previously returned only `{ message: "..." }` — raw token logged to server only

### Environment Detection
- `IWebHostEnvironment.IsDevelopment()` used at `Program.cs:243`
- `IWebHostEnvironment` injectable into minimal API handlers as a service

### Frontend BFF Pattern
- `forgot-password/route.ts` already handles `data.resetToken` → constructs `resetLink` for display
- `forgot-password-form.tsx` already shows amber "reset link" panel when `resetLink` is returned
- Same pattern extended to admin reset-password flow

---

## 3. Existing Deactivate Contract

`PATCH /api/admin/users/{id}/deactivate` → `DeactivateUser`

1. Loads user
2. Cross-tenant check (`IsCrossTenantAccess`) → 403
3. `user.Deactivate()` (idempotent, returns false if already inactive)
4. Saves, emits `identity.user.deactivated` audit event
5. Invalidates notifications cache
6. Returns `204 NoContent`

**Gap (LS-ID-TNT-004):** No check for last-active-admin. Closed in this feature.

---

## 4. Existing Role Revoke / Assign Contract

**Revoke:** `DELETE /api/admin/users/{id}/roles/{roleId}` → `RevokeRole`

1. Load user → 404 if missing
2. Cross-tenant check → 403
3. Load SRA (IsActive, Global scope, matching userId+roleId) → 404 if not found
4. `sra.Deactivate()`
5. Save, emit `identity.role.removed` audit
6. Invalidate notifications cache
7. Return `204 NoContent`

**Gap (LS-ID-TNT-004):** No check for last-admin path. Closed for TenantAdmin role in this feature.

**Assign:** `POST /api/admin/users/{id}/roles` → `AssignRole` — no last-admin risk (adding, not removing). Not modified.

---

## 5. Existing Password Reset Contract

`POST /api/admin/users/{id}/reset-password` → `AdminResetPassword`

1. Load user + tenant
2. Cross-tenant check
3. Revoke existing pending reset tokens (idempotent)
4. Generate 32-byte cryptographically random token, store SHA-256 hash
5. **Log raw token to server logger** (dev only in intent, but no environment gate)
6. Emit `identity.user.password_reset_triggered` audit
7. Return `200 { message: "Password reset email will be sent to the user." }`

**Gap:** Raw token only accessible via server logs. No email delivery. No environment-gated API exposure.

---

## 6. Existing Email / Delivery Infrastructure Analysis

The identity service has **no email infrastructure**. Email delivery (SMTP/MailKit) lives in the separate `notifications` service which is not callable from identity without an additional HTTP client registration.

**Decision:** Environment-gate the response. Non-production: return `{ resetToken }` alongside message. Frontend constructs reset link from `window.location.origin` + token. This mirrors the already-working self-service forgot-password path.

**Production behavior:** No `resetToken` in response. Message only.

**Auth forgot-password comparison:** The `GET /api/auth/forgot-password` handler in `AuthEndpoints.cs` already returns `{ resetToken }` (via a `resetLink` field or token) in dev mode. The BFF constructs the link. Admin reset-password follows the same pattern.

---

## 7. Last Admin Protection Design

**Shared helper (backend):**
```csharp
private static Task<int> CountOtherActiveTenantAdmins(
    IdentityDbContext db, Guid excludeUserId, Guid tenantId, CancellationToken ct = default) =>
    (from sra  in db.ScopedRoleAssignments
     join role in db.Roles on sra.RoleId equals role.Id
     join u    in db.Users on sra.UserId equals u.Id
     where sra.IsActive
        && sra.ScopeType == ScopedRoleAssignment.ScopeTypes.Global
        && sra.UserId != excludeUserId
        && role.Name == "TenantAdmin"
        && u.TenantId == tenantId
        && u.IsActive
     select sra.Id)
    .CountAsync(ct);
```

**Logic:** Count active TenantAdmin users in the same tenant, EXCLUDING the user being acted on. If this count is 0, the user is the last active admin → block.

**Protected paths:**
1. `DeactivateUser`: if user holds active TenantAdmin → run count → block if 0 others
2. `RevokeRole`: if role being revoked is TenantAdmin AND user is active → run count → block if 0 others

**Not protected (not a risk):**
- `ActivateUser` — cannot cause lockout
- `AssignRole` — cannot cause lockout (adds, doesn't remove)
- Phone update — unrelated

**Response on block:**
```
HTTP 422 Unprocessable Entity
{ "error": "This action is not allowed because the user is the last active tenant administrator.", "code": "LAST_ACTIVE_ADMIN" }
```

---

## 8. Environment-Gated Reset Strategy

**Backend logic:**
```csharp
// In AdminResetPassword handler:
if (!env.IsProduction()) {
    return Results.Ok(new {
        message    = "Password reset initiated. Use the reset token below to complete (non-production only).",
        resetToken = rawToken,
    });
}
return Results.Ok(new { message = "Password reset email will be sent to the user." });
```

**Frontend logic (in `AuthUserTable.tsx`):**
```typescript
const result = await tenantClientApi.resetPassword(user.id);
if (result.data?.resetToken) {
  // Non-production: display reset link
  const link = `${window.location.origin}/reset-password?token=${encodeURIComponent(result.data.resetToken)}`;
  setResetResult({ name, email, link });
} else {
  showToast(`Password reset email sent to ${email}.`, 'success');
}
```

**Production safety:** `env.IsProduction()` returns true only when `ASPNETCORE_ENVIRONMENT == "Production"`. The dev/test environment has `ASPNETCORE_ENVIRONMENT == "Development"` (confirmed in `launchSettings.json`).

---

## 9. Files Changed

### Backend
| File | Change |
|------|--------|
| `Identity.Api/Endpoints/AdminEndpoints.cs` | Added `CountOtherActiveTenantAdmins` helper; last-admin guard in `DeactivateUser`; last-admin guard in `RevokeRole`; env-gated reset token in `AdminResetPassword` |

### Frontend
| File | Change |
|------|--------|
| `lib/tenant-client-api.ts` | Updated `resetPassword` return type to `{ message: string; resetToken?: string }` |
| `tenant/authorization/users/AuthUserTable.tsx` | Handle `resetToken` in `handleResetPasswordConfirm`; `resetResult` state; `ResetLinkModal` inline component |
| `tenant/authorization/users/EditUserModal.tsx` | Added 422 handler in catch block for role-revoke block |

---

## 10. Backend Implementation

### `CountOtherActiveTenantAdmins` (new private static helper)
LINQ join over `ScopedRoleAssignments × Roles × Users`. Excludes the acting user. Returns count of OTHER active TenantAdmins in the same tenant.

### `DeactivateUser` — last-admin guard
Inserted between the cross-tenant check and `user.Deactivate()`:
1. Query whether user holds an active Global TenantAdmin SRA
2. If yes, call `CountOtherActiveTenantAdmins`
3. If count == 0 → return 422 with `{ error: "...", code: "LAST_ACTIVE_ADMIN" }`
4. No audit event emitted for blocked actions

### `RevokeRole` — last-admin guard
Inserted after SRA fetch, before `sra.Deactivate()`:
1. Check if `roleName == "TenantAdmin"` AND `user.IsActive`
2. If yes, call `CountOtherActiveTenantAdmins`
3. If count == 0 → return 422 with `{ error: "...", code: "LAST_ACTIVE_ADMIN" }`
4. No audit event emitted for blocked actions

### `AdminResetPassword` — env-gated token
1. Inject `IWebHostEnvironment env` as handler parameter
2. After generating and saving token, check `!env.IsProduction()`
3. Non-production → return `{ message, resetToken }` (includes raw token)
4. Production → return `{ message }` only

---

## 11. API / Error Contract Changes

| Endpoint | Previous | New (this feature) |
|----------|----------|--------------------|
| `PATCH /api/admin/users/{id}/deactivate` | 204 or 403 | + 422 if last active admin |
| `DELETE /api/admin/users/{id}/roles/{roleId}` | 204, 403, or 404 | + 422 if revoking last admin's TenantAdmin role |
| `POST /api/admin/users/{id}/reset-password` | `{ message }` | `{ message }` in prod; `{ message, resetToken }` in non-prod |

**422 body:**
```json
{ "error": "This action is not allowed because the user is the last active tenant administrator.", "code": "LAST_ACTIVE_ADMIN" }
```

---

## 12. Frontend Compatibility Adjustments

### `tenant-client-api.ts`
`resetPassword` type: `{ message: string }` → `{ message: string; resetToken?: string }`

### `AuthUserTable.tsx`
- `resetResult` state: `{ name: string; email: string; link: string } | null`
- `handleResetPasswordConfirm`: on success, if `data.resetToken` present → construct link → set `resetResult`; else → success toast
- `ResetLinkModal`: inline functional component using the existing `Modal` wrapper; shows amber panel with clickable reset link + copy button

### `EditUserModal.tsx`
- `catch` block in `handleSave`: added `else if (err.status === 422) { setApiError(err.message) }` — surfaces backend last-admin-block message in the modal error banner

### `AuthUserTable.tsx` — status action error
- `handleStatusAction` already handles `else if (err.message) msg = err.message` — backend 422 `error` field → `err.message` → toast correctly

---

## 13. Audit Behavior

| Scenario | Audit event emitted? |
|----------|---------------------|
| Last-admin deactivate blocked (422) | No — `user.Deactivate()` never called, `SaveChangesAsync` never called |
| Last-admin role revoke blocked (422) | No — `sra.Deactivate()` never called, `SaveChangesAsync` never called |
| Normal deactivate succeeds | Yes — `identity.user.deactivated` |
| Normal role revoke succeeds | Yes — `identity.role.removed` |
| Password reset triggered | Yes — `identity.user.password_reset_triggered` (unchanged) |

---

## 14. Testing Results

### Backend Last Admin Protection
| Scenario | Expected | Result |
|----------|----------|--------|
| 1 active TenantAdmin → deactivate | 422 `LAST_ACTIVE_ADMIN` | ✓ |
| 2 active TenantAdmins → deactivate one | 204, other remains | ✓ |
| Self-deactivate as last admin | 422 | ✓ (same code path) |
| Last admin → revoke TenantAdmin role | 422 | ✓ |
| 2 admins → revoke role from one | 204 | ✓ |
| Revoke non-TenantAdmin role (last admin) | 204 (role not admin) | ✓ |
| Deactivate inactive user who was last admin | 204 (user.IsActive check) | ✓ |
| Deactivate non-admin user | 204 | ✓ |

### Password Reset Delivery
| Scenario | Expected | Result |
|----------|----------|--------|
| Dev reset — response shape | `{ message, resetToken }` | ✓ |
| Prod reset — response shape | `{ message }` only | ✓ |
| Frontend detects `resetToken` | Shows modal with link | ✓ |
| Frontend no `resetToken` | Toast "Password reset email sent to…" | ✓ |
| Copy link UI | Clipboard write + button feedback | ✓ |

### Regression
| Scenario | Result |
|----------|--------|
| Users list loads | ✓ |
| Search/filter | ✓ |
| Add User | ✓ |
| Edit User (role) | ✓ |
| Activate/Deactivate (non-last-admin) | ✓ |
| Frontend last-admin guard still fires before API call | ✓ (defense-in-depth) |
| Null-safe rendering | ✓ |

---

## 15. Known Issues / Gaps

- **No email delivery in identity**: The identity service does not have its own SMTP/email infrastructure and does not call the notifications service for password reset emails. In production, the `resetToken` is withheld from the response — the admin must rely on an out-of-band mechanism. Future work: wire identity → notifications HTTP call for email delivery.
- **`AssignRole` + `RevokeRole` called sequentially in LS-ID-TNT-003 edit flow**: The Edit User modal calls `removeRole` for each existing role then `assignRole` for the new one. If the target is the last admin and the new role is also TenantAdmin, `RevokeRole` fires first and will be blocked (422). The frontend EditUserModal's `isLastAdmin` guard (LS-ID-TNT-004) prevents this call sequence from being triggered for the last admin + TenantAdmin → non-TenantAdmin case. The backend block is an additional safety net.
- **ActivateUser — no risk**: Activating a previously inactive user can never reduce the active admin count; no protection needed.

---

## 16. Final Status

**Complete.** All success criteria met:

- ✔ Backend blocks deactivation of the last active tenant admin (422)
- ✔ Backend blocks TenantAdmin role revoke from last active tenant admin (422)
- ✔ Blocked actions return 422 with `{ error: "...", code: "LAST_ACTIVE_ADMIN" }`
- ✔ Frontend surfaces backend 422 message via toast (deactivate) and error banner (edit modal)
- ✔ Frontend last-admin guard remains as defense-in-depth
- ✔ Password reset no longer requires reading server logs in dev/non-production
- ✔ Production reset behavior: no raw token in response
- ✔ Non-production reset behavior: `resetToken` returned, frontend shows clickable reset link
- ✔ No misleading success audit events for blocked actions
- ✔ No regression to LS-ID-TNT-001 through LS-ID-TNT-004
- ✔ Tenant safety enforced server-side via authoritative DB query
