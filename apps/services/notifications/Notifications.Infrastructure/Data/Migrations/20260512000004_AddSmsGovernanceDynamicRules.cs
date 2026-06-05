using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSmsGovernanceDynamicRules : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── ntf_SmsGovernanceRulePacks ────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsGovernanceRulePacks",
            columns: table => new
            {
                Id              = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TenantId        = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Name            = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                Description     = table.Column<string>(type: "text", nullable: true),
                Version         = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                Status          = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                Enabled         = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                InheritanceMode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "merge"),
                Priority        = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                EffectiveFrom   = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                EffectiveTo     = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreatedAt       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt       = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CreatedBy       = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                UpdatedBy       = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsGovernanceRulePacks", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRulePacks_Tenant_Enabled_Priority",
            table: "ntf_SmsGovernanceRulePacks",
            columns: ["TenantId", "Enabled", "Priority"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRulePacks_Status_Enabled",
            table: "ntf_SmsGovernanceRulePacks",
            columns: ["Status", "Enabled"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRulePacks_Effective",
            table: "ntf_SmsGovernanceRulePacks",
            columns: ["EffectiveFrom", "EffectiveTo"]);

        // ── ntf_SmsGovernanceRules ────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsGovernanceRules",
            columns: table => new
            {
                Id           = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                RulePackId   = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name         = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                Description  = table.Column<string>(type: "text", nullable: true),
                RuleType     = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                Pattern      = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                Severity     = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "block"),
                Enabled      = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                Priority     = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                MetadataJson = table.Column<string>(type: "text", nullable: true),
                CreatedAt    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt    = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CreatedBy    = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                UpdatedBy    = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsGovernanceRules", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRules_Pack_Enabled_Priority",
            table: "ntf_SmsGovernanceRules",
            columns: ["RulePackId", "Enabled", "Priority"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRules_Type_Enabled",
            table: "ntf_SmsGovernanceRules",
            columns: ["RuleType", "Enabled"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsGovRules_Severity",
            table: "ntf_SmsGovernanceRules",
            column: "Severity");

        // ── ntf_SmsComplianceProfiles ─────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsComplianceProfiles",
            columns: table => new
            {
                Id                     = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TenantId               = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Name                   = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                Description            = table.Column<string>(type: "text", nullable: true),
                Enabled                = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                DefaultRulePackIdsJson = table.Column<string>(type: "text", nullable: true),
                EnforcementMode        = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "standard"),
                CreatedAt              = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt              = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CreatedBy              = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                UpdatedBy              = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsComplianceProfiles", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsComplianceProfiles_Tenant_Enabled",
            table: "ntf_SmsComplianceProfiles",
            columns: ["TenantId", "Enabled"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsComplianceProfiles_EnforcementMode",
            table: "ntf_SmsComplianceProfiles",
            column: "EnforcementMode");

        // ── ntf_SmsComplianceProfileAssignments ───────────────────────────────
        migrationBuilder.CreateTable(
            name: "ntf_SmsComplianceProfileAssignments",
            columns: table => new
            {
                Id        = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TenantId  = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Scope     = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "tenant"),
                Enabled   = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_ntf_SmsComplianceProfileAssignments", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsComplianceAssignments_Tenant_Scope_Enabled",
            table: "ntf_SmsComplianceProfileAssignments",
            columns: ["TenantId", "Scope", "Enabled"]);

        migrationBuilder.CreateIndex(
            name: "IX_ntf_SmsComplianceAssignments_ProfileId",
            table: "ntf_SmsComplianceProfileAssignments",
            column: "ProfileId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ntf_SmsComplianceProfileAssignments");
        migrationBuilder.DropTable(name: "ntf_SmsComplianceProfiles");
        migrationBuilder.DropTable(name: "ntf_SmsGovernanceRules");
        migrationBuilder.DropTable(name: "ntf_SmsGovernanceRulePacks");
    }
}
