using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    [Migration("20260414100001_AddOrganizationProviderMode")]
    public partial class AddOrganizationProviderMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderMode",
                table: "idt_Organizations",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "sell");

            migrationBuilder.Sql("UPDATE idt_Organizations SET ProviderMode = 'sell' WHERE ProviderMode IS NULL OR ProviderMode = '';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderMode",
                table: "idt_Organizations");
        }
    }
}
