using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <summary>
    /// LS-NOTIF-SMS-011: Creates ntf_SmsEscalationPolicies and ntf_SmsAlertEscalations —
    /// policy-driven escalation routing and delivery attempt history for SMS operational alerts.
    ///
    /// Security: no credentials, phone numbers, CredentialsJson, or SettingsJson stored.
    /// Target (webhook URL / email) in ntf_SmsEscalationPolicies is admin-only config;
    /// only masked form is stored in ntf_SmsAlertEscalations.
    /// </summary>
    public partial class AddSmsEscalation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ntf_SmsEscalationPolicies ─────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "ntf_SmsEscalationPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),

                    Name    = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),

                    // Matching criteria — null = wildcard
                    AlertType        = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Severity         = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId         = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Provider         = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderConfigId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    // Channel config
                    ChannelType   = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Target        = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetDisplay = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Dedup + retry
                    CooldownMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 60),
                    RetryEnabled    = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    MaxRetryCount   = table.Column<int>(type: "int", nullable: false, defaultValue: 3),

                    // Audit
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                },
                constraints: table => table.PrimaryKey("PK_ntf_SmsEscalationPolicies", x => x.Id))
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SmsEscalationPolicies_Enabled_AlertType",
                table: "ntf_SmsEscalationPolicies",
                columns: ["Enabled", "AlertType"]);

            migrationBuilder.CreateIndex(
                name: "IX_SmsEscalationPolicies_Enabled_ChannelType",
                table: "ntf_SmsEscalationPolicies",
                columns: ["Enabled", "ChannelType"]);

            // ── ntf_SmsAlertEscalations ───────────────────────────────────────

            migrationBuilder.CreateTable(
                name: "ntf_SmsAlertEscalations",
                columns: table => new
                {
                    Id      = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AlertId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PolicyId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    // Channel info — TargetMasked only, never raw URL/email
                    ChannelType  = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetMasked = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Classification
                    Severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "warning")
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Lifecycle
                    Status        = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "pending")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttemptCount  = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SentAt        = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NextRetryAt   = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SuppressedUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),

                    // Dedup
                    PayloadHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Metadata (safe operational data only)
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Audit
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_ntf_SmsAlertEscalations", x => x.Id))
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SmsAlertEscalations_AlertId",
                table: "ntf_SmsAlertEscalations",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsAlertEscalations_Status_NextRetryAt",
                table: "ntf_SmsAlertEscalations",
                columns: ["Status", "NextRetryAt"]);

            migrationBuilder.CreateIndex(
                name: "IX_SmsAlertEscalations_AlertId_PolicyId_PayloadHash",
                table: "ntf_SmsAlertEscalations",
                columns: ["AlertId", "PolicyId", "PayloadHash"]);

            migrationBuilder.CreateIndex(
                name: "IX_SmsAlertEscalations_CreatedAt",
                table: "ntf_SmsAlertEscalations",
                column: "CreatedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ntf_SmsAlertEscalations");
            migrationBuilder.DropTable(name: "ntf_SmsEscalationPolicies");
        }
    }
}
