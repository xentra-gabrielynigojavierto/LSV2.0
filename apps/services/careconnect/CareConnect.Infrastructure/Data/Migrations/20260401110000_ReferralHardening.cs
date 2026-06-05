using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// LSCC-005-01: Referral Flow Hardening schema additions.
///
/// CareConnectNotifications:
///   - AttemptCount (int, NOT NULL, default 0): tracks how many send attempts were made.
///   - LastAttemptAtUtc (datetime?, nullable): timestamp of the most recent attempt.
///
/// Referrals:
///   - TokenVersion (int, NOT NULL, default 1): used for view token revocation.
///     Incrementing this value invalidates all previously issued HMAC tokens for
///     that referral. Newly generated tokens embed the current version; validation
///     rejects any token whose embedded version does not match the stored version.
/// </summary>
public partial class ReferralHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name:         "AttemptCount",
            table:        "CareConnectNotifications",
            type:         "int",
            nullable:     false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name:     "LastAttemptAtUtc",
            table:    "CareConnectNotifications",
            type:     "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name:         "TokenVersion",
            table:        "Referrals",
            type:         "int",
            nullable:     false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AttemptCount",     table: "CareConnectNotifications");
        migrationBuilder.DropColumn(name: "LastAttemptAtUtc", table: "CareConnectNotifications");
        migrationBuilder.DropColumn(name: "TokenVersion",     table: "Referrals");
    }
}
