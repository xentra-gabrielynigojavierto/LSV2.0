using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductKey",
                table: "flow_task_items",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "FLOW_GENERIC")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProductKey",
                table: "flow_definitions",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "FLOW_GENERIC")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProductKey",
                table: "flow_automation_hooks",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "FLOW_GENERIC")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_flow_task_items_TenantId_ProductKey",
                table: "flow_task_items",
                columns: new[] { "TenantId", "ProductKey" });

            migrationBuilder.CreateIndex(
                name: "IX_flow_definitions_TenantId_ProductKey",
                table: "flow_definitions",
                columns: new[] { "TenantId", "ProductKey" });

            migrationBuilder.CreateIndex(
                name: "IX_flow_automation_hooks_TenantId_ProductKey",
                table: "flow_automation_hooks",
                columns: new[] { "TenantId", "ProductKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_flow_task_items_TenantId_ProductKey",
                table: "flow_task_items");

            migrationBuilder.DropIndex(
                name: "IX_flow_definitions_TenantId_ProductKey",
                table: "flow_definitions");

            migrationBuilder.DropIndex(
                name: "IX_flow_automation_hooks_TenantId_ProductKey",
                table: "flow_automation_hooks");

            migrationBuilder.DropColumn(
                name: "ProductKey",
                table: "flow_task_items");

            migrationBuilder.DropColumn(
                name: "ProductKey",
                table: "flow_definitions");

            migrationBuilder.DropColumn(
                name: "ProductKey",
                table: "flow_automation_hooks");
        }
    }
}
