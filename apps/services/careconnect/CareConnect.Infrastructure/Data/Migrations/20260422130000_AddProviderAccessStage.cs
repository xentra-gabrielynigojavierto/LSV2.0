using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// CC2-INT-B06-02 — Adds provider access-stage lifecycle columns to cc_Providers.
///
/// AccessStage (varchar 20, NOT NULL, DEFAULT 'URL'):
///   Controls how the provider interacts with CareConnect portals.
///   URL          → referral token URLs only, no portal access (default for all rows)
///   COMMON_PORTAL → provider has an Identity user, can log in to the common portal
///   TENANT        → provider is provisioned as a full LegalSynq tenant
///
/// IdentityUserId (char 36, nullable):
///   The Identity service UserId linked during COMMON_PORTAL activation.
///   Null for URL-stage providers (no Identity account yet).
///
/// CommonPortalActivatedAtUtc (datetime, nullable):
///   Timestamp when the provider transitioned to COMMON_PORTAL.
///
/// TenantProvisionedAtUtc (datetime, nullable):
///   Timestamp when the provider transitioned to TENANT.
///
/// Migration strategy:
///   All existing rows default to 'URL' (backward compatible — no data loss).
/// </summary>
[Migration("20260422130000_AddProviderAccessStage")]
public partial class AddProviderAccessStage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // AccessStage — NOT NULL with DB-level default so all existing rows are URL
        migrationBuilder.AddColumn<string>(
            name:         "AccessStage",
            table:        "cc_Providers",
            type:         "varchar(20)",
            maxLength:    20,
            nullable:     false,
            defaultValue: "URL");

        // IdentityUserId — nullable Guid stored as char(36)
        migrationBuilder.AddColumn<Guid>(
            name:     "IdentityUserId",
            table:    "cc_Providers",
            type:     "char(36)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name:     "CommonPortalActivatedAtUtc",
            table:    "cc_Providers",
            type:     "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name:     "TenantProvisionedAtUtc",
            table:    "cc_Providers",
            type:     "datetime(6)",
            nullable: true);

        // Index for filtering/reporting by stage
        migrationBuilder.CreateIndex(
            name:    "IX_Providers_AccessStage",
            table:   "cc_Providers",
            column:  "AccessStage");

        // Index for resolving which provider a logged-in Identity user maps to
        migrationBuilder.CreateIndex(
            name:    "IX_Providers_IdentityUserId",
            table:   "cc_Providers",
            column:  "IdentityUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name:  "IX_Providers_IdentityUserId",
            table: "cc_Providers");

        migrationBuilder.DropIndex(
            name:  "IX_Providers_AccessStage",
            table: "cc_Providers");

        migrationBuilder.DropColumn(name: "TenantProvisionedAtUtc",      table: "cc_Providers");
        migrationBuilder.DropColumn(name: "CommonPortalActivatedAtUtc",   table: "cc_Providers");
        migrationBuilder.DropColumn(name: "IdentityUserId",               table: "cc_Providers");
        migrationBuilder.DropColumn(name: "AccessStage",                  table: "cc_Providers");
    }
}
