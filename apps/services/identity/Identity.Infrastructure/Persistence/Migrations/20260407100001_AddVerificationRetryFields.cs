using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    [Migration("20260407100001_AddVerificationRetryFields")]
    public partial class AddVerificationRetryFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VerificationAttemptCount",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVerificationAttemptUtc",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextVerificationRetryAtUtc",
                table: "Tenants",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerificationRetryExhausted",
                table: "Tenants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProvisioningFailureStage",
                table: "Tenants",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAtUtc",
                table: "TenantDomains",
                type: "datetime(6)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "VerificationAttemptCount", table: "Tenants");
            migrationBuilder.DropColumn(name: "LastVerificationAttemptUtc", table: "Tenants");
            migrationBuilder.DropColumn(name: "NextVerificationRetryAtUtc", table: "Tenants");
            migrationBuilder.DropColumn(name: "IsVerificationRetryExhausted", table: "Tenants");
            migrationBuilder.DropColumn(name: "ProvisioningFailureStage", table: "Tenants");
            migrationBuilder.DropColumn(name: "VerifiedAtUtc", table: "TenantDomains");
        }
    }
}
