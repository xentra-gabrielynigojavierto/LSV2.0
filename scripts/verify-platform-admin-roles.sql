-- ============================================================
-- Verification script: PlatformAdmin role fix (Task #198)
-- Run against the identity DB to confirm migration fix is applied.
-- All queries are read-only (SELECT only).
--
-- Context:
--   Migration 20260426000003_CorrectPlatformAdminRole and the
--   LS-ID-SUP-001 startup guard in Identity.Api/Program.cs both
--   ensure platform-tenant users hold PlatformAdmin (not TenantAdmin).
--   This script verifies the expected post-fix state.
-- ============================================================

-- Check 1: No platform-tenant user should hold TenantAdmin anymore.
-- Expected: 0 rows.
SELECT
    u.Email,
    r.Name  AS WrongRole
FROM `idt_ScopedRoleAssignments` sra
INNER JOIN `idt_Users` u ON u.Id = sra.UserId
INNER JOIN `idt_Roles` r ON r.Id = sra.RoleId
WHERE u.TenantId     = '20000000-0000-0000-0000-000000000001'   -- platform tenant
  AND sra.ScopeType  = 'GLOBAL'
  AND sra.IsActive   = 1
  AND sra.RoleId     = '30000000-0000-0000-0000-000000000002';  -- TenantAdmin

-- Check 2: All platform-tenant users should have PlatformAdmin.
-- Expected: at least one row containing admin@legalsynq.com.
SELECT
    u.Email,
    r.Name        AS RoleName,
    sra.ScopeType,
    sra.IsActive,
    sra.AssignedByUserId  -- NULL means auto-seeded by migration/guard
FROM `idt_ScopedRoleAssignments` sra
INNER JOIN `idt_Users` u ON u.Id = sra.UserId
INNER JOIN `idt_Roles` r ON r.Id = sra.RoleId
WHERE u.TenantId     = '20000000-0000-0000-0000-000000000001'
  AND sra.ScopeType  = 'GLOBAL'
  AND sra.IsActive   = 1
  AND sra.RoleId     = '30000000-0000-0000-0000-000000000001'  -- PlatformAdmin
ORDER BY u.Email;

-- Verified on 2026-04-26 after identity service restart (Task #198):
--
-- Check 1 result  →  0 rows  (no TenantAdmin assignments for platform users)
-- Check 2 result  →  admin@legalsynq.com | PlatformAdmin | GLOBAL | 1 | NULL
--
-- The LS-ID-SUP-001 guard in Identity.Api/Program.cs runs on every
-- startup and reported: 1 SRA corrected to PlatformAdmin, 0 inserted.
