using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowTaskSlaE10_3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueAt",
                table: "flow_workflow_tasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSlaEvaluatedAt",
                table: "flow_workflow_tasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaBreachedAt",
                table: "flow_workflow_tasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaPolicyKey",
                table: "flow_workflow_tasks",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SlaStatus",
                table: "flow_workflow_tasks",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "OnTrack")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_flow_workflow_tasks_status_dueat_eval",
                table: "flow_workflow_tasks",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "ix_flow_workflow_tasks_tenant_status_dueat",
                table: "flow_workflow_tasks",
                columns: new[] { "TenantId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "ix_flow_workflow_tasks_tenant_status_slastatus",
                table: "flow_workflow_tasks",
                columns: new[] { "TenantId", "Status", "SlaStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_status_dueat_eval",
                table: "flow_workflow_tasks");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_status_dueat",
                table: "flow_workflow_tasks");

            migrationBuilder.DropIndex(
                name: "ix_flow_workflow_tasks_tenant_status_slastatus",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "LastSlaEvaluatedAt",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "SlaBreachedAt",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "SlaPolicyKey",
                table: "flow_workflow_tasks");

            migrationBuilder.DropColumn(
                name: "SlaStatus",
                table: "flow_workflow_tasks");
        }
    }
}
