using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Corrects the ScopedRoleAssignment back-fill introduced in 20260426000001.
///
/// The original back-fill had two bugs:
///   1. It only covered the EARLIEST active user per tenant (MIN CreatedAtUtc).
///      All other users — including the production PlatformAdmin user — were skipped,
///      leaving them with roles=[] in their JWT and a guaranteed 403 on every
///      protected endpoint.
///   2. It assigned TenantAdmin to ALL users, including users in the platform/system
///      tenant who should receive PlatformAdmin.
///
/// This migration runs the corrected back-fill:
///   • Covers EVERY active user with no active GLOBAL ScopedRoleAssignment.
///   • Assigns PlatformAdmin to users in the platform tenant (20000000-…-000001).
///   • Assigns TenantAdmin to users in all other tenants.
///
/// Safe to re-run: INSERT IGNORE + NOT EXISTS guard prevents duplicates.
/// </summary>
[Migration("20260426000002_FixSupportRolesBackfill")]
public partial class FixSupportRolesBackfill : Migration
{
    private const string PlatformTenantId  = "20000000-0000-0000-0000-000000000001";
    private const string RolePlatformAdmin = "30000000-0000-0000-0000-000000000001";
    private const string RoleTenantAdmin   = "30000000-0000-0000-0000-000000000002";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Back-fill every active user who still has no active GLOBAL SRA.
        // Uses a CASE expression to assign the correct role per tenant type.
        migrationBuilder.Sql($@"
INSERT IGNORE INTO `idt_ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
SELECT
    UUID(),
    u.`Id`   AS UserId,
    CASE
        WHEN u.`TenantId` = '{PlatformTenantId}' THEN '{RolePlatformAdmin}'
        ELSE '{RoleTenantAdmin}'
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
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove only the SRAs inserted by this migration (those with no AssignedByUserId
        // and created at the same timestamp as the user — the back-fill signature).
        // Intentionally conservative: does not remove manually assigned SRAs.
        migrationBuilder.Sql(@"
DELETE sra
FROM   `idt_ScopedRoleAssignments` sra
JOIN   `idt_Users` u ON u.`Id` = sra.`UserId`
WHERE  sra.`ScopeType`        = 'GLOBAL'
  AND  sra.`AssignedByUserId` IS NULL
  AND  sra.`AssignedAtUtc`    = u.`CreatedAtUtc`;
");
    }
}
