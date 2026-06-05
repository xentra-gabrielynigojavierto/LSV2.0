using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderAvailabilityExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderAvailabilityExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProviderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FacilityId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    StartAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExceptionType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderAvailabilityExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderAvailabilityExceptions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProviderAvailabilityExceptions_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAvailabilityExceptions_FacilityId",
                table: "ProviderAvailabilityExceptions",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAvailabilityExceptions_ProviderId",
                table: "ProviderAvailabilityExceptions",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAvailabilityExceptions_TenantId_FacilityId_StartAtUtc",
                table: "ProviderAvailabilityExceptions",
                columns: new[] { "TenantId", "FacilityId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAvailabilityExceptions_TenantId_ProviderId_StartAtUtc",
                table: "ProviderAvailabilityExceptions",
                columns: new[] { "TenantId", "ProviderId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAvailabilityExceptions_TenantId_StartAtUtc_EndAtUtc",
                table: "ProviderAvailabilityExceptions",
                columns: new[] { "TenantId", "StartAtUtc", "EndAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderAvailabilityExceptions");
        }
    }
}
