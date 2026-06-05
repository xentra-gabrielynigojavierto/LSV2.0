using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProductAccess_TenantId_UserId_ProductCode",
                table: "UserProductAccess");

            migrationBuilder.AddColumn<int>(
                name: "AccessVersion",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_UserProductAccess_TenantId_UserId_ProductCode",
                table: "UserProductAccess",
                columns: new[] { "TenantId", "UserId", "ProductCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProductAccess_TenantId_UserId_ProductCode",
                table: "UserProductAccess");

            migrationBuilder.DropColumn(
                name: "AccessVersion",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_UserProductAccess_TenantId_UserId_ProductCode",
                table: "UserProductAccess",
                columns: new[] { "TenantId", "UserId", "ProductCode" });
        }
    }
}
