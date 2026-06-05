using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[Migration("20260330110001_AddOrganizationTypeCatalog")]
public partial class AddOrganizationTypeCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Phase 1: Create OrganizationTypes catalog table
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `OrganizationTypes` (
    `Id`          char(36)      NOT NULL COLLATE ascii_general_ci,
    `Code`        varchar(50)   NOT NULL,
    `DisplayName` varchar(100)  NOT NULL,
    `Description` varchar(500)  NULL,
    `IsSystem`    tinyint(1)    NOT NULL DEFAULT 0,
    `IsActive`    tinyint(1)    NOT NULL DEFAULT 1,
    `CreatedAtUtc` datetime(6)  NOT NULL,
    CONSTRAINT `PK_OrganizationTypes` PRIMARY KEY (`Id`)
) CHARACTER SET = utf8mb4;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @idx = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='OrganizationTypes' AND INDEX_NAME='IX_OrganizationTypes_Code')=0,
    'CREATE UNIQUE INDEX `IX_OrganizationTypes_Code` ON `OrganizationTypes` (`Code`)',
    'SELECT 1');
PREPARE stmt FROM @idx; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // Seed the five built-in org types
        migrationBuilder.Sql(@"
INSERT IGNORE INTO `OrganizationTypes` (`Id`, `Code`, `DisplayName`, `Description`, `IsSystem`, `IsActive`, `CreatedAtUtc`) VALUES
    ('70000000-0000-0000-0000-000000000001', 'INTERNAL',   'Internal',   'LegalSynq platform-internal organization',                        1, 1, '2024-01-01 00:00:00'),
    ('70000000-0000-0000-0000-000000000002', 'LAW_FIRM',   'Law Firm',   'Legal services organization that refers clients',                  1, 1, '2024-01-01 00:00:00'),
    ('70000000-0000-0000-0000-000000000003', 'PROVIDER',   'Provider',   'Healthcare or service provider that receives referrals',           1, 1, '2024-01-01 00:00:00'),
    ('70000000-0000-0000-0000-000000000004', 'FUNDER',     'Funder',     'Organization that funds cases or applications',                    1, 1, '2024-01-01 00:00:00'),
    ('70000000-0000-0000-0000-000000000005', 'LIEN_OWNER', 'Lien Owner', 'Organization that purchases and services liens',                   1, 1, '2024-01-01 00:00:00');");

        // Add OrganizationTypeId FK column to Organizations (nullable during migration window)
        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @col = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Organizations' AND COLUMN_NAME='OrganizationTypeId')=0,
    'ALTER TABLE `Organizations` ADD COLUMN `OrganizationTypeId` char(36) NULL COLLATE ascii_general_ci',
    'SELECT 1');
PREPARE stmt FROM @col; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @idx = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Organizations' AND INDEX_NAME='IX_Organizations_OrganizationTypeId')=0,
    'CREATE INDEX `IX_Organizations_OrganizationTypeId` ON `Organizations` (`OrganizationTypeId`)',
    'SELECT 1');
PREPARE stmt FROM @idx; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // Backfill OrganizationTypeId from existing OrgType string values
        migrationBuilder.Sql(@"
UPDATE `Organizations` SET `OrganizationTypeId` = '70000000-0000-0000-0000-000000000001' WHERE `OrgType` = 'INTERNAL'   AND `OrganizationTypeId` IS NULL;
UPDATE `Organizations` SET `OrganizationTypeId` = '70000000-0000-0000-0000-000000000002' WHERE `OrgType` = 'LAW_FIRM'   AND `OrganizationTypeId` IS NULL;
UPDATE `Organizations` SET `OrganizationTypeId` = '70000000-0000-0000-0000-000000000003' WHERE `OrgType` = 'PROVIDER'   AND `OrganizationTypeId` IS NULL;
UPDATE `Organizations` SET `OrganizationTypeId` = '70000000-0000-0000-0000-000000000004' WHERE `OrgType` = 'FUNDER'     AND `OrganizationTypeId` IS NULL;
UPDATE `Organizations` SET `OrganizationTypeId` = '70000000-0000-0000-0000-000000000005' WHERE `OrgType` = 'LIEN_OWNER' AND `OrganizationTypeId` IS NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"ALTER TABLE `Organizations` DROP INDEX IF EXISTS `IX_Organizations_OrganizationTypeId`;");
        migrationBuilder.Sql(@"ALTER TABLE `Organizations` DROP COLUMN IF EXISTS `OrganizationTypeId`;");
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS `OrganizationTypes`;");
    }
}
