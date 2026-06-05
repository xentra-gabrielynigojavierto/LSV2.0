using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reports.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueOverrideConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rpt_TenantReportOverrides_TenantId_ReportTemplateId",
                table: "rpt_TenantReportOverrides");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_TenantReportOverrides_TenantId_ReportTemplateId",
                table: "rpt_TenantReportOverrides",
                columns: new[] { "TenantId", "ReportTemplateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rpt_TenantReportOverrides_TenantId_ReportTemplateId",
                table: "rpt_TenantReportOverrides");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_TenantReportOverrides_TenantId_ReportTemplateId",
                table: "rpt_TenantReportOverrides",
                columns: new[] { "TenantId", "ReportTemplateId" });
        }
    }
}
