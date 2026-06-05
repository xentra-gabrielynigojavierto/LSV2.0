using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSmsTemplateGovernance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── ntf_SmsTemplates ─────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsTemplates",
            columns: table => new
            {
                Id                    = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TenantId              = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                TemplateKey           = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                Name                  = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                Description           = table.Column<string>(type: "text", nullable: true),
                Category              = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                Status                = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "draft"),
                CurrentVersion        = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                LatestApprovedVersion = table.Column<int>(type: "int", nullable: true),
                ContentClassification = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "transactional"),
                RequiresApproval      = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                Enabled               = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                CreatedAt             = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt             = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CreatedBy             = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                UpdatedBy             = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsTemplates", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsTemplates_Tenant_Key",
            table: "ntf_SmsTemplates",
            columns: ["TenantId", "TemplateKey"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsTemplates_Status_Enabled",
            table: "ntf_SmsTemplates",
            columns: ["Status", "Enabled"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsTemplates_Classification",
            table: "ntf_SmsTemplates",
            column: "ContentClassification");

        // ── ntf_SmsTemplateVersions ──────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsTemplateVersions",
            columns: table => new
            {
                Id                    = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TemplateId            = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                VersionNumber         = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                TemplateBody          = table.Column<string>(type: "text", nullable: false),
                VariableSchemaJson    = table.Column<string>(type: "text", nullable: true),
                ContentClassification = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, defaultValue: "transactional"),
                ApprovalStatus        = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "draft"),
                ApprovedBy            = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                ApprovedAt            = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                RejectionReason       = table.Column<string>(type: "text", nullable: true),
                CreatedAt             = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CreatedBy             = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsTemplateVersions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsTemplateVersions_Template_Version",
            table: "ntf_SmsTemplateVersions",
            columns: ["TemplateId", "VersionNumber"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsTemplateVersions_ApprovalStatus",
            table: "ntf_SmsTemplateVersions",
            column: "ApprovalStatus");

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsTemplateVersions_ApprovedAt",
            table: "ntf_SmsTemplateVersions",
            column: "ApprovedAt");

        // ── ntf_SmsTemplateGovernanceDecisions ───────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsTemplateGovernanceDecisions",
            columns: table => new
            {
                Id                       = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                NotificationId           = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                AttemptId                = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                TemplateId               = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                TemplateVersionId        = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                TenantId                 = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                DecisionType             = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "allow"),
                ReasonCode               = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                ContentClassification    = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                VariableValidationPassed = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                DecisionMetadataJson     = table.Column<string>(type: "text", nullable: true),
                CreatedAt                = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsTemplateGovernanceDecisions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsTemplateGovDecisions_Tenant_Dt",
            table: "ntf_SmsTemplateGovernanceDecisions",
            columns: ["TenantId", "CreatedAt"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsTemplateGovDecisions_DecisionType_Dt",
            table: "ntf_SmsTemplateGovernanceDecisions",
            columns: ["DecisionType", "CreatedAt"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsTemplateGovDecisions_ReasonCode_Dt",
            table: "ntf_SmsTemplateGovernanceDecisions",
            columns: ["ReasonCode", "CreatedAt"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsTemplateGovDecisions_TemplateId",
            table: "ntf_SmsTemplateGovernanceDecisions",
            column: "TemplateId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ntf_SmsTemplateGovernanceDecisions");
        migrationBuilder.DropTable(name: "ntf_SmsTemplateVersions");
        migrationBuilder.DropTable(name: "ntf_SmsTemplates");
    }
}
