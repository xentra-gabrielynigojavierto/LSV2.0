using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPermissionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: If this migration was partially applied on a prior run (MySQL auto-commits DDL
            // so AlterColumn/AddColumn/CreateTable may have committed before the migration record
            // was written to __EFMigrationsHistory), Program.cs has a pre-migration guard that
            // seeds the data idempotently and marks this migration as applied BEFORE Migrate() runs.
            // That guard means Up() is only ever executed against a schema where the columns do NOT
            // yet exist, so the standard EF DDL below is safe.

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "idt_Capabilities",
                type: "varchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "idt_Capabilities",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "idt_Capabilities",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "idt_Capabilities",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "idt_Capabilities",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "idt_Policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PolicyCode = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Effect = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false, defaultValue: "Allow")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idt_Policies", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "idt_PermissionPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PermissionCode = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PolicyId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idt_PermissionPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_idt_PermissionPolicies_idt_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "idt_Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "idt_PolicyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PolicyId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConditionType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Field = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Operator = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LogicalGroup = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idt_PolicyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_idt_PolicyRules_idt_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "idt_Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Referral", "SYNQ_CARECONNECT.referral:create", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Referral", "SYNQ_CARECONNECT.referral:read:own", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Referral", "SYNQ_CARECONNECT.referral:cancel", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000004"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Referral", "SYNQ_CARECONNECT.referral:read:addressed", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000005"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Referral", "SYNQ_CARECONNECT.referral:accept", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000006"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Referral", "SYNQ_CARECONNECT.referral:decline", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000007"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Provider", "SYNQ_CARECONNECT.provider:search", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000008"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Provider", "SYNQ_CARECONNECT.provider:map", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000009"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Appointment", "SYNQ_CARECONNECT.appointment:create", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000010"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Appointment", "SYNQ_CARECONNECT.appointment:update", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000011"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Appointment", "SYNQ_CARECONNECT.appointment:read:own", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000012"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Lien", "SYNQ_LIENS.lien:create", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000013"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Lien", "SYNQ_LIENS.lien:offer", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000014"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Lien", "SYNQ_LIENS.lien:read:own", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000015"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Lien", "SYNQ_LIENS.lien:browse", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000016"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Lien", "SYNQ_LIENS.lien:purchase", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000017"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Lien", "SYNQ_LIENS.lien:read:held", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000018"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Lien", "SYNQ_LIENS.lien:service", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000019"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Lien", "SYNQ_LIENS.lien:settle", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000020"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Application", "SYNQ_FUND.application:create", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000021"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Application", "SYNQ_FUND.application:read:own", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000022"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Application", "SYNQ_FUND.application:cancel", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000023"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Application", "SYNQ_FUND.application:read:addressed", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000024"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Application", "SYNQ_FUND.application:evaluate", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000025"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Application", "SYNQ_FUND.application:approve", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000026"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Application", "SYNQ_FUND.application:decline", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000027"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Party", "SYNQ_FUND.party:create", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000028"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Party", "SYNQ_FUND.party:read:own", null, null, null });

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000029"),
                columns: new[] { "Category", "Code", "CreatedBy", "UpdatedAtUtc", "UpdatedBy" },
                values: new object[] { "Application", "SYNQ_FUND.application:status:view", null, null, null });

            // LS-ID-TNT-011: SYNQ_PLATFORM pseudo-product — FK anchor for TENANT.* permissions.
            // Not a subscribable product; never added to TenantProducts.
            // INSERT IGNORE is used throughout this block so the statements are idempotent: MySQL
            // DDL auto-commits which prevents rollback when any earlier DML in the same migration
            // run has already been committed.
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `idt_Products` (`Id`, `Code`, `CreatedAtUtc`, `Description`, `IsActive`, `Name`)
                VALUES ('10000000-0000-0000-0000-000000000006', 'SYNQ_PLATFORM', '2025-01-01 00:00:00.000000',
                        'Platform/tenant operation capabilities', 1, 'SynqPlatform');");

            // LS-ID-TNT-011: Tenant-level permission catalog (SYNQ_PLATFORM pseudo-product).
            // Resolved via system Role → RolePermissionAssignment (not product roles).
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `idt_Capabilities`
                    (`Id`, `ProductId`, `Code`, `Name`, `Description`, `Category`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`)
                VALUES
                    ('60000000-0000-0000-0000-000000000030','10000000-0000-0000-0000-000000000006','TENANT.users:view',         'View Tenant Users',       'View the list of users in the tenant',                  'Users',       1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000031','10000000-0000-0000-0000-000000000006','TENANT.users:manage',       'Manage Tenant Users',     'Create, edit, and deactivate users in the tenant',      'Users',       1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000032','10000000-0000-0000-0000-000000000006','TENANT.groups:manage',      'Manage Access Groups',    'Create, edit, and delete tenant access groups',         'Groups',      1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000033','10000000-0000-0000-0000-000000000006','TENANT.roles:assign',       'Assign Roles',            'Assign or revoke roles for tenant users',               'Roles',       1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000034','10000000-0000-0000-0000-000000000006','TENANT.products:assign',    'Assign Product Access',   'Assign or revoke product access for tenant users',      'Products',    1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000035','10000000-0000-0000-0000-000000000006','TENANT.settings:manage',    'Manage Tenant Settings',  'Update tenant configuration and preferences',           'Settings',    1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000036','10000000-0000-0000-0000-000000000006','TENANT.audit:view',         'View Audit Logs',         'View identity and access audit events for the tenant',  'Audit',       1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL),
                    ('60000000-0000-0000-0000-000000000037','10000000-0000-0000-0000-000000000006','TENANT.invitations:manage', 'Manage User Invitations', 'Send, resend, and revoke user invitations',             'Invitations', 1,'2025-01-01 00:00:00.000000',NULL,NULL,NULL);");

            // LS-ID-TNT-011: Role → tenant permission mappings.
            // TenantAdmin gets all 8 tenant permissions; StandardUser gets users:view only.
            // The actual DB column is CapabilityId (not PermissionId) — matches the table DDL
            // from 20260401220001_UIX005_AddRoleCapabilityAssignments.
            migrationBuilder.Sql(@"
                INSERT IGNORE INTO `idt_RoleCapabilityAssignments` (`RoleId`, `CapabilityId`, `AssignedAtUtc`, `AssignedByUserId`)
                VALUES
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000030','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000031','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000032','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000033','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000034','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000035','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000036','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000002','60000000-0000-0000-0000-000000000037','2025-01-01 00:00:00.000000',NULL),
                    ('30000000-0000-0000-0000-000000000003','60000000-0000-0000-0000-000000000030','2025-01-01 00:00:00.000000',NULL);");

            migrationBuilder.CreateIndex(
                name: "IX_idt_PermissionPolicies_PermissionCode",
                table: "idt_PermissionPolicies",
                column: "PermissionCode");

            migrationBuilder.CreateIndex(
                name: "IX_idt_PermissionPolicies_PermissionCode_PolicyId",
                table: "idt_PermissionPolicies",
                columns: new[] { "PermissionCode", "PolicyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idt_PermissionPolicies_PolicyId",
                table: "idt_PermissionPolicies",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_idt_Policies_PolicyCode",
                table: "idt_Policies",
                column: "PolicyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_idt_Policies_ProductCode",
                table: "idt_Policies",
                column: "ProductCode");

            migrationBuilder.CreateIndex(
                name: "IX_idt_PolicyRules_PolicyId",
                table: "idt_PolicyRules",
                column: "PolicyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // LS-ID-TNT-011: Remove role → tenant permission mappings (must precede permission deletes).
            // Use raw SQL with the actual column name CapabilityId (not PermissionId).
            migrationBuilder.Sql(@"
                DELETE FROM `idt_RoleCapabilityAssignments`
                WHERE `CapabilityId` IN (
                    '60000000-0000-0000-0000-000000000030',
                    '60000000-0000-0000-0000-000000000031',
                    '60000000-0000-0000-0000-000000000032',
                    '60000000-0000-0000-0000-000000000033',
                    '60000000-0000-0000-0000-000000000034',
                    '60000000-0000-0000-0000-000000000035',
                    '60000000-0000-0000-0000-000000000036',
                    '60000000-0000-0000-0000-000000000037'
                ) AND `RoleId` IN (
                    '30000000-0000-0000-0000-000000000002',
                    '30000000-0000-0000-0000-000000000003'
                );");

            // LS-ID-TNT-011: Remove TENANT.* permissions and SYNQ_PLATFORM product.
            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000030"));
            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000031"));
            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000032"));
            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000033"));
            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000034"));
            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000035"));
            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000036"));
            migrationBuilder.DeleteData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000037"));
            migrationBuilder.DeleteData(
                table: "idt_Products",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"));

            // DROP TABLE IF EXISTS / DROP COLUMN IF EXISTS for idempotent rollback.
            migrationBuilder.Sql("DROP TABLE IF EXISTS `idt_PermissionPolicies`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `idt_PolicyRules`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `idt_Policies`;");
            migrationBuilder.Sql("ALTER TABLE `idt_Capabilities` DROP COLUMN IF EXISTS `Category`;");
            migrationBuilder.Sql("ALTER TABLE `idt_Capabilities` DROP COLUMN IF EXISTS `CreatedBy`;");
            migrationBuilder.Sql("ALTER TABLE `idt_Capabilities` DROP COLUMN IF EXISTS `UpdatedAtUtc`;");
            migrationBuilder.Sql("ALTER TABLE `idt_Capabilities` DROP COLUMN IF EXISTS `UpdatedBy`;");
            migrationBuilder.Sql(
                "ALTER TABLE `idt_Capabilities` MODIFY COLUMN `Code` varchar(100) CHARACTER SET utf8mb4 NOT NULL;");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                column: "Code",
                value: "referral:create");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                column: "Code",
                value: "referral:read:own");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"),
                column: "Code",
                value: "referral:cancel");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000004"),
                column: "Code",
                value: "referral:read:addressed");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000005"),
                column: "Code",
                value: "referral:accept");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000006"),
                column: "Code",
                value: "referral:decline");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000007"),
                column: "Code",
                value: "provider:search");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000008"),
                column: "Code",
                value: "provider:map");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000009"),
                column: "Code",
                value: "appointment:create");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000010"),
                column: "Code",
                value: "appointment:update");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000011"),
                column: "Code",
                value: "appointment:read:own");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000012"),
                column: "Code",
                value: "lien:create");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000013"),
                column: "Code",
                value: "lien:offer");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000014"),
                column: "Code",
                value: "lien:read:own");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000015"),
                column: "Code",
                value: "lien:browse");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000016"),
                column: "Code",
                value: "lien:purchase");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000017"),
                column: "Code",
                value: "lien:read:held");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000018"),
                column: "Code",
                value: "lien:service");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000019"),
                column: "Code",
                value: "lien:settle");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000020"),
                column: "Code",
                value: "application:create");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000021"),
                column: "Code",
                value: "application:read:own");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000022"),
                column: "Code",
                value: "application:cancel");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000023"),
                column: "Code",
                value: "application:read:addressed");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000024"),
                column: "Code",
                value: "application:evaluate");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000025"),
                column: "Code",
                value: "application:approve");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000026"),
                column: "Code",
                value: "application:decline");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000027"),
                column: "Code",
                value: "party:create");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000028"),
                column: "Code",
                value: "party:read:own");

            migrationBuilder.UpdateData(
                table: "idt_Capabilities",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000029"),
                column: "Code",
                value: "application:status:view");
        }
    }
}
