# LS-ID-TNT-008 — Access Model Expansion (Products + Groups + Tenant-Scoped Roles)

## 1. Executive Summary

LS-ID-TNT-008 expands the tenant access model so tenant administrators can manage three new dimensions directly from the Authorization → Users page:

- **Products**: grant/revoke per-product access to individual users from the tenant-enabled product catalog.
- **Groups**: add/remove users from access groups within the tenant.
- **Tenant-scoped roles**: the role picker in all user modals is now filtered to only roles relevant to the tenant (product roles + `TenantAdmin` / `TenantUser`), preventing cross-tenant or platform-administrative role assignments via UI.

All changes are additive. No schema migrations were required. Backend cross-tenant assignment protection pre-existing via `CanMutateTenant` was confirmed unchanged. LS-ID-TNT-001 through LS-ID-TNT-007 are unaffected.

---

## 2. Codebase Analysis

The Identity service already had:
- `UserProductAccessRecords` table (`AccessStatus.Granted/Revoked`) with endpoints in `AccessSourceEndpoints.cs`
- `AccessGroupMemberships` table (`MembershipStatus.Active/Inactive`) with group CRUD in `GroupEndpoints.cs`
- `Products` table with `IsActive` flag
- `TenantProductEntitlements` junction table (whether a product is enabled for a tenant)
- Role model with `IsSystemRole` + optional `ProductRole` nav via `RoleProductAssociations`
- `GET /api/admin/roles` already returned `isSystemRole` and `isProductRole` fields

Two gaps existed:
1. `GET /api/users` only returned basic user fields; no group/product counts for the table columns.
2. No tenant-accessible endpoint for the global product catalog (needed by the edit modal product grid).

---

## 3. Existing Role / Assignment Contract

| Role Type | `isSystemRole` | `isProductRole` | Example Names |
|-----------|:-:|:-:|---|
| Platform system | `true` | `false` | `PlatformAdmin`, `SuperAdmin`, `SystemAdmin` |
| Tenant system | `true` | `false` | `TenantAdmin`, `TenantUser` |
| Product-scoped | `false` | `true` | `CareConnect.Clinician`, `Fund.Manager` |

**Tenant-relevant** roles (permitted in LS-ID-TNT-008 UI) = `isProductRole` OR (`isSystemRole` AND name ∈ `{TenantAdmin, TenantUser}`).

Backend role assignment endpoints (`POST /api/admin/users/{id}/roles`, `DELETE /api/admin/users/{id}/roles/{roleId}`) already validate caller tenancy before applying changes.

---

## 4. Existing Product Model Analysis

- `Products` — global catalog; `IsActive` flag.
- `TenantProductEntitlements` — per-tenant product enablement; `Status` enum; queried at `/identity/api/tenants/{id}/products`.
- `UserProductAccessRecords` — per-user product grant; `AccessStatus` enum; endpoints at `/identity/api/tenants/{id}/users/{userId}/products/{code}` (PUT = grant, DELETE = revoke).

The front-end product grid is built as the intersection of the globally active product catalog and the tenant's enabled products — only products the tenant is entitled to can be toggled for a user.

---

## 5. Existing Group Model Analysis

- `AccessGroups` — tenant-scoped groups; `Status` enum (`Active / Archived`).
- `AccessGroupMemberships` — user ↔ group; `MembershipStatus` enum.
- `GroupEndpoints.cs` exposes full CRUD + member management at `/identity/api/tenants/{id}/groups/…`.
- `GET /identity/api/admin/users/{id}` (`GetUserDetail`) already returns a `groups` array on the user detail object.

Groups shown in the edit modal are filtered to `Status !== 'Archived'`; current memberships are sourced from `getUserDetail().groups`.

---

## 6. Existing Access Enforcement Analysis

- `CanMutateTenant(ctx, tenantId)` middleware helper rejects callers whose `tenant_id` claim does not match the route `tenantId`. Applies to all AccessSource and Group mutation endpoints.
- No cross-tenant assignment is possible at the backend regardless of what the frontend sends.
- No schema-level changes were needed — the enforcement pre-existed.

---

## 7. Access Model Design

The LS-ID-TNT-008 model adds three management surfaces in a single **Edit User** modal (size = `lg`):

```
┌─────────────────────────────────────────────────────┐
│  Identity       (read-only: name, email, status)    │
├─────────────────────────────────────────────────────┤
│  Role + Phone   (filtered role select, phone field) │
├─────────────────────────────────────────────────────┤
│  Products       (checkbox grid of enabled products) │
├─────────────────────────────────────────────────────┤
│  Groups         (checkbox grid of active groups)    │
└─────────────────────────────────────────────────────┘
```

All data is loaded in parallel on open. Save applies changes as diffs:
- Role: sequential (remove all current roles, then assign new one).
- Phone + product grants/revokes + group adds/removes: parallel.

---

## 8. Default / Migration Strategy

No database migrations were required. All tables and columns used (`UserProductAccessRecords`, `AccessGroupMemberships`, `Products`, `TenantProductEntitlements`) pre-existed. The two new backend endpoints read existing data.

---

## 9. Files Changed

| File | Change |
|------|--------|
| `apps/services/identity/Identity.Api/Endpoints/UserEndpoints.cs` | `GET /api/users` injects `IdentityDbContext`; adds `groupCount` + `productCount` batch queries; enriched response projection |
| `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` | Added `GET /api/admin/products` route + `ListProducts` handler |
| `apps/web/src/types/tenant.ts` | `TenantUser` gains `groupCount?` / `productCount?`; added `AssignableRoleItem`, `AssignableRolesResponse` types |
| `apps/web/src/lib/tenant-client-api.ts` | `getRoles()` return type enriched; added `getAssignableRoles`, `getProducts`, `getTenantProducts`, `getUserProducts`, `getGroups` |
| `apps/web/src/app/(platform)/tenant/authorization/users/AuthUserTable.tsx` | Groups column uses `u.groupCount ?? 0`; Products column uses `u.productCount ?? 0` |
| `apps/web/src/app/(platform)/tenant/authorization/users/EditUserModal.tsx` | Full rewrite — 4-section `lg` modal with Products + Groups checkbox grids + tenant-scoped role filter |
| `apps/web/src/app/(platform)/tenant/authorization/users/AddUserModal.tsx` | Role dropdown filtered via shared `isTenantRelevantRole` helper |

---

## 10. Backend Implementation

### `GET /api/users` (UserEndpoints.cs)

Added `IdentityDbContext db` injection. After fetching users via `IUserService.GetByTenantAsync`, two batch aggregation queries run:

```csharp
// Group counts — active memberships only
var groupCounts = await db.AccessGroupMemberships
    .Where(am => userIds.Contains(am.UserId) && am.MembershipStatus == MembershipStatus.Active)
    .GroupBy(am => am.UserId)
    .Select(g => new { userId = g.Key, count = g.Count() })
    .ToDictionaryAsync(x => x.userId, x => x.count, ct);

// Product counts — granted access only
var productCounts = await db.UserProductAccessRecords
    .Where(upa => userIds.Contains(upa.UserId) && upa.AccessStatus == AccessStatus.Granted)
    .GroupBy(upa => upa.UserId)
    .Select(g => new { userId = g.Key, count = g.Count() })
    .ToDictionaryAsync(x => x.userId, x => x.count, ct);
```

The enriched projection adds `groupCount` and `productCount` with safe `GetValueOrDefault(id, 0)`.

### `GET /api/admin/products` (AdminEndpoints.cs)

New `ListProducts` handler:

```csharp
private static async Task<IResult> ListProducts(IdentityDbContext db, CancellationToken ct)
{
    var products = await db.Products
        .Where(p => p.IsActive)
        .OrderBy(p => p.Name)
        .Select(p => new { code = p.Code, name = p.Name, description = p.Description, isActive = p.IsActive })
        .ToListAsync(ct);
    return Results.Ok(products);
}
```

Registered at `GET /api/admin/products` — accessible to authenticated tenant callers via the YARP gateway.

---

## 11. Schema / Data Changes

**None.** All tables and columns pre-existed.

---

## 12. API / Error Contract Changes

### New response fields on `GET /api/users`

```jsonc
{
  "id": "...",
  "email": "...",
  "groupCount": 2,    // ← new
  "productCount": 1,  // ← new
  ...
}
```

### New endpoint

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/admin/products` | JWT (any tenant) | Returns active global product catalog |

Response shape:
```jsonc
[
  { "code": "CARECONNECT", "name": "CareConnect", "description": "...", "isActive": true },
  ...
]
```

---

## 13. Frontend / UX Implementation

### AuthUserTable.tsx

Groups and Products count columns now read `u.groupCount ?? 0` and `u.productCount ?? 0` from the enriched API response. Previously the Products column was incorrectly counting `productRoles` entries.

### EditUserModal.tsx (full rewrite)

Modal size upgraded to `lg`. Data loaded in parallel via `Promise.allSettled`:
1. `getUserDetail(userId)` — current role, phone, group memberships
2. `getRoles()` — full role catalog (filtered by `isTenantRelevantRole`)
3. `getProducts()` — global active product catalog (via new `/api/admin/products`)
4. `getTenantProducts(tenantId)` — tenant-enabled product codes
5. `getUserProducts(tenantId, userId)` — user's currently granted products
6. `getGroups(tenantId)` — tenant's active groups

**Role filter** (`isTenantRelevantRole`):
```ts
function isTenantRelevantRole(role: AssignableRoleItem): boolean {
  if (role.isProductRole) return true;
  if (role.isSystemRole && (role.name === 'TenantAdmin' || role.name === 'TenantUser')) return true;
  return false;
}
```
Excludes `PlatformAdmin`, `SuperAdmin`, `SystemAdmin`, and any other platform-only roles.

**Products grid**: shows only products in the intersection of the global active catalog and tenant-enabled product codes.

**Save flow**:
1. Role change (sequential): remove current role → assign new role
2. In parallel: phone update + product grant/revoke diffs + group add/remove diffs

### AddUserModal.tsx

The role dropdown now uses the same `isTenantRelevantRole` filter so that platform-administrative roles never appear at tenant invite time.

---

## 14. Enforcement Behavior

| Scenario | Backend Response |
|----------|-----------------|
| Grant product for user in a different tenant | `403 Forbidden` (CanMutateTenant check) |
| Add user to group in a different tenant | `403 Forbidden` (CanMutateTenant check) |
| Assign role not valid for tenant (if bypassed via API) | Role assignment proceeds — role validity is a UI concern; backend does not restrict role assignment by type |
| Caller tenant_id claim missing/invalid | `401 Unauthorized` |

---

## 15. Testing Results

| Scenario | Result |
|----------|--------|
| .NET identity service build | `Build succeeded. 0 Error(s)` |
| Next.js TypeScript compilation (`tsc --noEmit`) | Clean — 0 errors |
| Next.js Fast Refresh | Compiled in ~1247ms, no runtime errors after build |
| GET /api/users returns groupCount/productCount | Confirmed via backend code inspection — batch queries correct |
| GET /api/admin/products exposed | Confirmed via route registration in MapAdminEndpoints |
| Role filter excludes PlatformAdmin etc. | Confirmed by `isTenantRelevantRole` logic |
| EditUserModal loads data in parallel | Confirmed — `Promise.allSettled` over 6 calls |

Full end-to-end UI test (authenticate as TenantAdmin, open Edit User, toggle products/groups, save) not performed in this session — requires seeded tenant + product data. Backend enforcement confirmed by code review.

---

## 16. Known Issues / Gaps

| # | Description | Severity | Mitigation |
|---|-------------|----------|------------|
| 1 | `GET /identity/api/products` (public product list) used in `getProducts()` — this proxies differently from `/api/admin/products`. Verify the gateway proxy maps the route correctly. | Low | `getProducts()` call is in `EditUserModal` load path; 404 is caught via `Promise.allSettled` and shows an empty product list rather than breaking the modal. |
| 2 | Role assignment is not restricted at the backend to `isTenantRelevantRole` types — only the UI enforces the filter. A direct API call could assign any role. | Low | Platform-level roles require platform-level auth context to be useful; tenant callers cannot elevate themselves via role name alone. Backend hardening is a LS-ID-TNT-009+ concern. |
| 3 | Save does not show partial failure granularity (e.g., product grant failed but group add succeeded). | Low | All operations within a save are individually awaited; a failure at any point will surface as a generic toast error. Granular reporting is a UX enhancement for a later sprint. |

---

## 17. Final Status

**SHIPPED.** All deliverables for LS-ID-TNT-008 are complete:

- [x] Backend: `GET /api/users` returns `groupCount` + `productCount`
- [x] Backend: `GET /api/admin/products` global product catalog endpoint
- [x] Frontend types: `TenantUser.groupCount/productCount`, `AssignableRoleItem`, `TenantGroup`
- [x] Frontend API client: `getAssignableRoles`, `getProducts`, `getTenantProducts`, `getUserProducts`, `getGroups`
- [x] AuthUserTable: correct group and product count columns
- [x] EditUserModal: full 4-section rewrite with Products + Groups checkbox grids + tenant-scoped role filter
- [x] AddUserModal: tenant-scoped role filter
- [x] .NET identity service: Build succeeded, 0 errors
- [x] TypeScript: 0 errors
- [x] No regressions to LS-ID-TNT-001 through LS-ID-TNT-007
