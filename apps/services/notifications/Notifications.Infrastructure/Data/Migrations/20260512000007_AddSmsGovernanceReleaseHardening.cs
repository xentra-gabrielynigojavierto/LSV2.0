using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations;

/// <summary>
/// LS-NOTIF-SMS-021-HARDENING: Adds activation concurrency locking and retry-tracking
/// columns to ntf_SmsGovernanceReleasePackages.
/// </summary>
public partial class AddSmsGovernanceReleaseHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Activation concurrency lock ───────────────────────────────────────
        migrationBuilder.AddColumn<Guid>(
            name:       "ActivationLockId",
            table:      "ntf_SmsGovernanceReleasePackages",
            type:       "char(36)",
            nullable:   true);

        migrationBuilder.AddColumn<DateTime>(
            name:       "ActivationLockAcquiredAt",
            table:      "ntf_SmsGovernanceReleasePackages",
            nullable:   true);

        migrationBuilder.AddColumn<DateTime>(
            name:       "ActivationLockExpiresAt",
            table:      "ntf_SmsGovernanceReleasePackages",
            nullable:   true);

        migrationBuilder.AddColumn<string>(
            name:       "ActivationLockedBy",
            table:      "ntf_SmsGovernanceReleasePackages",
            maxLength:  200,
            nullable:   true);

        // ── Retry tracking ────────────────────────────────────────────────────
        migrationBuilder.AddColumn<int>(
            name:         "ActivationAttemptCount",
            table:        "ntf_SmsGovernanceReleasePackages",
            nullable:     false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name:     "LastActivationAttemptAt",
            table:    "ntf_SmsGovernanceReleasePackages",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name:     "NextActivationRetryAt",
            table:    "ntf_SmsGovernanceReleasePackages",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name:      "LastActivationFailureReason",
            table:     "ntf_SmsGovernanceReleasePackages",
            maxLength: 500,
            nullable:  true);

        // Index to let the worker efficiently find retryable packages
        migrationBuilder.CreateIndex(
            name:    "IX_ntf_SmsGovRelPkgs_State_RetryAt",
            table:   "ntf_SmsGovernanceReleasePackages",
            columns: new[] { "ReleaseState", "NextActivationRetryAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name:  "IX_ntf_SmsGovRelPkgs_State_RetryAt",
            table: "ntf_SmsGovernanceReleasePackages");

        migrationBuilder.DropColumn(name: "ActivationLockId",              table: "ntf_SmsGovernanceReleasePackages");
        migrationBuilder.DropColumn(name: "ActivationLockAcquiredAt",      table: "ntf_SmsGovernanceReleasePackages");
        migrationBuilder.DropColumn(name: "ActivationLockExpiresAt",       table: "ntf_SmsGovernanceReleasePackages");
        migrationBuilder.DropColumn(name: "ActivationLockedBy",            table: "ntf_SmsGovernanceReleasePackages");
        migrationBuilder.DropColumn(name: "ActivationAttemptCount",        table: "ntf_SmsGovernanceReleasePackages");
        migrationBuilder.DropColumn(name: "LastActivationAttemptAt",       table: "ntf_SmsGovernanceReleasePackages");
        migrationBuilder.DropColumn(name: "NextActivationRetryAt",         table: "ntf_SmsGovernanceReleasePackages");
        migrationBuilder.DropColumn(name: "LastActivationFailureReason",   table: "ntf_SmsGovernanceReleasePackages");
    }
}
