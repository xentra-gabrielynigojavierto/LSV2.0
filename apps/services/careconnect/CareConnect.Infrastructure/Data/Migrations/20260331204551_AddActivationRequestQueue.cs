using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivationRequestQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use conditional SQL — these indexes may have already been dropped
            // by a prior schema change and would fail with a hard DropIndex call.
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.statistics
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='ReferralNotes'
                     AND INDEX_NAME='IX_ReferralNotes_ReferralId') > 0,
                  'ALTER TABLE `ReferralNotes` DROP INDEX `IX_ReferralNotes_ReferralId`',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.statistics
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='CareConnectNotifications'
                     AND INDEX_NAME='IX_CareConnectNotifications_Status_NextRetryAfterUtc') > 0,
                  'ALTER TABLE `CareConnectNotifications` DROP INDEX `IX_CareConnectNotifications_Status_NextRetryAfterUtc`',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.AlterColumn<int>(
                name: "TokenVersion",
                table: "Referrals",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "ReferrerName",
                table: "Referrals",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReferrerEmail",
                table: "Referrals",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(320)",
                oldMaxLength: 320,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            // AddColumn calls wrapped in IF NOT EXISTS checks (MySQL DDL is not transactional;
            // a previous failed run may have committed these columns already).
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.columns
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='Referrals'
                     AND COLUMN_NAME='OrganizationRelationshipId') = 0,
                  'ALTER TABLE `Referrals` ADD COLUMN `OrganizationRelationshipId` char(36) COLLATE ascii_general_ci NULL',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.columns
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='Providers'
                     AND COLUMN_NAME='OrganizationId') = 0,
                  'ALTER TABLE `Providers` ADD COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NULL',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.AlterColumn<string>(
                name: "SsnLast4",
                table: "Parties",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(4)",
                oldMaxLength: 4,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PartyType",
                table: "Parties",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "INDIVIDUAL")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.columns
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='Facilities'
                     AND COLUMN_NAME='OrganizationId') = 0,
                  'ALTER TABLE `Facilities` ADD COLUMN `OrganizationId` char(36) COLLATE ascii_general_ci NULL',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.AlterColumn<string>(
                name: "TriggerSource",
                table: "CareConnectNotifications",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Initial")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "AttemptCount",
                table: "CareConnectNotifications",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.columns
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='Appointments'
                     AND COLUMN_NAME='OrganizationRelationshipId') = 0,
                  'ALTER TABLE `Appointments` ADD COLUMN `OrganizationRelationshipId` char(36) COLLATE ascii_general_ci NULL',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            // ActivationRequests table — idempotent via CREATE TABLE IF NOT EXISTS
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `ActivationRequests` (
                    `Id`                   char(36) COLLATE ascii_general_ci NOT NULL,
                    `TenantId`             char(36) COLLATE ascii_general_ci NOT NULL,
                    `ReferralId`           char(36) COLLATE ascii_general_ci NOT NULL,
                    `ProviderId`           char(36) COLLATE ascii_general_ci NOT NULL,
                    `ProviderName`         varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `ProviderEmail`        varchar(320) CHARACTER SET utf8mb4 NOT NULL,
                    `RequesterName`        varchar(200) CHARACTER SET utf8mb4 NULL,
                    `RequesterEmail`       varchar(320) CHARACTER SET utf8mb4 NULL,
                    `ClientName`           varchar(250) CHARACTER SET utf8mb4 NULL,
                    `ReferringFirmName`    varchar(300) CHARACTER SET utf8mb4 NULL,
                    `RequestedService`     varchar(300) CHARACTER SET utf8mb4 NULL,
                    `Status`               varchar(20)  CHARACTER SET utf8mb4 NOT NULL,
                    `ApprovedByUserId`     char(36) COLLATE ascii_general_ci NULL,
                    `ApprovedAtUtc`        datetime(6) NULL,
                    `LinkedOrganizationId` char(36) COLLATE ascii_general_ci NULL,
                    `CreatedAtUtc`         datetime(6) NOT NULL,
                    `UpdatedAtUtc`         datetime(6) NOT NULL,
                    `CreatedByUserId`      char(36) COLLATE ascii_general_ci NULL,
                    `UpdatedByUserId`      char(36) COLLATE ascii_general_ci NULL,
                    PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_ActivationRequests_Providers_ProviderId`
                        FOREIGN KEY (`ProviderId`) REFERENCES `Providers` (`Id`) ON DELETE RESTRICT,
                    CONSTRAINT `FK_ActivationRequests_Referrals_ReferralId`
                        FOREIGN KEY (`ReferralId`) REFERENCES `Referrals` (`Id`) ON DELETE RESTRICT
                ) CHARACTER SET utf8mb4;");

            // All CreateIndex calls wrapped in IF NOT EXISTS guards
            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.statistics
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='Referrals'
                     AND INDEX_NAME='IX_Referrals_OrganizationRelationshipId') = 0,
                  'CREATE INDEX `IX_Referrals_OrganizationRelationshipId` ON `Referrals` (`OrganizationRelationshipId`)',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.statistics
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='Providers'
                     AND INDEX_NAME='IX_Providers_OrganizationId') = 0,
                  'CREATE INDEX `IX_Providers_OrganizationId` ON `Providers` (`OrganizationId`)',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.statistics
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='Providers'
                     AND INDEX_NAME='IX_Providers_TenantId_City_State') = 0,
                  'CREATE INDEX `IX_Providers_TenantId_City_State` ON `Providers` (`TenantId`, `City`, `State`)',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.statistics
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='Facilities'
                     AND INDEX_NAME='IX_Facilities_OrganizationId') = 0,
                  'CREATE INDEX `IX_Facilities_OrganizationId` ON `Facilities` (`OrganizationId`)',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.statistics
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='Appointments'
                     AND INDEX_NAME='IX_Appointments_OrganizationRelationshipId') = 0,
                  'CREATE INDEX `IX_Appointments_OrganizationRelationshipId` ON `Appointments` (`OrganizationRelationshipId`)',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.statistics
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='ActivationRequests'
                     AND INDEX_NAME='IX_ActivationRequests_ProviderId') = 0,
                  'CREATE INDEX `IX_ActivationRequests_ProviderId` ON `ActivationRequests` (`ProviderId`)',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.statistics
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='ActivationRequests'
                     AND INDEX_NAME='IX_ActivationRequests_ReferralId_ProviderId') = 0,
                  'CREATE INDEX `IX_ActivationRequests_ReferralId_ProviderId` ON `ActivationRequests` (`ReferralId`, `ProviderId`)',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
                SET @dbname = DATABASE();
                SET @s = IF(
                  (SELECT COUNT(*) FROM information_schema.statistics
                   WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='ActivationRequests'
                     AND INDEX_NAME='IX_ActivationRequests_Status_CreatedAt') = 0,
                  'CREATE INDEX `IX_ActivationRequests_Status_CreatedAt` ON `ActivationRequests` (`Status`, `CreatedAtUtc`)',
                  'SELECT 1');
                PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivationRequests");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_OrganizationRelationshipId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Providers_OrganizationId",
                table: "Providers");

            migrationBuilder.DropIndex(
                name: "IX_Providers_TenantId_City_State",
                table: "Providers");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_OrganizationId",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_OrganizationRelationshipId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "OrganizationRelationshipId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "OrganizationRelationshipId",
                table: "Appointments");

            migrationBuilder.AlterColumn<int>(
                name: "TokenVersion",
                table: "Referrals",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "ReferrerName",
                table: "Referrals",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "ReferrerEmail",
                table: "Referrals",
                type: "varchar(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "SsnLast4",
                table: "Parties",
                type: "char(4)",
                maxLength: 4,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(4)",
                oldMaxLength: 4,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PartyType",
                table: "Parties",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "INDIVIDUAL",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TriggerSource",
                table: "CareConnectNotifications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Initial",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "AttemptCount",
                table: "CareConnectNotifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralNotes_ReferralId",
                table: "ReferralNotes",
                column: "ReferralId");

            migrationBuilder.CreateIndex(
                name: "IX_CareConnectNotifications_Status_NextRetryAfterUtc",
                table: "CareConnectNotifications",
                columns: new[] { "Status", "NextRetryAfterUtc" });
        }
    }
}
