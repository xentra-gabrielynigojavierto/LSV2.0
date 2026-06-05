using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Flow linkage columns on tasks_Tasks ---
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "tasks_Tasks",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "WorkflowStepKey",
                table: "tasks_Tasks",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "WorkflowLinkageChangedAt",
                table: "tasks_Tasks",
                type: "datetime(6)",
                nullable: true);

            // --- New indexes on tasks_Tasks ---
            migrationBuilder.CreateIndex(
                name: "IX_Tasks_WorkflowInstanceId",
                table: "tasks_Tasks",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_SourceEntity",
                table: "tasks_Tasks",
                columns: new[] { "TenantId", "SourceEntityType", "SourceEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_AssignedUser_Status",
                table: "tasks_Tasks",
                columns: new[] { "TenantId", "AssignedUserId", "Status" });

            // --- tasks_LinkedEntities table ---
            migrationBuilder.CreateTable(
                name: "tasks_LinkedEntities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TaskId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntityId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelationshipType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks_LinkedEntities", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LinkedEntities_TaskId",
                table: "tasks_LinkedEntities",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkedEntities_EntityRef",
                table: "tasks_LinkedEntities",
                columns: new[] { "TenantId", "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tasks_LinkedEntities");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_WorkflowInstanceId",
                table: "tasks_Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_SourceEntity",
                table: "tasks_Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_TenantId_AssignedUser_Status",
                table: "tasks_Tasks");

            migrationBuilder.DropColumn(name: "WorkflowInstanceId",       table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "WorkflowStepKey",          table: "tasks_Tasks");
            migrationBuilder.DropColumn(name: "WorkflowLinkageChangedAt", table: "tasks_Tasks");
        }
    }
}
