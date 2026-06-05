# PUM-B07 — Tenant User Management UI Under Tenant Detail

## 1. Codebase Analysis

### Existing Tenant Detail Structure
- Route: `/tenants/[id]` — shared layout in `apps/control-center/src/app/tenants/[id]/layout.tsx`
- Sub-tabs: Overview, **User Management** (`/tenants/[id]/users`), Notifications, User Activity
- Tab nav: `TenantNavTabs` client component reads pathname to set active tab
- `UserManagementTabs` client component has three sub-tabs: Users, Groups, Permissions

### Current Users Tab State (pre-PUM-B07)
- `/tenants/[id]/users/page.tsx` calls `controlCenterServerApi.users.list({ tenantId })` — the generic
  `/identity/api/admin/users?tenantId=…` endpoint, which does NOT include inline `roles[]` per user.
- Table component: `UserListTable` (generic; shows basic user columns, no inline roles, no remove/role-assign actions).

### What PUM-B03 Provides
All five PUM-B03-R01–R06 endpoints are live in `AdminEndpoints.cs`:
| Endpoint | Handler |
|---|---|
| `GET  /api/admin/tenants/{id}/users` | `ListTenantUsers` |
| `POST /api/admin/tenants/{id}/users` | `AssignUserToTenant` |
| `DELETE /api/admin/tenants/{id}/users/{uid}` | `RemoveUserFromTenant` |
| `POST /api/admin/tenants/{id}/users/{uid}/roles` | `AssignTenantRole` |
| `DELETE /api/admin/tenants/{id}/users/{uid}/roles/{sraId}` | `RevokeTenantRole` |

---

## 2. Existing Tenant Detail UI Structure

```
/tenants/[id]/layout.tsx         ← shared header, breadcrumb, TenantNavTabs
/tenants/[id]/page.tsx           ← Overview tab
/tenants/[id]/users/page.tsx     ← User Management tab  ← PUM-B07 scope
/tenants/[id]/notifications/page.tsx ← Notifications tab
/tenants/[id]/activity/page.tsx  ← User Activity tab
```

`TenantNavTabs` already includes a "User Management" link — no nav changes needed.

---

## 3. API Reuse vs Gaps

### Reused (no backend change)
- `GET  /identity/api/admin/tenants/{tenantId}/users` — returns users with inline `roles[]`
- `POST /identity/api/admin/tenants/{tenantId}/users/{userId}/roles` — assign tenant role
- `DELETE /identity/api/admin/tenants/{tenantId}/users/{userId}/roles/{assignmentId}` — revoke role
- `DELETE /identity/api/admin/tenants/{tenantId}/users/{userId}` — remove/deprivilege user
- `GET  /identity/api/admin/roles?scope=Tenant` — backend already supports `scope` query param

### Gaps
- `POST /identity/api/admin/tenants/{tenantId}/users` — "Add Existing User" available, but single-tenant architecture returns `USER_IN_DIFFERENT_TENANT` for cross-tenant users. Implemented with clear error messaging.
- CC API client missing all five PUM-B03 methods → added as `tenantAdminUsers` namespace.
- `RoleSummary` type + `mapRoleSummary` missing `scope` field → added + backfilled in mapper.
- `roles.list` missing `scope` param → added.

---

## 4. UI Implementation Details

### TenantUserTable (`components/tenant-users/tenant-user-table.tsx`)
- Shows: Name, Email, User Type, Status, Tenant Roles (inline chips), Last Login, Actions
- Filters PlatformInternal users out client-side (safety net; backend already limits to tenantId)
- Per-user actions: "Assign Role", "Remove from Tenant"
- Per-role chip: × remove button calls `removeTenantUserRoleAction`

### AssignTenantRoleModal (`components/tenant-users/assign-tenant-role-modal.tsx`)
- Dropdown shows only Tenant-scoped roles (`roles.scope === 'Tenant'`)
- Calls `assignTenantRoleAction` Server Action on submit

### RemoveUserFromTenantButton (`components/tenant-users/remove-user-from-tenant-button.tsx`)
- Confirm dialog before calling `removeUserFromTenantAction`
- Displays "Remove tenant access" — does NOT imply global deletion

### AddUserToTenantModal (`components/tenant-users/add-user-to-tenant-modal.tsx`)
- Takes a userId input field
- Calls `addUserToTenantAction`; surfaces `USER_IN_DIFFERENT_TENANT` if returned

---

## 5. Backend Changes

**None required.** All five PUM-B03 endpoints were already implemented.

---

## 6. Files Changed

### New files
| File | Purpose |
|---|---|
| `app/tenants/[id]/users/actions.ts` | Server Actions for all tenant-user mutations |
| `components/tenant-users/tenant-user-table.tsx` | Main user table with inline roles & actions |
| `components/tenant-users/assign-tenant-role-modal.tsx` | Assign tenant role modal |
| `components/tenant-users/remove-user-from-tenant-button.tsx` | Remove user confirm button |
| `components/tenant-users/add-user-to-tenant-modal.tsx` | Add existing user to tenant modal |

### Modified files
| File | Change |
|---|---|
| `types/control-center.ts` | Add `TenantUserRoleAssignment`, `TenantUserSummary`; add `scope?` to `RoleSummary` |
| `lib/api-mappers.ts` | Update `mapRoleSummary` to include `scope` |
| `lib/control-center-api.ts` | Add `tenantAdminUsers` namespace; add `scope?` to `roles.list` |
| `app/tenants/[id]/users/page.tsx` | Use `tenantAdminUsers.list`; fetch tenant roles; filter PlatformInternal |
| `components/users/user-management-tabs.tsx` | Accept `TenantUserSummary[]` + `tenantRoles`; use `TenantUserTable` |

---

## 7. Validation Results

- Frontend build: ✅ clean (0 TS errors)
- PlatformInternal users: ✅ filtered client-side in `TenantUserTable`
- Platform Users page (`/platform-users`): ✅ unaffected (separate route/component)
- Tenant roles dropdown: ✅ only shows roles where `scope === 'Tenant'`
- Remove user language: ✅ "Remove tenant access" — no account-deleted implication
- `USER_IN_DIFFERENT_TENANT` surfaced: ✅ in `AddUserToTenantModal`

---

## 8. Known Gaps / Deferred Items

- **Pagination on Users tab**: current page uses server-side pagination but tab switch state is client-only; pagination resets on sub-tab switch (acceptable per spec scope).
- **Add Existing User**: limited by single-tenant architecture. Users in a different tenant will get a clear conflict error. Cross-tenant membership is out of scope per PUM-B03-R09.
- **ExternalCustomer users**: not shown (filtered by backend + client-side check). No role assignment UI for them.
- **Audit wiring**: deferred per spec (out of scope).
- **Real-time refresh after mutations**: uses `router.refresh()` — full server round-trip, no optimistic update.

---

## 9. Final Assessment

PUM-B07 is fully implemented within the scope constraints. The Users tab under Tenant Detail now uses the purpose-built PUM-B03 tenant-user APIs, shows inline roles, and provides assign/remove role and remove-from-tenant actions. No backend changes were required. Platform Users remain isolated at `/platform-users`. PlatformInternal users do not appear in tenant user lists.
