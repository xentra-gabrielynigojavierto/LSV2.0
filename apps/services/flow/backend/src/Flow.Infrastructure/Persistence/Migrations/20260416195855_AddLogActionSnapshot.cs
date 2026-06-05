using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLogActionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActionOrder",
                table: "flow_automation_execution_logs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ActionType",
                table: "flow_automation_execution_logs",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Backfill snapshot columns on existing log rows:
            //  1) if the log has an ActionId, use that action's ActionType/Order
            //  2) otherwise fall back to the parent hook's legacy ActionType
            migrationBuilder.Sql(@"
                UPDATE flow_automation_execution_logs l
                LEFT JOIN flow_automation_actions a ON a.Id = l.ActionId
                LEFT JOIN flow_automation_hooks h ON h.Id = l.WorkflowAutomationHookId
                SET l.ActionType = COALESCE(a.ActionType, h.ActionType, ''),
                    l.ActionOrder = COALESCE(a.`Order`, 0)
                WHERE l.ActionType = '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionOrder",
                table: "flow_automation_execution_logs");

            migrationBuilder.DropColumn(
                name: "ActionType",
                table: "flow_automation_execution_logs");
        }
    }
}
