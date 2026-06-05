using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

public partial class AddVisibilityScopeToNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ReferralNotes
        migrationBuilder.Sql(@"
SET @db = DATABASE();

SET @s1 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='ReferralNotes' AND COLUMN_NAME='OwnerOrganizationId')=0,
    'ALTER TABLE `ReferralNotes` ADD COLUMN `OwnerOrganizationId` char(36) NULL AFTER `ReferralId`',
    'SELECT 1');
PREPARE stmt FROM @s1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @s2 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='ReferralNotes' AND COLUMN_NAME='VisibilityScope')=0,
    'ALTER TABLE `ReferralNotes` ADD COLUMN `VisibilityScope` varchar(20) NOT NULL DEFAULT ''SHARED'' AFTER `OwnerOrganizationId`',
    'SELECT 1');
PREPARE stmt FROM @s2; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();

SET @ix1 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='ReferralNotes' AND INDEX_NAME='IX_ReferralNotes_ReferralId_Org_Visibility')=0,
    'CREATE INDEX `IX_ReferralNotes_ReferralId_Org_Visibility` ON `ReferralNotes` (`ReferralId`, `OwnerOrganizationId`, `VisibilityScope`)',
    'SELECT 1');
PREPARE stmt FROM @ix1; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // AppointmentNotes
        migrationBuilder.Sql(@"
SET @db = DATABASE();

SET @s3 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='AppointmentNotes' AND COLUMN_NAME='OwnerOrganizationId')=0,
    'ALTER TABLE `AppointmentNotes` ADD COLUMN `OwnerOrganizationId` char(36) NULL AFTER `AppointmentId`',
    'SELECT 1');
PREPARE stmt FROM @s3; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @s4 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='AppointmentNotes' AND COLUMN_NAME='VisibilityScope')=0,
    'ALTER TABLE `AppointmentNotes` ADD COLUMN `VisibilityScope` varchar(20) NOT NULL DEFAULT ''SHARED'' AFTER `OwnerOrganizationId`',
    'SELECT 1');
PREPARE stmt FROM @s4; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();

SET @ix2 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='AppointmentNotes' AND INDEX_NAME='IX_AppointmentNotes_AppointmentId_Org_Visibility')=0,
    'CREATE INDEX `IX_AppointmentNotes_AppointmentId_Org_Visibility` ON `AppointmentNotes` (`AppointmentId`, `OwnerOrganizationId`, `VisibilityScope`)',
    'SELECT 1');
PREPARE stmt FROM @ix2; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // Backfill: all existing notes default to SHARED
        migrationBuilder.Sql(@"
UPDATE `ReferralNotes` SET `VisibilityScope` = 'SHARED' WHERE `VisibilityScope` IS NULL OR `VisibilityScope` = '';
UPDATE `AppointmentNotes` SET `VisibilityScope` = 'SHARED' WHERE `VisibilityScope` IS NULL OR `VisibilityScope` = '';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE `ReferralNotes` DROP COLUMN `VisibilityScope`;");
        migrationBuilder.Sql("ALTER TABLE `ReferralNotes` DROP COLUMN `OwnerOrganizationId`;");
        migrationBuilder.Sql("ALTER TABLE `AppointmentNotes` DROP COLUMN `VisibilityScope`;");
        migrationBuilder.Sql("ALTER TABLE `AppointmentNotes` DROP COLUMN `OwnerOrganizationId`;");
    }
}
