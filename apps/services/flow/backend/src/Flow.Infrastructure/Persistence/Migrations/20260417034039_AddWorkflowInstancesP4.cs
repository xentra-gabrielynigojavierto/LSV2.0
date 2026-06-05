using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowInstancesP4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "flow_product_workflow_mappings",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "flow_workflow_instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WorkflowDefinitionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, defaultValue: "FLOW_GENERIC")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InitialTaskId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Active")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TenantId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flow_workflow_instances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_flow_workflow_instances_flow_definitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "flow_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_flow_workflow_instances_flow_task_items_InitialTaskId",
                        column: x => x.InitialTaskId,
                        principalTable: "flow_task_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_flow_product_workflow_mappings_WorkflowInstanceId",
                table: "flow_product_workflow_mappings",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_workflow_instances_InitialTaskId",
                table: "flow_workflow_instances",
                column: "InitialTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_workflow_instances_Status",
                table: "flow_workflow_instances",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_flow_workflow_instances_TenantId_ProductKey",
                table: "flow_workflow_instances",
                columns: new[] { "TenantId", "ProductKey" });

            migrationBuilder.CreateIndex(
                name: "IX_flow_workflow_instances_WorkflowDefinitionId",
                table: "flow_workflow_instances",
                column: "WorkflowDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_flow_product_workflow_mappings_flow_workflow_instances_Workf~",
                table: "flow_product_workflow_mappings",
                column: "WorkflowInstanceId",
                principalTable: "flow_workflow_instances",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_flow_product_workflow_mappings_flow_workflow_instances_Workf~",
                table: "flow_product_workflow_mappings");

            migrationBuilder.DropTable(
                name: "flow_workflow_instances");

            migrationBuilder.DropIndex(
                name: "IX_flow_product_workflow_mappings_WorkflowInstanceId",
                table: "flow_product_workflow_mappings");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "flow_product_workflow_mappings");
        }
    }
}
