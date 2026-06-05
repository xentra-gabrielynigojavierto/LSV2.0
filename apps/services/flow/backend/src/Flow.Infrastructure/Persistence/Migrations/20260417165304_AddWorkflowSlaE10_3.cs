using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowSlaE10_3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "flow_workflow_instances",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EscalationLevel",
                table: "flow_workflow_instances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSlaEvaluatedAt",
                table: "flow_workflow_instances",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueSince",
                table: "flow_workflow_instances",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaStatus",
                table: "flow_workflow_instances",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "OnTrack")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "DefaultSlaMinutes",
                table: "flow_definitions",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "flow_definitions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "DefaultSlaMinutes",
                value: null);

            migrationBuilder.CreateIndex(
                name: "ix_flow_workflow_instances_slastatus",
                table: "flow_workflow_instances",
                column: "SlaStatus");

            migrationBuilder.CreateIndex(
                name: "ix_flow_workflow_instances_status_dueat",
                table: "flow_workflow_instances",
                columns: new[] { "Status", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_instances_slastatus",
                table: "flow_workflow_instances");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_instances_status_dueat",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "EscalationLevel",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "LastSlaEvaluatedAt",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "OverdueSince",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "SlaStatus",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "DefaultSlaMinutes",
                table: "flow_definitions");
        }
    }
}
