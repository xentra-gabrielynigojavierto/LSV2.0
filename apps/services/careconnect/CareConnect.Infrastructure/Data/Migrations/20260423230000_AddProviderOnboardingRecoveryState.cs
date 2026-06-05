using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// BLK-CC-02 — Adds onboarding recovery state columns to cc_Providers.
///
/// Enables resumable provider onboarding: if Tenant provisioning succeeds but
/// Identity membership assignment fails, the pending tenant state is preserved
/// so the next retry can skip re-provisioning.
///
/// New columns:
///   PendingTenantId       (char(36), nullable)   — TenantId from Tenant service, cleared on success
///   PendingTenantCode     (varchar(100), nullable) — TenantCode from Tenant service
///   PendingTenantSubdomain (varchar(200), nullable) — Subdomain from Tenant service
///   TenantOnboardingStatus (varchar(30), NOT NULL, DEFAULT 'None')
///       Values: None | ProvisioningStarted | TenantProvisioned | Completed | Failed
///   LastOnboardingError   (varchar(500), nullable) — last failure reason for ops visibility
///   LastOnboardingAttemptAtUtc (datetime, nullable)
///
/// Migration strategy:
///   All existing rows default to TenantOnboardingStatus='None' (backward compatible).
/// </summary>
[Migration("20260423230000_AddProviderOnboardingRecoveryState")]
public partial class AddProviderOnboardingRecoveryState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name:     "PendingTenantId",
            table:    "cc_Providers",
            type:     "char(36)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name:         "PendingTenantCode",
            table:        "cc_Providers",
            type:         "varchar(100)",
            maxLength:    100,
            nullable:     true);

        migrationBuilder.AddColumn<string>(
            name:         "PendingTenantSubdomain",
            table:        "cc_Providers",
            type:         "varchar(200)",
            maxLength:    200,
            nullable:     true);

        migrationBuilder.AddColumn<string>(
            name:         "TenantOnboardingStatus",
            table:        "cc_Providers",
            type:         "varchar(30)",
            maxLength:    30,
            nullable:     false,
            defaultValue: "None");

        migrationBuilder.AddColumn<string>(
            name:         "LastOnboardingError",
            table:        "cc_Providers",
            type:         "varchar(500)",
            maxLength:    500,
            nullable:     true);

        migrationBuilder.AddColumn<DateTime>(
            name:     "LastOnboardingAttemptAtUtc",
            table:    "cc_Providers",
            type:     "datetime(6)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name:   "IX_Providers_TenantOnboardingStatus",
            table:  "cc_Providers",
            column: "TenantOnboardingStatus");

        migrationBuilder.CreateIndex(
            name:   "IX_Providers_PendingTenantId",
            table:  "cc_Providers",
            column: "PendingTenantId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name:  "IX_Providers_PendingTenantId",
            table: "cc_Providers");

        migrationBuilder.DropIndex(
            name:  "IX_Providers_TenantOnboardingStatus",
            table: "cc_Providers");

        migrationBuilder.DropColumn(name: "LastOnboardingAttemptAtUtc", table: "cc_Providers");
        migrationBuilder.DropColumn(name: "LastOnboardingError",         table: "cc_Providers");
        migrationBuilder.DropColumn(name: "TenantOnboardingStatus",      table: "cc_Providers");
        migrationBuilder.DropColumn(name: "PendingTenantSubdomain",      table: "cc_Providers");
        migrationBuilder.DropColumn(name: "PendingTenantCode",           table: "cc_Providers");
        migrationBuilder.DropColumn(name: "PendingTenantId",             table: "cc_Providers");
    }
}
