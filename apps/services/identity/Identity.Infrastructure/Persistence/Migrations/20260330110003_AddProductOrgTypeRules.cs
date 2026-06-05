using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[Migration("20260330110003_AddProductOrgTypeRules")]
public partial class AddProductOrgTypeRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Phase 3: ProductOrganizationTypeRules — replaces EligibleOrgType string on ProductRole
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `ProductOrganizationTypeRules` (
    `Id`                 char(36)    NOT NULL COLLATE ascii_general_ci,
    `ProductId`          char(36)    NOT NULL COLLATE ascii_general_ci,
    `ProductRoleId`      char(36)    NOT NULL COLLATE ascii_general_ci,
    `OrganizationTypeId` char(36)    NOT NULL COLLATE ascii_general_ci,
    `IsActive`           tinyint(1)  NOT NULL DEFAULT 1,
    `CreatedAtUtc`       datetime(6) NOT NULL,
    CONSTRAINT `PK_ProductOrganizationTypeRules` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PrOrgTypeRule_Product`    FOREIGN KEY (`ProductId`)         REFERENCES `Products`          (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PrOrgTypeRule_PrRole`     FOREIGN KEY (`ProductRoleId`)     REFERENCES `ProductRoles`      (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PrOrgTypeRule_OrgType`    FOREIGN KEY (`OrganizationTypeId`) REFERENCES `OrganizationTypes` (`Id`) ON DELETE RESTRICT
) CHARACTER SET = utf8mb4;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @ix1 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='ProductOrganizationTypeRules' AND INDEX_NAME='IX_PrOrgTypeRule_Unique')=0,
    'CREATE UNIQUE INDEX `IX_PrOrgTypeRule_Unique` ON `ProductOrganizationTypeRules` (`ProductRoleId`,`OrganizationTypeId`)',
    'SELECT 1');
PREPARE stmt FROM @ix1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ix2 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='ProductOrganizationTypeRules' AND INDEX_NAME='IX_PrOrgTypeRule_ProductId')=0,
    'CREATE INDEX `IX_PrOrgTypeRule_ProductId` ON `ProductOrganizationTypeRules` (`ProductId`)',
    'SELECT 1');
PREPARE stmt FROM @ix2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ix3 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='ProductOrganizationTypeRules' AND INDEX_NAME='IX_PrOrgTypeRule_OrgTypeId')=0,
    'CREATE INDEX `IX_PrOrgTypeRule_OrgTypeId` ON `ProductOrganizationTypeRules` (`OrganizationTypeId`)',
    'SELECT 1');
PREPARE stmt FROM @ix3; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // Backfill from ProductRole.EligibleOrgType seed values
        // (product IDs and role IDs match SeedIds constants)
        migrationBuilder.Sql(@"
INSERT IGNORE INTO `ProductOrganizationTypeRules` (`Id`, `ProductId`, `ProductRoleId`, `OrganizationTypeId`, `IsActive`, `CreatedAtUtc`) VALUES
    ('90000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000003', '50000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000002', 1, '2024-01-01 00:00:00'),
    ('90000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000003', '50000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000003', 1, '2024-01-01 00:00:00'),
    ('90000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000002', '50000000-0000-0000-0000-000000000003', '70000000-0000-0000-0000-000000000002', 1, '2024-01-01 00:00:00'),
    ('90000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000002', '50000000-0000-0000-0000-000000000004', '70000000-0000-0000-0000-000000000005', 1, '2024-01-01 00:00:00'),
    ('90000000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000002', '50000000-0000-0000-0000-000000000005', '70000000-0000-0000-0000-000000000005', 1, '2024-01-01 00:00:00'),
    ('90000000-0000-0000-0000-000000000006', '10000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000006', '70000000-0000-0000-0000-000000000002', 1, '2024-01-01 00:00:00'),
    ('90000000-0000-0000-0000-000000000007', '10000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000007', '70000000-0000-0000-0000-000000000004', 1, '2024-01-01 00:00:00');");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS `ProductOrganizationTypeRules`;");
    }
}
