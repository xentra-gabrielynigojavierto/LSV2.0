using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// PUM-B01: Adds UserType column to idt_Users.
/// Existing rows receive the default value 'TenantUser'.
/// Safe additive migration — no data destruction.
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260424200000_AddUserType")]
public partial class AddUserType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Safe additive: add UserType with a default of 'TenantUser' so all
        // existing rows are classified correctly without a data backfill step.
        migrationBuilder.AddColumn<string>(
            name: "UserType",
            table: "idt_Users",
            type: "varchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "TenantUser");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UserType",
            table: "idt_Users");
    }
}
