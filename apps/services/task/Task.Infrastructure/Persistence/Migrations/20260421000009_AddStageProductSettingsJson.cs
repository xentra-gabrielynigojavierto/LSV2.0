using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    public partial class AddStageProductSettingsJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name:      "ProductSettingsJson",
                table:     "tasks_StageConfigs",
                type:      "TEXT",
                nullable:  true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name:  "ProductSettingsJson",
                table: "tasks_StageConfigs");
        }
    }
}
