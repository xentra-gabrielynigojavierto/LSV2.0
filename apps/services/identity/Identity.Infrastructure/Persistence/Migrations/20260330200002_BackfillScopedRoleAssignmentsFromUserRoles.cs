using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase F prep: closes the ScopedRoleAssignment dual-write coverage gap.
///
/// Context:
///   Migration 20260330110004 backfilled ScopedRoleAssignments from UserRoleAssignments
///   (the richer legacy table). However, the simpler UserRoles join table (composite key:
///   UserId + RoleId) was written to independently and may have rows without a corresponding
///   UserRoleAssignment — and therefore without a ScopedRoleAssignment.
///
///   UserRepository.AddAsync now writes both tables simultaneously (dual-write added in
///   Step 4), but existing users created before Step 4 may have UserRole rows with no
///   ScopedRoleAssignment GLOBAL record.
///
/// This migration closes the gap by inserting a GLOBAL-scoped ScopedRoleAssignment for
/// every UserRole row that does not already have a GLOBAL active ScopedRoleAssignment
/// for the same (UserId, RoleId) pair.
///
/// Idempotent: uses INSERT IGNORE + NOT EXISTS guard. Safe to re-run.
/// </summary>
[Migration("20260330200002_BackfillScopedRoleAssignmentsFromUserRoles")]
public partial class BackfillScopedRoleAssignmentsFromUserRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
INSERT INTO `ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
SELECT
    UUID(),
    ur.`UserId`,
    ur.`RoleId`,
    'GLOBAL',
    u.`TenantId`,
    NULL,
    NULL,
    NULL,
    1,
    ur.`AssignedAtUtc`,
    ur.`AssignedAtUtc`,
    NULL
FROM `UserRoles` ur
JOIN `Users` u ON u.`Id` = ur.`UserId`
WHERE NOT EXISTS (
    SELECT 1
    FROM   `ScopedRoleAssignments` sra
    WHERE  sra.`UserId`    = ur.`UserId`
    AND    sra.`RoleId`    = ur.`RoleId`
    AND    sra.`ScopeType` = 'GLOBAL'
    AND    sra.`IsActive`  = 1
);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Selectively remove the rows that were inserted by this migration:
        // any GLOBAL ScopedRoleAssignment that corresponds to a UserRole row
        // but has no matching UserRoleAssignment (i.e., it was added here).
        migrationBuilder.Sql(@"
DELETE sra
FROM   `ScopedRoleAssignments` sra
JOIN   `UserRoles` ur ON ur.`UserId` = sra.`UserId` AND ur.`RoleId` = sra.`RoleId`
WHERE  sra.`ScopeType` = 'GLOBAL'
AND    NOT EXISTS (
    SELECT 1
    FROM   `UserRoleAssignments` ura
    WHERE  ura.`UserId` = sra.`UserId`
    AND    ura.`RoleId` = sra.`RoleId`
);");
    }
}
