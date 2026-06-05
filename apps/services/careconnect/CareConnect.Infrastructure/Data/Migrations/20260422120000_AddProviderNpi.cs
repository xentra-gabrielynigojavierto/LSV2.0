using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// CC2-INT-B06-01 — Adds NPI (National Provider Identifier) to the shared provider registry.
///
/// NPI is used as the primary deduplication key when adding providers to networks.
/// A non-unique index is added for lookup performance (uniqueness enforced at app layer
/// because MySQL 8.0 does not support partial/filtered unique indexes).
/// </summary>
[Migration("20260422120000_AddProviderNpi")]
public partial class AddProviderNpi : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Npi",
            table: "cc_Providers",
            type: "varchar(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Providers_Npi",
            table: "cc_Providers",
            column: "Npi");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Providers_Npi",
            table: "cc_Providers");

        migrationBuilder.DropColumn(
            name: "Npi",
            table: "cc_Providers");
    }
}
