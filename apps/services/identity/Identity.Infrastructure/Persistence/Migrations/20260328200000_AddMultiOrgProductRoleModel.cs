using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiOrgProductRoleModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Organizations ─────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrgType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Organizations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_TenantId_Name",
                table: "Organizations",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_TenantId_OrgType",
                table: "Organizations",
                columns: new[] { "TenantId", "OrgType" });

            // ── OrganizationDomains ───────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "OrganizationDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Domain = table.Column<string>(type: "varchar(253)", maxLength: 253, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DomainType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPrimary = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsVerified = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationDomains_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationDomains_Domain",
                table: "OrganizationDomains",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationDomains_OrganizationId",
                table: "OrganizationDomains",
                column: "OrganizationId");

            // ── OrganizationProducts ──────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "OrganizationProducts",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EnabledAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GrantedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationProducts", x => new { x.OrganizationId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_OrganizationProducts_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationProducts_ProductId",
                table: "OrganizationProducts",
                column: "ProductId");

            // ── ProductRoles ──────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ProductRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EligibleOrgType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRoles_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRoles_Code",
                table: "ProductRoles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRoles_ProductId_EligibleOrgType",
                table: "ProductRoles",
                columns: new[] { "ProductId", "EligibleOrgType" });

            // ── Capabilities ──────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Capabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Capabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Capabilities_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Capabilities_Code",
                table: "Capabilities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Capabilities_ProductId",
                table: "Capabilities",
                column: "ProductId");

            // ── RoleCapabilities ──────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "RoleCapabilities",
                columns: table => new
                {
                    ProductRoleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CapabilityId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleCapabilities", x => new { x.ProductRoleId, x.CapabilityId });
                    table.ForeignKey(
                        name: "FK_RoleCapabilities_ProductRoles_ProductRoleId",
                        column: x => x.ProductRoleId,
                        principalTable: "ProductRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleCapabilities_Capabilities_CapabilityId",
                        column: x => x.CapabilityId,
                        principalTable: "Capabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RoleCapabilities_CapabilityId",
                table: "RoleCapabilities",
                column: "CapabilityId");

            // ── UserOrganizationMemberships ───────────────────────────────────
            migrationBuilder.CreateTable(
                name: "UserOrganizationMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MemberRole = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOrganizationMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserOrganizationMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserOrganizationMemberships_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationMemberships_UserId_OrganizationId",
                table: "UserOrganizationMemberships",
                columns: new[] { "UserId", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationMemberships_OrganizationId",
                table: "UserOrganizationMemberships",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOrganizationMemberships_UserId_IsActive",
                table: "UserOrganizationMemberships",
                columns: new[] { "UserId", "IsActive" });

            // ── UserRoleAssignments ───────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "UserRoleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RoleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoleAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoleAssignments_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoleAssignments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId_RoleId_OrganizationId",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "RoleId", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_RoleId",
                table: "UserRoleAssignments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_OrganizationId",
                table: "UserRoleAssignments",
                column: "OrganizationId");

            // ── Seed data via raw SQL (bypasses Designer.cs model requirement) ──

            // Organizations
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Organizations` (`Id`,`TenantId`,`Name`,`DisplayName`,`OrgType`,`IsActive`,`CreatedAtUtc`,`UpdatedAtUtc`,`CreatedByUserId`,`UpdatedByUserId`)
                VALUES ('40000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','LegalSynq Platform','LegalSynq Internal','INTERNAL',1,'2024-01-01 00:00:00','2024-01-01 00:00:00',NULL,NULL);
            ");

            // OrganizationDomains
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `OrganizationDomains` (`Id`,`OrganizationId`,`Domain`,`DomainType`,`IsPrimary`,`IsVerified`,`CreatedAtUtc`)
                VALUES ('40000000-0000-0000-0000-000000000002','40000000-0000-0000-0000-000000000001','legalsynq.legalsynq.com','SUBDOMAIN',1,1,'2024-01-01 00:00:00');
            ");

            // OrganizationProducts
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `OrganizationProducts` (`OrganizationId`,`ProductId`,`IsEnabled`,`EnabledAtUtc`,`GrantedByUserId`) VALUES
                ('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',1,'2024-01-01 00:00:00',NULL),
                ('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000002',1,'2024-01-01 00:00:00',NULL),
                ('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000003',1,'2024-01-01 00:00:00',NULL),
                ('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000004',1,'2024-01-01 00:00:00',NULL),
                ('40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000005',1,'2024-01-01 00:00:00',NULL);
            ");

            // ProductRoles
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `ProductRoles` (`Id`,`ProductId`,`Code`,`Name`,`EligibleOrgType`,`Description`,`IsActive`,`CreatedAtUtc`) VALUES
                ('50000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000003','CARECONNECT_REFERRER','CareConnect Referrer','LAW_FIRM','Law firm that refers clients to providers',1,'2024-01-01 00:00:00'),
                ('50000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000003','CARECONNECT_RECEIVER','CareConnect Receiver','PROVIDER','Provider that receives referrals',1,'2024-01-01 00:00:00'),
                ('50000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000002','SYNQLIEN_SELLER','SynqLien Seller','LAW_FIRM','Law firm that creates and offers liens',1,'2024-01-01 00:00:00'),
                ('50000000-0000-0000-0000-000000000004','10000000-0000-0000-0000-000000000002','SYNQLIEN_BUYER','SynqLien Buyer','LIEN_OWNER','Lien owner that purchases liens',1,'2024-01-01 00:00:00'),
                ('50000000-0000-0000-0000-000000000005','10000000-0000-0000-0000-000000000002','SYNQLIEN_HOLDER','SynqLien Holder','LIEN_OWNER','Lien owner that services and settles liens',1,'2024-01-01 00:00:00'),
                ('50000000-0000-0000-0000-000000000006','10000000-0000-0000-0000-000000000001','SYNQFUND_REFERRER','SynqFund Referrer','LAW_FIRM','Law firm that submits fund applications on behalf of clients',1,'2024-01-01 00:00:00'),
                ('50000000-0000-0000-0000-000000000007','10000000-0000-0000-0000-000000000001','SYNQFUND_FUNDER','SynqFund Funder','FUNDER','Funder that evaluates and funds applications',1,'2024-01-01 00:00:00'),
                ('50000000-0000-0000-0000-000000000008','10000000-0000-0000-0000-000000000001','SYNQFUND_APPLICANT_PORTAL','SynqFund Applicant Portal',NULL,'Limited read-only portal access for fund applicants',1,'2024-01-01 00:00:00');
            ");

            // Capabilities — CareConnect
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Capabilities` (`Id`,`ProductId`,`Code`,`Name`,`Description`,`IsActive`,`CreatedAtUtc`) VALUES
                ('60000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000003','referral:create','Create Referral','Create a new referral',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000003','referral:read:own','Read Own Referrals','View referrals you initiated',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000003','referral:cancel','Cancel Referral','Cancel a referral you initiated',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000004','10000000-0000-0000-0000-000000000003','referral:read:addressed','Read Addressed Referrals','View referrals addressed to your organization',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000005','10000000-0000-0000-0000-000000000003','referral:accept','Accept Referral','Accept an incoming referral',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000006','10000000-0000-0000-0000-000000000003','referral:decline','Decline Referral','Decline an incoming referral',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000007','10000000-0000-0000-0000-000000000003','provider:search','Search Providers','Search for providers by criteria',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000008','10000000-0000-0000-0000-000000000003','provider:map','View Provider Map','View providers on a geographic map',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000009','10000000-0000-0000-0000-000000000003','appointment:create','Create Appointment','Schedule an appointment',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000010','10000000-0000-0000-0000-000000000003','appointment:update','Update Appointment','Modify an existing appointment',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000011','10000000-0000-0000-0000-000000000003','appointment:read:own','Read Own Appointments','View your organization''s appointments',1,'2024-01-01 00:00:00');
            ");

            // Capabilities — SynqLien
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Capabilities` (`Id`,`ProductId`,`Code`,`Name`,`Description`,`IsActive`,`CreatedAtUtc`) VALUES
                ('60000000-0000-0000-0000-000000000012','10000000-0000-0000-0000-000000000002','lien:create','Create Lien','Create a new lien record',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000013','10000000-0000-0000-0000-000000000002','lien:offer','Offer Lien','Offer a lien for sale',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000014','10000000-0000-0000-0000-000000000002','lien:read:own','Read Own Liens','View liens you created',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000015','10000000-0000-0000-0000-000000000002','lien:browse','Browse Liens','Browse available liens for purchase',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000016','10000000-0000-0000-0000-000000000002','lien:purchase','Purchase Lien','Purchase a lien',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000017','10000000-0000-0000-0000-000000000002','lien:read:held','Read Held Liens','View liens you hold',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000018','10000000-0000-0000-0000-000000000002','lien:service','Service Lien','Service an active lien',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000019','10000000-0000-0000-0000-000000000002','lien:settle','Settle Lien','Settle and close a lien',1,'2024-01-01 00:00:00');
            ");

            // Capabilities — SynqFund
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `Capabilities` (`Id`,`ProductId`,`Code`,`Name`,`Description`,`IsActive`,`CreatedAtUtc`) VALUES
                ('60000000-0000-0000-0000-000000000020','10000000-0000-0000-0000-000000000001','application:create','Create Application','Submit a new fund application',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000021','10000000-0000-0000-0000-000000000001','application:read:own','Read Own Applications','View applications you submitted',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000022','10000000-0000-0000-0000-000000000001','application:cancel','Cancel Application','Cancel a pending application',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000023','10000000-0000-0000-0000-000000000001','application:read:addressed','Read Addressed Applications','View applications addressed to your organization',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000024','10000000-0000-0000-0000-000000000001','application:evaluate','Evaluate Application','Perform underwriting evaluation',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000025','10000000-0000-0000-0000-000000000001','application:approve','Approve Application','Approve and fund an application',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000026','10000000-0000-0000-0000-000000000001','application:decline','Decline Application','Decline a fund application',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000027','10000000-0000-0000-0000-000000000001','party:create','Create Party','Create a party profile for a client',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000028','10000000-0000-0000-0000-000000000001','party:read:own','Read Own Party','View party profiles you created',1,'2024-01-01 00:00:00'),
                ('60000000-0000-0000-0000-000000000029','10000000-0000-0000-0000-000000000001','application:status:view','View Application Status','View the status of a fund application',1,'2024-01-01 00:00:00');
            ");

            // RoleCapabilities
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `RoleCapabilities` (`ProductRoleId`,`CapabilityId`) VALUES
                ('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000001'),
                ('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000002'),
                ('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000003'),
                ('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000007'),
                ('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000008'),
                ('50000000-0000-0000-0000-000000000001','60000000-0000-0000-0000-000000000011'),
                ('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000004'),
                ('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000005'),
                ('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000006'),
                ('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000009'),
                ('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000010'),
                ('50000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000011'),
                ('50000000-0000-0000-0000-000000000003','60000000-0000-0000-0000-000000000012'),
                ('50000000-0000-0000-0000-000000000003','60000000-0000-0000-0000-000000000013'),
                ('50000000-0000-0000-0000-000000000003','60000000-0000-0000-0000-000000000014'),
                ('50000000-0000-0000-0000-000000000004','60000000-0000-0000-0000-000000000015'),
                ('50000000-0000-0000-0000-000000000004','60000000-0000-0000-0000-000000000016'),
                ('50000000-0000-0000-0000-000000000004','60000000-0000-0000-0000-000000000017'),
                ('50000000-0000-0000-0000-000000000005','60000000-0000-0000-0000-000000000017'),
                ('50000000-0000-0000-0000-000000000005','60000000-0000-0000-0000-000000000018'),
                ('50000000-0000-0000-0000-000000000005','60000000-0000-0000-0000-000000000019'),
                ('50000000-0000-0000-0000-000000000006','60000000-0000-0000-0000-000000000020'),
                ('50000000-0000-0000-0000-000000000006','60000000-0000-0000-0000-000000000021'),
                ('50000000-0000-0000-0000-000000000006','60000000-0000-0000-0000-000000000022'),
                ('50000000-0000-0000-0000-000000000006','60000000-0000-0000-0000-000000000027'),
                ('50000000-0000-0000-0000-000000000006','60000000-0000-0000-0000-000000000028'),
                ('50000000-0000-0000-0000-000000000007','60000000-0000-0000-0000-000000000023'),
                ('50000000-0000-0000-0000-000000000007','60000000-0000-0000-0000-000000000024'),
                ('50000000-0000-0000-0000-000000000007','60000000-0000-0000-0000-000000000025'),
                ('50000000-0000-0000-0000-000000000007','60000000-0000-0000-0000-000000000026'),
                ('50000000-0000-0000-0000-000000000008','60000000-0000-0000-0000-000000000029'),
                ('50000000-0000-0000-0000-000000000008','60000000-0000-0000-0000-000000000028');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RoleCapabilities");
            migrationBuilder.DropTable(name: "UserRoleAssignments");
            migrationBuilder.DropTable(name: "UserOrganizationMemberships");
            migrationBuilder.DropTable(name: "Capabilities");
            migrationBuilder.DropTable(name: "ProductRoles");
            migrationBuilder.DropTable(name: "OrganizationProducts");
            migrationBuilder.DropTable(name: "OrganizationDomains");
            migrationBuilder.DropTable(name: "Organizations");
        }
    }
}
