using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "flow_workflow_transitions",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "flow_workflow_stages",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "flow_task_items",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "flow_notifications",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "flow_definitions",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "flow_automation_hooks",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "flow_automation_execution_logs",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE flow_definitions SET TenantId = 'default' WHERE TenantId = '';");
            migrationBuilder.Sql("UPDATE flow_workflow_stages SET TenantId = 'default' WHERE TenantId = '';");
            migrationBuilder.Sql("UPDATE flow_workflow_transitions SET TenantId = 'default' WHERE TenantId = '';");
            migrationBuilder.Sql("UPDATE flow_automation_hooks SET TenantId = 'default' WHERE TenantId = '';");
            migrationBuilder.Sql("UPDATE flow_automation_execution_logs SET TenantId = 'default' WHERE TenantId = '';");
            migrationBuilder.Sql("UPDATE flow_task_items SET TenantId = 'default' WHERE TenantId = '';");
            migrationBuilder.Sql("UPDATE flow_notifications SET TenantId = 'default' WHERE TenantId = '';");

            migrationBuilder.UpdateData(
                table: "flow_definitions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_stages",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_stages",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_stages",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_stages",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_stages",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000004"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000005"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000006"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000007"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000008"),
                column: "TenantId",
                value: "default");

            migrationBuilder.UpdateData(
                table: "flow_workflow_transitions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000009"),
                column: "TenantId",
                value: "default");

            migrationBuilder.CreateIndex(
                name: "IX_flow_workflow_transitions_TenantId",
                table: "flow_workflow_transitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_workflow_stages_TenantId",
                table: "flow_workflow_stages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_task_items_TenantId",
                table: "flow_task_items",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_notifications_TenantId",
                table: "flow_notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_definitions_TenantId",
                table: "flow_definitions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_automation_hooks_TenantId",
                table: "flow_automation_hooks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_automation_execution_logs_TenantId",
                table: "flow_automation_execution_logs",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_flow_workflow_transitions_TenantId",
                table: "flow_workflow_transitions");

            migrationBuilder.DropIndex(
                name: "IX_flow_workflow_stages_TenantId",
                table: "flow_workflow_stages");

            migrationBuilder.DropIndex(
                name: "IX_flow_task_items_TenantId",
                table: "flow_task_items");

            migrationBuilder.DropIndex(
                name: "IX_flow_notifications_TenantId",
                table: "flow_notifications");

            migrationBuilder.DropIndex(
                name: "IX_flow_definitions_TenantId",
                table: "flow_definitions");

            migrationBuilder.DropIndex(
                name: "IX_flow_automation_hooks_TenantId",
                table: "flow_automation_hooks");

            migrationBuilder.DropIndex(
                name: "IX_flow_automation_execution_logs_TenantId",
                table: "flow_automation_execution_logs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "flow_workflow_transitions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "flow_workflow_stages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "flow_task_items");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "flow_notifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "flow_definitions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "flow_automation_hooks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "flow_automation_execution_logs");
        }
    }
}
