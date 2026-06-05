using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

public partial class AddParties : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `Parties` (
    `Id`                    char(36)        NOT NULL,
    `TenantId`              char(36)        NOT NULL,
    `OwnerOrganizationId`   char(36)        NOT NULL,
    `PartyType`             varchar(20)     NOT NULL DEFAULT 'INDIVIDUAL',
    `FirstName`             varchar(100)    NOT NULL,
    `LastName`              varchar(100)    NOT NULL,
    `MiddleName`            varchar(100)    NULL,
    `PreferredName`         varchar(100)    NULL,
    `DateOfBirth`           date            NULL,
    `SsnLast4`              char(4)         NULL,
    `LinkedUserId`          char(36)        NULL,
    `IsActive`              tinyint(1)      NOT NULL DEFAULT 1,
    `CreatedByUserId`       char(36)        NULL,
    `CreatedAtUtc`          datetime(6)     NOT NULL,
    `UpdatedAtUtc`          datetime(6)     NOT NULL,
    CONSTRAINT `PK_Parties` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;");

        migrationBuilder.Sql(@"
CREATE INDEX `IX_Parties_TenantId_Name`
    ON `Parties` (`TenantId`, `LastName`, `FirstName`);");

        migrationBuilder.Sql(@"
CREATE INDEX `IX_Parties_OwnerOrganizationId`
    ON `Parties` (`OwnerOrganizationId`);");

        migrationBuilder.Sql(@"
CREATE INDEX `IX_Parties_TenantId_LinkedUserId`
    ON `Parties` (`TenantId`, `LinkedUserId`);");

        migrationBuilder.Sql(@"
CREATE INDEX `IX_Parties_TenantId_Dob_Name`
    ON `Parties` (`TenantId`, `DateOfBirth`, `LastName`);");

        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `PartyContacts` (
    `Id`            char(36)        NOT NULL,
    `PartyId`       char(36)        NOT NULL,
    `ContactType`   varchar(20)     NOT NULL,
    `Value`         varchar(320)    NOT NULL,
    `IsPrimary`     tinyint(1)      NOT NULL DEFAULT 0,
    `IsVerified`    tinyint(1)      NOT NULL DEFAULT 0,
    `CreatedAtUtc`  datetime(6)     NOT NULL,
    CONSTRAINT `PK_PartyContacts` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PartyContacts_Parties` FOREIGN KEY (`PartyId`)
        REFERENCES `Parties` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;");

        migrationBuilder.Sql(@"
CREATE UNIQUE INDEX `IX_PartyContacts_PartyId_Type_Value`
    ON `PartyContacts` (`PartyId`, `ContactType`, `Value`);");

        migrationBuilder.Sql(@"
CREATE INDEX `IX_PartyContacts_Type_Value`
    ON `PartyContacts` (`ContactType`, `Value`);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `PartyContacts`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `Parties`;");
    }
}
