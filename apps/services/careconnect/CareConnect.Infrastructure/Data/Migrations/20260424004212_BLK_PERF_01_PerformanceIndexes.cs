using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BLK_PERF_01_PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: DedupeKey column already exists on cc_CareConnectNotifications from
            // 20260404000000_AddNotificationDedupeKey — do not re-add it here.

            migrationBuilder.CreateTable(
                name: "cc_ProviderNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cc_ProviderNetworks", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cc_NetworkProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProviderNetworkId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProviderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cc_NetworkProviders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cc_NetworkProviders_cc_ProviderNetworks_ProviderNetworkId",
                        column: x => x.ProviderNetworkId,
                        principalTable: "cc_ProviderNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cc_NetworkProviders_cc_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "cc_Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_TenantId_Status_CreatedAtUtc",
                table: "cc_Referrals",
                columns: new[] { "TenantId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cc_ReferralProviderReassignments_ReferralId",
                table: "cc_ReferralProviderReassignments",
                column: "ReferralId");

            // NOTE: IX_CareConnectNotifications_DedupeKey index already exists from
            // 20260404000000_AddNotificationDedupeKey — do not re-create it here.

            migrationBuilder.CreateIndex(
                name: "IX_BlockedProviderAccessLogs_TenantId_AttemptedAtUtc",
                table: "cc_BlockedProviderAccessLogs",
                columns: new[] { "TenantId", "AttemptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivationRequests_TenantId_Status_CreatedAt",
                table: "cc_ActivationRequests",
                columns: new[] { "TenantId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cc_NetworkProviders_ProviderId",
                table: "cc_NetworkProviders",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_cc_NetworkProviders_ProviderNetworkId_ProviderId",
                table: "cc_NetworkProviders",
                columns: new[] { "ProviderNetworkId", "ProviderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkProviders_TenantId_ProviderNetworkId",
                table: "cc_NetworkProviders",
                columns: new[] { "TenantId", "ProviderNetworkId" });

            migrationBuilder.CreateIndex(
                name: "IX_cc_ProviderNetworks_TenantId_Name",
                table: "cc_ProviderNetworks",
                columns: new[] { "TenantId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cc_NetworkProviders");

            migrationBuilder.DropTable(
                name: "cc_ProviderNetworks");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_TenantId_Status_CreatedAtUtc",
                table: "cc_Referrals");

            migrationBuilder.DropIndex(
                name: "IX_cc_ReferralProviderReassignments_ReferralId",
                table: "cc_ReferralProviderReassignments");

            // NOTE: IX_CareConnectNotifications_DedupeKey not dropped here — owned by AddNotificationDedupeKey.

            migrationBuilder.DropIndex(
                name: "IX_BlockedProviderAccessLogs_TenantId_AttemptedAtUtc",
                table: "cc_BlockedProviderAccessLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivationRequests_TenantId_Status_CreatedAt",
                table: "cc_ActivationRequests");

            // NOTE: DedupeKey column not dropped here — owned by AddNotificationDedupeKey migration.
        }
    }
}
