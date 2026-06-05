using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-B04-01 — adds GenerationRuleId and GeneratingTemplateId columns to tasks_Tasks
    /// so the canonical Task service can store Liens generation-engine provenance and support
    /// duplicate-prevention queries from LienTaskGenerationEngine without querying liens_Tasks.
    /// </summary>
    public partial class GenerationMetadataColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GenerationRuleId",
                table: "tasks_Tasks",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "GeneratingTemplateId",
                table: "tasks_Tasks",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            // Composite indexes to support duplicate-prevention queries:
            //   GET /api/tasks?generationRuleId=&sourceProductCode=SYNQ_LIENS&excludeTerminal=true
            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_Product_GenerationRule",
                table: "tasks_Tasks",
                columns: new[] { "TenantId", "SourceProductCode", "GenerationRuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TenantId_Product_GeneratingTemplate",
                table: "tasks_Tasks",
                columns: new[] { "TenantId", "SourceProductCode", "GeneratingTemplateId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_TenantId_Product_GenerationRule",
                table: "tasks_Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_TenantId_Product_GeneratingTemplate",
                table: "tasks_Tasks");

            migrationBuilder.DropColumn(
                name: "GenerationRuleId",
                table: "tasks_Tasks");

            migrationBuilder.DropColumn(
                name: "GeneratingTemplateId",
                table: "tasks_Tasks");
        }
    }
}
