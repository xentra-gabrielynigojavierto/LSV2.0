using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <summary>
    /// LS-NOTIF-SMS-014: Creates ntf_SmsRoutingPolicies and ntf_SmsRoutingDecisions.
    ///
    /// ntf_SmsRoutingPolicies — admin-defined routing policies (priority/cost/health/hybrid/regional modes).
    /// ntf_SmsRoutingDecisions — persisted routing engine decisions for audit/debug/reporting.
    ///
    /// Security: no credentials, CredentialsJson, SettingsJson, auth tokens, webhook URLs,
    /// or raw phone numbers stored in either table.
    /// ProviderConfigId fields are opaque Guids only.
    /// </summary>
    public partial class AddSmsRouting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ntf_SmsRoutingPolicies ────────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "ntf_SmsRoutingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),

                    // null = global/platform policy; set = tenant-specific policy
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    Name    = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),

                    // Optional matching constraints (null = wildcard)
                    Region      = table.Column<string>(type: "varchar(50)",  maxLength: 50,  nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CountryCode = table.Column<string>(type: "varchar(10)",  maxLength: 10,  nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // priority | cost_optimized | health_optimized | hybrid | regional
                    RoutingMode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // JSON string arrays — no credentials stored
                    PreferredProvidersJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExcludedProvidersJson  = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    MaxEstimatedCostPerMessage = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    RequireHealthyProvider     = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    FallbackToPlatform         = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Priority                   = table.Column<int>(type: "int", nullable: false, defaultValue: 0),

                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                },
                constraints: table => table.PrimaryKey("PK_ntf_SmsRoutingPolicies", x => x.Id))
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsRoutingPolicies_Tenant_Enabled_Priority",
                table: "ntf_SmsRoutingPolicies",
                columns: new[] { "TenantId", "Enabled", "Priority" });

            // ── ntf_SmsRoutingDecisions ───────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "ntf_SmsRoutingDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),

                    TenantId       = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    NotificationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    // Linked to NotificationAttempt after send completes
                    AttemptId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    // Matched policy (null if no policy found / priority fallback)
                    RoutingPolicyId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    RoutingMode     = table.Column<string>(type: "varchar(30)",  maxLength: 30,  nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SelectedProvider = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Opaque Guid — no credential data
                    SelectedProviderConfigId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ProviderOwnershipMode    = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // JSON string arrays of provider type names only — no credentials
                    CandidateProvidersJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExcludedProvidersJson  = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    DecisionReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    EstimatedCostAmount = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    CostCurrency        = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Reserved for future health snapshot metadata (JSON)
                    HealthSnapshotJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    Region      = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CountryCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_ntf_SmsRoutingDecisions", x => x.Id))
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsRoutingDecisions_Tenant_CreatedAt",
                table: "ntf_SmsRoutingDecisions",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsRoutingDecisions_NotificationId",
                table: "ntf_SmsRoutingDecisions",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsRoutingDecisions_PolicyId",
                table: "ntf_SmsRoutingDecisions",
                column: "RoutingPolicyId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ntf_SmsRoutingDecisions");
            migrationBuilder.DropTable(name: "ntf_SmsRoutingPolicies");
        }
    }
}
