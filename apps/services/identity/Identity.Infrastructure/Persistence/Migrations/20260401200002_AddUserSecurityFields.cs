using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSecurityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UIX-003-03: Add security / session fields to Users table
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAtUtc",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LockedByAdminId",
                table: "Users",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAtUtc",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // UIX-003-03: PasswordResetTokens table for admin-triggered password reset flow
            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id                 = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId             = table.Column<Guid>(type: "char(36)", nullable: false),
                    TenantId           = table.Column<Guid>(type: "char(36)", nullable: false),
                    TriggeredByAdminId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TokenHash          = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Status             = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ExpiresAtUtc       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UsedAtUtc          = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RevokedAtUtc       = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                table: "PasswordResetTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId",
                table: "PasswordResetTokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PasswordResetTokens");

            migrationBuilder.DropColumn(name: "IsLocked",        table: "Users");
            migrationBuilder.DropColumn(name: "LockedAtUtc",     table: "Users");
            migrationBuilder.DropColumn(name: "LockedByAdminId", table: "Users");
            migrationBuilder.DropColumn(name: "LastLoginAtUtc",  table: "Users");
            migrationBuilder.DropColumn(name: "SessionVersion",  table: "Users");
        }
    }
}
