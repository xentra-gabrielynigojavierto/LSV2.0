# UIX-003 Access Control Management — Completion Report

**Date:** 2026-04-01  
**Scope:** Control Center — User Detail Page  
**Status:** COMPLETE ✓

---

## Objective

Extend the user detail page (`/tenant-users/[id]`) with live, interactive panels for managing a user's:
- System role assignments (assign / revoke)
- Organization memberships (add / remove / set-primary)
- Group memberships (add / remove)

All panels wire end-to-end: client component → BFF route → CC API lib → API Gateway → Identity service.

---

## Deliverables

### T001 — Backend: Organization List Endpoint
**File:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`

Added `ListOrganizations` handler:
- Route: `GET /api/admin/organizations?tenantId=`
- Returns active organizations filtered by tenant (or all if no `tenantId`)
- Route registered in `MapAdminEndpoints()`

### T002 — CC Types + API Lib

**Type added** (`types/control-center.ts`):
```ts
export interface OrgSummary {
  id: string; tenantId: string; name: string;
  displayName: string; orgType: string; isActive: boolean;
}
```

**CC API methods added** (`lib/control-center-api.ts`):

| Method | Endpoint |
|--------|----------|
| `users.assignRole(userId, roleId)` | `POST /identity/api/admin/users/{id}/roles` |
| `users.revokeRole(userId, roleId)` | `DELETE /identity/api/admin/users/{id}/roles/{roleId}` |
| `organizations.listByTenant(tenantId)` | `GET /identity/api/admin/organizations?tenantId=` |

### T003 — BFF Routes (7 new route files)

All routes are PlatformAdmin-gated via `requirePlatformAdmin()`.

| Method | BFF Route | Proxies to |
|--------|-----------|-----------|
| POST | `/api/identity/admin/users/[id]/roles` | `users.assignRole` |
| DELETE | `/api/identity/admin/users/[id]/roles/[roleId]` | `users.revokeRole` |
| POST | `/api/identity/admin/users/[id]/memberships` | `users.assignMembership` |
| DELETE | `/api/identity/admin/users/[id]/memberships/[membershipId]` | `users.removeMembership` |
| POST | `/api/identity/admin/users/[id]/memberships/[membershipId]/set-primary` | `users.setPrimaryMembership` |
| POST | `/api/identity/admin/groups/[id]/members` | `groups.addMember` |
| DELETE | `/api/identity/admin/groups/[id]/members/[userId]` | `groups.removeMember` |

All DELETE routes surface 409 conflicts from the backend as 409 to the client (membership safety rules).

### T004 — Panel Components + Page Update

#### `components/users/role-assignment-panel.tsx`
- Lists current role assignments with **Revoke** (confirm → yes/no)
- Dropdown of unassigned roles with **Assign**
- GLOBAL scope — matches backend default

#### `components/users/org-membership-panel.tsx`
- Lists memberships: org name, memberRole badge, Primary badge
- **Set Primary** button on non-primary rows
- **Remove** button (confirm → yes/no) — backend 409 LAST_MEMBERSHIP / PRIMARY_MEMBERSHIP surfaced as error messages
- Dropdown of orgs not yet joined with memberRole selector (Member / Admin / Billing / ReadOnly) and **Add**

#### `components/users/group-membership-panel.tsx`
- Lists current groups with joined date and link to group detail page (`Routes.groupDetail`)
- **Remove** button (confirm → yes/no)
- Dropdown of active groups not yet joined with **Add**
- Uses `userId` segment in the DELETE BFF route (matches backend `DELETE …/members/{userId}`)

#### `app/tenant-users/[id]/page.tsx` (updated)
- Fetches `availableRoles`, `availableOrgs`, `availableGroups` in parallel with `Promise.allSettled` — individual failures are non-fatal
- Groups fetched with `pageSize: 200` to avoid pagination truncation
- Renders an **Access Control** section divider below `UserDetailCard`
- All 3 panels rendered with live data from `UserDetail`

---

## Data Flow

```
Browser click
  → fetch BFF route (/api/identity/admin/...)
    → requirePlatformAdmin() guard
      → controlCenterServerApi.users.*() / groups.*()
        → apiClient.post|del() via API Gateway (port 5010)
          → Identity service (port 5001)
            → AdminEndpoints.cs handler
              → revalidateTag(CACHE_TAGS.users)  ← cache busted
  → router.refresh()  ← Next.js re-fetches server page
    → UserDetail re-fetched with updated memberships/roles/groups
```

---

## TypeScript Status

Zero new TypeScript errors introduced.  
7 pre-existing errors in `notifications/delivery-issues` and `notifications/providers` pages — untouched per spec.

---

## Safety Notes

- **Membership removal** is guarded by backend 409 rules:  
  - `LAST_MEMBERSHIP` — cannot remove the only active membership  
  - `PRIMARY_MEMBERSHIP` — must set-primary another membership first  
  Both codes surface as user-readable error messages in the panel.
- **Role revocation** includes a confirm step (Yes / No inline).  
- **Membership removal** includes a confirm step (Yes / No inline).  
- **Group removal** includes a confirm step (Yes / No inline).  
- All mutation routes call `revalidateTag(CACHE_TAGS.users)` server-side to bust Next.js cache.
