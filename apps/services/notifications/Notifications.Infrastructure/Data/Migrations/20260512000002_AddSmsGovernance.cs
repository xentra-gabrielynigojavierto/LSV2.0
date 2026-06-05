using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <summary>
    /// LS-NOTIF-SMS-017: SMS Governance Policies, Compliance Controls, and Operational Guardrails.
    ///
    /// 1. Creates ntf_SmsGovernancePolicies — governance policy definitions.
    ///    Supports: quiet_hours, geographic_restriction, rate_limit, provider_governance,
    ///    retry_governance, escalation_guardrail.
    ///    TenantId = NULL means platform-wide policy. PolicyJson = structured config JSON.
    ///    No credentials, no secrets, no phone numbers stored.
    ///
    /// 2. Creates ntf_SmsGovernanceDecisions — governance decision audit log.
    ///    Records allow/delay/throttle/block/review_required/override_allowed outcomes.
    ///    Raw phone numbers are NEVER stored — only safe operational metadata.
    ///    CountryCode/Region are inferred from phone transiently; only the code is stored.
    ///
    /// Security: No phone numbers, credentials, CredentialsJson, SettingsJson, auth tokens,
    /// webhook URLs, or raw provider payloads stored in either table.
    /// </summary>
    public partial class AddSmsGovernance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ntf_SmsGovernancePolicies ─────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "ntf_SmsGovernancePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    Name       = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PolicyType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled    = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Priority   = table.Column<int>(type: "int", nullable: false, defaultValue: 100),

                    // Structured policy config — no credentials, no secrets, no phone numbers
                    PolicyJson = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    EmergencyOverrideAllowed = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),

                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsGovernancePolicies", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovPolicies_Tenant_Type_Enabled",
                table: "ntf_SmsGovernancePolicies",
                columns: new[] { "TenantId", "PolicyType", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovPolicies_Type_Enabled_Priority",
                table: "ntf_SmsGovernancePolicies",
                columns: new[] { "PolicyType", "Enabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovPolicies_UpdatedAt",
                table: "ntf_SmsGovernancePolicies",
                column: "UpdatedAt");

            // ── ntf_SmsGovernanceDecisions ────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "ntf_SmsGovernanceDecisions",
                columns: table => new
                {
                    Id             = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NotificationId = table.Column<Guid>(type: "char(36)", nullable: true,  collation: "ascii_general_ci"),
                    AttemptId      = table.Column<Guid>(type: "char(36)", nullable: true,  collation: "ascii_general_ci"),
                    TenantId       = table.Column<Guid>(type: "char(36)", nullable: true,  collation: "ascii_general_ci"),
                    PolicyId       = table.Column<Guid>(type: "char(36)", nullable: true,  collation: "ascii_general_ci"),

                    PolicyType   = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DecisionType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "allow")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonCode   = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    ProviderType     = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderConfigId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    // Safe operational fields — ISO country code only, never raw phone
                    CountryCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region      = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    EffectiveAt          = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DecisionMetadataJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt            = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsGovernanceDecisions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovDecisions_Tenant_Dt",
                table: "ntf_SmsGovernanceDecisions",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovDecisions_DecisionType_Dt",
                table: "ntf_SmsGovernanceDecisions",
                columns: new[] { "DecisionType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovDecisions_PolicyType_Dt",
                table: "ntf_SmsGovernanceDecisions",
                columns: new[] { "PolicyType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsGovDecisions_NotifId",
                table: "ntf_SmsGovernanceDecisions",
                column: "NotificationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ntf_SmsGovernanceDecisions");
            migrationBuilder.DropTable(name: "ntf_SmsGovernancePolicies");
        }
    }
}
