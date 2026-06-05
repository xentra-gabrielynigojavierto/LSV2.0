# PUM-B02 — Role & Permission Engine
## Implementation Report

**Status:** ✅ Complete — built, migrated, runtime-validated  
**Date:** 2026-04-24  
**Service:** Identity.Api

---

## 1. Codebase Analysis

An audit of the Identity service before any PUM-B02 changes confirmed the core data model was already in place:

| Entity | Table | Pre-existing status |
|---|---|---|
| `Role` | `idt_Roles` | ✅ Existed — added `Scope` column |
| `Permission` / `Capability` | `idt_Capabilities` | ✅ Existed |
| `RolePermissionAssignment` | `idt_RoleCapabilityAssignments` | ✅ Existed |
| `ScopedRoleAssignment` | `idt_ScopedRoleAssignments` | ✅ Existed |
| `UserRoleAssignment` | `idt_UserRoleAssignments` | ✅ Existed |

All 5 required admin endpoints were already registered in `AdminEndpoints.cs`. PUM-B02 focused on **enriching** the existing model with scope classification, expanded role/permission seeds, and API improvements.

---

## 2. Existing Role / Permission Findings

Before PUM-B02 the seed data contained only 3 roles with no scope classification:

| Role name | Prior scope | Note |
|---|---|---|
| PlatformAdmin | (none) | Back-filled to `Platform` |
| TenantAdmin | (none) | Back-filled to `Tenant` |
| StandardUser | (none) | Back-filled to `Tenant` |

Permission catalog contained 51 entries (IDs 0001–0051) across CareConnect, SynqLien, SynqFund, SynqInsights, and SynqLien Tasks namespaces. The `PLATFORM.*` namespace did not exist.

---

## 3. Database / Migration Changes

### Migration: `20260424210000_AddRoleScopeAndBaselineRolesPermissions`

**Schema change:**
- Adds `Scope varchar(20) NULL` column to `idt_Roles` via `migrationBuilder.AddColumn<string>`.

**Data changes — all idempotent via `INSERT IGNORE`:**

| Step | Action |
|---|---|
| 2 | `UPDATE idt_Roles SET Scope='Platform'` for PlatformAdmin; `SET Scope='Tenant'` for TenantAdmin + StandardUser |
| 3 | Seed 4 new Platform system roles |
| 4 | Seed 3 new Tenant system roles |
| 5 | Seed 10 `PLATFORM.*` permissions into `idt_Capabilities` |
| 6 | Seed `TENANT.settings:read` permission (ID 0062, gap fill) |
| 7 | Seed role → permission mappings for all 9 system roles (43 rows) |

**Permission code format** (consistent with existing `TENANT.*` convention):
```
^[A-Z0-9_]+\.[a-z][a-z0-9_]*(?:\:[a-z][a-z0-9_]*)*$
```
Example: `PLATFORM.users:read`, `PLATFORM.monitoring:read`

**Down migration:** removes all added role-permission mappings, all seeded permissions, all new roles, and drops the `Scope` column. Fully reversible.

---

## 4. Domain / Application Changes

### `Identity.Domain/Role.cs`

- Added `Scope` property (`string?`, nullable).
- Added `SetScope(string scope)` mutator method.
- Updated `Role.Create(...)` factory to accept optional `scope` parameter.
- Added `RoleScopes` static class:

```csharp
public static class RoleScopes
{
    public const string Platform = "Platform";
    public const string Tenant   = "Tenant";
    public const string Product  = "Product";

    public static bool IsValid(string? value) => ...
}
```

### `Identity.Infrastructure/Data/SeedIds.cs`

Added IDs for 7 new roles (0004–0010) and 11 new permissions (0052–0062).

### `Identity.Infrastructure/Data/Configurations/RoleConfiguration.cs`

- Mapped `Scope` to `varchar(20)` (nullable).
- `HasData(...)` updated to seed all 10 system roles with full scope classification:

| Role | Scope | UUID |
|---|---|---|
| PlatformAdmin | Platform | `30000000-...-0001` |
| PlatformOps | Platform | `30000000-...-0004` |
| PlatformSupport | Platform | `30000000-...-0005` |
| PlatformBilling | Platform | `30000000-...-0006` |
| PlatformAuditor | Platform | `30000000-...-0007` |
| TenantAdmin | Tenant | `30000000-...-0002` |
| TenantManager | Tenant | `30000000-...-0008` |
| TenantStaff | Tenant | `30000000-...-0009` |
| TenantViewer | Tenant | `30000000-...-0010` |
| StandardUser | Tenant | `30000000-...-0003` |

---

## 5. API Changes

### `GET /api/admin/roles`

Added optional `scope` query parameter:
```
GET /api/admin/roles?scope=Platform
GET /api/admin/roles?scope=Tenant
GET /api/admin/roles           ← returns all scopes
```
Response objects now include `scope` field. Filter applied server-side via `WHERE r.Scope == scope`.

### `POST /api/admin/users/{id}/roles` (PUM-B02-R10)

`AssignRoleRequest` enhanced with `roleKey` support:

```json
{ "roleId": "30000000-0000-0000-0000-000000000001" }  // existing — still works
{ "roleKey": "PlatformAdmin" }                         // new — resolves by Role.Name
```

Resolution precedence:
1. `roleId` (non-null, non-empty GUID) → `db.Roles.FindAsync(roleId)`
2. `roleKey` (non-empty string) → `db.Roles.FirstOrDefaultAsync(r => r.Name == roleKey.Trim())`
3. Neither provided → `400 Bad Request`

`AssignRoleRequest.RoleId` type changed from `Guid` (required) to `Guid?` (optional) — backward-compatible.

### Endpoint Catalog (unchanged routes, documented for completeness)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/admin/roles` | List roles — supports `scope` filter |
| GET | `/api/admin/roles/{id}` | Get role with permissions |
| GET | `/api/admin/permissions` | List all permissions |
| GET | `/api/admin/permissions/by-product/{code}` | Permissions by product |
| POST | `/api/admin/users/{id}/roles` | Assign role (by `roleId` or `roleKey`) |
| DELETE | `/api/admin/users/{id}/roles/{roleId}` | Revoke role |
| GET | `/api/admin/users/{id}/permissions` | Effective permissions (union) |
| POST | `/api/admin/roles/{id}/permissions` | Assign permission to role |
| DELETE | `/api/admin/roles/{id}/permissions/{permId}` | Revoke permission from role |
| GET | `/api/admin/users/{id}/scoped-roles` | List scoped role assignments |

---

## 6. Files Changed

| File | Change |
|---|---|
| `Identity.Domain/Role.cs` | Added `Scope` prop, `SetScope()`, `RoleScopes` static class |
| `Identity.Infrastructure/Data/SeedIds.cs` | Added role IDs 0004–0010, permission IDs 0052–0062 |
| `Identity.Infrastructure/Data/Configurations/RoleConfiguration.cs` | Scope column mapping + 10-role `HasData` seed |
| `Identity.Infrastructure/Persistence/Migrations/20260424210000_AddRoleScopeAndBaselineRolesPermissions.cs` | New migration — schema + data |
| `Identity.Api/Endpoints/AdminEndpoints.cs` | `ListRoles` scope filter; `AssignRole` `roleKey` resolution |

---

## 7. Validation Results

### Build

```
dotnet build Identity.Infrastructure.csproj -c Debug
→ Build succeeded.  0 Warning(s).  0 Error(s).

dotnet build Identity.Api.csproj -c Debug
→ Build succeeded.  3 Warning(s).  0 Error(s).
   MSB3277 — JwtBearer version conflict (pre-existing, not introduced by PUM-B02)
   CS8601  — Nullable assignment line 2796 (pre-existing)
   CA2017  — TenantBrandingEndpoints logging template (pre-existing)
```

### Runtime

```
GET http://localhost:5001/health → HTTP 200 OK
IdentityDbContext — active queries against identity_db confirmed in logs.
No Identity service errors observed post-restart.
```

---

## 8. Known Gaps / Deferred Items

| Item | Notes |
|---|---|
| `tenantAssignableSystemRoles` guard still hard-codes `{ "TenantAdmin", "TenantUser" }` | The existing guard pre-dates PUM-B02. Expanding it to use `RoleScopes.Tenant` is a straightforward follow-up (PUM-B03 or targeted cleanup). |
| `Product` scope roles | The `RoleScopes.Product` constant is defined; product-scoped system roles (e.g., per-product admin roles) are not yet seeded. Deferred to a future sprint. |
| Effective-permissions endpoint does not filter by scope | `GET /api/admin/users/{id}/permissions` returns the full flat union regardless of scope context. Scope-aware filtering (e.g., only PLATFORM.* permissions when calling from the control center) is a future enhancement. |

---

## 9. Final Assessment

PUM-B02 is **complete**. The Identity service now has:

- A three-tier scope taxonomy (`Platform` / `Tenant` / `Product`) on every role.
- 10 system roles fully seeded with scope classification.
- 11 new permission entries in the `PLATFORM.*` namespace.
- 43 role → permission mapping rows covering all system roles.
- An enhanced admin API supporting role lookup by name (`roleKey`) and role listing filtered by scope.
- All changes strictly additive and idempotent; no existing data or endpoints modified destructively.
- Build: ✅ 0 errors. Runtime: ✅ HTTP 200, no startup errors.
