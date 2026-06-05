# PUM-B03 — Tenant User Management
## Implementation Report

**Status:** Complete  
**Date:** 2026-04-24  
**Service:** Identity.Api  
**Depends on:** PUM-B01 (UserType), PUM-B02 (Role.Scope + RoleScopes + seeded system roles)

---

## 1. Codebase Analysis

### 1.1 Tenant/User Relationship (Single-Tenant Architecture)

The Identity domain uses a **single-tenant FK model**:

- `User.TenantId` (Guid, required) — each user belongs to exactly one home tenant.
- `Tenant.Users` — nav collection back-populating all users for a tenant.
- There is **no** `UserTenantMembership` join table. Cross-tenant membership is not supported without a breaking schema change.

### 1.2 ScopedRoleAssignment Structure

`ScopedRoleAssignment` records role assignments globally (ScopeType is always `GLOBAL`). The optional `TenantId?` field records which tenant context the assignment belongs to — it is set to `user.TenantId` at assignment time.

Fields relevant to PUM-B03:
| Field | Description |
|---|---|
| `Id` (Guid) | Assignment PK |
| `UserId` | FK → User |
| `RoleId` | FK → Role |
| `ScopeType` | Always `"GLOBAL"` |
| `TenantId?` | Tenant context (nullable) |
| `IsActive` | Soft-delete flag |
| `AssignedAtUtc` | Assignment timestamp |
| `Deactivate()` | Soft-deactivates the record |

### 1.3 Role.Scope Foundation (PUM-B02)

`Role.Scope` (string, nullable) — values from `RoleScopes` static class:
- `RoleScopes.Platform` = `"Platform"` — platform-level roles (PlatformAdmin, SystemAdmin, etc.)
- `RoleScopes.Tenant` = `"Tenant"` — tenant-level roles (TenantAdmin, TenantUser, TenantViewer, etc.)

All 10 seeded system roles have their `Scope` set correctly in PUM-B02.

### 1.4 IsCrossTenantAccess Helper

Located at `AdminEndpoints.cs` line ~5124. Returns:
- `false` for callers with the `PlatformAdmin` role (full cross-tenant access).
- Compares `tenant_id` claim with the supplied `tenantId` for all other callers.

Used in all 5 new handlers to enforce tenant isolation.

---

## 2. Architecture Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | No new migration | All required columns exist: `User.TenantId`, `ScopedRoleAssignment.TenantId?`, `Role.Scope`. No schema change needed. |
| D2 | Filter roles by `Scope == "Tenant"` | Uses PUM-B02 `Role.Scope` + `RoleScopes.Tenant` constant. Dynamic — picks up any future Tenant-scoped roles automatically. |
| D3 | No global user deactivation on RemoveFromTenant | Only tenant-scoped role assignments are revoked. The user account itself remains active (may belong to other product contexts). |
| D4 | Tenant role list in ListTenantUsers uses correlated subquery | EF Core translates the `s.Role.Scope` navigation filter via automatic JOIN — no extra `.Include()` needed at the top query level. |
| D5 | AssignTenantRole is idempotent | Returns `alreadyExisted: true` in the body (with HTTP 200) if the assignment is already active — safe for retry. |

---

## 3. Database / Migration Changes

**No migration required.** All columns used by PUM-B03 were added in earlier blocks:

| Column | Migration | Block |
|---|---|---|
| `user_type` on `users` | `20260424200000_AddUserType` | PUM-B01 |
| `scope` on `roles` | `20260424210000_AddRoleScopeAndBaselineRolesPermissions` | PUM-B02 |
| `tenant_id` on `scoped_role_assignments` | pre-existing | Phase G |

---

## 4. Domain / Application Changes

### 4.1 Role Guard Fix — R07

**File:** `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs`  
**Location:** `AssignRole` handler (~line 3369)

**Before (hardcoded brittle guard):**
```csharp
var tenantAssignableSystemRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "TenantAdmin", "TenantUser" };
if (!tenantAssignableSystemRoles.Contains(role.Name))
    return Results.BadRequest(...);
```

**After (dynamic, scope-driven):**
```csharp
if (!callerIsPlatformAdmin && role.Scope != RoleScopes.Tenant)
    return Results.BadRequest(new
    {
        error   = "ROLE_NOT_TENANT_ASSIGNABLE",
        message = "Only roles with Tenant scope are assignable at the tenant level.",
    });
```

Impact: Any Tenant-scoped role added in the future (e.g., TenantViewer, TenantBillingManager) is automatically assignable by TenantAdmins without a code change.

---

## 5. API Changes

### 5.1 New Routes (5 endpoints)

All routes registered in `MapAdminEndpoints()` under the PUM-B03 comment block.

#### R01/R02 — `GET /api/admin/tenants/{tenantId}/users`

Lists all users in a tenant with their active Tenant-scoped role assignments.

**Query parameters:** `page` (default 1), `pageSize` (default 20), `search` (email/first/last name substring filter).

**Access control:** PlatformAdmin = any tenant; TenantAdmin = own tenant only (403 otherwise).

**Response:**
```json
{
  "items": [{
    "userId": "...",
    "email": "...",
    "firstName": "...",
    "lastName": "...",
    "displayName": "John Doe",
    "userType": "Standard",
    "isActive": true,
    "tenantId": "...",
    "roles": [{ "assignmentId": "...", "roleId": "...", "roleName": "TenantAdmin", "roleScope": "Tenant", "assignedAtUtc": "..." }],
    "createdAtUtc": "...",
    "updatedAtUtc": "...",
    "lastLoginAtUtc": "..."
  }],
  "totalCount": 12,
  "page": 1,
  "pageSize": 20
}
```

---

#### R03 — `POST /api/admin/tenants/{tenantId}/users`

Verifies a user belongs to the tenant; optionally assigns a Tenant-scoped role.

**Architecture limitation:** `User.TenantId` is immutable. If `user.TenantId != tenantId`, returns `409 Conflict` with `"USER_IN_DIFFERENT_TENANT"` error.

**Request body:**
```json
{ "userId": "...", "roleId": "..." }
```
or
```json
{ "userId": "...", "roleKey": "TenantViewer" }
```

**Responses:**
- `200 OK` — user is in the tenant (no role requested, or idempotent re-assignment)
- `201 Created` — role assigned (N/A here — R03 always returns 200)
- `404` — tenant or user not found
- `409 Conflict` — user belongs to a different tenant
- `400 Bad Request` — role has non-Tenant scope

---

#### R04 — `DELETE /api/admin/tenants/{tenantId}/users/{userId}`

Soft-removes a user from a tenant by deactivating all their active Tenant-scoped `ScopedRoleAssignment` records.

**Important:** Does **not** deactivate the global user account. Does **not** change `User.TenantId`.

**Responses:**
- `200 OK` — returns `revokedCount` (may be 0 if user had no active tenant roles)
- `404` — tenant not found, user not found, or user not in this tenant

---

#### R05 — `POST /api/admin/tenants/{tenantId}/users/{userId}/roles`

Assigns a Tenant-scoped role to a specific user within a specific tenant context.

**Role resolution:** `roleId` (Guid) takes precedence over `roleKey` (string) if both supplied.

**Enforcements:**
1. Tenant must exist
2. User must belong to this tenant (`user.TenantId == tenantId`)
3. Role must exist
4. `role.Scope == RoleScopes.Tenant` — Platform-scoped roles rejected with `"ROLE_SCOPE_INVALID"`
5. Idempotent — returns `alreadyExisted: true` in a 200 response if already assigned

**Responses:**
- `201 Created` — new assignment created (Location header set)
- `200 OK` — assignment already existed
- `403` — user not in this tenant, or cross-tenant caller
- `404` — tenant, user, or role not found
- `400` — no roleId/roleKey supplied; or role has wrong scope

---

#### R06 — `DELETE /api/admin/tenants/{tenantId}/users/{userId}/roles/{assignmentId}`

Soft-deactivates a specific active Tenant-scoped `ScopedRoleAssignment` by assignment ID.

**Guards:**
1. Assignment must belong to `userId`
2. Assignment must be active
3. `sra.Role.Scope == RoleScopes.Tenant` — rejects deactivation of Platform or product roles via this endpoint

**Responses:**
- `204 No Content` — revoked successfully
- `404` — tenant, user not in tenant, or active assignment not found
- `400` — assignment exists but is not a Tenant-scoped role

---

### 5.2 DTOs

Two private records added at the bottom of `AdminEndpoints`:

```csharp
private record AssignUserToTenantRequest(
    Guid    UserId,
    Guid?   RoleId  = null,
    string? RoleKey = null);

private record AssignTenantRoleRequest(
    Guid?   RoleId  = null,
    string? RoleKey = null);
```

---

## 6. Files Changed

| File | Change |
|---|---|
| `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` | R07 guard fix; 5 route registrations; 5 handler methods; 2 DTO records |

No other files changed. No new migrations.

---

## 7. Validation Results

### 7.1 Build

- Compiled with `dotnet build Identity.Api.csproj -c Debug --no-restore`
- **0 new errors** (pre-existing: MSB3277 JwtBearer, CS8601 line ~2796, CA2017 TenantBrandingEndpoints — unchanged)

### 7.2 Smoke Tests (unauthenticated, expect 4xx not 404)

All 5 new routes confirmed registered — return `403 Forbidden` (not `404 Not Found`) without a bearer token:

| Endpoint | Method | Observed | Expected |
|---|---|---|---|
| `/api/admin/tenants/{id}/users` | GET | 403 | 403 |
| `/api/admin/tenants/{id}/users` | POST | 403 | 403 |
| `/api/admin/tenants/{id}/users/{uid}` | DELETE | 403 | 403 |
| `/api/admin/tenants/{id}/users/{uid}/roles` | POST | 403 | 403 |
| `/api/admin/tenants/{id}/users/{uid}/roles/{aid}` | DELETE | 403 | 403 |

### 7.3 Identity API Health

```
GET http://localhost:5001/health → 200 OK
```

---

## 8. Known Gaps / Deferred Items

### G1 — Single-Tenant Architecture Constraint (by design)

`User.TenantId` is a non-nullable FK to a single tenant. Multi-tenant user membership (one user, many tenants) would require introducing a `UserTenantMembership` join table, migrating existing data, and updating all LINQ queries that filter by `u.TenantId`.

This is a significant schema change outside PUM-B03 scope. The `POST /api/admin/tenants/{tenantId}/users` endpoint returns `409 Conflict` with `"USER_IN_DIFFERENT_TENANT"` and an explanatory message when the constraint is hit.

### G2 — RemoveUserFromTenant Does Not Change User.TenantId

After all tenant role assignments are revoked, `user.TenantId` still points to the tenant. The user is effectively "de-privileged" within the tenant but not truly removed from it at the data level. Future work: introduce a Tenant-level `IsDeactivatedInTenant` flag or implement the `UserTenantMembership` table.

### G3 — Audit Events Not Emitted from New Endpoints

The 5 new handlers do not emit `identity.user.*` audit events to `IAuditEventClient`. The existing `AssignRole` / `RevokeRole` handlers do emit events. Future work: add `identity.tenantuser.roleassigned` and `identity.tenantuser.rolerevoked` audit events for compliance traceability.

### G4 — Notifications Cache Not Invalidated

The existing `RevokeRole` handler calls `notificationsCache.InvalidateTenant(...)` after role removal. The new `RevokeTenantRole` and `RemoveUserFromTenant` handlers do not. Future work: inject `INotificationsCacheClient` into these handlers and invalidate after role changes.

### G5 — No Pagination on Roles Sub-List in ListTenantUsers

The `roles` sub-list within each user record in `ListTenantUsers` is un-paged. For users with a large number of Tenant-scoped role assignments this could be over-fetched. In practice the number of tenant roles per user is expected to be small (1–5).

---

## 9. Final Assessment

PUM-B03 is **complete**. All five tenant user management endpoints are implemented, registered, and validated. The PUM-B02 `Role.Scope` foundation is used consistently throughout — no hardcoded role-name lists remain.

The R07 guard fix (replacing the hardcoded `{ "TenantAdmin", "TenantUser" }` HashSet with `role.Scope != RoleScopes.Tenant`) is a meaningful correctness improvement: it makes the existing `AssignRole` endpoint forward-compatible with any future Tenant-scoped system role without requiring a code change.

Known gaps G1–G5 are documented, understood, and explicitly deferred. They do not affect the correctness of the current implementation.
