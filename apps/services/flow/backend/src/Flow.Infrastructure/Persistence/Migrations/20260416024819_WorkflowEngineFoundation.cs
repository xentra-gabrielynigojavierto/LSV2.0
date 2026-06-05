using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowEngineFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStageId",
                table: "task_items",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "workflow_stages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WorkflowDefinitionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MappedStatus = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsInitial = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsTerminal = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_stages_flow_definitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "flow_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "workflow_transitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WorkflowDefinitionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FromStageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ToStageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_transitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_transitions_flow_definitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "flow_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workflow_transitions_workflow_stages_FromStageId",
                        column: x => x.FromStageId,
                        principalTable: "workflow_stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workflow_transitions_workflow_stages_ToStageId",
                        column: x => x.ToStageId,
                        principalTable: "workflow_stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "flow_definitions",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "Description", "Name", "Status", "UpdatedAt", "UpdatedBy", "Version" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "system", "Default workflow with standard task lifecycle stages and transitions.", "Standard Task Flow", "Active", null, null, "1.0" });

            migrationBuilder.InsertData(
                table: "workflow_stages",
                columns: new[] { "Id", "IsInitial", "IsTerminal", "Key", "MappedStatus", "Name", "Order", "WorkflowDefinitionId" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), true, false, "open", "Open", "Open", 1, new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000002"), false, false, "in-progress", "InProgress", "In Progress", 2, new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000003"), false, false, "blocked", "Blocked", "Blocked", 3, new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000004"), false, true, "done", "Done", "Done", 4, new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000005"), false, true, "cancelled", "Cancelled", "Cancelled", 5, new Guid("10000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.InsertData(
                table: "workflow_transitions",
                columns: new[] { "Id", "FromStageId", "IsActive", "Name", "ToStageId", "WorkflowDefinitionId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000001"), true, "Start Work", new Guid("20000000-0000-0000-0000-000000000002"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000002"), new Guid("20000000-0000-0000-0000-000000000001"), true, "Cancel", new Guid("20000000-0000-0000-0000-000000000005"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000003"), new Guid("20000000-0000-0000-0000-000000000002"), true, "Block", new Guid("20000000-0000-0000-0000-000000000003"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000004"), new Guid("20000000-0000-0000-0000-000000000002"), true, "Complete", new Guid("20000000-0000-0000-0000-000000000004"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000005"), new Guid("20000000-0000-0000-0000-000000000002"), true, "Cancel", new Guid("20000000-0000-0000-0000-000000000005"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000006"), new Guid("20000000-0000-0000-0000-000000000003"), true, "Unblock", new Guid("20000000-0000-0000-0000-000000000002"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000007"), new Guid("20000000-0000-0000-0000-000000000003"), true, "Cancel", new Guid("20000000-0000-0000-0000-000000000005"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000008"), new Guid("20000000-0000-0000-0000-000000000004"), true, "Reopen", new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0000-000000000009"), new Guid("20000000-0000-0000-0000-000000000005"), true, "Reopen", new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_items_WorkflowStageId",
                table: "task_items",
                column: "WorkflowStageId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_stages_WorkflowDefinitionId_Key",
                table: "workflow_stages",
                columns: new[] { "WorkflowDefinitionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_transitions_FromStageId",
                table: "workflow_transitions",
                column: "FromStageId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_transitions_ToStageId",
                table: "workflow_transitions",
                column: "ToStageId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_transitions_WorkflowDefinitionId_FromStageId_ToStag~",
                table: "workflow_transitions",
                columns: new[] { "WorkflowDefinitionId", "FromStageId", "ToStageId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_task_items_flow_definitions_FlowDefinitionId",
                table: "task_items",
                column: "FlowDefinitionId",
                principalTable: "flow_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_task_items_workflow_stages_WorkflowStageId",
                table: "task_items",
                column: "WorkflowStageId",
                principalTable: "workflow_stages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_items_flow_definitions_FlowDefinitionId",
                table: "task_items");

            migrationBuilder.DropForeignKey(
                name: "FK_task_items_workflow_stages_WorkflowStageId",
                table: "task_items");

            migrationBuilder.DropTable(
                name: "workflow_transitions");

            migrationBuilder.DropTable(
                name: "workflow_stages");

            migrationBuilder.DropIndex(
                name: "IX_task_items_WorkflowStageId",
                table: "task_items");

            migrationBuilder.DeleteData(
                table: "flow_definitions",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.DropColumn(
                name: "WorkflowStageId",
                table: "task_items");
        }
    }
}
