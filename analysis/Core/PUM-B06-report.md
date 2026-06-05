# PUM-B06 — Platform Admin User Management UI

**Date:** 2026-04-24
**Author:** Main Agent
**Scope:** Control Center — Platform Internal User Management

---

## 1. Objective

Implement `/platform-users` and `/platform-users/[userId]` pages in the Control Center that
allow PlatformAdmin operators to view, invite, activate/deactivate, lock/unlock, and assign
platform-scoped roles to `UserType=PlatformInternal` users exclusively.

---

## 2. Existing API Inventory

| Endpoint | Status | Notes |
|---|---|---|
| `GET /identity/api/admin/users?userType=PlatformInternal` | LIVE | `userType` param supported (PUM-B01, line 1416 AdminEndpoints.cs) |
| `GET /identity/api/admin/users/{id}` | LIVE | Returns `userType` field in response |
| `POST /identity/api/admin/users/{id}/activate` | LIVE | Reuses unchanged |
| `PATCH /identity/api/admin/users/{id}/deactivate` | LIVE | Reuses unchanged |
| `POST /identity/api/admin/users/{id}/lock` | LIVE | Reuses unchanged |
| `POST /identity/api/admin/users/{id}/unlock` | LIVE | Reuses unchanged |
| `POST /identity/api/admin/users/{id}/roles` | LIVE | Assigns GLOBAL-scoped role |
| `DELETE /identity/api/admin/users/{id}/roles/{roleId}` | LIVE | Revokes role |
| `GET /identity/api/admin/users/{id}/assignable-roles` | LIVE | Returns eligibility metadata |
| `GET /identity/api/admin/roles` | LIVE | Returns all roles including Platform-scoped |
| `POST /identity/api/admin/platform-users/invite` | NEW | Needed — InviteUser hardcodes TenantUser type |

---

## 3. Type Gap

`UserSummary` in `control-center.ts` lacks a `userType` field. The Identity API returns
`userType` as `"PlatformInternal"` | `"TenantUser"` | `"ExternalCustomer"`. Adding `userType?`
as optional string allows the existing `mapUserSummary` to be updated without breaking
existing pages.

---

## 4. New Backend Endpoint

**Route:** `POST /identity/api/admin/platform-users/invite`

**Why:** The existing `InviteUser` handler (line 4059) calls `User.Create(...)` without
passing `userType`, which defaults to `TenantUser`. There is no other way to create a
`PlatformInternal` user via the existing API.

**Request:**
```json
{ "email": "...", "firstName": "...", "lastName": "...", "roleId": "<optional guid>" }
```

**Behaviour:**
1. Validate email/firstName/lastName.
2. Look up the platform tenant (first active tenant by `CreatedAtUtc`, same approach as
   the UIX-002-C product-role seeder in Program.cs line 355–359).
3. Check for existing email collision across the whole system (not tenant-scoped).
4. Create `User` with `UserType.PlatformInternal` + deactivated (pending invite).
5. Optionally assign an initial Platform role.
6. Create `UserInvitation` with `PortalOrigins.TenantPortal` (same as InviteUser).
7. Send activation email; return `{ activationLink }`.

**Auth:** Requires `TENANT.users:manage` permission (same as InviteUser — no dedicated
platform-user permission code exists in the current PermissionCodes set).

---

## 5. Frontend Architecture

### 5.1 Route Constants (routes.ts)

```ts
platformUsers:       '/platform-users'
platformUserDetail:  (id: string) => `/platform-users/${id}`
```

### 5.2 Navigation (nav.ts)

New section `PLATFORM USERS` added to the IDENTITY group containing:
- Platform Users  `/platform-users`

### 5.3 List Page — `/platform-users`

Model: `apps/control-center/src/app/tenant-users/page.tsx`

- Auth: `requirePlatformAdmin()`
- Fetches: `controlCenterServerApi.users.list({ userType: 'PlatformInternal', page, search, status })`
- Components: `PlatformUserTable` (new), `InvitePlatformUserButton` (new)
- Features: search, status filter, pagination, "Invite Platform User" button

### 5.4 Detail Page — `/platform-users/[userId]`

Model: `apps/control-center/src/app/tenant-users/[id]/page.tsx`

- Auth: `requirePlatformAdmin()`
- Reuses existing panels: `UserDetailCard`, `UserActions`, `UserSecurityPanel`,
  `UserActivityPanel`, `EffectivePermissionsPanel`, `RoleAssignmentPanel`
- Back-link goes to `/platform-users`

### 5.5 New Components

| Component | Purpose |
|---|---|
| `PlatformUserTable` | Table listing PlatformInternal users with name, email, status, last login |
| `InvitePlatformUserModal` | Modal form: email, firstName, lastName, optional role dropdown |
| `InvitePlatformUserButton` | Client component wrapping the modal |

---

## 6. Cache

All mutations revalidate `CACHE_TAGS.users` — already done by the existing
`activate`, `deactivate`, `lock`, `unlock`, `assignRole`, `revokeRole` methods.

---

## 7. Files Changed

| File | Change |
|---|---|
| `analysis/PUM-B06-report.md` | This file |
| `apps/control-center/src/types/control-center.ts` | Add `userType?` to `UserSummary` |
| `apps/control-center/src/lib/api-mappers.ts` | Map `userType` in `mapUserSummary` |
| `apps/control-center/src/lib/routes.ts` | Add `platformUsers`, `platformUserDetail` |
| `apps/control-center/src/lib/nav.ts` | Add PLATFORM USERS nav section |
| `apps/control-center/src/lib/control-center-api.ts` | Add `userType` to `users.list`; add `users.invitePlatformUser` |
| `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` | Add `InvitePlatformUser` handler + route |
| `apps/control-center/src/components/platform-users/platform-user-table.tsx` | New |
| `apps/control-center/src/components/platform-users/invite-platform-user-modal.tsx` | New |
| `apps/control-center/src/components/platform-users/invite-platform-user-button.tsx` | New |
| `apps/control-center/src/app/platform-users/page.tsx` | New list page |
| `apps/control-center/src/app/platform-users/[userId]/page.tsx` | New detail page |
