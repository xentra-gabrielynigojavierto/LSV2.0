using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActionId",
                table: "flow_automation_execution_logs",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "flow_automation_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    HookId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ActionType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfigJson = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flow_automation_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_flow_automation_actions_flow_automation_hooks_HookId",
                        column: x => x.HookId,
                        principalTable: "flow_automation_hooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_flow_automation_execution_logs_ActionId",
                table: "flow_automation_execution_logs",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_automation_actions_HookId_Order",
                table: "flow_automation_actions",
                columns: new[] { "HookId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_flow_automation_actions_TenantId",
                table: "flow_automation_actions",
                column: "TenantId");

            // Data migration: seed one AutomationAction per existing hook from the
            // legacy ActionType/ConfigJson columns so the new Actions table becomes
            // the canonical source for the executor going forward.
            migrationBuilder.Sql(@"
                INSERT INTO flow_automation_actions (Id, HookId, ActionType, ConfigJson, `Order`, TenantId)
                SELECT UUID(), h.Id, h.ActionType, h.ConfigJson, 0, h.TenantId
                FROM flow_automation_hooks h
                WHERE h.ActionType IS NOT NULL AND h.ActionType <> ''
                  AND NOT EXISTS (
                    SELECT 1 FROM flow_automation_actions a WHERE a.HookId = h.Id
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flow_automation_actions");

            migrationBuilder.DropIndex(
                name: "IX_flow_automation_execution_logs_ActionId",
                table: "flow_automation_execution_logs");

            migrationBuilder.DropColumn(
                name: "ActionId",
                table: "flow_automation_execution_logs");
        }
    }
}
