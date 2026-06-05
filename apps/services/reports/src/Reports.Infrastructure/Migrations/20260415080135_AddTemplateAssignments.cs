using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reports.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rpt_ReportTemplateAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReportTemplateId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AssignmentScope = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrganizationType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiredFeatureCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MinimumTierCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rpt_ReportTemplateAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rpt_ReportTemplateAssignments_rpt_ReportDefinitions_ReportTe~",
                        column: x => x.ReportTemplateId,
                        principalTable: "rpt_ReportDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "rpt_ReportTemplateAssignmentTenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReportTemplateAssignmentId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rpt_ReportTemplateAssignmentTenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rpt_ReportTemplateAssignmentTenants_rpt_ReportTemplateAssign~",
                        column: x => x.ReportTemplateAssignmentId,
                        principalTable: "rpt_ReportTemplateAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportTemplateAssignments_IsActive",
                table: "rpt_ReportTemplateAssignments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportTemplateAssignments_ProductCode_OrganizationType",
                table: "rpt_ReportTemplateAssignments",
                columns: new[] { "ProductCode", "OrganizationType" });

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportTemplateAssignments_ReportTemplateId",
                table: "rpt_ReportTemplateAssignments",
                column: "ReportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportTemplateAssignments_ReportTemplateId_AssignmentSco~",
                table: "rpt_ReportTemplateAssignments",
                columns: new[] { "ReportTemplateId", "AssignmentScope", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportTemplateAssignmentTenants_ReportTemplateAssignmen~1",
                table: "rpt_ReportTemplateAssignmentTenants",
                columns: new[] { "ReportTemplateAssignmentId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportTemplateAssignmentTenants_ReportTemplateAssignment~",
                table: "rpt_ReportTemplateAssignmentTenants",
                column: "ReportTemplateAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportTemplateAssignmentTenants_TenantId",
                table: "rpt_ReportTemplateAssignmentTenants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportTemplateAssignmentTenants_TenantId_IsActive",
                table: "rpt_ReportTemplateAssignmentTenants",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rpt_ReportTemplateAssignmentTenants");

            migrationBuilder.DropTable(
                name: "rpt_ReportTemplateAssignments");
        }
    }
}
