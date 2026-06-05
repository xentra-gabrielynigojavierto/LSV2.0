using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

public partial class AddReferralOrgAndPartyColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add columns only if they don't already exist (idempotent)
        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @tbl = 'Referrals';

SET @s1 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME=@tbl AND COLUMN_NAME='ReferringOrganizationId')=0,
    'ALTER TABLE `Referrals` ADD COLUMN `ReferringOrganizationId` char(36) NULL AFTER `TenantId`',
    'SELECT 1');
PREPARE stmt FROM @s1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @s2 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME=@tbl AND COLUMN_NAME='ReceivingOrganizationId')=0,
    'ALTER TABLE `Referrals` ADD COLUMN `ReceivingOrganizationId` char(36) NULL AFTER `ReferringOrganizationId`',
    'SELECT 1');
PREPARE stmt FROM @s2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @s3 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME=@tbl AND COLUMN_NAME='SubjectPartyId')=0,
    'ALTER TABLE `Referrals` ADD COLUMN `SubjectPartyId` char(36) NULL AFTER `ReceivingOrganizationId`',
    'SELECT 1');
PREPARE stmt FROM @s3; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @s4 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME=@tbl AND COLUMN_NAME='SubjectNameSnapshot')=0,
    'ALTER TABLE `Referrals` ADD COLUMN `SubjectNameSnapshot` varchar(250) NULL AFTER `SubjectPartyId`',
    'SELECT 1');
PREPARE stmt FROM @s4; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @s5 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME=@tbl AND COLUMN_NAME='SubjectDobSnapshot')=0,
    'ALTER TABLE `Referrals` ADD COLUMN `SubjectDobSnapshot` date NULL AFTER `SubjectNameSnapshot`',
    'SELECT 1');
PREPARE stmt FROM @s5; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();

SET @ix1 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Referrals' AND INDEX_NAME='IX_Referrals_ReferringOrgId_Status')=0,
    'CREATE INDEX `IX_Referrals_ReferringOrgId_Status` ON `Referrals` (`ReferringOrganizationId`, `Status`)',
    'SELECT 1');
PREPARE stmt FROM @ix1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ix2 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Referrals' AND INDEX_NAME='IX_Referrals_ReceivingOrgId_Status')=0,
    'CREATE INDEX `IX_Referrals_ReceivingOrgId_Status` ON `Referrals` (`ReceivingOrganizationId`, `Status`)',
    'SELECT 1');
PREPARE stmt FROM @ix2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ix3 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Referrals' AND INDEX_NAME='IX_Referrals_SubjectPartyId')=0,
    'CREATE INDEX `IX_Referrals_SubjectPartyId` ON `Referrals` (`SubjectPartyId`)',
    'SELECT 1');
PREPARE stmt FROM @ix3; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE `Referrals` DROP COLUMN `SubjectDobSnapshot`;");
        migrationBuilder.Sql("ALTER TABLE `Referrals` DROP COLUMN `SubjectNameSnapshot`;");
        migrationBuilder.Sql("ALTER TABLE `Referrals` DROP COLUMN `SubjectPartyId`;");
        migrationBuilder.Sql("ALTER TABLE `Referrals` DROP COLUMN `ReceivingOrganizationId`;");
        migrationBuilder.Sql("ALTER TABLE `Referrals` DROP COLUMN `ReferringOrganizationId`;");
    }
}
