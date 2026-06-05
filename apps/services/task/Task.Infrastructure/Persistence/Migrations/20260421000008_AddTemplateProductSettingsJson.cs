using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-MIG-02 — adds ProductSettingsJson TEXT column to tasks_Templates.
    /// Stores SYNQ_LIENS-specific template extensions (ContextType, ApplicableWorkflowStageId,
    /// DefaultRoleId) as a JSON blob. NULL for all existing rows and for non-LIENS products.
    /// </summary>
    public partial class AddTemplateProductSettingsJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name:     "ProductSettingsJson",
                table:    "tasks_Templates",
                type:     "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name:  "ProductSettingsJson",
                table: "tasks_Templates");
        }
    }
}
