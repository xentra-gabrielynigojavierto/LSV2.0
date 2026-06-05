using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <summary>
    /// LS-NOTIF-SMS-015: Regional Intelligence, Provider Quality Scoring, and Adaptive Routing.
    ///
    /// 1. Creates ntf_SmsProviderQualitySnapshots — periodic quality snapshots calculated from
    ///    local NotificationAttempt telemetry. No phone numbers, credentials, or raw payloads stored.
    ///
    /// 2. Adds 5 nullable columns to ntf_SmsRoutingDecisions — adaptive routing metadata:
    ///    InferredCountryCode, InferredRegion, ProviderQualityScore, AdaptiveScore, AdaptiveInputsJson.
    ///    InferredCountryCode is derived from an E.164 prefix — never a raw phone number.
    ///
    /// Security: No phone numbers, credentials, CredentialsJson, SettingsJson, auth tokens,
    /// webhook URLs, or raw provider payloads stored in either table.
    /// ProviderConfigId fields are opaque Guids only.
    /// </summary>
    public partial class AddSmsQualityAndAdaptiveRouting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ntf_SmsProviderQualitySnapshots ───────────────────────────────

            migrationBuilder.CreateTable(
                name: "ntf_SmsProviderQualitySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),

                    ProviderType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Opaque provider config reference — no credentials
                    ProviderConfigId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ProviderOwnershipMode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // null = platform-wide; set = tenant-scoped aggregate
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    // Derived geography — never a raw phone number
                    Region      = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CountryCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Snapshot window
                    WindowStart = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WindowEnd   = table.Column<DateTime>(type: "datetime(6)", nullable: false),

                    // Attempt counts
                    TotalAttempts          = table.Column<int>(type: "int", nullable: false),
                    DeliveredAttempts      = table.Column<int>(type: "int", nullable: false),
                    FailedAttempts         = table.Column<int>(type: "int", nullable: false),
                    RetryAttempts          = table.Column<int>(type: "int", nullable: false),
                    DeadLetterAttempts     = table.Column<int>(type: "int", nullable: false),
                    ReconciledAttempts     = table.Column<int>(type: "int", nullable: false),
                    ReconciliationFailures = table.Column<int>(type: "int", nullable: false),

                    // Latency
                    AverageLatencyMs = table.Column<decimal>(type: "decimal(18,4)", nullable: true),

                    // Rates (0-1)
                    DeliverySuccessRate       = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    FailureRate               = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    RetryRate                 = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    DeadLetterRate            = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ReconciliationFailureRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),

                    // Cost
                    AverageEffectiveCost     = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    CostPerDeliveredMessage  = table.Column<decimal>(type: "decimal(18,8)", nullable: true),

                    // Scores (0-100)
                    QualityScore        = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CostEfficiencyScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    HealthPenalty       = table.Column<decimal>(type: "decimal(5,4)", nullable: false),

                    CalculatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsProviderQualitySnapshots", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Indexes for quality snapshots
            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsQualitySnapshots_Provider_Calc",
                table: "ntf_SmsProviderQualitySnapshots",
                columns: new[] { "ProviderType", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsQualitySnapshots_Tenant_Provider_Calc",
                table: "ntf_SmsProviderQualitySnapshots",
                columns: new[] { "TenantId", "ProviderType", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsQualitySnapshots_Country_Provider_Calc",
                table: "ntf_SmsProviderQualitySnapshots",
                columns: new[] { "CountryCode", "ProviderType", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsQualitySnapshots_Config_Calc",
                table: "ntf_SmsProviderQualitySnapshots",
                columns: new[] { "ProviderConfigId", "CalculatedAt" });

            // ── ntf_SmsRoutingDecisions: add adaptive routing columns ──────────
            // All new columns are nullable — no data migration needed.
            // InferredCountryCode is derived from E.164 prefix; NEVER a raw phone number.

            migrationBuilder.AddColumn<string>(
                name: "InferredCountryCode",
                table: "ntf_SmsRoutingDecisions",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "InferredRegion",
                table: "ntf_SmsRoutingDecisions",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderQualityScore",
                table: "ntf_SmsRoutingDecisions",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdaptiveScore",
                table: "ntf_SmsRoutingDecisions",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdaptiveInputsJson",
                table: "ntf_SmsRoutingDecisions",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove quality snapshots table
            migrationBuilder.DropTable(name: "ntf_SmsProviderQualitySnapshots");

            // Remove adaptive routing columns from decisions
            migrationBuilder.DropColumn(name: "InferredCountryCode", table: "ntf_SmsRoutingDecisions");
            migrationBuilder.DropColumn(name: "InferredRegion",       table: "ntf_SmsRoutingDecisions");
            migrationBuilder.DropColumn(name: "ProviderQualityScore", table: "ntf_SmsRoutingDecisions");
            migrationBuilder.DropColumn(name: "AdaptiveScore",        table: "ntf_SmsRoutingDecisions");
            migrationBuilder.DropColumn(name: "AdaptiveInputsJson",   table: "ntf_SmsRoutingDecisions");
        }
    }
}
