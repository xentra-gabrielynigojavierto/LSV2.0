using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsGovernanceRollout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ntf_SmsGovernanceRolloutPlans ─────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ntf_SmsGovernanceRolloutPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "char(36)", nullable: false),
                    ReleasePackageId = table.Column<string>(type: "char(36)", nullable: false),
                    TenantId = table.Column<string>(type: "char(36)", nullable: true),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    RolloutState = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    RolloutStrategy = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    CurrentStageNumber = table.Column<int>(type: "int", nullable: true),
                    RollbackThresholdJson = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PausedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResumedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RolledBackAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FailureReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsGovernanceRolloutPlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceRolloutPlans_ReleasePackageId",
                table: "ntf_SmsGovernanceRolloutPlans",
                column: "ReleasePackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceRolloutPlans_Tenant_State_Dt",
                table: "ntf_SmsGovernanceRolloutPlans",
                columns: new[] { "TenantId", "RolloutState", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceRolloutPlans_State_Dt",
                table: "ntf_SmsGovernanceRolloutPlans",
                columns: new[] { "RolloutState", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceRolloutPlans_Strategy_Dt",
                table: "ntf_SmsGovernanceRolloutPlans",
                columns: new[] { "RolloutStrategy", "CreatedAt" });

            // ── ntf_SmsGovernanceRolloutStages ────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ntf_SmsGovernanceRolloutStages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "char(36)", nullable: false),
                    RolloutPlanId = table.Column<string>(type: "char(36)", nullable: false),
                    StageNumber = table.Column<int>(type: "int", nullable: false),
                    StageName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    StageState = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    TenantPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FailureReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsGovernanceRolloutStages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceRolloutStages_PlanId_StageNum",
                table: "ntf_SmsGovernanceRolloutStages",
                columns: new[] { "RolloutPlanId", "StageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceRolloutStages_PlanId_State",
                table: "ntf_SmsGovernanceRolloutStages",
                columns: new[] { "RolloutPlanId", "StageState" });

            // ── ntf_SmsGovernanceTenantCohorts ────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ntf_SmsGovernanceTenantCohorts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "char(36)", nullable: false),
                    RolloutPlanId = table.Column<string>(type: "char(36)", nullable: false),
                    StageId = table.Column<string>(type: "char(36)", nullable: true),
                    TenantId = table.Column<string>(type: "char(36)", nullable: false),
                    CohortName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RolledBackAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsGovernanceTenantCohorts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceTenantCohorts_PlanId_TenantId",
                table: "ntf_SmsGovernanceTenantCohorts",
                columns: new[] { "RolloutPlanId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceTenantCohorts_StageId_TenantId",
                table: "ntf_SmsGovernanceTenantCohorts",
                columns: new[] { "StageId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceTenantCohorts_CohortName",
                table: "ntf_SmsGovernanceTenantCohorts",
                column: "CohortName");

            // ── ntf_SmsGovernanceRolloutAuditEvents ───────────────────────────
            migrationBuilder.CreateTable(
                name: "ntf_SmsGovernanceRolloutAuditEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "char(36)", nullable: false),
                    RolloutPlanId = table.Column<string>(type: "char(36)", nullable: false),
                    StageId = table.Column<string>(type: "char(36)", nullable: true),
                    TenantId = table.Column<string>(type: "char(36)", nullable: true),
                    EventType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    PreviousState = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    NewState = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Actor = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsGovernanceRolloutAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceRolloutAuditEvents_PlanId_Dt",
                table: "ntf_SmsGovernanceRolloutAuditEvents",
                columns: new[] { "RolloutPlanId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceRolloutAuditEvents_EventType_Dt",
                table: "ntf_SmsGovernanceRolloutAuditEvents",
                columns: new[] { "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovernanceRolloutAuditEvents_StageId_Dt",
                table: "ntf_SmsGovernanceRolloutAuditEvents",
                columns: new[] { "StageId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ntf_SmsGovernanceRolloutPlans");
            migrationBuilder.DropTable(name: "ntf_SmsGovernanceRolloutStages");
            migrationBuilder.DropTable(name: "ntf_SmsGovernanceTenantCohorts");
            migrationBuilder.DropTable(name: "ntf_SmsGovernanceRolloutAuditEvents");
        }
    }
}
