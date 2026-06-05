using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// Adds the cc_ReferralProviderReassignments table so that provider reassignment events
/// are recorded locally and can be surfaced in the referral audit timeline without
/// querying the central Audit service.
///
/// Table design:
///   - PreviousProviderId is nullable: it is null for the initial assignment, populated
///     for every subsequent reassignment.
///   - ReassignedByUserId is nullable: system-initiated reassignments have no actor.
///   - Indexed on (TenantId, ReferralId) for per-referral timeline queries.
///   - Indexed on (TenantId, ReassignedAtUtc) for time-windowed admin queries.
/// </summary>
[Migration("20260422000000_AddProviderReassignmentLog")]
public partial class AddProviderReassignmentLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "cc_ReferralProviderReassignments",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "char(36)",
                    nullable: false,
                    collation: "ascii_general_ci"),

                ReferralId = table.Column<Guid>(
                    type: "char(36)",
                    nullable: false,
                    collation: "ascii_general_ci"),

                TenantId = table.Column<Guid>(
                    type: "char(36)",
                    nullable: false,
                    collation: "ascii_general_ci"),

                PreviousProviderId = table.Column<Guid>(
                    type: "char(36)",
                    nullable: true,
                    collation: "ascii_general_ci"),

                NewProviderId = table.Column<Guid>(
                    type: "char(36)",
                    nullable: false,
                    collation: "ascii_general_ci"),

                ReassignedByUserId = table.Column<Guid>(
                    type: "char(36)",
                    nullable: true,
                    collation: "ascii_general_ci"),

                ReassignedAtUtc = table.Column<DateTime>(
                    type: "datetime(6)",
                    nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cc_ReferralProviderReassignments", x => x.Id);
                table.ForeignKey(
                    name:             "FK_cc_ReferralProviderReassignments_cc_Referrals_ReferralId",
                    column:           x => x.ReferralId,
                    principalTable:   "cc_Referrals",
                    principalColumn:  "Id",
                    onDelete:         ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name:    "IX_cc_ReferralProviderReassignments_TenantId_ReferralId",
            table:   "cc_ReferralProviderReassignments",
            columns: new[] { "TenantId", "ReferralId" });

        migrationBuilder.CreateIndex(
            name:    "IX_cc_ReferralProviderReassignments_TenantId_ReassignedAtUtc",
            table:   "cc_ReferralProviderReassignments",
            columns: new[] { "TenantId", "ReassignedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "cc_ReferralProviderReassignments");
    }
}
