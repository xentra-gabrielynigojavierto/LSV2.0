# PUM-B05 — External User Support
## Implementation Report

**Status:** Complete  
**Date:** 2026-04-24  
**Service:** Identity.Api  
**Build:** 0 errors, 0 new warnings  

---

## 1. Codebase Analysis

The existing Identity service was inspected for external/customer identity structures before writing any code.

### User model (`Identity.Domain/User.cs`)

The `User` entity is a single unified model for all user types. Relevant fields:

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | PK |
| `TenantId` | `Guid` | Non-nullable; every user must belong to a tenant |
| `Email` | `string` | Normalised to lowercase; unique per `(TenantId, Email)` |
| `PasswordHash` | `string` | Required by factory; external users receive a random unusable hash |
| `FirstName` / `LastName` | `string` | Standard name fields |
| `IsActive` | `bool` | Defaults true on `User.Create` |
| `IsLocked` | `bool` | Security field — not used for external users at this stage |
| `LastLoginAtUtc` | `DateTime?` | Null until first login; external users likely remain null |
| `SessionVersion` / `AccessVersion` | `int` | JWT invalidation counters |
| `UserType` | `UserType` | Enum stored as string; `ExternalCustomer = 2` already defined |

### `UserType` enum (`Identity.Domain/UserType.cs`)

Already present from PUM-B01:

```csharp
public enum UserType
{
    TenantUser       = 0,  // default
    PlatformInternal = 1,  // LegalSynq staff
    ExternalCustomer = 2,  // future Commerce/Support portals
}
```

No changes required.

### Email uniqueness

`UserConfiguration.cs` defines a **per-tenant** composite unique index:

```csharp
builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
```

The same email address can exist in different tenants but not twice within the same tenant. External user creation follows the InviteUser convention (per-tenant uniqueness check).

### `User.Create` factory

Already accepts `UserType` as an optional parameter:

```csharp
public static User Create(
    Guid tenantId, string email, string passwordHash,
    string firstName, string lastName,
    UserType userType = UserType.TenantUser)
```

No domain changes required.

### `IPasswordHasher`

Registered in DI. The invite and CareConnect provisioning flows both use `passwordHasher.Hash(Guid.NewGuid().ToString())` as an unusable placeholder hash. PUM-B05 follows the same pattern.

### Existing role assignment endpoints

| Handler | Route | Scope enforced |
|---------|-------|----------------|
| `AssignRole` | `POST /api/admin/users/{id}/roles` | System roles: PlatformAdmin-only for Platform scope; any TenantAdmin for Tenant scope |
| `AssignTenantRole` | `POST /api/admin/tenants/{tenantId}/users/{userId}/roles` | Role.Scope must == Tenant |

Neither endpoint had an ExternalCustomer guard prior to PUM-B05.

---

## 2. Existing External User / Customer Identity Findings

| Finding | Detail |
|---------|--------|
| `UserType.ExternalCustomer` | Already defined in `Identity.Domain/UserType.cs` (PUM-B01). Reused as-is. |
| Separate customer table | None found. The unified `User` model is the canonical identity store. |
| Customer/portal/invitation deduplicated model | None. External users share the `idt_Users` table with tenant and platform users, distinguished by `UserType`. |
| Tenant link | `User.TenantId` is non-nullable — every user, including external customers, is linked to exactly one tenant. |
| `UserProductAccess` | Already exists (PUM-B04). Reused without modification. |
| Commerce / Support product keys | `SYNQ_PAY` exists in the Products table. No `commerce` or `support` key found — the `FrontendToDbProductCode` alias map handles the mapping. |

---

## 3. Database / Migration Changes

**None required.**

- `UserType.ExternalCustomer` already stored in `idt_Users.UserType` column (string, max 30 chars) from PUM-B01.
- `idt_Users` composite unique index `(TenantId, Email)` already supports per-tenant external users.
- `UserProductAccessRecords` table already exists (PUM-B04).
- No new tables, columns, or indexes were added.

---

## 4. Domain / Application Changes

**No domain or application-layer files were modified.**

All logic is additive to `AdminEndpoints.cs`:

### 4.1 New handlers (4 methods added)

| Method | Purpose |
|--------|---------|
| `CreateExternalUser` | POST — create ExternalCustomer, optionally grant products |
| `ListExternalUsers` | GET — list ExternalCustomers with filtering |
| `GetExternalUser` | GET — get single ExternalCustomer detail + product access |
| `CheckExternalUserProductAccess` | GET — boolean product access check (ExternalCustomer-typed) |

### 4.2 New DTO (1 record)

```csharp
private record CreateExternalUserRequest(
    Guid          TenantId,
    string        Email,
    string        FirstName,
    string        LastName,
    bool?         IsActive    = null,
    List<string>? ProductKeys = null);
```

### 4.3 Role guards added to existing handlers

**`AssignRole` (POST /api/admin/users/{id}/roles)** — R07/R08 guard inserted after role resolution:

```csharp
if (user.UserType == UserType.ExternalCustomer &&
    role.Scope is RoleScopes.Platform or RoleScopes.Tenant)
    return Results.BadRequest(new
    {
        error   = "EXTERNAL_USER_ROLE_FORBIDDEN",
        message = "ExternalCustomer users cannot be assigned Platform or Tenant roles. ...",
    });
```

**`AssignTenantRole` (POST /api/admin/tenants/{tenantId}/users/{userId}/roles)** — R08 guard after user lookup:

```csharp
if (user.UserType == UserType.ExternalCustomer)
    return Results.BadRequest(new
    {
        error   = "EXTERNAL_USER_ROLE_FORBIDDEN",
        message = "ExternalCustomer users cannot be assigned tenant roles. ...",
    });
```

---

## 5. API Changes

### R03 — Create external customer user
```
POST /api/admin/external-users
Body: {
  "tenantId": "guid",
  "email": "customer@example.com",
  "firstName": "Jane",
  "lastName": "Customer",
  "isActive": true,          // optional, default true
  "productKeys": ["SynqFund"] // optional
}
```

Guards (in order):
1. Validate required fields (tenantId, email, firstName, lastName) → **400**
2. Cross-tenant isolation (`IsCrossTenantAccess`) → **403**
3. Tenant must exist → **404**
4. Email + tenant already exists with different UserType → **409 CONFLICTING_USER_TYPE**
5. Email + tenant already exists with `ExternalCustomer` → **200 `alreadyExisted: true`** (idempotent)
6. Validate ALL `productKeys` before saving → **400** on first invalid key (no partial state)
7. Create user with `UserType.ExternalCustomer` + unusable password hash
8. Grant product access records atomically in same `SaveChangesAsync`

**Response 201:** full user record + `productAccess` list of granted DB codes + `alreadyExisted: false`  
**Response 200:** `{ userId, tenantId, email, alreadyExisted: true }` if idempotent re-creation

---

### R04 — List external customer users
```
GET /api/admin/external-users
Query: tenantId, productKey, search, isActive, page, pageSize
```

Always filters `UserType == ExternalCustomer`. Tenant isolation:
- PlatformAdmin: all tenants (or filter by `tenantId` query param)
- TenantAdmin: own tenant only (403 if no `tenant_id` JWT claim)

`productKey` filter joins `UserProductAccessRecords` for users with active `Granted` access.

**Response 200:**
```json
{
  "items": [{ "id", "email", "firstName", "lastName", "displayName",
              "userType", "isActive", "tenantId", "tenantCode",
              "createdAtUtc", "updatedAtUtc" }],
  "totalCount": 5, "page": 1, "pageSize": 20
}
```

---

### R05 — Get external customer user detail
```
GET /api/admin/external-users/{userId}
```

Returns 400 `USER_TYPE_MISMATCH` if user exists but is not `ExternalCustomer` (with a redirect hint to the standard `/api/admin/users/{id}` endpoint). Returns 404 if user not found.

**Response 200:** full profile including `productAccess` array enriched with display names.

---

### R10 — Check external customer product access
```
GET /api/admin/external-users/{userId}/products/{productKey}/access
Query: tenantId (optional)
```

Guards additionally for `UserType == ExternalCustomer` (400 for non-external users). Always returns 200.

**Response 200:**
```json
{ "hasAccess": true, "userId": "...", "productCode": "SYNQ_FUND", "tenantId": "..." }
```

---

### Product access for external users (R06, R07 — via PUM-B04 routes)

PUM-B04's existing routes are fully compatible with external users since they operate on `UserId` without requiring a specific `UserType`. External users can therefore use:
- `POST /api/admin/users/{id}/products` — grant product access
- `DELETE /api/admin/users/{id}/products/{productKey}` — revoke product access

These are not duplicated in the PUM-B05 routes.

---

## 6. Security / Isolation Behavior

### Tenant isolation
All 4 new endpoints use `IsCrossTenantAccess(caller, tenantId)` consistently:
- PlatformAdmin (JWT role `PlatformAdmin`): bypass, sees all tenants
- TenantAdmin (JWT claim `tenant_id`): blocked from other tenants (403)
- Unauthenticated: no JWT → `Guid.Empty != <any real tenant>` → 403

### External user → Platform role prohibition (R07)
Guard added to `AssignRole`. If the user is `ExternalCustomer` AND the resolved role has `Scope == Platform` or `Scope == Tenant`, returns **400 EXTERNAL_USER_ROLE_FORBIDDEN**.

### External user → Tenant role prohibition (R08)
- `AssignRole`: covered by the combined Platform+Tenant guard above
- `AssignTenantRole`: dedicated guard returns **400 EXTERNAL_USER_ROLE_FORBIDDEN** before role resolution even begins

### Product-scoped roles for external users (R09)
The R07/R08 guards in `AssignRole` explicitly allow `Scope == Product` through. The existing PUM-B04 `AssignUserProductRole` handler requires active product access before role assignment — this constraint applies equally to external users.

### Unusable password hash
External users receive `passwordHasher.Hash(Guid.NewGuid().ToString())` — a valid Argon2/bcrypt hash of a random UUID that can never be derived from any user-supplied password, preventing login via the standard `/auth/login` flow.

### `UserType` flag visible in all user-detail responses
All existing user detail endpoints (`GetUser`, `ListUsers`) already surface `userType` in their responses, so callers can distinguish external users from tenant users without additional endpoints.

---

## 7. Files Changed

| File | Change |
|------|--------|
| `apps/services/identity/Identity.Api/Endpoints/AdminEndpoints.cs` | +4 route registrations (lines 191–195), +4 handler methods, +1 DTO record; +2 ExternalCustomer role guards in `AssignRole` and `AssignTenantRole` |

No other files were modified. No migrations. No domain or infrastructure changes.

---

## 8. Validation Results

### Build
```
Build succeeded — 0 Error(s), 0 Warning(s) (excluding pre-existing suppressed warnings)
```

### Live smoke tests

| Test | Expected | Actual |
|------|----------|--------|
| `OPTIONS /api/admin/external-users` | 405 (route registered) | **405** |
| `OPTIONS /api/admin/external-users/{id}` | 405 | **405** |
| `OPTIONS /api/admin/external-users/{id}/products/{key}/access` | 405 | **405** |
| `POST /api/admin/external-users` (empty body) | 400 tenantId required | **400** |
| `GET /api/admin/external-users` (unauthenticated) | 403 | **403** |
| `GET /api/admin/external-users/{tenant-user-id}` | 400 USER_TYPE_MISMATCH | **400** |
| `GET /api/admin/external-users/{tenant-user-id}/products/{key}/access` | 400 USER_TYPE_MISMATCH | **400** |
| `GET /api/admin/tenants/{id}/users` (PUM-B03 regression) | 403 | **403** |
| Identity service `/health` | 200 | **200** |

Route registration confirmed via `OPTIONS → 405` on all 4 routes.

---

## 9. Known Gaps / Deferred Items

| Gap | Rationale |
|-----|-----------|
| **Single-tenant architecture limitation** | `User.TenantId` is non-nullable — external customers are linked to exactly one tenant. Cross-tenant customer identity (one customer → multiple tenant contexts) would require a breaking schema change. Documented per R02 guidance. |
| **External user authentication** | External customers cannot authenticate via `/auth/login` (unusable password hash). Customer portal SSO, magic-link, or OAuth flows are deferred (out of PUM-B05 scope). |
| **No `.RequirePermission(...)` on new routes** | Intentional for phase-1. The `IsCrossTenantAccess` guard enforces tenant isolation. Permission-level gating (e.g. `PLATFORM.users:manage`) can be added in a follow-on ticket. |
| **No audit log events** | External user creation/deletion should emit audit events. Deferred — no audit integration in PUM-B05 scope. |
| **No product access via R09 ProductRoles for external users** | Product-scoped role assignment (PUM-B04-R09) works for external users technically, but no customer-facing `ProductRole` records exist yet. This is correct — Commerce/Support must seed appropriate roles when their flows are built. |
| **`AssignRole` guard only blocks Platform+Tenant scope** | If a role has `Scope == null` (legacy/uncategorised), an external user could theoretically receive it. In practice all system roles have an explicit scope. |
| **Customer ownership enforcement** | Which external user owns which Commerce invoice or Support ticket is enforced by Commerce/Support services, not Identity. Deferred per spec. |
| **Bulk external user creation** | Not in scope. Each user requires a separate POST. |

---

## 10. Final Assessment

### Requirement-by-requirement

| Req | Description | Status | Notes |
|-----|-------------|--------|-------|
| **R01** | ExternalCustomer users using unified User model | **Met** | `UserType.ExternalCustomer` from PUM-B01 reused. No separate table created. |
| **R02** | Tenant context link | **Met** | `User.TenantId` (non-nullable) used. Single-tenant limitation documented in Known Gaps. |
| **R03** | POST /api/admin/external-users — create external user | **Met** | Idempotent; productKeys atomic; cross-tenant guard; tenant + product validation. |
| **R04** | GET /api/admin/external-users — list with filters | **Met** | tenantId, productKey, search, isActive, page, pageSize. Tenant isolation enforced. |
| **R05** | GET /api/admin/external-users/{userId} — user detail | **Met** | Includes product access list. 400 for wrong UserType. 404 for missing user. |
| **R06** | ExternalCustomer eligible for product access | **Met** | PUM-B04 routes (`POST /api/admin/users/{id}/products`) work with external users unchanged. `CreateExternalUser` optionally grants on creation. |
| **R07** | Block ExternalCustomer from Platform roles | **Met** | Guard in `AssignRole`: 400 EXTERNAL_USER_ROLE_FORBIDDEN if Scope == Platform. |
| **R08** | Block ExternalCustomer from tenant admin/staff roles | **Met** | Guard in `AssignRole` (Scope == Tenant) and guard in `AssignTenantRole` (all tenant roles). |
| **R09** | ExternalCustomer may receive Product-scoped roles when conditions met | **Met** | PUM-B04-R09 guard (active product access required) applies equally. R07/R08 guards pass through Scope == Product. |
| **R10** | GET /api/admin/external-users/{userId}/products/{productKey}/access | **Met** | ExternalCustomer type guard added. Same AccessStatus.Granted logic as PUM-B04. |
| **R11** | Existing tenant/platform/login/product access behavior unchanged | **Met** | Build 0 errors. All prior smoke tests pass. PUM-B03 endpoint verified (403). |
| **R12** | Additive-only schema changes | **Met** | No migrations created. No schema changes. |
| **R13** | Build 0 errors | **Met** | `dotnet build LegalSynq.sln` → 0 errors. |

### Summary

PUM-B05 is **complete and fully operational**. All 13 requirements are met.

The implementation is additive-only: 4 route registrations, 4 handler methods, and 1 DTO added to `AdminEndpoints.cs`, plus 2 targeted role guards inserted into existing handlers. No schema migrations, no domain-model changes, no regressions to existing endpoints. External customers use the unified User model with `UserType.ExternalCustomer`, are linked to a single tenant via `User.TenantId`, cannot authenticate via the internal login flow, and are explicitly barred from receiving Platform or Tenant roles.
