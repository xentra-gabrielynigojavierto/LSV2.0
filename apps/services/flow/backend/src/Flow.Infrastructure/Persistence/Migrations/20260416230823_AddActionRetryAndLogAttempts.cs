using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActionRetryAndLogAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default 1: legacy log rows (pre-019-C) all represent exactly one
            // execution attempt — that matches pre-019-C semantics (single attempt,
            // success or failure). New rows will have Attempts populated by the
            // executor; the column-level default is still 1 as a safety net.
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "flow_automation_execution_logs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Backfill any rows that existed before this migration to Attempts=1.
            // (Defensive: column AddColumn with defaultValue:1 already populates
            // existing rows, but this guarantees the contract even if a previous
            // partial application of this migration left rows at 0.)
            migrationBuilder.Sql(
                "UPDATE flow_automation_execution_logs SET Attempts = 1 WHERE Attempts = 0;");

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "flow_automation_actions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RetryDelaySeconds",
                table: "flow_automation_actions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StopOnFailure",
                table: "flow_automation_actions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "flow_automation_execution_logs");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "flow_automation_actions");

            migrationBuilder.DropColumn(
                name: "RetryDelaySeconds",
                table: "flow_automation_actions");

            migrationBuilder.DropColumn(
                name: "StopOnFailure",
                table: "flow_automation_actions");
        }
    }
}
