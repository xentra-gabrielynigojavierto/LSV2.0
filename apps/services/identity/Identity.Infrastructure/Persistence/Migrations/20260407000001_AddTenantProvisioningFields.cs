using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [Migration("20260407000001_AddTenantProvisioningFields")]
    public partial class AddTenantProvisioningFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Subdomain",
                table: "Tenants",
                type: "varchar(63)",
                maxLength: 63,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProvisioningStatus",
                table: "Tenants",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastProvisioningAttemptUtc",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvisioningFailureReason",
                table: "Tenants",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Subdomain",
                table: "Tenants",
                column: "Subdomain",
                unique: true,
                filter: "`Subdomain` IS NOT NULL");

            migrationBuilder.Sql(
                "UPDATE `Tenants` SET `ProvisioningStatus` = 'Active' WHERE `Code` = 'LEGALSYNQ'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Subdomain",
                table: "Tenants");

            migrationBuilder.DropColumn(name: "Subdomain", table: "Tenants");
            migrationBuilder.DropColumn(name: "ProvisioningStatus", table: "Tenants");
            migrationBuilder.DropColumn(name: "LastProvisioningAttemptUtc", table: "Tenants");
            migrationBuilder.DropColumn(name: "ProvisioningFailureReason", table: "Tenants");
        }
    }
}
