using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[Migration("20260330110002_AddRelationshipGraph")]
public partial class AddRelationshipGraph : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Phase 2a: RelationshipTypes catalog
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `RelationshipTypes` (
    `Id`           char(36)     NOT NULL COLLATE ascii_general_ci,
    `Code`         varchar(80)  NOT NULL,
    `DisplayName`  varchar(150) NOT NULL,
    `Description`  varchar(500) NULL,
    `IsDirectional` tinyint(1)  NOT NULL DEFAULT 1,
    `IsSystem`     tinyint(1)   NOT NULL DEFAULT 0,
    `IsActive`     tinyint(1)   NOT NULL DEFAULT 1,
    `CreatedAtUtc` datetime(6)  NOT NULL,
    CONSTRAINT `PK_RelationshipTypes` PRIMARY KEY (`Id`)
) CHARACTER SET = utf8mb4;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @idx = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='RelationshipTypes' AND INDEX_NAME='IX_RelationshipTypes_Code')=0,
    'CREATE UNIQUE INDEX `IX_RelationshipTypes_Code` ON `RelationshipTypes` (`Code`)',
    'SELECT 1');
PREPARE stmt FROM @idx; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // Seed relationship types
        migrationBuilder.Sql(@"
INSERT IGNORE INTO `RelationshipTypes` (`Id`, `Code`, `DisplayName`, `Description`, `IsDirectional`, `IsSystem`, `IsActive`, `CreatedAtUtc`) VALUES
    ('80000000-0000-0000-0000-000000000001', 'REFERS_TO',              'Refers To',             'Sending org refers clients to the receiving org',                 1, 1, 1, '2024-01-01 00:00:00'),
    ('80000000-0000-0000-0000-000000000002', 'ACCEPTS_REFERRALS_FROM', 'Accepts Referrals From','Receiving org accepts referrals from the sending org',            1, 1, 1, '2024-01-01 00:00:00'),
    ('80000000-0000-0000-0000-000000000003', 'FUNDED_BY',              'Funded By',             'Case or org is funded by the target organization',               1, 1, 1, '2024-01-01 00:00:00'),
    ('80000000-0000-0000-0000-000000000004', 'SERVICES_FOR',           'Services For',          'Organization provides services for the target org or client',    1, 1, 1, '2024-01-01 00:00:00'),
    ('80000000-0000-0000-0000-000000000005', 'ASSIGNS_LIEN_TO',        'Assigns Lien To',       'Organization assigns a lien to the target lien-owner org',       1, 1, 1, '2024-01-01 00:00:00'),
    ('80000000-0000-0000-0000-000000000006', 'MEMBER_OF_NETWORK',      'Member Of Network',     'Organization is a member of the target network or group',        0, 1, 1, '2024-01-01 00:00:00');");

        // Phase 2b: OrganizationRelationships table
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `OrganizationRelationships` (
    `Id`                   char(36)    NOT NULL COLLATE ascii_general_ci,
    `TenantId`             char(36)    NOT NULL COLLATE ascii_general_ci,
    `SourceOrganizationId` char(36)    NOT NULL COLLATE ascii_general_ci,
    `TargetOrganizationId` char(36)    NOT NULL COLLATE ascii_general_ci,
    `RelationshipTypeId`   char(36)    NOT NULL COLLATE ascii_general_ci,
    `ProductId`            char(36)    NULL COLLATE ascii_general_ci,
    `IsActive`             tinyint(1)  NOT NULL DEFAULT 1,
    `EstablishedAtUtc`     datetime(6) NOT NULL,
    `CreatedAtUtc`         datetime(6) NOT NULL,
    `UpdatedAtUtc`         datetime(6) NOT NULL,
    `CreatedByUserId`      char(36)    NULL COLLATE ascii_general_ci,
    CONSTRAINT `PK_OrganizationRelationships` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrgRel_Source` FOREIGN KEY (`SourceOrganizationId`) REFERENCES `Organizations` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_OrgRel_Target` FOREIGN KEY (`TargetOrganizationId`) REFERENCES `Organizations` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_OrgRel_RelType` FOREIGN KEY (`RelationshipTypeId`) REFERENCES `RelationshipTypes` (`Id`) ON DELETE RESTRICT
) CHARACTER SET = utf8mb4;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @ix1 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='OrganizationRelationships' AND INDEX_NAME='IX_OrgRel_Unique')=0,
    'CREATE UNIQUE INDEX `IX_OrgRel_Unique` ON `OrganizationRelationships` (`TenantId`,`SourceOrganizationId`,`TargetOrganizationId`,`RelationshipTypeId`)',
    'SELECT 1');
PREPARE stmt FROM @ix1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ix2 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='OrganizationRelationships' AND INDEX_NAME='IX_OrgRel_Source')=0,
    'CREATE INDEX `IX_OrgRel_Source` ON `OrganizationRelationships` (`TenantId`,`SourceOrganizationId`)',
    'SELECT 1');
PREPARE stmt FROM @ix2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ix3 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='OrganizationRelationships' AND INDEX_NAME='IX_OrgRel_Target')=0,
    'CREATE INDEX `IX_OrgRel_Target` ON `OrganizationRelationships` (`TenantId`,`TargetOrganizationId`)',
    'SELECT 1');
PREPARE stmt FROM @ix3; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ix4 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='OrganizationRelationships' AND INDEX_NAME='IX_OrgRel_RelType')=0,
    'CREATE INDEX `IX_OrgRel_RelType` ON `OrganizationRelationships` (`RelationshipTypeId`)',
    'SELECT 1');
PREPARE stmt FROM @ix4; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // Phase 2c: ProductRelationshipTypeRules table
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `ProductRelationshipTypeRules` (
    `Id`                 char(36)    NOT NULL COLLATE ascii_general_ci,
    `ProductId`          char(36)    NOT NULL COLLATE ascii_general_ci,
    `RelationshipTypeId` char(36)    NOT NULL COLLATE ascii_general_ci,
    `IsActive`           tinyint(1)  NOT NULL DEFAULT 1,
    `CreatedAtUtc`       datetime(6) NOT NULL,
    CONSTRAINT `PK_ProductRelationshipTypeRules` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PrRelRule_Product` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PrRelRule_RelType` FOREIGN KEY (`RelationshipTypeId`) REFERENCES `RelationshipTypes` (`Id`) ON DELETE RESTRICT
) CHARACTER SET = utf8mb4;");

        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @ix1 = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='ProductRelationshipTypeRules' AND INDEX_NAME='IX_PrRelRule_Unique')=0,
    'CREATE UNIQUE INDEX `IX_PrRelRule_Unique` ON `ProductRelationshipTypeRules` (`ProductId`,`RelationshipTypeId`)',
    'SELECT 1');
PREPARE stmt FROM @ix1; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        // Seed product-relationship rules
        migrationBuilder.Sql(@"
INSERT IGNORE INTO `ProductRelationshipTypeRules` (`Id`, `ProductId`, `RelationshipTypeId`, `IsActive`, `CreatedAtUtc`) VALUES
    ('81000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000003', '80000000-0000-0000-0000-000000000001', 1, '2024-01-01 00:00:00'),
    ('81000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000003', '80000000-0000-0000-0000-000000000002', 1, '2024-01-01 00:00:00'),
    ('81000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000003', 1, '2024-01-01 00:00:00'),
    ('81000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000002', '80000000-0000-0000-0000-000000000005', 1, '2024-01-01 00:00:00');");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS `ProductRelationshipTypeRules`;");
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS `OrganizationRelationships`;");
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS `RelationshipTypes`;");
    }
}
