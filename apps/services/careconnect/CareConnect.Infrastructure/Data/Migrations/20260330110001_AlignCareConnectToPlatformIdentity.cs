using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// Phase 5: Align CareConnect domain to platform Identity model.
/// - Providers gain OrganizationId (nullable FK to Identity.Organizations)
/// - Facilities gain OrganizationId (nullable FK to Identity.Organizations)
/// - Referrals gain OrganizationRelationshipId (nullable FK to Identity.OrganizationRelationships)
/// - Appointments gain OrganizationRelationshipId (same)
/// All columns are nullable; existing rows are left as-is (transitional migration window).
/// Provider map, search, referral creation, and appointment scheduling are unaffected.
/// </summary>
public partial class AlignCareConnectToPlatformIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Providers: add OrganizationId ─────────────────────────────────────
        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @col = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Providers' AND COLUMN_NAME='OrganizationId')=0,
    'ALTER TABLE `Providers` ADD COLUMN `OrganizationId` char(36) NULL COLLATE ascii_general_ci',
    'SELECT 1');
PREPARE stmt FROM @col; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @idx = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Providers' AND INDEX_NAME='IX_Providers_OrganizationId')=0,
    'CREATE INDEX `IX_Providers_OrganizationId` ON `Providers` (`OrganizationId`)',
    'SELECT 1');
PREPARE stmt FROM @idx; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // ── Facilities: add OrganizationId ───────────────────────────────────
        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @col = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Facilities' AND COLUMN_NAME='OrganizationId')=0,
    'ALTER TABLE `Facilities` ADD COLUMN `OrganizationId` char(36) NULL COLLATE ascii_general_ci',
    'SELECT 1');
PREPARE stmt FROM @col; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @idx = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Facilities' AND INDEX_NAME='IX_Facilities_OrganizationId')=0,
    'CREATE INDEX `IX_Facilities_OrganizationId` ON `Facilities` (`OrganizationId`)',
    'SELECT 1');
PREPARE stmt FROM @idx; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // ── Referrals: add OrganizationRelationshipId ────────────────────────
        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @col = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Referrals' AND COLUMN_NAME='OrganizationRelationshipId')=0,
    'ALTER TABLE `Referrals` ADD COLUMN `OrganizationRelationshipId` char(36) NULL COLLATE ascii_general_ci AFTER `ReceivingOrganizationId`',
    'SELECT 1');
PREPARE stmt FROM @col; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @idx = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Referrals' AND INDEX_NAME='IX_Referrals_OrganizationRelationshipId')=0,
    'CREATE INDEX `IX_Referrals_OrganizationRelationshipId` ON `Referrals` (`OrganizationRelationshipId`)',
    'SELECT 1');
PREPARE stmt FROM @idx; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // ── Appointments: add OrganizationRelationshipId ─────────────────────
        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @col = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Appointments' AND COLUMN_NAME='OrganizationRelationshipId')=0,
    'ALTER TABLE `Appointments` ADD COLUMN `OrganizationRelationshipId` char(36) NULL COLLATE ascii_general_ci AFTER `SubjectPartyId`',
    'SELECT 1');
PREPARE stmt FROM @col; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @idx = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Appointments' AND INDEX_NAME='IX_Appointments_OrganizationRelationshipId')=0,
    'CREATE INDEX `IX_Appointments_OrganizationRelationshipId` ON `Appointments` (`OrganizationRelationshipId`)',
    'SELECT 1');
PREPARE stmt FROM @idx; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"ALTER TABLE `Appointments` DROP INDEX IF EXISTS `IX_Appointments_OrganizationRelationshipId`;");
        migrationBuilder.Sql(@"ALTER TABLE `Appointments` DROP COLUMN IF EXISTS `OrganizationRelationshipId`;");
        migrationBuilder.Sql(@"ALTER TABLE `Referrals` DROP INDEX IF EXISTS `IX_Referrals_OrganizationRelationshipId`;");
        migrationBuilder.Sql(@"ALTER TABLE `Referrals` DROP COLUMN IF EXISTS `OrganizationRelationshipId`;");
        migrationBuilder.Sql(@"ALTER TABLE `Facilities` DROP INDEX IF EXISTS `IX_Facilities_OrganizationId`;");
        migrationBuilder.Sql(@"ALTER TABLE `Facilities` DROP COLUMN IF EXISTS `OrganizationId`;");
        migrationBuilder.Sql(@"ALTER TABLE `Providers` DROP INDEX IF EXISTS `IX_Providers_OrganizationId`;");
        migrationBuilder.Sql(@"ALTER TABLE `Providers` DROP COLUMN IF EXISTS `OrganizationId`;");
    }
}
