# LS-ID-TNT-013 — Tenant Portal Permission Management UI

## 1. Executive Summary

LS-ID-TNT-013 adds a **Permission Management UI** to the Tenant Portal's Authorization section. It gives tenant administrators:

1. **Role Permissions editor** — view and edit which TENANT.* permissions are assigned to each tenant role.
2. **Permission Catalog viewer** — a read-only reference of all available tenant-level permissions grouped by category.
3. **User effective permission inspection** — continued via the existing user detail page (Effective Access) and Access Explainability tab; both already surface permission sources comprehensively.

A single new backend endpoint was added to expose the TENANT.* permission catalog in a tenant-isolated, tenant-admin-accessible way. All existing role-permission mutation endpoints already supported TenantAdmin access for non-system roles — no mutation endpoint changes were needed.

Governance boundaries are preserved: only `TENANT.*` (SYNQ_PLATFORM) permissions appear in the Tenant Portal UI. Product/platform governance remains in Control Center.

---

## 2. Codebase Analysis

### Permission API Layer
- `GET /api/admin/permissions/catalog` — returns all permissions grouped by product. No handler-level auth check (gateway enforces JWT validity only). Used by Control Center.
- `GET /api/admin/permissions/role-assignments` — returns system role → permission seed mappings. No handler-level auth check.
- `GET /api/permissions/effective` — tenant-scoped effective permissions for a user. Standard JWT.
- `GET /api/admin/roles` — returns all roles with `{ id, name, isSystemRole, isProductRole, productCode?, productName? }`. Called by Tenant Portal already.
- `GET /api/admin/roles/{id}/permissions` — returns role's assigned permissions. TenantAdmin can access for own tenant's roles.
- `POST /api/admin/roles/{id}/permissions` — assign permission to role. TenantAdmin can call for non-system roles in own tenant.
- `DELETE /api/admin/roles/{id}/permissions/{permId}` — revoke permission from role. Same guard as above.

### Gateway Authorization
The YARP gateway enforces **authenticated JWT only** for `/identity/{**catch-all}`. It does NOT discriminate between PlatformAdmin and TenantAdmin at the routing layer. All granular RBAC is in the handlers.

### Role Entity
```csharp
Role { Id, TenantId, Name, Description, IsSystemRole, CreatedAtUtc, UpdatedAtUtc }
```
`IsSystemRole = true` for `TenantAdmin` and `StandardUser` (platform-managed). Custom tenant roles have `IsSystemRole = false`.

### Existing Tenant Portal Authorization UI
- Auth section at `/tenant/authorization/` with tabs: Users, Groups, Access, Simulator.
- **User detail** (`/users/[userId]`): already shows Effective Products, Effective Roles, Effective Permissions, Policy Impact via `accessDebug` API response.
- **Access Explainability** (`/access`): has sub-tabs including User Explorer (drill into any user's permissions).
- **No existing "Roles" or "Permissions" management tab** in the Tenant Portal.

### Data flow
- Server Components: `tenantServerApi` → `serverApi.get(GATEWAY_URL + path)` with `Authorization: Bearer <cookie-token>`
- Client Components: `tenantClientApi` → `apiClient.get(/identity/api/...)` → BFF proxy at `/api/identity/[...path]` → reads `platform_session` cookie, adds `Authorization: Bearer`, forwards to `GATEWAY_URL/identity/...`

---

## 3. Existing Permission API Analysis

### Mutation endpoints available to TenantAdmin
`POST /api/admin/roles/{id}/permissions` with body `{ permissionId: string }`:
- Guard 1: `IsSystemRole && !PlatformAdmin → 403` — prevents TenantAdmin from editing system roles
- Guard 2: `IsCrossTenantAccess → 403` — prevents TenantAdmin from editing other tenants' roles
- Result: TenantAdmin **CAN** assign permissions to non-system roles in their own tenant ✓

`DELETE /api/admin/roles/{id}/permissions/{permId}`:
- Same guards as above
- TenantAdmin CAN revoke permissions from non-system roles in their own tenant ✓

### New endpoint needed
`GET /api/tenants/{tenantId}/permissions/tenant-catalog`:
- Required because: no existing endpoint returns **only** TENANT.* permissions in a tenant-scoped, TenantAdmin-accessible way
- The existing `/api/admin/permissions/catalog` returns all permissions (all products) with no tenant isolation claim
- Adding a clean, purpose-built endpoint keeps the API explicit and properly isolated

### Effective permission inspection
`GET /api/permissions/effective` already exists and is callable from Tenant Portal via standard JWT. The existing user detail page at `/tenant/authorization/users/[userId]` calls `/api/admin/users/{userId}/access-debug` which gives richer source-annotated data. Both paths work.

---

## 4. Existing Roles / Authorization UI Analysis

### Authorization nav tabs (before LS-ID-TNT-013)
- Users → `/tenant/authorization/users`
- Groups → `/tenant/authorization/groups`
- Access → `/tenant/authorization/access`
- Simulator → `/tenant/authorization/simulator`

### Modal / panel patterns
- `UserDetailClient.tsx`: two-panel layout with identity info left + editable access info right; inline role picker using `showRolePicker` state flag; `ConfirmModal` for destructive actions; Toast notifications.
- `GroupDetailClient.tsx`: similar pattern — list + inline action panels.
- `AccessExplainabilityClient.tsx`: multi-sub-tab view with User Explorer, Permissions, Search.

### Role list shape (from `GET /api/admin/roles`)
```json
[{ "id": "...", "name": "...", "isSystemRole": true/false, "isProductRole": true/false, "productCode": "...", "productName": "..." }]
```

### Existing `getRolePermissions` in tenant-api.ts
Already defined at line 74 calling `GET /api/admin/roles/{roleId}/permissions`. This is the server-side method.

---

## 5. UI Placement and Interaction Design

### Chosen placement: new **Permissions** tab in the Authorization nav

**Rationale:**
- The Authorization section is the natural home for permission management — it already handles users, groups, access explainability, and simulation.
- Adding a "Permissions" tab keeps the admin surface cohesive and consistent.
- A dedicated tab allows the two distinct views (Role Permissions editor + Catalog) to coexist cleanly.
- No existing tab would be overloaded or redesigned.

**Rejected options:**
- _Extend existing Roles page_: No standalone roles page exists in the Tenant Portal; roles are only shown in the context of users and groups.
- _Add to Access tab_: The Access tab already has four sub-tabs (Overview, User Explorer, Permissions, Search); adding role-permission editing would make it too wide in scope.
- _Extend group detail_: Group detail manages role assignments to groups, not the permission definitions within roles — different abstraction level.

### View switcher inside the Permissions tab
- **"Role Permissions"** (default): left column = roles list; right column = permission checklist for selected role; editable for non-system, non-product roles; read-only with explanation for system roles.
- **"Permission Catalog"**: read-only reference list of all TENANT.* permissions, grouped by category with code + description.

### User effective permission inspection
The existing paths are already functional:
- `/tenant/authorization/users/[userId]` → Effective Permissions section (sourced from `accessDebug`)
- `/tenant/authorization/access` → User Explorer sub-tab

No additional inspection surface was added (LS-ID-TNT-013 scope allows deferring this enhancement since the functionality exists).

---

## 6. Mutation API Design

### New endpoint: `GET /api/tenants/{tenantId}/permissions/tenant-catalog`
**Location:** `PermissionCatalogEndpoints.cs`

**Authorization:**
- PlatformAdmin: any `tenantId`
- TenantAdmin: only own `tenantId` (JWT `tenant_id` claim must match)

**Response:**
```json
{
  "tenantId": "...",
  "permissions": [
    { "id": "...", "code": "TENANT.users:manage", "name": "Manage Users", "description": "...", "category": "User Management" }
  ],
  "totalCount": 8
}
```

### Reused mutation endpoints (no changes needed)
- `POST /api/admin/roles/{id}/permissions` — body: `{ permissionId: string }`
- `DELETE /api/admin/roles/{id}/permissions/{permissionId}`

Both already support TenantAdmin for non-system roles in their tenant via the `IsSystemRole` and `IsCrossTenantAccess` guards.

---

## 7. Files Changed

| File | Change type | Description |
|---|---|---|
| `Identity.Api/Endpoints/PermissionCatalogEndpoints.cs` | Modified | Added `using System.Security.Claims`; registered `GET /api/tenants/{tenantId}/permissions/tenant-catalog`; added `GetTenantPermissionCatalog` handler |
| `apps/web/src/types/tenant.ts` | Modified | Added `TenantPermissionCatalogItem`, `TenantPermissionCatalogResponse`, `RolePermissionEntry`, `RolePermissionsResponse`, `TenantRoleItem` |
| `apps/web/src/lib/tenant-api.ts` | Modified | Added `getTenantPermissionCatalog(tenantId)`; typed `getRoles()` return as `TenantRoleItem[]` |
| `apps/web/src/lib/tenant-client-api.ts` | Modified | Added `getRolePermissions(roleId)`, `assignRolePermission(roleId, permissionId)`, `revokeRolePermission(roleId, permissionId)` |
| `apps/web/src/components/tenant/authorization-nav.tsx` | Modified | Added `Permissions` tab (`ri-key-2-line` icon) between Access and Simulator |
| `apps/web/src/app/(platform)/tenant/authorization/permissions/page.tsx` | Created | Server Component: loads `getTenantPermissionCatalog` + `getRoles`; error boundary; renders `PermissionsClient` |
| `apps/web/src/app/(platform)/tenant/authorization/permissions/PermissionsClient.tsx` | Created | Client Component: view switcher (Role Permissions / Catalog), roles list, permission checklist with pending-change tracking, save/cancel/toast |

---

## 8. Backend Implementation

### `GetTenantPermissionCatalog` handler
```csharp
private static async Task<IResult> GetTenantPermissionCatalog(
    Guid tenantId, IdentityDbContext db, ClaimsPrincipal caller, CancellationToken ct)
{
    if (!caller.IsInRole("PlatformAdmin"))
    {
        var raw = caller.FindFirstValue("tenant_id");
        if (raw is null || !Guid.TryParse(raw, out var callerTid) || callerTid != tenantId)
            return Results.Forbid();
    }

    var permissions = await db.Permissions
        .Where(p => p.IsActive && p.Product.Code == ProductCodes.SynqPlatform)
        .OrderBy(p => p.Category).ThenBy(p => p.Code)
        .Select(p => new { p.Id, p.Code, p.Name, p.Description, p.Category })
        .ToListAsync(ct);

    return Results.Ok(new { TenantId = tenantId, Permissions = permissions, TotalCount = permissions.Count });
}
```

The `ProductCodes.SynqPlatform` constant (`"SYNQ_PLATFORM"`) filters to TENANT.* capabilities only, since all TENANT.* permissions are seeded under the SYNQ_PLATFORM pseudo-product.

---

## 9. Frontend / UX Implementation

### `authorization-nav.tsx`
Added `{ href: '/tenant/authorization/permissions', label: 'Permissions', icon: 'ri-key-2-line' }` between Access and Simulator tabs.

### `permissions/page.tsx` (Server Component)
- Calls `requireTenantAdmin()` for access control (redirects non-admin users)
- `Promise.allSettled` for `getTenantPermissionCatalog(tid)` + `getRoles()` — graceful degradation if one fails
- Passes `tenantPermissions`, `roles`, `isTenantAdmin` to `PermissionsClient`

### `permissions/PermissionsClient.tsx` (Client Component)

#### View: Role Permissions
- **Left column**: filtered list of non-product roles (both system and tenant custom roles); system roles labeled "System role — read only"; selected role highlighted with primary border
- **Right column**:
  - Empty state: icon + instructional text
  - System role selected: amber notice that permissions are platform-managed
  - Non-system role selected: full TENANT.* permission checklist grouped by category
  - Each permission: checkbox + name + description + code
  - `unsaved` badge on items with pending changes
  - Save/Cancel buttons appear in header and in sticky bottom bar when pending changes exist
  - Change count shown in Save button label

#### Pending change tracking
- `pendingAdd: Set<string>` — permission IDs to be assigned
- `pendingRemove: Set<string>` — permission IDs to be revoked
- `isChecked(permId)`: pendingAdd → true, pendingRemove → false, else DB state
- On Save: `Promise.all([...addOps, ...removeOps])` then reload

#### View: Permission Catalog
- Read-only grouped list of all TENANT.* permissions with code, name, description
- Footer note: product-level permissions are not shown here

#### Authorization enforcement
- `isEditable = isTenantAdmin && !selectedRole.isSystemRole && !selectedRole.isProductRole`
- Checkboxes disabled when `!isEditable || saving`
- Backend also enforces: `assignRolePermission` / `revokeRolePermission` return 403 for system roles (handler guard)
- `ApiError.isForbidden` checked in save() to show a clear 403 message

---

## 10. API / Error Contract Changes

### New endpoint
`GET /api/tenants/{tenantId}/permissions/tenant-catalog`
- `200 OK` with `{ tenantId, permissions: [...], totalCount }`
- `403 Forbidden` if TenantAdmin accesses another tenant's catalog

### Existing endpoints (no changes)
- `POST /api/admin/roles/{id}/permissions` → `201 Created` on success, `403` for system role or cross-tenant, `404` if role/permission not found, `200` if already assigned (idempotent)
- `DELETE /api/admin/roles/{id}/permissions/{permId}` → `204 No Content` on success, `403` / `404` on failure

### Frontend error handling
- Load errors: inline red banner in the role detail panel
- Save errors: toast notification (4-second auto-dismiss); 403 gives specific "You do not have permission" message
- Network errors: generic error message in toast
- Empty states: styled placeholders for no roles / no permissions / no tenant permissions in catalog

---

## 11. Testing Results

### Build verification
- `.NET` services compile and start cleanly with new `GetTenantPermissionCatalog` endpoint
- Next.js compiles with new TypeScript types, updated API clients, nav, page, and client component

### Functional scenarios

**Permission catalog load**
- TenantAdmin navigates to `/tenant/authorization/permissions` → sees Role Permissions view with roles list
- Selects catalog tab → sees all 8 TENANT.* permissions grouped by category

**Role permission view**
- TenantAdmin selects a system role (e.g., "Tenant Admin") → amber notice displayed, checkboxes disabled, correct permissions shown in checked state
- TenantAdmin selects a custom tenant role → editable checklist shown

**Role permission editing**
- TenantAdmin checks an unchecked permission → "unsaved" badge appears, Save button activates
- TenantAdmin clicks Save → `assignRolePermission` / `revokeRolePermission` called → success toast shown → list reloaded
- TenantAdmin clicks Cancel → pending changes discarded

**Authorization enforcement**
- StandardUser (non-admin) cannot reach this page (`requireTenantAdmin()` redirects)
- TenantAdmin cannot save on system roles (checkboxes disabled; backend also returns 403)

**Regression**
- Users, Groups, Access, Simulator tabs unchanged and functional
- Invite flow, password reset, product/group assignment unaffected
- LS-ID-TNT-012 backend permission enforcement continues to apply to all mutation routes

---

## 12. Known Issues / Gaps

1. **User effective permission inspection** — no new surface added to the Permissions page for user inspection. Existing paths (`/users/[userId]` Effective Access section and `/access` User Explorer) already provide this functionality. A future iteration could add a "Inspect User" search panel to the Permissions page.

2. **Role permission count badge** — the roles list does not show how many TENANT.* permissions each role currently has without selecting each one. This would require an additional fetch per role on page load. Deferred for performance reasons.

3. **Audit logging** — `assignRolePermission` / `revokeRolePermission` calls in AdminEndpoints already write audit events (from prior implementation). The new tenant catalog endpoint does not audit reads (consistent with read-only endpoints pattern).

4. **Product role display** — product roles are filtered out of the Tenant Portal Permissions view (they are only listed for context in other tabs). Product role → permission editing remains in Control Center.

5. **TenantSettingsManage / TenantAuditView enforcement** — read-only permission gating remains deferred (LS-ID-TNT-013+ scope).

6. **`AccessExplainabilityClient` Permissions sub-tab** — that tab shows all permissions (not just TENANT.*) for cross-reference. This is fine — it's a diagnostic view, not a governance surface.

---

## 13. Final Status

**COMPLETE**

- ✅ New `GET /api/tenants/{tenantId}/permissions/tenant-catalog` endpoint returns TENANT.* permissions only, with TenantAdmin tenant isolation guard
- ✅ New **Permissions** tab added to Authorization nav
- ✅ Tenant permission catalog view implemented (read-only, grouped by category)
- ✅ Tenant role → permission view implemented (roles list + checklist)
- ✅ Tenant role → permission editing implemented (non-system, non-product roles only)
- ✅ System roles shown as read-only with clear governance explanation
- ✅ Product/platform permissions not exposed in Tenant Portal
- ✅ Save/cancel/pending-change tracking with clear feedback
- ✅ 403 errors surfaced with role-appropriate messaging
- ✅ User effective permission inspection continues via existing `/users/[userId]` and `/access` tabs
- ✅ LS-ID-TNT-001 through LS-ID-TNT-012 behavior not regressed
- ✅ Governance boundary between Tenant Portal (TENANT.* only) and Control Center (all permissions) preserved
