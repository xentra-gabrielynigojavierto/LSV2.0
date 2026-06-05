# PUM-B01 — User Directory + Core Model
## Implementation Report

**Status:** Complete  
**Date:** 2026-04-24  
**Service:** Identity  
**Build:** ✅ Debug 0 errors | ✅ Release 0 errors  
**Migration:** ✅ `20260424200000_AddUserType` applied successfully to `identity_db`  

---

## 1. Codebase Analysis

The Identity service is a custom multi-tenant .NET 8 microservice using:
- Clean Architecture (Domain / Application / Infrastructure / Api layers)
- EF Core 8 with Pomelo MySQL provider
- Custom BCrypt authentication (no ASP.NET Identity)
- Minimal API endpoints via `IEndpointRouteBuilder`
- Migrations: manual EF migration files without `.Designer.cs` snapshots (the ModelSnapshot is intentionally minimal)

The service has two distinct sets of user endpoints:
- `/api/users` — tenant-scoped (callers can only access their own tenant's users)
- `/api/admin/users` — platform-facing (PlatformAdmins see all tenants; TenantAdmins see their tenant)

---

## 2. Existing Identity/User Model Findings

The `User` entity (`Identity.Domain/User.cs`) already contained:
| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | Primary key |
| `TenantId` | `Guid` | FK → Tenant |
| `Email` | `string` (max 320) | Unique within tenant |
| `FirstName` | `string` (max 100) | |
| `LastName` | `string` (max 100) | |
| `IsActive` | `bool` | Active/inactive flag |
| `CreatedAtUtc` | `DateTime` | |
| `UpdatedAtUtc` | `DateTime` | |
| `LastLoginAtUtc` | `DateTime?` | Nullable — populated by `RecordLogin()` |
| `IsLocked` | `bool` | Administrative lock |
| `LockedAtUtc` | `DateTime?` | |
| `LockedByAdminId` | `Guid?` | |
| `SessionVersion` | `int` | JWT session invalidation counter |
| `AccessVersion` | `int` | JWT access invalidation counter |
| `AvatarDocumentId` | `Guid?` | Documents service reference |
| `Phone` | `string?` (max 32) | E.164 format |

**Missing for PUM-B01:** `UserType` field to distinguish PlatformInternal / TenantUser / ExternalCustomer.

**Admin endpoints** (`AdminEndpoints.cs`):
- `GET /api/admin/users` — `ListUsers` handler: already had search (email/firstName/lastName), status filter (active/inactive/invited), tenant scoping, and pagination. **Missing:** `userType` and `isActive` filters; `userType` not included in response.
- `GET /api/admin/users/{id}` — `GetUser` handler: already loaded full user detail with memberships, groups, roles. **Missing:** `userType` and `isActive` in response.

---

## 3. Database / Migration Changes

### New Migration: `20260424200000_AddUserType`

**File:** `apps/services/identity/Identity.Infrastructure/Persistence/Migrations/20260424200000_AddUserType.cs`

**SQL equivalent:**
```sql
ALTER TABLE idt_Users
  ADD COLUMN UserType varchar(30) NOT NULL DEFAULT 'TenantUser';
```

- **Safe additive migration** — no existing rows are destroyed or modified beyond receiving the `'TenantUser'` default value.
- **MySQL 8.0 compatible** — uses `AddColumn` with `defaultValue: "TenantUser"` so all existing rows are automatically classified as `TenantUser`.
- **String storage** — stored as `varchar(30)` for readability and forward compatibility (not a MySQL `ENUM`, which is hard to evolve).
- **Applied:** Confirmed applied to `identity_db` on startup (log line: `Applying migration '20260424200000_AddUserType'`).

---

## 4. API Changes

### GET /identity/api/admin/users

**New query parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `userType` | `string` | Filter by `TenantUser`, `PlatformInternal`, or `ExternalCustomer` (case-insensitive). No filter if omitted or invalid. |
| `isActive` | `string` | Boolean filter: `true` = active only, `false` = inactive only. Distinct from the legacy `status` string filter. Omit for no filter. |

Existing parameters preserved: `page`, `pageSize`, `search`, `tenantId`, `status`.

**New response fields per item:**
```json
{
  "userType": "TenantUser",
  "isActive": true
}
```

### GET /identity/api/admin/users/{id}

**New response fields:**
```json
{
  "userType": "TenantUser",
  "isActive": true
}
```

Existing fields preserved and ordering improved (moved `lastLoginAtUtc` next to `updatedAtUtc`).

---

## 5. Files Changed

| File | Change |
|------|--------|
| `apps/services/identity/Identity.Domain/UserType.cs` | **New** — `UserType` enum with `TenantUser = 0`, `PlatformInternal = 1`, `ExternalCustomer = 2` |
| `apps/services/identity/Identity.Domain/User.cs` | Added `UserType` property (default `TenantUser`) and `SetUserType()` method; added optional `userType` parameter to `User.Create()` factory |
| `apps/services/identity/Identity.Infrastructure/Data/Configurations/UserConfiguration.cs` | Added `UserType` property mapping: `varchar(30)`, `HasConversion<string>()`, `HasDefaultValue(UserType.TenantUser)` |
| `apps/services/identity/Identity.Infrastructure/Persistence/Migrations/20260424200000_AddUserType.cs` | **New** — Additive migration adding `UserType varchar(30) NOT NULL DEFAULT 'TenantUser'` to `idt_Users` |
| `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` | Enhanced `ListUsers` (added `userType` + `isActive` filter params, included both in response); enhanced `GetUser` (added `userType` + `isActive` to response) |

---

## 6. Validation Results

### Build
```
Debug:   0 errors, 3 pre-existing warnings (MSB3277, CS8601, CA2017)
Release: 0 errors, 3 pre-existing warnings (MSB3277, CS8601, CA2017)
```

All 3 warnings are pre-existing (JwtBearer version conflict in BuildingBlocks, null reference at line 2789 unrelated to PUM-B01, logging template mismatch in TenantBrandingEndpoints).

### Migration
Migration `20260424200000_AddUserType` confirmed applied to `identity_db` on dev startup:
```
Applying migration '20260424200000_AddUserType'.
VALUES ('20260424200000_AddUserType', '8.0.0');
```

### Runtime
Identity service started and listening on port 5001 in Development mode. No errors related to the new `UserType` column or admin endpoint changes. All existing endpoint behavior preserved.

---

## 7. Known Gaps / Deferred Items

| Item | Reason | Ticket |
|------|--------|--------|
| No UI to set `UserType` on existing users | Out of scope for PUM-B01. `SetUserType()` method is in place for future admin action. | Defer to PUM-B02+ |
| `PlatformInternal` and `ExternalCustomer` users cannot currently be created | `User.Create()` defaults to `TenantUser`; a separate provisioning flow for platform-internal users is deferred. | Defer |
| Permission hardening on `/api/admin/users` | ListUsers and GetUser rely on the existing gateway trust model (no `.RequirePermission()` call). Consistent with existing admin endpoint pattern per the codebase. | Document only |
| `UpdatedAt` field on User | Already exists (`UpdatedAtUtc`). Used by `SetUserType()`. No change needed. | — |

---

## 8. Final Assessment

PUM-B01 is fully implemented within the defined scope:

- ✅ **PUM-B01-R01** — Unified user model with `PlatformInternal`, `TenantUser`, `ExternalCustomer` via new `UserType` enum.
- ✅ **PUM-B01-R02** — User records include email, name fields, status, user type, created/updated dates, last login date.
- ✅ **PUM-B01-R03** — Admin paginated user directory at `GET /api/admin/users`.
- ✅ **PUM-B01-R04** — Directory endpoint supports filtering by `userType` and `isActive`.
- ✅ **PUM-B01-R05** — Directory endpoint supports search by email and display name (firstName + lastName).
- ✅ **PUM-B01-R06** — Single user profile endpoint at `GET /api/admin/users/{id}`.
- ✅ **PUM-B01-R07** — No duplicate user tables introduced. Existing `idt_Users` reused.
- ✅ **PUM-B01-R08** — Existing login/authentication behavior preserved. `UserType` is additive; auth paths unmodified.
- ✅ **PUM-B01-R09** — Migration is strictly additive (`ADD COLUMN` with default, no `DROP` or `ALTER` of existing columns).
- ✅ **PUM-B01-R10** — Debug and Release builds complete with 0 errors.
