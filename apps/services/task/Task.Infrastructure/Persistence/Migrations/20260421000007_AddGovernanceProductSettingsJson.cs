using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-MIG-01 — adds ProductSettingsJson TEXT column to tasks_GovernanceSettings.
    /// This column stores product-specific governance extensions as a JSON blob.
    /// It is initially NULL for all existing rows; SYNQ_LIENS governance migration
    /// writes Liens-specific fields (RequireCaseLinkOnCreate, AllowMultipleAssignees,
    /// DefaultStartStageMode, ExplicitStartStageId) into this column.
    /// </summary>
    public partial class AddGovernanceProductSettingsJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name:      "ProductSettingsJson",
                table:     "tasks_GovernanceSettings",
                type:      "TEXT",
                nullable:  true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name:  "ProductSettingsJson",
                table: "tasks_GovernanceSettings");
        }
    }
}
