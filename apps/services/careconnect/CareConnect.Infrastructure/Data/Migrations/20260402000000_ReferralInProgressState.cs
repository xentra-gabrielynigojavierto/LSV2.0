using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// LSCC-01-001-01: Referral State Machine Correction.
///
/// Migrates all existing Referral rows with Status='Scheduled' to Status='InProgress'.
///
/// Background:
///   'Scheduled' was previously used as the canonical active state after a provider
///   accepted a referral. This coupling to appointment scheduling was incorrect —
///   the referral workflow is independent of the appointment workflow.
///   'InProgress' is the correct canonical state for an accepted referral that is
///   actively being worked on by the receiving provider.
///
/// Effect:
///   Any row with Status='Scheduled' (i.e. previously-accepted referrals that had
///   an appointment booked against them) will be normalised to 'InProgress'.
///   The transition matrix in ReferralWorkflowRules retains a legacy Scheduled entry
///   so any in-flight data that arrives after migration can still be normalised safely.
/// </summary>
[Migration("20260402000000_ReferralInProgressState")]
public partial class ReferralInProgressState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE `Referrals` SET `Status` = 'InProgress' WHERE `Status` = 'Scheduled';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE `Referrals` SET `Status` = 'Scheduled' WHERE `Status` = 'InProgress';");
    }
}
