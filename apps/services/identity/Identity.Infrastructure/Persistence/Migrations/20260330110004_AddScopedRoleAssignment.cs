using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[Migration("20260330110004_AddScopedRoleAssignment")]
public partial class AddScopedRoleAssignment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Phase 4: ScopedRoleAssignments — enriched UserRoleAssignment successor
        // Existing UserRoleAssignments are preserved unchanged during the migration window.
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `ScopedRoleAssignments` (
    `Id`                        char(36)    NOT NULL COLLATE ascii_general_ci,
    `UserId`                    char(36)    NOT NULL COLLATE ascii_general_ci,
    `RoleId`                    char(36)    NOT NULL COLLATE ascii_general_ci,
    `ScopeType`                 varchar(30) NOT NULL,
    `TenantId`                  char(36)    NULL COLLATE ascii_general_ci,
    `OrganizationId`            char(36)    NULL COLLATE ascii_general_ci,
    `OrganizationRelationshipId` char(36)   NULL COLLATE ascii_general_ci,
    `ProductId`                 char(36)    NULL COLLATE ascii_general_ci,
    `IsActive`                  tinyint(1)  NOT NULL DEFAULT 1,
    `AssignedAtUtc`             datetime(6) NOT NULL,
    `UpdatedAtUtc`              datetime(6) NOT NULL,
    `AssignedByUserId`          char(36)    NULL COLLATE ascii_general_ci,
    CONSTRAINT `PK_ScopedRoleAssignments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ScopedRoleAssignment_User` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ScopedRoleAssignment_Role` FOREIGN KEY (`RoleId`) REFERENCES `Roles` (`Id`) ON DELETE RESTRICT
) CHARACTER SET = utf8mb4;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();

SET @ix1 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='ScopedRoleAssignments' AND INDEX_NAME='IX_ScopedRoleAssignments_User_Role_Scope')=0,
    'CREATE INDEX `IX_ScopedRoleAssignments_User_Role_Scope` ON `ScopedRoleAssignments` (`UserId`,`RoleId`,`ScopeType`,`OrganizationId`,`ProductId`)',
    'SELECT 1');
PREPARE stmt FROM @ix1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ix2 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='ScopedRoleAssignments' AND INDEX_NAME='IX_ScopedRoleAssignments_User_Active')=0,
    'CREATE INDEX `IX_ScopedRoleAssignments_User_Active` ON `ScopedRoleAssignments` (`UserId`,`IsActive`)',
    'SELECT 1');
PREPARE stmt FROM @ix2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ix3 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='ScopedRoleAssignments' AND INDEX_NAME='IX_ScopedRoleAssignments_RelationshipId')=0,
    'CREATE INDEX `IX_ScopedRoleAssignments_RelationshipId` ON `ScopedRoleAssignments` (`OrganizationRelationshipId`)',
    'SELECT 1');
PREPARE stmt FROM @ix3; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // Migrate existing UserRoleAssignments to ScopedRoleAssignments
        // Scope is ORGANIZATION if OrganizationId is set, otherwise GLOBAL.
        migrationBuilder.Sql(@"
INSERT IGNORE INTO `ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`, `OrganizationId`, `OrganizationRelationshipId`, `ProductId`, `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
SELECT
    UUID(),
    ura.`UserId`,
    ura.`RoleId`,
    CASE WHEN ura.`OrganizationId` IS NOT NULL THEN 'ORGANIZATION' ELSE 'GLOBAL' END,
    u.`TenantId`,
    ura.`OrganizationId`,
    NULL,
    NULL,
    1,
    ura.`AssignedAtUtc`,
    ura.`AssignedAtUtc`,
    ura.`AssignedByUserId`
FROM `UserRoleAssignments` ura
JOIN `Users` u ON u.`Id` = ura.`UserId`;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS `ScopedRoleAssignments`;");
    }
}
