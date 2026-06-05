using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-MIG-09 — Final cleanup: drops Liens config tables that are no longer
    /// authoritative. The Task service is the sole owner of templates and governance.
    ///
    /// Tables dropped:
    ///   liens_TaskTemplates        — config authority transferred to Task service (MIG-07)
    ///   liens_TaskGovernanceSettings — config authority transferred to Task service (MIG-08)
    ///
    /// Prerequisites satisfied:
    ///   MIG-07: all template writes → Task service; LiensTemplateSyncService disabled.
    ///   MIG-08: all governance writes → Task service; LiensGovernanceSyncService disabled.
    ///   MIG-09: all fallback reads and mirror writes removed from service layer.
    ///           No code references either table at runtime.
    ///
    /// Tables intentionally NOT dropped in this migration:
    ///   liens_WorkflowConfigs, liens_WorkflowStages, liens_WorkflowTransitions
    ///   liens_TaskGenerationRules, liens_GeneratedTaskMetadata
    ///   (stage/transition ownership not yet flipped; generation rules are separate domain)
    ///
    /// Rollback: Down() recreates both tables. Data is repopulated from Task service
    /// via mirror writes or sync on the next applicable write operation.
    /// </summary>
    public partial class DropLiensConfigTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop indexes before table (EF convention; MySQL drops them with the table, but being explicit)
            migrationBuilder.DropIndex(
                name:  "IX_TaskTemplates_TenantId_ContextType",
                table: "liens_TaskTemplates");

            migrationBuilder.DropIndex(
                name:  "IX_TaskTemplates_TenantId_IsActive",
                table: "liens_TaskTemplates");

            migrationBuilder.DropTable(name: "liens_TaskTemplates");

            // liens_TaskGovernanceSettings has a UNIQUE constraint (not a named index in EF)
            // but MySQL will drop it automatically with the table.
            migrationBuilder.DropTable(name: "liens_TaskGovernanceSettings");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate liens_TaskTemplates
            migrationBuilder.CreateTable(
                name: "liens_TaskTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultTitle = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultDescription = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultPriority = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultDueOffsetDays = table.Column<int>(type: "int", nullable: true),
                    DefaultRoleId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContextType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApplicableWorkflowStageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LastUpdatedByName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastUpdatedSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_TaskTemplates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_TenantId_ContextType",
                table: "liens_TaskTemplates",
                columns: new[] { "TenantId", "ContextType" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_TenantId_IsActive",
                table: "liens_TaskTemplates",
                columns: new[] { "TenantId", "IsActive" });

            // Recreate liens_TaskGovernanceSettings
            migrationBuilder.CreateTable(
                name: "liens_TaskGovernanceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequireAssigneeOnCreate = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    RequireCaseLinkOnCreate = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    AllowMultipleAssignees = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    RequireWorkflowStageOnCreate = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    DefaultStartStageMode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExplicitStartStageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LastUpdatedByName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastUpdatedSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_TaskGovernanceSettings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "UX_TaskGovernance_TenantId_ProductCode",
                table: "liens_TaskGovernanceSettings",
                columns: new[] { "TenantId", "ProductCode" },
                unique: true);
        }
    }
}
