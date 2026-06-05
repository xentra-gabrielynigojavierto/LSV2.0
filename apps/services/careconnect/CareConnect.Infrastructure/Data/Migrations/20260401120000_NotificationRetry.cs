using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// LSCC-005-02: Notification retry infrastructure schema additions.
///
/// CareConnectNotifications:
///   - NextRetryAfterUtc (datetime?, nullable): when the next automatic retry should occur.
///     Set by the application layer based on the retry policy schedule.
///     Cleared when the notification is sent or all retries are exhausted.
///   - TriggerSource (varchar(20), NOT NULL, default 'Initial'): distinguishes how the
///     notification was triggered — Initial (first system send), AutoRetry (background worker),
///     or ManualResend (operator action). Enables clear audit trail separation.
///
/// A composite index on (Status, NextRetryAfterUtc) is added to support the retry worker's
/// efficient query for retry-eligible notifications.
/// </summary>
public partial class NotificationRetry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name:     "NextRetryAfterUtc",
            table:    "CareConnectNotifications",
            type:     "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name:         "TriggerSource",
            table:        "CareConnectNotifications",
            type:         "varchar(20)",
            maxLength:    20,
            nullable:     false,
            defaultValue: "Initial");

        migrationBuilder.CreateIndex(
            name:    "IX_CareConnectNotifications_Status_NextRetryAfterUtc",
            table:   "CareConnectNotifications",
            columns: new[] { "Status", "NextRetryAfterUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name:  "IX_CareConnectNotifications_Status_NextRetryAfterUtc",
            table: "CareConnectNotifications");

        migrationBuilder.DropColumn(name: "NextRetryAfterUtc", table: "CareConnectNotifications");
        migrationBuilder.DropColumn(name: "TriggerSource",     table: "CareConnectNotifications");
    }
}
