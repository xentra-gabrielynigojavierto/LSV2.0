using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// LSCC-005: Adds ReferrerEmail and ReferrerName to the Referrals table.
///
/// These fields capture the referrer's contact information at creation time
/// so that acceptance confirmation emails can be sent without cross-service
/// lookups to the Identity service.
///
/// Both columns are nullable — existing referrals created before this migration
/// will simply have null values, and acceptance emails will be skipped for them.
/// </summary>
public partial class AddReferrerFieldsToReferral : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name:        "ReferrerEmail",
            table:       "Referrals",
            type:        "varchar(320)",
            maxLength:   320,
            nullable:    true);

        migrationBuilder.AddColumn<string>(
            name:        "ReferrerName",
            table:       "Referrals",
            type:        "varchar(200)",
            maxLength:   200,
            nullable:    true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ReferrerEmail", table: "Referrals");
        migrationBuilder.DropColumn(name: "ReferrerName",  table: "Referrals");
    }
}
