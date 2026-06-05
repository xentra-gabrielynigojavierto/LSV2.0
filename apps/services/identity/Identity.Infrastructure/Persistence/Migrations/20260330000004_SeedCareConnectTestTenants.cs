using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedCareConnectTestTenants : Migration
    {
        // ── ID ranges used in this migration ─────────────────────────────────
        //   Tenants               20000000-0000-0000-0000-00000000000{2,3}
        //   Roles (HARTWELL)      31000000-0000-0000-0000-00000000000{1,2}
        //   Roles (MERIDIAN)      32000000-0000-0000-0000-00000000000{1,2}
        //   Users (HARTWELL)      21000000-0000-0000-0000-00000000000{1,2,3}
        //   Users (MERIDIAN)      22000000-0000-0000-0000-00000000000{1,2}
        //   Orgs                  41000000-0000-0000-0000-000000000001  (HARTWELL)
        //                         42000000-0000-0000-0000-000000000001  (MERIDIAN)
        //   Memberships (HARTWELL) 41000000-0000-0000-0000-00000000000{2,3,4}
        //   Memberships (MERIDIAN) 42000000-0000-0000-0000-00000000000{2,3}
        // ─────────────────────────────────────────────────────────────────────

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Tenants ────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Tenants` (`Id`,`Name`,`Code`,`IsActive`,`CreatedAtUtc`,`UpdatedAtUtc`) VALUES
                ('20000000-0000-0000-0000-000000000002','Hartwell & Associates','HARTWELL',1,'2024-02-15 08:30:00','2024-02-15 08:30:00'),
                ('20000000-0000-0000-0000-000000000003','Meridian Care Group','MERIDIAN',1,'2024-03-01 09:00:00','2024-03-01 09:00:00');
            ");

            // ── 2. System roles (per-tenant copies) ───────────────────────────
            //   HARTWELL roles
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Roles` (`Id`,`TenantId`,`Name`,`Description`,`IsSystemRole`,`CreatedAtUtc`,`UpdatedAtUtc`) VALUES
                ('31000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000002','TenantAdmin','Tenant-level administration access',1,'2024-02-15 08:30:00','2024-02-15 08:30:00'),
                ('31000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000002','StandardUser','Standard user access',1,'2024-02-15 08:30:00','2024-02-15 08:30:00');
            ");
            //   MERIDIAN roles
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Roles` (`Id`,`TenantId`,`Name`,`Description`,`IsSystemRole`,`CreatedAtUtc`,`UpdatedAtUtc`) VALUES
                ('32000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','TenantAdmin','Tenant-level administration access',1,'2024-03-01 09:00:00','2024-03-01 09:00:00'),
                ('32000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000003','StandardUser','Standard user access',1,'2024-03-01 09:00:00','2024-03-01 09:00:00');
            ");

            // ── 3. Users ──────────────────────────────────────────────────────
            //   All HARTWELL users share the password: hartwell123!
            //   Hash: $2a$12$FhcogSUbGGiLl/sRLJxylOFE.UJU2i5rACVAyO4wiX7jYxxEnuGkS
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Users` (`Id`,`TenantId`,`Email`,`PasswordHash`,`FirstName`,`LastName`,`IsActive`,`CreatedAtUtc`,`UpdatedAtUtc`) VALUES
                ('21000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000002','margaret@hartwell.law','$2a$12$FhcogSUbGGiLl/sRLJxylOFE.UJU2i5rACVAyO4wiX7jYxxEnuGkS','Margaret','Hartwell',1,'2024-02-15 08:30:00','2024-02-15 08:30:00'),
                ('21000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000002','james.whitmore@hartwell.law','$2a$12$FhcogSUbGGiLl/sRLJxylOFE.UJU2i5rACVAyO4wiX7jYxxEnuGkS','James','Whitmore',1,'2024-02-16 09:00:00','2024-02-16 09:00:00'),
                ('21000000-0000-0000-0000-000000000003','20000000-0000-0000-0000-000000000002','olivia.chen@hartwell.law','$2a$12$FhcogSUbGGiLl/sRLJxylOFE.UJU2i5rACVAyO4wiX7jYxxEnuGkS','Olivia','Chen',1,'2024-02-17 09:30:00','2024-02-17 09:30:00');
            ");
            //   All MERIDIAN users share the password: meridian123!
            //   Hash: $2a$12$CIXHD3tNU7bpPleD5a0fn.aNNcA1uuNo/7btu43Brwt06ciQHv2uS
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Users` (`Id`,`TenantId`,`Email`,`PasswordHash`,`FirstName`,`LastName`,`IsActive`,`CreatedAtUtc`,`UpdatedAtUtc`) VALUES
                ('22000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','dr.ramirez@meridiancare.com','$2a$12$CIXHD3tNU7bpPleD5a0fn.aNNcA1uuNo/7btu43Brwt06ciQHv2uS','Elena','Ramirez',1,'2024-03-01 09:00:00','2024-03-01 09:00:00'),
                ('22000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000003','alex.diallo@meridiancare.com','$2a$12$CIXHD3tNU7bpPleD5a0fn.aNNcA1uuNo/7btu43Brwt06ciQHv2uS','Alex','Diallo',1,'2024-03-02 09:00:00','2024-03-02 09:00:00');
            ");

            // ── 4. UserRoles ──────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `UserRoles` (`UserId`,`RoleId`,`AssignedAtUtc`) VALUES
                ('21000000-0000-0000-0000-000000000001','31000000-0000-0000-0000-000000000001','2024-02-15 08:30:00'),
                ('21000000-0000-0000-0000-000000000002','31000000-0000-0000-0000-000000000002','2024-02-16 09:00:00'),
                ('21000000-0000-0000-0000-000000000003','31000000-0000-0000-0000-000000000002','2024-02-17 09:30:00'),
                ('22000000-0000-0000-0000-000000000001','32000000-0000-0000-0000-000000000001','2024-03-01 09:00:00'),
                ('22000000-0000-0000-0000-000000000002','32000000-0000-0000-0000-000000000002','2024-03-02 09:00:00');
            ");

            // ── 5. Organizations ──────────────────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Organizations` (`Id`,`TenantId`,`Name`,`DisplayName`,`OrgType`,`IsActive`,`CreatedAtUtc`,`UpdatedAtUtc`,`CreatedByUserId`,`UpdatedByUserId`) VALUES
                ('41000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000002','Hartwell & Associates','Hartwell & Associates','LAW_FIRM',1,'2024-02-15 08:30:00','2024-02-15 08:30:00',NULL,NULL),
                ('42000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000003','Meridian Care Group','Meridian Care Group','PROVIDER',1,'2024-03-01 09:00:00','2024-03-01 09:00:00',NULL,NULL);
            ");

            // ── 6. OrganizationProducts (CareConnect = 10000000-...-000000000003) ─
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `OrganizationProducts` (`OrganizationId`,`ProductId`,`IsEnabled`,`EnabledAtUtc`,`GrantedByUserId`) VALUES
                ('41000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000003',1,'2024-02-16 09:00:00',NULL),
                ('42000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000003',1,'2024-03-02 10:00:00',NULL);
            ");

            // ── 7. UserOrganizationMemberships ────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `UserOrganizationMemberships` (`Id`,`UserId`,`OrganizationId`,`MemberRole`,`IsActive`,`JoinedAtUtc`,`GrantedByUserId`) VALUES
                ('41000000-0000-0000-0000-000000000002','21000000-0000-0000-0000-000000000001','41000000-0000-0000-0000-000000000001','ADMIN',1,'2024-02-15 08:30:00',NULL),
                ('41000000-0000-0000-0000-000000000003','21000000-0000-0000-0000-000000000002','41000000-0000-0000-0000-000000000001','MEMBER',1,'2024-02-16 09:00:00','21000000-0000-0000-0000-000000000001'),
                ('41000000-0000-0000-0000-000000000004','21000000-0000-0000-0000-000000000003','41000000-0000-0000-0000-000000000001','MEMBER',1,'2024-02-17 09:30:00','21000000-0000-0000-0000-000000000001'),
                ('42000000-0000-0000-0000-000000000002','22000000-0000-0000-0000-000000000001','42000000-0000-0000-0000-000000000001','ADMIN',1,'2024-03-01 09:00:00',NULL),
                ('42000000-0000-0000-0000-000000000003','22000000-0000-0000-0000-000000000002','42000000-0000-0000-0000-000000000001','MEMBER',1,'2024-03-02 09:00:00','22000000-0000-0000-0000-000000000001');
            ");

            // ── 8. TenantProducts (legacy display layer — not used for access control) ─
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `TenantProducts` (`TenantId`,`ProductId`,`IsEnabled`,`EnabledAtUtc`) VALUES
                ('20000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000003',1,'2024-02-16 09:00:00'),
                ('20000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000003',1,'2024-03-02 10:00:00');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM `TenantProducts`      WHERE `TenantId` IN ('20000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000003');
                DELETE FROM `UserOrganizationMemberships` WHERE `Id` LIKE '41000000-0000-0000-0000-0000000000%' OR `Id` LIKE '42000000-0000-0000-0000-0000000000%';
                DELETE FROM `OrganizationProducts` WHERE `OrganizationId` IN ('41000000-0000-0000-0000-000000000001','42000000-0000-0000-0000-000000000001');
                DELETE FROM `Organizations`        WHERE `Id` IN ('41000000-0000-0000-0000-000000000001','42000000-0000-0000-0000-000000000001');
                DELETE FROM `UserRoles`            WHERE `UserId` LIKE '21000000-0000-0000-0000-0000000000%' OR `UserId` LIKE '22000000-0000-0000-0000-0000000000%';
                DELETE FROM `Users`                WHERE `Id` LIKE '21000000-0000-0000-0000-0000000000%' OR `Id` LIKE '22000000-0000-0000-0000-0000000000%';
                DELETE FROM `Roles`                WHERE `Id` LIKE '31000000-0000-0000-0000-0000000000%' OR `Id` LIKE '32000000-0000-0000-0000-0000000000%';
                DELETE FROM `Tenants`              WHERE `Id` IN ('20000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000003');
            ");
        }
    }
}
