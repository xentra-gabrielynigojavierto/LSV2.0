using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowInstanceExecutionStateP5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedToUserId",
                table: "flow_workflow_instances",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentStageId",
                table: "flow_workflow_instances",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepKey",
                table: "flow_workflow_instances",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LastErrorMessage",
                table: "flow_workflow_instances",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "flow_workflow_instances",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_flow_workflow_instances_AssignedToUserId",
                table: "flow_workflow_instances",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_workflow_instances_CurrentStageId",
                table: "flow_workflow_instances",
                column: "CurrentStageId");

            migrationBuilder.AddForeignKey(
                name: "FK_flow_workflow_instances_flow_workflow_stages_CurrentStageId",
                table: "flow_workflow_instances",
                column: "CurrentStageId",
                principalTable: "flow_workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_flow_workflow_instances_flow_workflow_stages_CurrentStageId",
                table: "flow_workflow_instances");

            migrationBuilder.DropIndex(
                name: "IX_flow_workflow_instances_AssignedToUserId",
                table: "flow_workflow_instances");

            migrationBuilder.DropIndex(
                name: "IX_flow_workflow_instances_CurrentStageId",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "CurrentStageId",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "CurrentStepKey",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "LastErrorMessage",
                table: "flow_workflow_instances");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "flow_workflow_instances");
        }
    }
}
