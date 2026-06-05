using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductWorkflowMappingsP3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "flow_product_workflow_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceEntityType = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceEntityId = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WorkflowDefinitionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WorkflowInstanceTaskId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CorrelationKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, defaultValue: "Active")
                        .Annotation("MySql:CharSet", "utf8mb4"),
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
                    table.PrimaryKey("PK_flow_product_workflow_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_flow_product_workflow_mappings_flow_definitions_WorkflowDefi~",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "flow_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_flow_product_workflow_mappings_flow_task_items_WorkflowInsta~",
                        column: x => x.WorkflowInstanceTaskId,
                        principalTable: "flow_task_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_flow_product_workflow_mappings_TenantId_ProductKey",
                table: "flow_product_workflow_mappings",
                columns: new[] { "TenantId", "ProductKey" });

            migrationBuilder.CreateIndex(
                name: "IX_flow_product_workflow_mappings_WorkflowDefinitionId",
                table: "flow_product_workflow_mappings",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_flow_product_workflow_mappings_WorkflowInstanceTaskId",
                table: "flow_product_workflow_mappings",
                column: "WorkflowInstanceTaskId");

            migrationBuilder.CreateIndex(
                name: "ix_pwm_product_entity",
                table: "flow_product_workflow_mappings",
                columns: new[] { "TenantId", "ProductKey", "SourceEntityType", "SourceEntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flow_product_workflow_mappings");
        }
    }
}
