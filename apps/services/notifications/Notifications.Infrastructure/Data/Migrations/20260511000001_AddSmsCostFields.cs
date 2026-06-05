using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    /// <summary>
    /// LS-NOTIF-SMS-013: Adds SMS cost metadata columns to ntf_NotificationAttempts.
    ///
    /// All columns are nullable — pre-existing rows remain valid without any data migration.
    /// No credentials, phone numbers, RecipientJson, CredentialsJson, or raw provider payloads
    /// are stored in any of these columns.
    ///
    /// CostSource values:
    ///   "estimated"           — from per-provider configured estimate
    ///   "provider_reconciled" — from vendor billing API (future, requires Twilio adapter extension)
    ///   "manual"              — operator-entered correction
    ///   "unavailable"         — no estimate configured; cost is NULL
    /// </summary>
    public partial class AddSmsCostFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCostAmount",
                table: "ntf_NotificationAttempts",
                type: "decimal(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualCostAmount",
                table: "ntf_NotificationAttempts",
                type: "decimal(18,8)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CostCurrency",
                table: "ntf_NotificationAttempts",
                type: "varchar(3)",
                maxLength: 3,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CostSource",
                table: "ntf_NotificationAttempts",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CostRecordedAt",
                table: "ntf_NotificationAttempts",
                type: "datetime(6)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EstimatedCostAmount", table: "ntf_NotificationAttempts");
            migrationBuilder.DropColumn(name: "ActualCostAmount",    table: "ntf_NotificationAttempts");
            migrationBuilder.DropColumn(name: "CostCurrency",        table: "ntf_NotificationAttempts");
            migrationBuilder.DropColumn(name: "CostSource",          table: "ntf_NotificationAttempts");
            migrationBuilder.DropColumn(name: "CostRecordedAt",      table: "ntf_NotificationAttempts");
        }
    }
}
