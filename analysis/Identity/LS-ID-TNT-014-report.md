# LS-ID-TNT-014 — Control Center Product Permission Governance UI

## 1. Executive Summary

LS-ID-TNT-014 completes the Control Center product permission governance surface for platform administrators. The feature was implemented as targeted fixes and enhancements on top of already-substantial existing infrastructure — the Control Center already had `/permissions`, `/roles`, and `/roles/[id]` pages with role-permission management. The work for this ticket was:

1. **Bug fix**: Corrected a `capabilityId` vs `permissionId` field name mismatch that caused all role-permission assignment calls to fail silently against the backend.
2. **Auth hardening**: Elevated permissions and role-detail pages (and their BFF routes) from `requireAdmin()` to `requirePlatformAdmin()`, properly scoping product governance to platform administrators only.
3. **Governance scoping**: Updated `RolePermissionPanel` to filter the permission picker to the role's own product when the role is a product role (`isProductRole=true`), preventing cross-product permission leakage and keeping TENANT.* permissions out of product role editing.
4. **Product-level grouping**: Updated the roles list to display product roles grouped per-product (CareConnect, SynqLien, SynqFund) rather than a flat undifferentiated table.
5. **Governance boundary notices**: Added explicit informational banners to both `/permissions` and `/roles` pages clarifying that product/platform governance lives in Control Center and tenant-level governance remains in the Tenant Portal.
6. **Nav badges**: Marked Permissions and Roles nav items as LIVE (both are now fully wired to real backend endpoints with proper auth).

No new backend endpoints were required. No role-create/archive UI was implemented (backend has no validated CRUD for role entities — only permission-to-role assignment exists).

---

## 2. Codebase Analysis

### Control Center app structure
- **Standalone Next.js app** at `apps/control-center/` (runs on `:5004`)
- **Auth guards**: `requirePlatformAdmin()` for platform-admin-only pages, `requireAdmin()` for shared pages
- **API pattern**: Server Components call `controlCenterServerApi.*` which proxies to the identity gateway; Client Components fetch BFF routes at `/api/identity/admin/...`
- **Nav**: `CC_NAV` in `apps/control-center/src/lib/nav.ts` defines sidebar sections and badge status

### Existing governance infrastructure (pre-LS-ID-TNT-014)
| Page | Auth | Status |
|---|---|---|
| `/permissions` | `requireAdmin()` ← wrong | Catalog existed, creation wired |
| `/roles` | `requirePlatformAdmin()` ✓ | List existed, grouped by system/product/other |
| `/roles/[id]` | `requireAdmin()` ← wrong | Detail + RolePermissionPanel existed |

### Data model
- `Permission` entity: `{ Id, Code, Name, Description, Category, ProductId, IsActive }`
- `Role` entity: `{ Id, TenantId, Name, IsSystemRole }`
- `ProductRole` seed: maps a product code to role definitions; `isProductRole` flag derived at query time
- `RolePermissionAssignment` join: `{ RoleId, PermissionId, AssignedByUserId, AssignedAtUtc }`
- Product permissions seeded per product (CareConnect, SynqLien, SynqFund, SYNQ_PLATFORM for TENANT.*)

---

## 3. Existing Product Permission / Role API Analysis

### Read endpoints (already existed, all working)
- `GET /api/admin/permissions/catalog` — all permissions grouped by product
- `GET /api/admin/permissions` — filterable list (`productId`, `search` query params)
- `GET /api/admin/roles` — all roles with `isSystemRole`, `isProductRole`, `productCode`, `productName`, `capabilityCount`, `userCount`, `allowedOrgTypes`
- `GET /api/admin/roles/{id}` — role detail
- `GET /api/admin/roles/{id}/permissions` — permissions assigned to a role
- `GET /api/admin/users/{id}/permissions` — effective permissions for a user

### Mutation endpoints (already existed)
- `POST /api/admin/roles/{id}/permissions` — body: `{ "permissionId": "<guid>" }`
  - Guards: `isSystemRole && !PlatformAdmin → 403`; cross-tenant → 403
- `DELETE /api/admin/roles/{id}/permissions/{permissionId}` — revoke
  - Same guards
- `POST /api/admin/permissions` — create permission (PlatformAdmin governance)
- `PATCH /api/admin/permissions/{id}` — update
- `DELETE /api/admin/permissions/{id}` — deactivate

### Role entity CRUD
- **No CREATE/UPDATE/ARCHIVE for role entities found**. The backend only manages permission assignments to roles. Product roles are seeded by product definitions at provisioning time.
- Decision: Role maintenance UI (create/edit/archive) was NOT implemented per spec — "only if existing backend support is present."

### Critical bug discovered
`controlCenterServerApi.roles.assignPermission()` was sending `{ capabilityId }` to the backend, but the backend `AssignRolePermissionRequest` binds `PermissionId` (camelCase: `permissionId`). This caused all assignment calls from Control Center to be silently rejected (400 Bad Request from the backend, with the role receiving no permission).

---

## 4. Existing Control Center UI Analysis

### `/permissions` page (pre-LS-ID-TNT-014)
- Already showed a complete permission catalog grouped by product
- Already had search bar and product filter pills
- Already had `PermissionCreateDialog` for PlatformAdmin to define new permissions
- **Problem**: Used `requireAdmin()` — TenantAdmin users could access product permission governance
- **Problem**: No governance boundary notice clarifying it is a product/platform-only surface

### `/roles` page (pre-LS-ID-TNT-014)
- Already listed all roles separated into System / Product / Other sections
- Product roles section was a flat table — all product roles (CareConnect + SynqLien + SynqFund) mixed together without per-product grouping
- Had a good info banner but described system roles only

### `/roles/[id]` page with `RolePermissionPanel` (pre-LS-ID-TNT-014)
- Already had full assign/revoke capability management UI
- Already showed permissions grouped by product in the assigned list
- **Problem**: Used `requireAdmin()` — TenantAdmin could access product role permission management
- **Problem**: Permission picker showed the FULL catalog including TENANT.* permissions — a product role (e.g., SynqLien role) could be given TENANT.users:manage capability
- **Problem**: `assignPermission()` sent the wrong field name (`capabilityId` instead of `permissionId`) — assignment was broken

---

## 5. UI Placement and Interaction Design

### Chosen placement: enhance existing `/permissions` and `/roles` / `/roles/[id]` pages

**Rationale:**
- The pages already existed with solid structure — no new page or navigation section was needed
- The gaps were auth hardening + governance scoping + grouping quality + bug fix, not missing pages
- Creating a new "Product Governance" section would duplicate the existing surface

**Why no new page was created:**
- `/permissions` already serves as the product permission catalog
- `/roles` grouped by product serves as the product roles overview
- `/roles/[id]` with `RolePermissionPanel` serves as the role → permission mapping editor

**Alternatives rejected:**
- New `/product-governance` page — redundant given existing infrastructure
- Product-tab accordion on `/roles` — rejected in favor of per-product `ProductRoleGroup` sub-sections (cleaner separation when there are many roles per product)
- Inline permission grid/matrix — rejected in favor of existing add/remove-one-at-a-time pattern (consistent with existing UX)

---

## 6. Mutation API Design (if needed)

No new backend endpoints were required. The existing endpoints already support all needed operations:

| Operation | Endpoint | Auth |
|---|---|---|
| View catalog | `GET /api/admin/permissions` | JWT (PlatformAdmin enforced at CC BFF layer) |
| Assign permission to role | `POST /api/admin/roles/{id}/permissions` | `isSystemRole` guard + cross-tenant guard |
| Revoke permission from role | `DELETE /api/admin/roles/{id}/permissions/{id}` | Same guards |

The only change at the "API design" level was the CC API client fix: `{ permissionId: capabilityId }` (was `{ capabilityId }`).

---

## 7. Files Changed

| File | Change type | Description |
|---|---|---|
| `apps/control-center/src/lib/control-center-api.ts` | Modified | `assignPermission`: `{ capabilityId }` → `{ permissionId: capabilityId }` — fixes broken assignment |
| `apps/control-center/src/lib/nav.ts` | Modified | Permissions and Roles nav badges → `'LIVE'` |
| `apps/control-center/src/app/permissions/page.tsx` | Modified | `requireAdmin()` → `requirePlatformAdmin()`; governance boundary notice added |
| `apps/control-center/src/app/roles/page.tsx` | Modified | Governance boundary notice updated to reference product/tenant separation |
| `apps/control-center/src/app/roles/[id]/page.tsx` | Modified | `requireAdmin()` → `requirePlatformAdmin()`; passes `isProductRole`, `productCode`, `productName` to `RolePermissionPanel`; removed `isTenantAdmin` prop (no longer needed for CC-only page) |
| `apps/control-center/src/app/api/identity/admin/roles/[id]/permissions/route.ts` | Modified | `requireAdmin()` → `requirePlatformAdmin()` on GET and POST |
| `apps/control-center/src/app/api/identity/admin/roles/[id]/permissions/[capabilityId]/route.ts` | Modified | `requireAdmin()` → `requirePlatformAdmin()` on DELETE |
| `apps/control-center/src/components/roles/role-permission-panel.tsx` | Modified | Added `isProductRole`, `productCode`, `productName` props; product-scoped catalog filtering in picker; product governance notice for product roles; removed `isTenantAdmin` prop |
| `apps/control-center/src/components/roles/role-list-table.tsx` | Modified | Product roles section now grouped per-product with `ProductRoleGroup` component; product color badges; "Manage →" link for product roles |

---

## 8. Backend Implementation

No backend changes were required.

### Bug fix: `assignPermission` field name (frontend only)
```typescript
// Before (broken — backend expects "permissionId", not "capabilityId")
await apiClient.post(`/identity/api/admin/roles/${id}/permissions`, { capabilityId });

// After (correct)
await apiClient.post(`/identity/api/admin/roles/${id}/permissions`, { permissionId: capabilityId });
```

The backend `AssignRolePermissionRequest(Guid PermissionId)` binds from `{ "permissionId": "..." }` in the JSON body. The old `{ "capabilityId": "..." }` payload would cause `PermissionId` to be `Guid.Empty`, failing the `FirstOrDefaultAsync` lookup and returning 404 or an unhelpful error.

---

## 9. Frontend / UX Implementation

### Auth hardening
| Location | Before | After |
|---|---|---|
| `/permissions` page | `requireAdmin()` (TenantAdmin access) | `requirePlatformAdmin()` |
| `/roles/[id]` page | `requireAdmin()` | `requirePlatformAdmin()` |
| CC BFF `GET/POST /api/identity/admin/roles/[id]/permissions` | `requireAdmin()` | `requirePlatformAdmin()` |
| CC BFF `DELETE /api/identity/admin/roles/[id]/permissions/[capabilityId]` | `requireAdmin()` | `requirePlatformAdmin()` |

The Tenant Portal BFF (at `apps/web/src/app/api/identity/[...path]`) is a completely separate Next.js application and is unaffected — tenant role management continues to work via the Tenant Portal's own BFF which correctly uses `requireTenantAdmin()`.

### Product-scoped permission picker
In `RolePermissionPanel`, the `available` permission list is now:
```typescript
const scopedCatalog = (isProductRole && productCode)
  ? catalog.filter(c => c.productCode === productCode)
  : catalog;
```
- **Product role** (e.g., SynqLien Contributor): only SynqLien permissions appear in the picker → TENANT.* and CareConnect permissions are excluded
- **System/custom role**: full catalog shown (PlatformAdmin governance scope)

A blue info banner appears on product role detail pages: "This is a [ProductName] product role. The permission picker is scoped to [ProductName] capabilities only..."

### Product roles grouped by product
The "Product Roles" section in `RoleListTable` is now rendered as `ProductRoleGroup` components:
- Each product (CareConnect, SynqLien, SynqFund) gets its own panel with a colored product badge header
- The header shows total permissions count across all roles in that product
- Each role row shows allowed org types, permission count badge, user count, and "Manage →" link
- When productRoles is empty, the section is omitted

### Governance notices
- **Permissions page**: "This catalog covers product and platform permissions only. Tenant-level permissions (TENANT.*) are managed per-tenant in the Tenant Portal — they are not editable from Control Center."
- **Roles page**: "System roles are platform-managed and read-only. Product roles are provisioned from product definitions — select a product role to manage its permission mappings. Tenant-level roles and their permissions are managed in the Tenant Portal, not here."
- **Role detail (product role)**: Blue info box on `RolePermissionPanel` explaining permission picker scoping
- **Role detail (system role)**: Amber warning box explaining system roles are immutable

---

## 10. API / Error Contract Changes

### No new endpoints
All mutations use existing endpoints. The only change is the CC API client now sends the correct `permissionId` field.

### Auth change: CC BFF hardened
- `GET /api/identity/admin/roles/{id}/permissions` — now returns 401 for TenantAdmin (was previously accessible)
- `POST /api/identity/admin/roles/{id}/permissions` — same
- `DELETE /api/identity/admin/roles/{id}/permissions/{id}` — same
- **Impact on Tenant Portal**: None — Tenant Portal uses its own BFF at `apps/web/src/app/api/identity/[...path]`

### Error handling (pre-existing, unchanged)
- Backend `isSystemRole` guard → 403 (surfaced as error banner in `RolePermissionPanel`)
- Not found → 404 (surfaced as role-not-found state in `/roles/[id]`)
- Network failures → error banner in panel

---

## 11. Testing Results

### Build validation
- ✅ All .NET services: `Build succeeded. 0 Error(s)`
- ✅ Next.js (web + control-center): Fast Refresh clean, no TypeScript errors
- ✅ Pre-existing MSB3277 JwtBearer version conflict warning (Documents.Api) — pre-existing, not introduced by this ticket

### Scenario validation

**Permission catalog load**
- PlatformAdmin navigates to `/permissions` → catalog loads grouped by product → governance notice visible
- TenantAdmin attempts to navigate to `/permissions` in CC → `requirePlatformAdmin()` redirects to login ✓

**Product roles list**
- `/roles` shows System Roles + per-product sub-sections for Product Roles (CareConnect, SynqLien, SynqFund) + Custom Roles
- Each product sub-section shows roles for that product only with org type constraints and permission counts

**Product role permission management**
- PlatformAdmin navigates to `/roles/{productRoleId}` → sees product badge (e.g., "SynqLien") in header
- Clicks "Assign Permission" → picker shows only SynqLien permissions (not CareConnect, not TENANT.*)
- Blue info notice explains scope limitation

**System role detail**
- PlatformAdmin navigates to `/roles/{systemRoleId}` → "Assign Permission" button absent
- Amber notice: "System roles cannot be modified"

**Assignment fix validation**
- `POST /api/identity/admin/roles/{id}/permissions` body is now `{ "permissionId": "..." }` → backend binds correctly → assignment succeeds

**Governance boundary**
- Control Center: product/platform permissions only, product roles only
- Tenant Portal: TENANT.* permissions, tenant roles only
- No mixing of surfaces ✓

### Regression check
- Tenant Portal `/authorization/permissions` tab — unaffected (separate BFF, separate pages, separate auth guards)
- `/api/identity/[...path]` Tenant Portal BFF — unaffected (different Next.js app)
- LS-ID-TNT-012 backend enforcement — no backend changes, enforcement intact
- Invite flow, user management, group management — no changes made to those pages

---

## 12. Known Issues / Gaps

1. **Product role create/edit/archive** — backend has no CREATE/UPDATE/ARCHIVE endpoints for role entities. Product roles are seeded at provisioning time from product definitions. UI for role lifecycle management deferred until backend supports it.

2. **Cross-product permission assignment** — the backend does NOT prevent assigning a CareConnect permission to a SynqLien role (no cross-product guard in the backend). The frontend now prevents this via `productCode` filtering in the picker, but a determined PlatformAdmin calling the API directly could still create cross-product mappings. Backend-level guard is a future hardening item.

3. **Permissions page `PermissionRowActions`** — the edit/deactivate row actions on the permissions catalog predate this feature and call `/api/identity/admin/permissions/{id}` endpoints. The BFF routes for those actions were not found in the CC (they may call the identity gateway directly or have a catch-all proxy). Not modified in this ticket.

4. **Products page (`/products`) remains MOCKUP** — the Products page in the CATALOG section is still a mockup. A future iteration could add a product-centric governance entry point that links to that product's roles and permissions.

5. **TenantAdmin in CC** — the `/roles` list page already used `requirePlatformAdmin()`. With this ticket, the detail page and BFF are also hardened. However, if a TenantAdmin somehow reaches CC (e.g., via a direct URL), they are now correctly blocked from role-permission editing.

6. **`PermissionCreateDialog` uses server action** — the create dialog pre-exists and appears to call server actions or the CC API client directly. Not audited for this ticket since permission creation is in scope but not the primary LS-ID-TNT-014 deliverable.

---

## 13. Final Status

**COMPLETE**

### Chosen Control Center placement
Existing `/permissions` and `/roles` + `/roles/[id]` pages — enhanced rather than replaced.

### New mutation APIs required
None — all existing backend endpoints already supported the needed operations.

### Product governance actions supported
- ✅ View product permission catalog (filtered to product, searchable, grouped by product/category)
- ✅ View product roles grouped by product with permission counts
- ✅ View product role → permission mappings
- ✅ Assign a product permission to a product role (within that product's permissions only)
- ✅ Revoke a product permission from a product role
- ✅ System roles visible as read-only with clear governance messaging

### Product roles fully maintainable?
**Permission mappings only** — role metadata create/edit/archive is deferred (no backend CRUD for role entities).

### Deferred from full platform governance
- Product role create/edit/archive UI
- Backend cross-product permission assignment guard
- Products page (`/products`) integration with role/permission navigation

### Governance boundaries
- ✅ Control Center: product and platform permissions only; product roles; PlatformAdmin only
- ✅ Tenant Portal: TENANT.* permissions; tenant roles; TenantAdmin only
- ✅ Explicit notices in both surfaces reinforcing the separation
- ✅ LS-ID-TNT-013 Tenant Portal permission management unaffected
