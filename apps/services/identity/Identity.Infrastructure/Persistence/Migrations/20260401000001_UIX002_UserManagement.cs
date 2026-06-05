using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [Migration("20260401000001_UIX002_UserManagement")]
    public partial class UIX002_UserManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Add IsPrimary to UserOrganizationMemberships ─────────────
            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "UserOrganizationMemberships",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // ── 2. Create TenantGroups ────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "TenantGroups",
                columns: table => new
                {
                    Id              = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: false),
                    TenantId        = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: false),
                    Name            = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description     = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    IsActive        = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantGroups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantGroups_TenantId",
                table: "TenantGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantGroups_TenantId_Name",
                table: "TenantGroups",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            // ── 3. Create GroupMemberships ────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "GroupMemberships",
                columns: table => new
                {
                    Id            = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: false),
                    GroupId       = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: false),
                    UserId        = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: false),
                    TenantId      = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: false),
                    JoinedAtUtc   = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMemberships_TenantGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "TenantGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_GroupId_UserId",
                table: "GroupMemberships",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_UserId",
                table: "GroupMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_TenantId",
                table: "GroupMemberships",
                column: "TenantId");

            // ── 4. Create UserInvitations ────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "UserInvitations",
                columns: table => new
                {
                    Id              = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: false),
                    UserId          = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: false),
                    TenantId        = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "char(36) CHARACTER SET ascii COLLATE ascii_general_ci", nullable: true),
                    TokenHash       = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false),
                    Status          = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    PortalOrigin    = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ExpiresAtUtc    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AcceptedAtUtc   = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RevokedAtUtc    = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserInvitations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserInvitations_UserId",
                table: "UserInvitations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInvitations_UserId_Status",
                table: "UserInvitations",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInvitations_TokenHash",
                table: "UserInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserInvitations");
            migrationBuilder.DropTable(name: "GroupMemberships");
            migrationBuilder.DropTable(name: "TenantGroups");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "UserOrganizationMemberships");
        }
    }
}
