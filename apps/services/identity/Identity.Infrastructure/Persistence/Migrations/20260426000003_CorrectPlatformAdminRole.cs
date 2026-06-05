using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Corrects ScopedRoleAssignments for users in the platform (system) tenant who
/// were incorrectly assigned TenantAdmin by migration 20260426000001.
///
/// Root cause chain:
///   1. 20260426000001 assigned TenantAdmin to ALL users (no tenant-type distinction).
///   2. 20260426000002 used NOT EXISTS to skip users that already had any GLOBAL SRA,
///      so platform-tenant users remained with TenantAdmin instead of PlatformAdmin.
///
/// Fix:
///   UPDATE every auto-seeded GLOBAL TenantAdmin SRA that belongs to a platform-tenant
///   user to PlatformAdmin.  "Auto-seeded" is identified by AssignedByUserId IS NULL —
///   the signature of all back-fill migrations (they never set an assigning user).
///
/// Safe to re-run: the WHERE clause is fully idempotent (a second run finds 0 rows).
/// </summary>
[Migration("20260426000003_CorrectPlatformAdminRole")]
public partial class CorrectPlatformAdminRole : Migration
{
    private const string PlatformTenantId  = "20000000-0000-0000-0000-000000000001";
    private const string RolePlatformAdmin = "30000000-0000-0000-0000-000000000001";
    private const string RoleTenantAdmin   = "30000000-0000-0000-0000-000000000002";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Step 1: Update wrongly-assigned TenantAdmin → PlatformAdmin for platform-tenant users
        migrationBuilder.Sql($@"
UPDATE `idt_ScopedRoleAssignments` sra
INNER JOIN `idt_Users` u ON u.`Id` = sra.`UserId`
SET   sra.`RoleId`       = '{RolePlatformAdmin}',
      sra.`UpdatedAtUtc` = UTC_TIMESTAMP()
WHERE u.`TenantId`           = '{PlatformTenantId}'
  AND sra.`ScopeType`        = 'GLOBAL'
  AND sra.`IsActive`         = 1
  AND sra.`RoleId`           = '{RoleTenantAdmin}'
  AND sra.`AssignedByUserId` IS NULL;
");

        // Step 2: Catch any platform-tenant user still missing a GLOBAL SRA entirely
        // (edge case: if 20260426000001 ran but 20260426000002 was skipped for this user)
        migrationBuilder.Sql($@"
INSERT IGNORE INTO `idt_ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
SELECT
    UUID(),
    u.`Id`            AS UserId,
    '{RolePlatformAdmin}' AS RoleId,
    'GLOBAL'          AS ScopeType,
    u.`TenantId`,
    NULL, NULL, NULL,
    1                 AS IsActive,
    u.`CreatedAtUtc`  AS AssignedAtUtc,
    u.`CreatedAtUtc`  AS UpdatedAtUtc,
    NULL              AS AssignedByUserId
FROM `idt_Users` u
WHERE u.`TenantId` = '{PlatformTenantId}'
  AND u.`IsActive` = 1
  AND NOT EXISTS (
    SELECT 1
    FROM   `idt_ScopedRoleAssignments` sra2
    WHERE  sra2.`UserId`    = u.`Id`
      AND  sra2.`ScopeType` = 'GLOBAL'
      AND  sra2.`IsActive`  = 1
  );
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Revert PlatformAdmin back to TenantAdmin for platform-tenant users
        // that were auto-seeded (AssignedByUserId IS NULL).
        migrationBuilder.Sql($@"
UPDATE `idt_ScopedRoleAssignments` sra
INNER JOIN `idt_Users` u ON u.`Id` = sra.`UserId`
SET   sra.`RoleId`       = '{RoleTenantAdmin}',
      sra.`UpdatedAtUtc` = UTC_TIMESTAMP()
WHERE u.`TenantId`           = '{PlatformTenantId}'
  AND sra.`ScopeType`        = 'GLOBAL'
  AND sra.`IsActive`         = 1
  AND sra.`RoleId`           = '{RolePlatformAdmin}'
  AND sra.`AssignedByUserId` IS NULL;
");
    }
}
