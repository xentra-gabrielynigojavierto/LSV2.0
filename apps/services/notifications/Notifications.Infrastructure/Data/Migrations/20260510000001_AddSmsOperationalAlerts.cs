using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <summary>
    /// LS-NOTIF-SMS-010: Creates ntf_SmsOperationalAlerts — persisted threshold-rule alerts
    /// raised by the SmsOperationalAlertWorker when SMS delivery or reconciliation metrics
    /// breach configured thresholds.
    ///
    /// No credentials, phone numbers, RecipientJson, CredentialsJson, or SettingsJson are stored.
    /// </summary>
    public partial class AddSmsOperationalAlerts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ntf_SmsOperationalAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),

                    // Classification
                    AlertType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "warning")
                        .Annotation("MySql:CharSet", "utf8mb4"),

                    // Scope
                    TenantId         = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Provider         = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderConfigId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),

                    // Threshold context
                    MetricValue           = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    ThresholdValue        = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Message               = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvaluationWindowStart = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EvaluationWindowEnd   = table.Column<DateTime>(type: "datetime(6)", nullable: false),

                    // Lifecycle
                    Status          = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "active")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OccurrenceCount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    FirstObservedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastObservedAt  = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResolvedAt      = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolvedBy      = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResolutionNote  = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SuppressedUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),

                    // Audit
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ntf_SmsOperationalAlerts", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Primary operational query: active alerts newest-first.
            migrationBuilder.CreateIndex(
                name: "IX_SmsOperationalAlerts_Status_LastObservedAt",
                table: "ntf_SmsOperationalAlerts",
                columns: new[] { "Status", "LastObservedAt" });

            // Deduplication lookup: find active alert by (AlertType, Status, TenantId, Provider, ProviderConfigId).
            migrationBuilder.CreateIndex(
                name: "IX_SmsOperationalAlerts_AlertType_Status_Scope",
                table: "ntf_SmsOperationalAlerts",
                columns: new[] { "AlertType", "Status", "TenantId", "Provider", "ProviderConfigId" });

            // Tenant-scoped view.
            migrationBuilder.CreateIndex(
                name: "IX_SmsOperationalAlerts_TenantId_Status_CreatedAt",
                table: "ntf_SmsOperationalAlerts",
                columns: new[] { "TenantId", "Status", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ntf_SmsOperationalAlerts");
        }
    }
}
