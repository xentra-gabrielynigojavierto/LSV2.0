using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Task.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-MIG-04 — Creates tasks_StageTransitions table.
    /// Minimal task-board allowed-moves model: from_stage → to_stage per (tenant, product).
    /// No conditions, no rules engine, no branching logic.
    /// </summary>
    public partial class AddStageTransitions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tasks_StageTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(
                        type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceProductCode = table.Column<string>(
                        type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromStageId = table.Column<Guid>(
                        type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ToStageId = table.Column<Guid>(
                        type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IsActive = table.Column<bool>(
                        type: "tinyint(1)", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(
                        type: "int", nullable: false, defaultValue: 0),
                    CreatedByUserId = table.Column<Guid>(
                        type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(
                        type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(
                        type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(
                        type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks_StageTransitions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name:    "IX_StageTransitions_TenantId_Product",
                table:   "tasks_StageTransitions",
                columns: new[] { "TenantId", "SourceProductCode" });

            migrationBuilder.CreateIndex(
                name:    "IX_StageTransitions_FromStage",
                table:   "tasks_StageTransitions",
                columns: new[] { "TenantId", "SourceProductCode", "FromStageId" });

            migrationBuilder.CreateIndex(
                name:    "UX_StageTransitions_Unique",
                table:   "tasks_StageTransitions",
                columns: new[] { "TenantId", "SourceProductCode", "FromStageId", "ToStageId" },
                unique:  true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "tasks_StageTransitions");
        }
    }
}
