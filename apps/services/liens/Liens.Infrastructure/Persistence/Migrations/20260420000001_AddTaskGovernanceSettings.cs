using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskGovernanceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "liens_TaskGovernanceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    RequireAssigneeOnCreate = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    RequireCaseLinkOnCreate = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    AllowMultipleAssignees = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    RequireWorkflowStageOnCreate = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    DefaultStartStageMode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "FIRST_ACTIVE_STAGE"),
                    ExplicitStartStageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LastUpdatedByName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    LastUpdatedSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "TENANT_PRODUCT_SETTINGS"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "liens_TaskGovernanceSettings");
        }
    }
}
