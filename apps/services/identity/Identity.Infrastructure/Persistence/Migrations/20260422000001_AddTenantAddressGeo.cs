using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260422000001_AddTenantAddressGeo")]
public partial class AddTenantAddressGeo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AddressLine1",
            table: "idt_Tenants",
            type: "varchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "City",
            table: "idt_Tenants",
            type: "varchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "State",
            table: "idt_Tenants",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PostalCode",
            table: "idt_Tenants",
            type: "varchar(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "Latitude",
            table: "idt_Tenants",
            type: "double",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "Longitude",
            table: "idt_Tenants",
            type: "double",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GeoPointSource",
            table: "idt_Tenants",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AddressLine1",   table: "idt_Tenants");
        migrationBuilder.DropColumn(name: "City",           table: "idt_Tenants");
        migrationBuilder.DropColumn(name: "State",          table: "idt_Tenants");
        migrationBuilder.DropColumn(name: "PostalCode",     table: "idt_Tenants");
        migrationBuilder.DropColumn(name: "Latitude",       table: "idt_Tenants");
        migrationBuilder.DropColumn(name: "Longitude",      table: "idt_Tenants");
        migrationBuilder.DropColumn(name: "GeoPointSource", table: "idt_Tenants");
    }
}
