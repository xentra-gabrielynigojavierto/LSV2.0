using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tenant.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProvisioningState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── BLK-TS-02: Tenant-owned provisioning state ────────────────────────
            //
            // ProvisioningStatus — tracks DNS/infra readiness for this tenant.
            //   Default = 'Unknown' so all existing rows are valid without a backfill.
            //
            // ProvisionedAtUtc — UTC timestamp of first successful provision.
            //
            // LastProvisioningError — last failure message (cleared on success).

            migrationBuilder.AddColumn<string>(
                name:          "ProvisioningStatus",
                table:         "tenant_Tenants",
                type:          "varchar(50)",
                maxLength:     50,
                nullable:      false,
                defaultValue:  "Unknown");

            migrationBuilder.AddColumn<DateTime>(
                name:     "ProvisionedAtUtc",
                table:    "tenant_Tenants",
                type:     "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name:      "LastProvisioningError",
                table:     "tenant_Tenants",
                type:      "varchar(1000)",
                maxLength: 1000,
                nullable:  true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ProvisioningStatus",    table: "tenant_Tenants");
            migrationBuilder.DropColumn(name: "ProvisionedAtUtc",      table: "tenant_Tenants");
            migrationBuilder.DropColumn(name: "LastProvisioningError", table: "tenant_Tenants");
        }
    }
}
