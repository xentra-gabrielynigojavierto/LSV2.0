using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <summary>
    /// LS-NOTIF-SMS-007: Adds persisted reconciliation tracking columns to ntf_NotificationAttempts.
    /// These columns allow SMS activity APIs to filter and summarise reconciliation outcomes
    /// without consulting audit events or inferring from delivery status alone.
    /// No credentials, raw provider payloads, or phone numbers are stored in any of these columns.
    /// </summary>
    public partial class AddSmsReconciliationTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastReconciliationOutcome",
                table: "ntf_NotificationAttempts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReconciledAt",
                table: "ntf_NotificationAttempts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastReconciliationErrorCode",
                table: "ntf_NotificationAttempts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LastReconciliationProviderStatus",
                table: "ntf_NotificationAttempts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LastReconciliationNormalizedStatus",
                table: "ntf_NotificationAttempts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ReconciliationAttemptCount",
                table: "ntf_NotificationAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LastReconciliationOutcome",         table: "ntf_NotificationAttempts");
            migrationBuilder.DropColumn(name: "LastReconciledAt",                  table: "ntf_NotificationAttempts");
            migrationBuilder.DropColumn(name: "LastReconciliationErrorCode",       table: "ntf_NotificationAttempts");
            migrationBuilder.DropColumn(name: "LastReconciliationProviderStatus",  table: "ntf_NotificationAttempts");
            migrationBuilder.DropColumn(name: "LastReconciliationNormalizedStatus",table: "ntf_NotificationAttempts");
            migrationBuilder.DropColumn(name: "ReconciliationAttemptCount",        table: "ntf_NotificationAttempts");
        }
    }
}
