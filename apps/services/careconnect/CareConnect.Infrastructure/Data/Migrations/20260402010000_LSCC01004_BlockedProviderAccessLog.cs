using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// LSCC-01-004: Admin Queue and Operational Visibility.
///
/// Creates the BlockedProviderAccessLogs table to record every failed provider
/// access-readiness check. This enables:
///   - the admin blocked-provider queue (/careconnect/admin/providers/blocked)
///   - dashboard metrics for blocked-access attempt counts
///
/// Table design:
///   - best-effort write: failures never block the user-facing flow
///   - indexed on (UserId, AttemptedAtUtc) for per-user history queries
///   - indexed on AttemptedAtUtc for time-windowed dashboard queries
///   - all FK columns are nullable — partial context (e.g., no provider) is still useful
/// </summary>
[Migration("20260402010000_LSCC01004_BlockedProviderAccessLog")]
public partial class LSCC01004_BlockedProviderAccessLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BlockedProviderAccessLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "char(36)",
                    nullable: false,
                    collation: "ascii_general_ci"),

                TenantId = table.Column<Guid>(
                    type: "char(36)",
                    nullable: true,
                    collation: "ascii_general_ci"),

                UserId = table.Column<Guid>(
                    type: "char(36)",
                    nullable: true,
                    collation: "ascii_general_ci"),

                UserEmail = table.Column<string>(
                    type: "varchar(256)",
                    maxLength: 256,
                    nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),

                OrganizationId = table.Column<Guid>(
                    type: "char(36)",
                    nullable: true,
                    collation: "ascii_general_ci"),

                ProviderId = table.Column<Guid>(
                    type: "char(36)",
                    nullable: true,
                    collation: "ascii_general_ci"),

                ReferralId = table.Column<Guid>(
                    type: "char(36)",
                    nullable: true,
                    collation: "ascii_general_ci"),

                FailureReason = table.Column<string>(
                    type: "varchar(128)",
                    maxLength: 128,
                    nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),

                AttemptedAtUtc = table.Column<DateTime>(
                    type: "datetime(6)",
                    nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BlockedProviderAccessLogs", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name:    "IX_BlockedProviderAccessLogs_UserId_AttemptedAtUtc",
            table:   "BlockedProviderAccessLogs",
            columns: new[] { "UserId", "AttemptedAtUtc" });

        migrationBuilder.CreateIndex(
            name:    "IX_BlockedProviderAccessLogs_AttemptedAtUtc",
            table:   "BlockedProviderAccessLogs",
            column:  "AttemptedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "BlockedProviderAccessLogs");
    }
}
