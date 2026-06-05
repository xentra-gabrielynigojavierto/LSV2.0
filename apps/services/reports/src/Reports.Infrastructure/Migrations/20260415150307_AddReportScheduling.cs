using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reports.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rpt_ReportSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReportTemplateId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProductCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrganizationType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FrequencyType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FrequencyConfigJson = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Timezone = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NextRunAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    LastRunAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    UseOverride = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ExportFormat = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeliveryMethod = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeliveryConfigJson = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParametersJson = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequiredFeatureCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
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
                    table.PrimaryKey("PK_rpt_ReportSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rpt_ReportSchedules_rpt_ReportDefinitions_ReportTemplateId",
                        column: x => x.ReportTemplateId,
                        principalTable: "rpt_ReportDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "rpt_ReportScheduleRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReportScheduleId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ExecutionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ExportId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScheduledForUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    FailureReason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeliveryResultJson = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedFileName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedFileSize = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rpt_ReportScheduleRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rpt_ReportScheduleRuns_rpt_ReportSchedules_ReportScheduleId",
                        column: x => x.ReportScheduleId,
                        principalTable: "rpt_ReportSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportScheduleRuns_ReportScheduleId",
                table: "rpt_ReportScheduleRuns",
                column: "ReportScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportScheduleRuns_ReportScheduleId_CreatedAtUtc",
                table: "rpt_ReportScheduleRuns",
                columns: new[] { "ReportScheduleId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportScheduleRuns_Status",
                table: "rpt_ReportScheduleRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportSchedules_IsActive",
                table: "rpt_ReportSchedules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportSchedules_IsActive_NextRunAtUtc",
                table: "rpt_ReportSchedules",
                columns: new[] { "IsActive", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportSchedules_NextRunAtUtc",
                table: "rpt_ReportSchedules",
                column: "NextRunAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportSchedules_ReportTemplateId",
                table: "rpt_ReportSchedules",
                column: "ReportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportSchedules_TenantId",
                table: "rpt_ReportSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_rpt_ReportSchedules_TenantId_IsActive",
                table: "rpt_ReportSchedules",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rpt_ReportScheduleRuns");

            migrationBuilder.DropTable(
                name: "rpt_ReportSchedules");
        }
    }
}
