using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskEngineRefinement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AssignedTo",
                table: "task_items",
                newName: "AssignedToUserId");

            migrationBuilder.AddColumn<string>(
                name: "AssignedToOrgId",
                table: "task_items",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AssignedToRoleKey",
                table: "task_items",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_AssignedToOrgId",
                table: "task_items",
                column: "AssignedToOrgId");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_AssignedToRoleKey",
                table: "task_items",
                column: "AssignedToRoleKey");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_AssignedToUserId",
                table: "task_items",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_context_type_context_id",
                table: "task_items",
                columns: new[] { "context_type", "context_id" });

            migrationBuilder.CreateIndex(
                name: "IX_task_items_FlowDefinitionId",
                table: "task_items",
                column: "FlowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_Status",
                table: "task_items",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_items_AssignedToOrgId",
                table: "task_items");

            migrationBuilder.DropIndex(
                name: "IX_task_items_AssignedToRoleKey",
                table: "task_items");

            migrationBuilder.DropIndex(
                name: "IX_task_items_AssignedToUserId",
                table: "task_items");

            migrationBuilder.DropIndex(
                name: "IX_task_items_context_type_context_id",
                table: "task_items");

            migrationBuilder.DropIndex(
                name: "IX_task_items_FlowDefinitionId",
                table: "task_items");

            migrationBuilder.DropIndex(
                name: "IX_task_items_Status",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "AssignedToOrgId",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "AssignedToRoleKey",
                table: "task_items");

            migrationBuilder.RenameColumn(
                name: "AssignedToUserId",
                table: "task_items",
                newName: "AssignedTo");
        }
    }
}
