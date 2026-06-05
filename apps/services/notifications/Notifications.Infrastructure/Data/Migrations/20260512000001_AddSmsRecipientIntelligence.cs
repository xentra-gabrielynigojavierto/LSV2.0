using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <summary>
    /// LS-NOTIF-SMS-016: Recipient Intelligence, Suppression Intelligence, and Delivery Reputation Management.
    ///
    /// 1. Creates ntf_SmsRecipientReputationSnapshots — recipient-level delivery reputation calculated
    ///    from local NotificationAttempt telemetry. RecipientHash is HMAC-SHA256(normalizedPhone, salt).
    ///    Raw phone numbers are NEVER stored. All fields are aggregate operational metrics.
    ///
    /// 2. Creates ntf_SmsSuppressionDecisions — audit log for suppression evaluation outcomes.
    ///    Records allow/warn/soft_suppress/hard_suppress/review_required decisions.
    ///    Raw phone numbers are NEVER stored.
    ///
    /// Security: No phone numbers, credentials, CredentialsJson, SettingsJson, auth tokens,
    /// webhook URLs, or raw provider payloads stored in either table.
    /// RecipientHash is an opaque HMAC-SHA256 token — computationally irreversible without salt.
    /// </summary>
    public partial class AddSmsRecipientIntelligence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ntf_SmsRecipientReputationSnapshots ───────────────────────────

            migrationBuilder.CreateTable(
                name: "ntf_SmsRecipientReputationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),

                    // Opaque HMAC-SHA256 token — never a raw phone number
                    RecipientHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    TenantId     = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ProviderType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CountryCode  = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region       = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Attempt counts
                    TotalAttempts              = table.Column<int>(type: "int", nullable: false),
                    DeliveredAttempts          = table.Column<int>(type: "int", nullable: false),
                    FailedAttempts             = table.Column<int>(type: "int", nullable: false),
                    RetryAttempts              = table.Column<int>(type: "int", nullable: false),
                    DeadLetterAttempts         = table.Column<int>(type: "int", nullable: false),
                    CarrierRejectedAttempts    = table.Column<int>(type: "int", nullable: false),
                    InvalidDestinationAttempts = table.Column<int>(type: "int", nullable: false),

                    // Latency
                    AverageLatencyMs = table.Column<decimal>(type: "decimal(10,2)", nullable: true),

                    // Rates
                    DeliverySuccessRate = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    FailureRate         = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    RetryRate           = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    DeadLetterRate      = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    CarrierFailureRate  = table.Column<decimal>(type: "decimal(5,4)", nullable: false),

                    // Scores
                    InvalidNumberRisk    = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    RetrySuppressionRisk = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    QualityScore         = table.Column<decimal>(type: "decimal(5,2)", nullable: false),

                    // Classification
                    DestinationRiskLevel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "low")
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Timestamps
                    LastAttemptAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CalculatedAt  = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_ntf_SmsRecipientReputationSnapshots", x => x.Id))
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsRecipientSnapshots_Hash",
                table: "ntf_SmsRecipientReputationSnapshots",
                column: "RecipientHash");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsRecipientSnapshots_Tenant_Hash",
                table: "ntf_SmsRecipientReputationSnapshots",
                columns: new[] { "TenantId", "RecipientHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsRecipientSnapshots_Provider_Hash",
                table: "ntf_SmsRecipientReputationSnapshots",
                columns: new[] { "ProviderType", "RecipientHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsRecipientSnapshots_Country_Calc",
                table: "ntf_SmsRecipientReputationSnapshots",
                columns: new[] { "CountryCode", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsRecipientSnapshots_RiskLevel",
                table: "ntf_SmsRecipientReputationSnapshots",
                column: "DestinationRiskLevel");

            // ── ntf_SmsSuppressionDecisions ────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "ntf_SmsSuppressionDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),

                    // Opaque recipient hash — never a raw phone number
                    RecipientHash  = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    TenantId       = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    NotificationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AttemptId      = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    DecisionType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonCode   = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    RiskScore    = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    QualityScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    RetryCount   = table.Column<int>(type: "int", nullable: false),

                    ProviderType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CountryCode  = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region       = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    DecisionMetadataJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_ntf_SmsSuppressionDecisions", x => x.Id))
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsSuppressionDecisions_Hash",
                table: "ntf_SmsSuppressionDecisions",
                column: "RecipientHash");

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsSuppressionDecisions_Tenant_Hash",
                table: "ntf_SmsSuppressionDecisions",
                columns: new[] { "TenantId", "RecipientHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ntf_SmsSuppressionDecisions_Tenant_Type_Dt",
                table: "ntf_SmsSuppressionDecisions",
                columns: new[] { "TenantId", "DecisionType", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ntf_SmsSuppressionDecisions");
            migrationBuilder.DropTable(name: "ntf_SmsRecipientReputationSnapshots");
        }
    }
}
