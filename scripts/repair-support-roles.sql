-- ============================================================
-- Production repair script: Support module role catalog + SRA
-- Run ONCE against the identity DB (legalsynq_identity).
-- Safe to re-run — all operations use INSERT IGNORE + NOT EXISTS.
-- ============================================================

-- Step 1: Seed support-specific roles if missing
INSERT IGNORE INTO `idt_Roles`
    (`Id`, `TenantId`, `Name`, `Description`, `IsSystemRole`, `Scope`, `CreatedAtUtc`, `UpdatedAtUtc`)
VALUES
    ('30000000-0000-0000-0000-000000000011', '20000000-0000-0000-0000-000000000001', 'SupportAdmin',     'Full support administration',                1, 'Support',  '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('30000000-0000-0000-0000-000000000012', '20000000-0000-0000-0000-000000000001', 'SupportManager',   'Support team manager',                      1, 'Support',  '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('30000000-0000-0000-0000-000000000013', '20000000-0000-0000-0000-000000000001', 'SupportAgent',     'Frontline support agent',                   1, 'Support',  '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('30000000-0000-0000-0000-000000000014', '20000000-0000-0000-0000-000000000001', 'TenantUser',       'Regular authenticated tenant user',         1, 'Tenant',   '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('30000000-0000-0000-0000-000000000015', '20000000-0000-0000-0000-000000000001', 'ExternalCustomer', 'External customer submitting own tickets',   0, 'External', '2024-01-01 00:00:00', '2024-01-01 00:00:00');

-- Step 2: Back-fill ScopedRoleAssignment for EVERY active user without one.
-- Assigns PlatformAdmin to system-tenant users, TenantAdmin to all others.
-- (Corrects migration 20260426000001 which only covered the earliest user per tenant.)
INSERT IGNORE INTO `idt_ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
SELECT
    UUID(),
    u.`Id`   AS UserId,
    CASE
        WHEN u.`TenantId` = '20000000-0000-0000-0000-000000000001'
             THEN '30000000-0000-0000-0000-000000000001'   -- PlatformAdmin
        ELSE      '30000000-0000-0000-0000-000000000002'   -- TenantAdmin
    END      AS RoleId,
    'GLOBAL' AS ScopeType,
    u.`TenantId`,
    NULL, NULL, NULL,
    1        AS IsActive,
    u.`CreatedAtUtc` AS AssignedAtUtc,
    u.`CreatedAtUtc` AS UpdatedAtUtc,
    NULL     AS AssignedByUserId
FROM `idt_Users` u
WHERE u.`IsActive` = 1
  AND NOT EXISTS (
    SELECT 1
    FROM   `idt_ScopedRoleAssignments` sra
    WHERE  sra.`UserId`    = u.`Id`
      AND  sra.`ScopeType` = 'GLOBAL'
      AND  sra.`IsActive`  = 1
  );

-- Step 3: Correct wrongly-assigned TenantAdmin → PlatformAdmin for platform-tenant users.
-- Required because migration 20260426000001 assigned TenantAdmin to ALL users, including
-- system-tenant users. Migration 20260426000002's NOT EXISTS guard then skipped them.
UPDATE `idt_ScopedRoleAssignments` sra
INNER JOIN `idt_Users` u ON u.`Id` = sra.`UserId`
SET   sra.`RoleId`       = '30000000-0000-0000-0000-000000000001',  -- PlatformAdmin
      sra.`UpdatedAtUtc` = UTC_TIMESTAMP()
WHERE u.`TenantId`           = '20000000-0000-0000-0000-000000000001'
  AND sra.`ScopeType`        = 'GLOBAL'
  AND sra.`IsActive`         = 1
  AND sra.`RoleId`           = '30000000-0000-0000-0000-000000000002'  -- TenantAdmin
  AND sra.`AssignedByUserId` IS NULL;

-- Verify: every active user should now have a Role in the result
SELECT
    u.Id     AS UserId,
    u.Email,
    u.TenantId,
    r.Name   AS Role,
    sra.ScopeType,
    sra.IsActive
FROM   idt_Users u
LEFT   JOIN idt_ScopedRoleAssignments sra
       ON   sra.UserId    = u.Id
       AND  sra.ScopeType = 'GLOBAL'
       AND  sra.IsActive  = 1
LEFT   JOIN idt_Roles r ON r.Id = sra.RoleId
WHERE  u.IsActive = 1
ORDER  BY u.TenantId, u.CreatedAtUtc
LIMIT  100;
