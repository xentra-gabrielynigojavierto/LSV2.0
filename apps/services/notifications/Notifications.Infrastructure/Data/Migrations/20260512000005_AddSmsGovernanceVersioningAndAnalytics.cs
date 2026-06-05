using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSmsGovernanceVersioningAndAnalytics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── ntf_SmsGovernanceRuleVersions ─────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsGovernanceRuleVersions",
            columns: table => new
            {
                Id              = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RuleId          = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RulePackId      = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                VersionNumber   = table.Column<int>(type: "int", nullable: false),
                RuleSnapshotJson = table.Column<string>(type: "mediumtext", nullable: false),
                ChangeType      = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                ChangeReason    = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                CreatedAt       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CreatedBy       = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsGovernanceRuleVersions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "UIX_ntf_SmsGovRuleVersions_Rule_Version",
            table: "ntf_SmsGovernanceRuleVersions",
            columns: ["RuleId", "VersionNumber"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRuleVersions_Pack_CreatedAt",
            table: "ntf_SmsGovernanceRuleVersions",
            columns: ["RulePackId", "CreatedAt"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRuleVersions_CreatedAt",
            table: "ntf_SmsGovernanceRuleVersions",
            column: "CreatedAt");

        // ── ntf_SmsGovernanceRulePackVersions ─────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsGovernanceRulePackVersions",
            columns: table => new
            {
                Id                        = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RulePackId                = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                VersionNumber             = table.Column<int>(type: "int", nullable: false),
                PackSnapshotJson          = table.Column<string>(type: "mediumtext", nullable: false),
                IncludedRulesSnapshotJson = table.Column<string>(type: "longtext", nullable: true),
                ChangeType                = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                ChangeReason              = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                CreatedAt                 = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CreatedBy                 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsGovernanceRulePackVersions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "UIX_ntf_SmsGovPackVersions_Pack_Version",
            table: "ntf_SmsGovernanceRulePackVersions",
            columns: ["RulePackId", "VersionNumber"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovPackVersions_CreatedAt",
            table: "ntf_SmsGovernanceRulePackVersions",
            column: "CreatedAt");

        // ── ntf_SmsGovernanceRuleMatchMetrics ─────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsGovernanceRuleMatchMetrics",
            columns: table => new
            {
                Id              = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RuleId          = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                RulePackId      = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                TenantId        = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                RuleType        = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                Severity        = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                DecisionType    = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                ReasonCode      = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                MatchCount      = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                BlockCount      = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                WarnCount       = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                ReviewCount     = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                AllowCount      = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                SimulationCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                LiveCount       = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                WindowStart     = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                WindowEnd       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                LastMatchedAt   = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreatedAt       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsGovernanceRuleMatchMetrics", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovMatchMetrics_Rule_Tenant_Window",
            table: "ntf_SmsGovernanceRuleMatchMetrics",
            columns: ["RuleId", "TenantId", "WindowStart"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovMatchMetrics_Pack_Window",
            table: "ntf_SmsGovernanceRuleMatchMetrics",
            columns: ["RulePackId", "WindowStart"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovMatchMetrics_Tenant_Window",
            table: "ntf_SmsGovernanceRuleMatchMetrics",
            columns: ["TenantId", "WindowStart"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovMatchMetrics_WindowStart",
            table: "ntf_SmsGovernanceRuleMatchMetrics",
            column: "WindowStart");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ntf_SmsGovernanceRuleVersions");
        migrationBuilder.DropTable(name: "ntf_SmsGovernanceRulePackVersions");
        migrationBuilder.DropTable(name: "ntf_SmsGovernanceRuleMatchMetrics");
    }
}
