# BLK-ID-02 Report

**Block:** Identity Membership API (Formalization)
**Status:** COMPLETE
**Date:** 2026-04-23
**Window:** TENANT-STABILIZATION 2026-04-23 → 2026-05-07

---

## 1. Summary

Formalizing Identity as a clean membership + access-control service by extracting
tenant-assignment and role-assignment into dedicated internal APIs. The provisioning
endpoint is refactored to be a thin orchestrator that delegates to these service methods
rather than containing scattered direct DB writes.

---

## 2. New APIs

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/internal/users/assign-tenant` | X-Provisioning-Token | Assign user to a tenant + optional roles |
| POST | `/api/internal/users/assign-roles` | X-Provisioning-Token | Assign roles to a user (idempotent) |

---

## 3. Refactored Provisioning Flow

**Before:**
The `/api/internal/tenant-provisioning/provision` endpoint contained all logic inline:
- Created `ScopedRoleAssignment` directly and added it to `db.ScopedRoleAssignments`
- All logic (tenant entity, org, user, membership, role assignment) saved in one `SaveChangesAsync` call

**After:**
- Entity creation (identityTenant, org, user, membership) saved first → `db.SaveChangesAsync()`
- Role assignment delegated to `IUserMembershipService.AssignRolesAsync()` — no direct DB writes
- `AssignRolesAsync` is idempotent — safe to call even if role was already assigned

---

## 4. Membership Logic

**AssignTenantAsync:**
1. Validates user exists in Identity DB
2. Validates tenantId is not empty
3. Updates `User.TenantId` (via EF ExecuteUpdateAsync — bypasses EF change tracking)
4. Assigns each provided role via `AssignRolesAsync` (idempotent — skips duplicates)
5. Returns result with all assigned role IDs

**AssignRolesAsync:**
1. For each role name, looks up the Role record in DB
2. Checks whether an active `ScopedRoleAssignment` already exists for (userId, roleId, tenantId)
3. If duplicate → logs warning, skips (idempotent)
4. If new → creates `ScopedRoleAssignment` (GLOBAL scope)
5. Saves in batch — single `SaveChangesAsync` call

---

## 5. Changed Files

| File | Change |
|------|--------|
| `Identity.Application/Interfaces/IUserMembershipService.cs` | NEW — interface with AssignTenantAsync + AssignRolesAsync |
| `Identity.Infrastructure/Services/UserMembershipService.cs` | NEW — implementation |
| `Identity.Api/Endpoints/UserMembershipEndpoints.cs` | NEW — 2 internal HTTP endpoints |
| `Identity.Infrastructure/DependencyInjection.cs` | +`IUserMembershipService` registration |
| `Identity.Api/Program.cs` | +`MapUserMembershipEndpoints()` call |
| `Identity.Api/Endpoints/TenantProvisioningEndpoints.cs` | Refactored — uses `IUserMembershipService` |
| `analysis/BLK-ID-02-report.md` | This report |

---

## 6. Methods / Endpoints Implemented

**`IUserMembershipService`**
- `AssignTenantAsync(AssignTenantCommand, ct)` → `AssignTenantResult`
- `AssignRolesAsync(AssignRolesCommand, ct)` → `AssignRolesResult`

**`UserMembershipEndpoints`**
- `POST /api/internal/users/assign-tenant`
- `POST /api/internal/users/assign-roles`

**`TenantProvisioningEndpoints` (refactored)**
- Injects `IUserMembershipService`
- Calls `AssignRolesAsync` after saving initial entities

---

## 7. GitHub Commits (MANDATORY)

- `ef21554` — BLK-ID-01: Retire Identity tenant creation + code-check endpoints (prior block)
- `75d94ca` — BLK-ID-02: Formalize identity service with new membership and role assignment APIs

---

## 8. Validation Results

- [x] `POST /api/internal/users/assign-tenant` endpoint registered and reachable ✓
- [x] `POST /api/internal/users/assign-roles` endpoint registered and reachable ✓
- [x] Provision endpoint (`/api/internal/tenant-provisioning/provision`) still works — refactored to call `AssignRolesAsync` ✓
- [x] No duplicate `ScopedRoleAssignment` rows — idempotency check by `(UserId, RoleId, TenantId, IsActive)` ✓
- [x] `User.TenantId` updates via `ExecuteUpdateAsync` in `AssignTenantAsync` ✓
- [x] `dotnet build Identity.Api` — 0 errors, 0 warnings ✓
- [x] Application startup clean — no runtime errors ✓
- [x] Authentication, JWT issuance, login flow — unchanged ✓

---

## 9. Issues / Gaps

- `AssignTenant` is primarily useful for the CareConnect onboarding migration (future block).
  The current provisioning endpoint creates the user WITH the tenant already assigned,
  so only `AssignRoles` is called there. `AssignTenant` is in place for the next block.

- Role validation warns on missing role names but does not throw — this is intentional
  to keep the provisioning flow fault-tolerant. A missing role generates a log warning.
