using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

public partial class AddOrgAndPartyToAppointments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @tbl = 'Appointments';

SET @s1 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME=@tbl AND COLUMN_NAME='ReferringOrganizationId')=0,
    'ALTER TABLE `Appointments` ADD COLUMN `ReferringOrganizationId` char(36) NULL AFTER `TenantId`',
    'SELECT 1');
PREPARE stmt FROM @s1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @s2 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME=@tbl AND COLUMN_NAME='ReceivingOrganizationId')=0,
    'ALTER TABLE `Appointments` ADD COLUMN `ReceivingOrganizationId` char(36) NULL AFTER `ReferringOrganizationId`',
    'SELECT 1');
PREPARE stmt FROM @s2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @s3 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME=@tbl AND COLUMN_NAME='SubjectPartyId')=0,
    'ALTER TABLE `Appointments` ADD COLUMN `SubjectPartyId` char(36) NULL AFTER `ReceivingOrganizationId`',
    'SELECT 1');
PREPARE stmt FROM @s3; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();

SET @ix1 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Appointments' AND INDEX_NAME='IX_Appointments_ReceivingOrgId_Status')=0,
    'CREATE INDEX `IX_Appointments_ReceivingOrgId_Status` ON `Appointments` (`ReceivingOrganizationId`, `Status`)',
    'SELECT 1');
PREPARE stmt FROM @ix1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ix2 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Appointments' AND INDEX_NAME='IX_Appointments_SubjectPartyId')=0,
    'CREATE INDEX `IX_Appointments_SubjectPartyId` ON `Appointments` (`SubjectPartyId`)',
    'SELECT 1');
PREPARE stmt FROM @ix2; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE `Appointments` DROP COLUMN `SubjectPartyId`;");
        migrationBuilder.Sql("ALTER TABLE `Appointments` DROP COLUMN `ReceivingOrganizationId`;");
        migrationBuilder.Sql("ALTER TABLE `Appointments` DROP COLUMN `ReferringOrganizationId`;");
    }
}
